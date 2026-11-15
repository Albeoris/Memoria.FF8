using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    public void Installed_expanded_Angelo_support_uses_original_reaction_or_periodic_clock(ushort id)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action is { KernelSection: 9, Command: 240 } && action.ActionId == id);
        (string status, string detail) = RunExpandedAngeloSupport(directory, PrepareField(directory), action);
        Assert.True(status == "logical", detail);
    }

    private static (string Status, string Detail) RunExpandedAngeloSupport(string directory, GameState field, BattleNativeAction action)
    {
        const ushort formation = 71;
        const byte rinoaId = 4;
        using GameAssets game = new(directory);
        NativeSavedBattleFlags flags = field.SavedBattleFlags ?? game.ReadNewGameSavedBattleFlags();
        Assert.Equal(0x1A, flags.EffectiveKnownMask & 0x1A);
        NativeLimitProgress progress = Assert.IsType<NativeLimitProgress>(field.LimitProgress);
        byte learned = (byte)(1 << (action.ActionId - 11));
        field = field with
        {
            SavedBattleFlags = flags with { Value = (byte)(flags.Value & ~0x1A) },
            LimitProgress = progress with { AngeloFlags = (byte)((progress.AngeloFlags & 0xF0) | learned) },
            CharacterVitals = field.CharacterVitals!.AcquireGuardianForce(0)
        };

        foreach (byte seed in new byte[] { 0, 5, 9, 11, 19, 30 })
        {
            using GameWorld world = StartBattle(directory, field, formation, nativeSeed: seed, prepareParty: records =>
            {
                if (action.ActionId is 12 or 13)
                {
                    ushort hp = action.ActionId == 12 ? (ushort)450 : (ushort)1;
                    records[rinoaId] = records[rinoaId] with { HitPoints = new(hp, hp) };
                }
            });
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            BattleCombatant rinoa = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == rinoaId);
            world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.Normal));
            BattleActionPresentation? cast = null;
            int firstEvent = battle.Events.Count;
            for (int frame = 0; frame < 4500 && battle.Outcome == BattleOutcome.Active; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (cast is null && battle.ActiveAction is { NativeQueuedCommand: 240 } active && active.NativeAction?.ActionId == action.ActionId)
                {
                    cast = active;
                    Assert.IsNotType<NativeBattleTargetRequestAdapter.DeferredOperands>(cast.TargetRequest.Selection);
                    Assert.Equal(action.MagicId, cast.NativeAction.MagicId);
                }
                if (cast is null)
                {
                    firstEvent = battle.Events.Count;
                    continue;
                }
                BattleEvent[] events = battle.Events.Skip(firstEvent).Where(entry => entry.ActorId == cast.ActorId).ToArray();
                if (!events.Any(entry => entry.Kind == "ActionComplete"))
                {
                    continue;
                }
                BattleEvent contact = Assert.Single(events, entry => entry.Kind == "EffectContact");
                Assert.Equal(cast.TargetId, contact.TargetId);
                if (action.ActionId == 11)
                {
                    Assert.Equal(rinoa.Id, cast.ActorId);
                    Assert.Contains(battle.Events.Take(firstEvent), entry => entry.ActorId == cast.TargetId && entry.TargetId == rinoa.Id && entry.Kind == "Damage" && entry.Amount > 0);
                }
                else
                {
                    Assert.Equal(rinoa.Id, contact.TargetId);
                }
                if (action.ActionId == 13)
                {
                    Assert.True(rinoa.IsAlive);
                }
                return ("logical", $"Formation {formation}, seed {seed}, {cast.NativePlayerReaction}, original {action.Name} contacted {contact.TargetId} once.");
            }
        }
        return ("blocked", $"The original {action.Name} trigger was not observed in the bounded entries.");
    }
}
