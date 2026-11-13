using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Fact]
    public void Installed_expanded_Renzokuken_selects_and_completes_its_original_160_timeline()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        (string status, string detail) = RunExpandedRenzokuken160(directory, PrepareField(directory), game, assets);
        Assert.Equal("logical", status);
        Assert.Contains("4 contacts", detail);
    }

    private static (string Status, string Detail) RunExpandedRenzokuken160(string directory, GameState field, GameAssets game, BattleAssets assets)
    {
        const ushort formation = 71;
        const int targetModel = 36;
        const ushort effect = 160;
        const int invocation = 5 << 16;
        const string casterId = "party-0";
        BattleNativeAction squall = Assert.Single(assets.NativeActions, action => action is { Command: 249, ActionId: 0, CharacterId: 0 });
        Assert.Contains(assets.LoadEncounter(formation).Enemies, placement => placement.ModelId == targetModel);
        BattleNativeRenzokukenResource resource = Assert.Single(game.ReadNativeRenzokukenResources(), candidate => candidate.MagicId == effect);
        Assert.Equal(4, resource.Timeline.InputThresholds.Length);

        // The selected result must come from a fresh encounter seed and a real HUD crisis roll.
        for (byte seed = 0; seed < 24; seed++)
        {
            using GameWorld world = StartBattle(directory, field, formation, limit: squall, nativeSeed: seed, prepareHigherBand: true);
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
            if (!hud.NativeLimitAvailable || battle.GetNativeLimitLevel(caster.Id) != 1)
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
            int before = battle.Events.Count;
            hud.Attack();

            BattleAttackCommand command = Assert.Single(battle.QueuedActions, queued => queued.ActorId == casterId);
            Assert.Equal((byte)5, command.NativeQueuedCommand);
            Assert.Equal((byte)1, command.NativeRenzokuken!.CrisisLevel);
            Assert.Equal(target.Id, command.TargetId);
            Assert.Equal(0, command.NativeTargetMask);
            for (int frame = 0; frame < 240 && caster.IsBusy && battle.GetNativeRenzokuken(casterId) is null; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            BattleNativeRenzokukenState process = Assert.IsType<BattleNativeRenzokukenState>(battle.GetNativeRenzokuken(casterId));
            if (process.MagicId != effect || process.FinisherActionId is not null)
            {
                continue;
            }

            Assert.Equal(target.Id, battle.Capture().NativeTimedAction!.Command.TargetId);
            CompleteExpandedRenzokuken(world, battle, casterId);
            BattleEvent[] contacts = battle.Events.Skip(before).Where(entry => entry.ActorId == casterId && entry.Kind == "EffectContact" && entry.Ability == BattleAbility.LimitBreak).ToArray();
            Assert.Equal(resource.Timeline.InputThresholds.Length, contacts.Length);
            Assert.All(contacts, contact => Assert.Equal(target.Id, contact.TargetId));
            Assert.True(target.CurrentHp < initialHp);
            Assert.False(caster.IsBusy);
            Assert.Null(battle.GetNativeRenzokuken(casterId));
            Assert.Null(battle.Capture().NativeTimedAction);
            Assert.Single(battle.Events.Skip(before), entry => entry.ActorId == casterId && entry.Kind == "ActionComplete");
            return ("logical", $"Formation {formation}, seed {seed}, {target.Id}, original Renzokuken effect {effect}, {contacts.Length} contacts and JSON continuation.");
        }

        return ("blocked", "No bounded genuine HUD invocation selected the installed effect 160 without a finisher.");
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
