using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Installed_expanded_automatic_guardian_dispatches_from_the_original_entry_roll(ushort id)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action is { KernelSection: 9, Command: 245 } && action.ActionId == id);
        (string status, string detail) = RunExpandedAutomaticGuardian(directory, PrepareField(directory), action);
        Assert.True(status == "logical", detail);
    }

    private static (string Status, string Detail) RunExpandedAutomaticGuardian(string directory, GameState field, BattleNativeAction action)
    {
        const ushort formation = 71;
        const byte odinUnlock = 2;
        const byte gilgameshUnlock = 8;
        using GameAssets game = new(directory);
        NativeSavedBattleFlags flags = field.SavedBattleFlags ?? game.ReadNewGameSavedBattleFlags();
        Assert.Equal(0x0A, flags.EffectiveKnownMask & 0x0A);
        byte selectedUnlock = action.ActionId == 0 ? odinUnlock : gilgameshUnlock;
        field = field with
        {
            SavedBattleFlags = flags with { Value = (byte)((flags.Value & ~0x0A) | selectedUnlock) },
            CharacterVitals = field.CharacterVitals!.AcquireGuardianForce(0)
        };

        for (int seed = 0; seed <= byte.MaxValue; seed++)
        {
            using GameWorld world = StartBattle(directory, field, formation, nativeSeed: (byte)seed);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            BattleAttackCommand? queued = battle.QueuedActions.FirstOrDefault(command => command is { NativeQueuedCommand: 245, NativePlayerReaction: BattleNativePlayerReaction.AutomaticGuardian });
            if (queued?.NativeAction?.ActionId != action.ActionId)
            {
                continue;
            }

            Assert.Equal(action.MagicId, queued.NativeAction.MagicId);
            Assert.Equal("party-0", queued.ActorId);
            Assert.Equal(0, queued.NativeTargetMask);
            Assert.Null(queued.NativeCallback);
            Assert.True(battle.Capture().NativeSupport!.EntryInitialized);
            string[] targets = battle.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.Id).ToArray();
            Assert.Equal(11, targets.Length);
            int start = battle.Events.Count;
            string snapshot = JsonSerializer.Serialize(battle.Capture());
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);
            for (int frame = 0; frame < 300 && !battle.Events.Skip(start).Any(entry => entry.ActorId == queued.ActorId && entry.Kind == "ActionComplete"); frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                restored.Tick();
            }
            BattleEvent[] events = battle.Events.Skip(start).Where(entry => entry.ActorId == queued.ActorId).ToArray();
            Assert.Single(events, entry => entry.Kind == "ActionComplete");
            Assert.Equal(targets, events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.TargetId));
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            return ("logical", $"Formation {formation}, seed {seed}, original automatic {action.Name}, {targets.Length} contacts and JSON continuation.");
        }

        return ("blocked", $"No original entry seed selected automatic guardian {action.Name} in formation {formation}.");
    }
}
