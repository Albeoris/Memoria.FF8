using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGaugeRegistrationPresentationTests
{
    [Theory]
    [InlineData(0x219u, 0x19u)]
    [InlineData(0x215u, 0x15u)]
    public void Installed_participant_deferred_ready_callback_is_consumed_once_in_bank_one_preserving_reaction_and_full_gauge(uint registeredFlags, uint consumedFlags)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original participant and native clock resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleDefinition definition = NativeBattleMusicSessionPresentationTests.CreateDefinition(game, assets) with { NativeClockRules = assets.ReadNativeClockRules(), NativeBattleSpeed = game.ReadNewGameInputConfiguration().NativeBattleSpeedByte };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        battle.SetMode(BattleAtbMode.Normal);
        BattleSnapshot source = battle.Capture();
        BattleCombatantSnapshot enemy = source.Combatants.Single(actor => actor.Id == "enemy");
        NativeBattleParticipantSnapshot participant = enemy.NativeParticipant!;
        NativeBattleAtbState gauge = enemy.NativeAtb!;
        //Explicit post-registration boundary from4842B0, with an unrelated pending reaction retained.
        //The actual action-pause owner blocks event dispatch while4847B0 still publishes the queued callback.
        source = source with
        {
            NativeClock = source.NativeClock! with { ActionPause = 1 },
            NativeReactionOrder = 1,
            Combatants = source.Combatants.Select(actor => actor.Id == enemy.Id ? actor with { Atb = BattleSession.AtbMaximum, NativeAtb = gauge with { Current = gauge.Maximum }, NativeParticipant = participant with { NativeCombatantFlags = registeredFlags, PendingReaction = new(2, 0) } } : actor).ToArray()
        };
        BattleSession first = Restore(source);
        BattleSession second = Restore(source);
        TimeSpan callbackStep = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callbackStep);
        second.AdvanceNativeGraphicsPresentation(callbackStep);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        BattleSnapshot after = first.Capture();
        BattleCombatantSnapshot retained = after.Combatants.Single(actor => actor.Id == "enemy");
        Assert.Equal(consumedFlags, retained.NativeParticipant!.NativeCombatantFlags);
        Assert.Equal(new NativeBattlePendingReaction(2, 0), retained.NativeParticipant.PendingReaction);
        Assert.Equal(gauge.Maximum, retained.NativeAtb!.Current);
        Assert.Empty(after.NativeCommandBanks!.Banks[0].LogicalCommands.OfType<BattleAttackCommand>());
        BattleAttackCommand command = Assert.Single(after.NativeCommandBanks.Banks[1].LogicalCommands.OfType<BattleAttackCommand>());
        Assert.Equal(new BattleNativeCallback(3, 1), command.NativeCallback);
        Assert.Equal(1, after.NativeCommandBanks.CountActor(3));
        BattleSession restored = Restore(after);
        first.AdvanceNativeGraphicsPresentation(callbackStep);
        restored.AdvanceNativeGraphicsPresentation(callbackStep);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Single(first.NativeCommandBanks.Banks[1].LogicalCommands.OfType<BattleAttackCommand>());
        Assert.Equal(new NativeBattlePendingReaction(2, 0), first.Capture().Combatants.Single(actor => actor.Id == "enemy").NativeParticipant!.PendingReaction);
    }

    private static BattleSession Restore(BattleSnapshot source)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!);
    }
}
