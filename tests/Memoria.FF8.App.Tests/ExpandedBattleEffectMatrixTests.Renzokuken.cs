using System.Text.Json;
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
    [InlineData(161, 6)]
    public void Installed_expanded_Renzokuken_selects_and_completes_its_original_timeline(ushort effect, int contacts)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        (string status, string detail) = RunExpandedRenzokuken(directory, PrepareField(directory), game, assets, effect);
        Assert.True(status == "logical", detail);
        Assert.Contains($"{contacts} contacts", detail);
    }

    private static (string Status, string Detail) RunExpandedRenzokuken(string directory, GameState field, GameAssets game, BattleAssets assets, ushort? effect, BattleNativeAction? finisher = null)
    {
        ushort formation = effect == 161 ? (ushort)738 : (ushort)71;
        int targetModel = effect == 161 ? 131 : 36;
        const int invocation = 5 << 16;
        const string casterId = "party-0";
        BattleNativeAction squall = Assert.Single(assets.NativeActions, action => action is { Command: 249, ActionId: 0, CharacterId: 0 });
        Assert.Contains(assets.LoadEncounter(formation).Enemies, placement => placement.ModelId == targetModel);
        ushort[] selectors = assets.ReadNativeRenzokukenMagicIds(targetModel).ToArray();
        if (effect is ushort requestedEffect)
        {
            Assert.Contains(requestedEffect, selectors.Skip(2));
        }

        // The selected result must come from a fresh encounter seed and a real HUD crisis roll.
        for (int seed = 0; seed < 96; seed++)
        {
            using GameWorld world = StartBattle(directory, field, formation, limit: squall, nativeSeed: (byte)seed, prepareHigherBand: true);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == casterId);
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
            Assert.Equal(10, target.Slot!.Value.Position);
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
            Assert.Equal(0, command.NativeTargetMask);
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
            CompleteExpandedRenzokuken(world, battle, casterId);
            BattleEvent[] contacts = battle.Events.Skip(before).Where(entry => entry.ActorId == casterId && entry.Kind == "EffectContact" && entry.Ability == BattleAbility.LimitBreak && entry.Message == "Renzokuken").ToArray();
            Assert.Equal(resource.Timeline.InputThresholds.Length, contacts.Length);
            Assert.All(contacts, contact => Assert.Equal(target.Id, contact.TargetId));
            Assert.True(target.CurrentHp < initialHp);
            Assert.False(caster.IsBusy);
            Assert.Null(battle.GetNativeRenzokuken(casterId));
            Assert.Null(battle.Capture().NativeTimedAction);
            Assert.Single(battle.Events.Skip(before), entry => entry.ActorId == casterId && entry.Kind == "ActionComplete");
            if (finisher is not null)
            {
                return ("logical", $"Formation {formation}, seed {seed}, original weapon {caster.WeaponId}, {finisher.Name} effect {finisher.MagicId}, {contacts.Length} Renzokuken contacts and JSON continuation.");
            }
            return ("logical", $"Formation {formation}, seed {seed}, {target.Id}, original Renzokuken effect {effect}, {contacts.Length} contacts and JSON continuation.");
        }

        return ("blocked", $"No bounded genuine HUD invocation selected the installed effect {finisher?.MagicId ?? effect}.");
    }

    private static void CompleteExpandedRenzokuken(GameWorld world, BattleSession battle, string casterId)
    {
        TimeSpan frameTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30);
        BattleSession? restored = null;
        for (int frame = 0; frame < 480 && battle.GetNativeRenzokuken(casterId) is not null; frame++)
        {
            if (battle.GunbladeTriggerWindowOpen)
            {
                world.ApplyBattleInput(new(BattleInputKind.GunbladeTrigger));
                restored?.TriggerGunblade();
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            battle.AdvanceNativeRenzokukenPresentation(frameTime);
            if (restored is not null)
            {
                restored.Tick();
                restored.AdvanceNativeRenzokukenPresentation(frameTime);
            }
            else if (battle.GetNativeRenzokuken(casterId) is { Input.ContactIndex: > 0 })
            {
                string snapshot = JsonSerializer.Serialize(battle.Capture());
                restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);
                Assert.Equal(snapshot, JsonSerializer.Serialize(restored.Capture()));
            }
        }
        Assert.NotNull(restored);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
