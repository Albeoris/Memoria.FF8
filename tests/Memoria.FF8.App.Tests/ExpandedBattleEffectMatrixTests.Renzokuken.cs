using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(160, 4)]
    [InlineData(141, 5)]
    [InlineData(259, 6)]
    [InlineData(332, 7)]
    [InlineData(333, 8)]
    [InlineData(159, 4)]
    [InlineData(161, 6)]
    [InlineData(334, 6)]
    [InlineData(336, 7)]
    [InlineData(339, 6)]
    [InlineData(341, 7)]
    public void Installed_expanded_Renzokuken_selects_and_completes_its_original_timeline(ushort effect, int contacts)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        (string status, string detail) = RunExpandedRenzokuken(directory, PrepareField(directory), game, assets, effect);
        Assert.True(status == "logical", detail);
        Assert.Contains($"{contacts} contacts", detail);
    }

    [Fact]
    public void Installed_expanded_Renzokuken_preserves_contact_owner_after_sorceress_allocations()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        (string status, string detail) = RunExpandedRenzokuken(directory, PrepareField(directory, game.ReadColdProcessNativeBattleMemory()), game, assets, 337);
        Assert.True(status == "logical", detail);
    }

    private static bool IsExpandedRenzokukenEffect(int effect) => effect is 141 or 159 or 160 or 161 or 259 or >= 332 and <= 337 or >= 339 and <= 343;

    private static (string Status, string Detail) RunExpandedRenzokuken(string directory, GameState field, GameAssets game, BattleAssets assets, ushort? effect, BattleNativeAction? finisher = null)
    {
        bool originalAllocation = effect is 335 or 337 or 339 or 340 or 342 or 343;
        (int formation, int targetModel) = effect switch
        {
            159 => (9, 92),
            161 => (738, 131),
            334 => (326, 67),
            335 => (63, 77),
            336 => (354, 90),
            337 => (813, 117),
            339 => (796, 112),
            340 => (982, 126),
            341 => (712, 84),
            342 => (510, 124),
            343 => (509, 122),
            _ => (71, 36)
        };
        const int invocation = 5 << 16;
        const string casterId = "party-0";
        BattleNativeAction squall = Assert.Single(assets.NativeActions, action => action is { Command: 249, ActionId: 0, CharacterId: 0 });
        Assert.Contains(assets.LoadEncounter(formation).Slots, placement => placement.ModelId == targetModel);
        ushort[] selectors = assets.ReadNativeRenzokukenMagicIds(targetModel).ToArray();
        if (effect is ushort requestedEffect)
        {
            Assert.Contains(requestedEffect, selectors.Skip(2));
        }

        // The selected result must come from a fresh encounter seed and a real HUD crisis roll.
        bool sawActiveTarget = false;
        for (int seed = 0; seed < 96; seed++)
        {
            if (originalAllocation && seed == 3 && !sawActiveTarget)
            {
                break;
            }
            using GameWorld world = StartBattle(directory, field, checked((ushort)formation), limit: squall, nativeSeed: (byte)seed, prepareHigherBand: true, requireExpanded: !originalAllocation);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            if (!battle.UsesExpandedRoster)
            {
                return ("blocked", $"Formation {formation} remains on its original battle host because the expanded host cannot execute all required encounter services.");
            }
            BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == casterId);
            if (effect == 335 && !AdvanceAuthoredModelActivation(world, battle, out _))
            {
                return ("blocked", "The original formation 63 death action did not activate its authored model.");
            }
            if (effect == 337 && !AdvanceSorceressAllocations(world, battle, targetModel))
            {
                return ("blocked", "The original Sorceress allocations did not reach the last model after their lethal contacts.");
            }
            if (effect == 334)
            {
                // Bahamut fills ATB in its initialization callback. Give Squall the first HUD selection.
                caster.Atb = BattleSession.AtbMaximum;
            }
            for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null) && battle.Outcome == BattleOutcome.Active; turn++)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
            }
            if (!caster.IsReady || battle.ActiveAction is not null)
            {
                continue;
            }

            BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
            BattleCombatant? originalTarget = battle.Combatants.LastOrDefault(candidate => candidate.ModelId == targetModel && !candidate.IsPlayer && candidate.IsActive && candidate.IsAlive && !candidate.Untargetable);
            if (originalAllocation && originalTarget is null)
            {
                continue;
            }
            sawActiveTarget = true;
            hud.SelectParty(caster.Id);
            byte crisis = battle.GetNativeLimitLevel(caster.Id);
            if (!hud.NativeLimitAvailable || effect is ushort requested && selectors[crisis + 1] != requested && selectors[crisis + 2] != requested)
            {
                continue;
            }
            hud.SetNativeCommandButtons(0x2000);
            Assert.Equal(BattleAbility.LimitBreak, hud.Commands[0]);
            hud.Choose(0);
            Assert.Equal(invocation, Assert.Single(hud.ActionChoices).Id);
            Assert.True(hud.SelectingTarget);
            BattleCombatant target = hud.Targets.Last(candidate => candidate.ModelId == targetModel);
            if (originalAllocation)
            {
                Assert.Equal(originalTarget!.Id, target.Id);
            }
            else
            {
                Assert.Equal(10, target.Slot!.Value.Position);
            }
            hud.SelectTarget(target.Id, false);
            Assert.Equal(target.Id, hud.SelectedTarget?.Id);
            int initialHp = target.CurrentHp;
            string[] availableEnemies = battle.Combatants.Where(candidate => !candidate.IsPlayer && candidate.IsAlive && candidate.IsActive && !candidate.Untargetable).Select(candidate => candidate.Id).ToArray();
            int before = battle.Events.Count;
            hud.Attack();

            BattleAttackCommand command = Assert.Single(battle.QueuedActions, queued => queued.ActorId == casterId);
            Assert.Equal((byte)5, command.NativeQueuedCommand);
            Assert.Equal(crisis, command.NativeRenzokuken!.CrisisLevel);
            Assert.Equal(target.Id, command.TargetId);
            Assert.IsNotType<NativeBattleTargetRequestAdapter.DeferredOperands>(command.TargetRequest.Selection);
            for (int frame = 0; frame < 240 && caster.IsBusy && battle.GetNativeRenzokuken(casterId) is null; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            BattleNativeRenzokukenState process = Assert.IsType<BattleNativeRenzokukenState>(battle.GetNativeRenzokuken(casterId));
            if (effect is ushort selectedEffect && process.MagicId != selectedEffect || finisher is not null && process.FinisherActionId != finisher.ActionId)
            {
                continue;
            }

            Assert.Equal(target.Id, battle.Capture().NativeTimedAction!.Command.TargetId);
            if (finisher is not null)
            {
                AssertExpandedRenzokukenFinisher(battle, finisher, caster, target, availableEnemies, before);
            }
            BattleNativeRenzokukenResource resource = Assert.Single(game.ReadNativeRenzokukenResources(), candidate => candidate.MagicId == process.MagicId);
            int contacts = resource.Timeline.InputThresholds.Length;
            CompleteExpandedRenzokuken(world, battle, casterId, target.Id, process.MagicId, contacts);
            Assert.True(target.CurrentHp < initialHp);
            Assert.False(caster.IsBusy);
            Assert.Null(battle.GetNativeRenzokuken(casterId));
            Assert.Null(battle.Capture().NativeTimedAction);
            Assert.Single(battle.Events.Skip(before), entry => entry.ActorId == casterId && entry.Kind == "ActionComplete");
            if (finisher is not null)
            {
                return ("logical", $"Formation {formation}, seed {seed}, original weapon {caster.WeaponId}, {finisher.Name} effect {finisher.MagicId}, {contacts} Renzokuken contacts on the attached process.");
            }
            return ("logical", $"Formation {formation}, seed {seed}, {target.Id}, original Renzokuken effect {effect}, {contacts} contacts on the attached process.");
        }

        return ("blocked", originalAllocation && !sawActiveTarget
            ? $"Formation {formation} has no targetable allocation of original model {targetModel} at the reached phase."
            : $"No bounded genuine HUD invocation selected the installed effect {finisher?.MagicId ?? effect}.");
    }

    private static bool AdvanceSorceressAllocations(GameWorld world, BattleSession battle, int finalModel)
    {
        BattleCombatant attacker = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        HashSet<string> defeated = [];
        for (int step = 0; step < 40 && battle.Outcome == BattleOutcome.Active; step++)
        {
            BattleCombatant? enemy = battle.Combatants.SingleOrDefault(candidate => !candidate.IsPlayer && candidate.IsActive && candidate.IsAlive && !candidate.Untargetable &&
                process.ActionPackets.ModelAliases.TryEncodeExact(battle.ReadPresentationModelOwner(candidate.Id), out _));
            if (enemy?.ModelId == finalModel)
            {
                Assert.Equal(10, defeated.Count);
                return true;
            }
            if (enemy is null || battle.ActiveAction is not null || battle.QueuedActions.Count != 0 || attacker.IsBusy)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
                continue;
            }
            // Prepare a lethal HP boundary and leave allocation, counters and targetability to the original callbacks.
            enemy.CurrentHp = 1;
            attacker.Atb = BattleSession.AtbMaximum;
            world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: attacker.Id, TargetId: enemy.Id));
            FinishTurnStimulus(world, battle, attacker);
            if (!enemy.IsAlive)
            {
                Assert.True(defeated.Add(enemy.Id));
            }
        }
        return false;
    }

    private static void CompleteExpandedRenzokuken(GameWorld world, BattleSession battle, string casterId, string targetId, ushort magic, int expectedContacts)
    {
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleActionPresentation action = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
        BattleModelOwner caster = battle.ReadPresentationModelOwner(casterId);
        BattleModelOwner target = battle.ReadPresentationModelOwner(targetId);
        long token = Assert.IsType<long>(process.ActiveActionToken);
        bool observedMidContact = false;
        long observedCallback = process.Graphics.Callback;
        List<NativeBattlePresentationContactReceived> contacts = [];
        List<NativeBattlePresentationCompletionRequested> completions = [];
        for (int frame = 0; frame < 1200 && (battle.GetNativeRenzokuken(casterId) is not null || battle.ActiveAction is not null || process.ActiveActionToken is not null); frame++)
        {
            if (battle.NativeRenzokukenTriggerWindowOpen)
            {
                Assert.True(battle.TriggerNativeRenzokuken());
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.Presentation);
            if (process.Graphics.Callback != observedCallback && process.LastFrameServices is { } services)
            {
                observedCallback = process.Graphics.Callback;
                foreach (NativeBattlePresentationReceipt receipt in services.ReadOrderedReceipts())
                {
                    if (receipt is NativeBattlePresentationContactReceived { Renzokuken: { } hit } contact && hit.Token == token)
                    {
                        contacts.Add(contact);
                    }
                    else if (receipt is NativeBattlePresentationCompletionRequested { Callback: 0x47DE70 } completion && completion.Owner == caster)
                    {
                        completions.Add(completion);
                    }
                }
            }
            if (battle.GetNativeRenzokuken(casterId) is { } current)
            {
                Assert.Same(action, battle.ActiveAction);
                Assert.Equal(token, process.ActiveActionToken);
                Assert.Equal(magic, current.MagicId);
                if (process.TriggerWidget is { SourceMagicId: ushort widgetMagic } widget)
                {
                    Assert.Equal(caster, widget.Actor);
                    Assert.Equal(magic, widgetMagic);
                    Assert.Equal(current.Input, widget.Input);
                }
                if (!observedMidContact && current.Input.ContactIndex > 0)
                {
                    Assert.Equal(targetId, battle.Capture().NativeTimedAction!.Command.TargetId);
                    observedMidContact = true;
                }
            }
        }
        Assert.True(observedMidContact);
        Assert.Equal(Enumerable.Range(1, expectedContacts), contacts.Select(contact => (int)contact.Renzokuken!.Index));
        Assert.All(contacts, contact =>
        {
            Assert.Equal(caster, contact.Renzokuken!.Owner.Actor);
            Assert.Equal(target, contact.Packet.Primary.Recipient);
            Assert.NotNull(contact.Packet.Occurrence);
        });
        Assert.Single(completions);
        Assert.Null(battle.GetNativeRenzokuken(casterId));
        Assert.Null(battle.ActiveAction);
        Assert.Null(process.ActiveActionToken);
        Assert.Null(process.TriggerWidget);
    }
}
