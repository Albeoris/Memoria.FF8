using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeSlotsInvocationTests
{
    [Fact]
    public void Slots_roll_consumes_four_native_draws_and_render_queries_preserve_the_owned_result()
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession battle = CreateBattle(assets, 0);
        NativeBattleRandomState expectedRandom = battle.Capture().Random!;
        expectedRandom = expectedRandom.Draw(out byte levelRoll).Draw(out byte bucketRoll).Draw(out byte spellRoll).Draw(out byte repeatRoll);
        BattleNativeSlotsRoll expected = NativeBattleSlotsRules.Roll(assets.ReadNativeLimitDefinition(5), 20, 4, levelRoll, bucketRoll, spellRoll, repeatRoll);

        BattleNativeSlotsRoll roll = battle.RollNativeSlots("selphie");

        Assert.Equal(expected, roll);
        Assert.Equal(JsonSerializer.Serialize(expectedRandom), JsonSerializer.Serialize(battle.Capture().Random));
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        Assert.Single(battle.GetNativePlayerActionChoices("selphie", true));
        Assert.Equal(snapshot, JsonSerializer.Serialize(battle.Capture()));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);
        Assert.Equal(roll, restored.GetNativeSlotsRoll("selphie"));
        Assert.Equal(snapshot, JsonSerializer.Serialize(restored.Capture()));
        battle.QueueNativePlayerCommand("selphie", "enemy", 16, roll.MagicId);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal(roll, queued.NativeSlots);
        Assert.Equal((byte)16, queued.NativeAction!.Command);
        Assert.Equal(1, battle.NativeCommandBanks.CountActor(0));
        NativeBattleCommandBank bank = battle.NativeCommandBanks.Banks[1];
        Assert.Equal(roll.Repetitions, Enumerable.Range(0, 3).Count(index => bank.Packets[0].ReadWord(6 + 2 * index) != 0));
        string accepted = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(accepted, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(accepted)!).Capture()));
    }

    [Fact]
    public void Special_magic_learns_at_dispatch_and_unknown_saved_learning_fails_before_queue_mutation()
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession battle = CreateBattle(assets, 0x80);
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("selphie", 4) { Slots = new(53, 2) }] });
        battle.QueueNativePlayerCommand("selphie", "enemy", 16, 53);
        Assert.Null(battle.NativeScriptState.LimitSpecialFlagsBefore);

        battle.Tick();

        Assert.Equal((byte)0x80, battle.NativeScriptState.LimitSpecialFlagsBefore);
        Assert.Equal((byte)0x84, battle.NativeScriptState.LimitSpecialFlagsAfter);
        Assert.Equal((byte)4, battle.NativeScriptState.LimitSpecialSetMask);
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(snapshot, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!).Capture()));
        BattleSession unknown = CreateBattle(assets, null);
        unknown = BattleSession.Restore(unknown.Capture() with { NativeLimits = [new("selphie", 4) { Slots = new(53, 2) }] });
        string before = JsonSerializer.Serialize(unknown.Capture());
        Assert.Throws<NotSupportedException>(() => unknown.QueueNativePlayerCommand("selphie", "enemy", 16, 53));
        Assert.Equal(before, JsonSerializer.Serialize(unknown.Capture()));
    }

    private static BattleSession CreateBattle(BattleAssets assets, byte? specialFlags)
    {
        NativeLimitProgress progress = new(new string('A', 64), 0, 0, 0, 0, Enumerable.Repeat((byte)0, 8).ToImmutableArray()) { SpecialMagicFlags = specialFlags };
        BattleSession battle = new(new BattleDefinition { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeActions = assets.NativeActions.ToArray(), LimitProgress = progress, NativeLimitsDisabled = false, Combatants = [new() { Id = "selphie", Name = "Selphie", NativeStatusResistances = new byte[40], IsPlayer = true, ModelId = 5, Level = 20, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(5) }, new() { Id = "enemy", Name = "Enemy", NativeStatusResistances = new byte[40], Speed = 1 }] }, BattleAtbMode.TurnBased);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("selphie", 4)] });
    }

    private static BattleAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return new BattleAssets(directory);
    }
}
