using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRinoaLimitTests
{
    [Theory]
    [InlineData(1, 0xF, 0)]
    [InlineData(4, 0x5, 2)]
    [InlineData(3, 0x3, 1)]
    public void Combine_uses_the_highest_learned_finisher_at_or_below_crisis(byte crisis, byte learned, ushort expected)
    {
        Assert.Equal(expected, NativeBattleLimitRules.AngeloActionId(crisis, learned));
        Assert.Throws<InvalidDataException>(() => NativeBattleLimitRules.AngeloActionId(crisis, 0));
    }

    [Fact]
    public void Rinoa_saved_menu_gates_and_learned_finisher_own_the_queued_action()
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession battle = CreateBattle(assets, 0x20, 0x50);
        string before = JsonSerializer.Serialize(battle.Capture());

        BattleActionDefinition[] choices = battle.GetNativePlayerActionChoices("rinoa", true).ToArray();

        Assert.Equal(new ushort[] { 2, 4 }, choices.Select(choice => choice.NativeAction!.ActionId));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Throws<InvalidOperationException>(() => battle.QueueNativePlayerCommand("rinoa", "enemy", 19, 3));
        battle.QueueNativePlayerCommand("rinoa", "enemy", 19, 2);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal((byte)19, queued.NativeQueuedCommand);
        Assert.Equal((ushort)2, queued.NativeAction!.ActionId);
        Assert.Equal((ushort)0x8007, queued.NativeTargetMask);
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(snapshot, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!).Capture()));
    }

    [Fact]
    public void Angel_Wing_is_gated_by_the_saved_byte_and_keeps_its_native_self_target()
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession hidden = CreateBattle(assets, 0x10, 0x10);
        Assert.Empty(hidden.GetNativePlayerActionChoices("rinoa", true));
        BattleSession battle = CreateBattle(assets, 0x30, 0x10);
        BattleActionDefinition choice = Assert.Single(battle.GetNativePlayerActionChoices("rinoa", true));

        battle.QueueNativePlayerCommand("rinoa", "ally", 19, choice.NativeAction!.ActionId);

        Assert.Equal((ushort)4, choice.NativeAction.ActionId);
        Assert.Equal((ushort)0x81, Assert.Single(battle.QueuedActions).NativeTargetMask);
        battle.Tick();
        Assert.NotNull(battle.ActiveAction);
        Assert.Equal("rinoa", battle.ActiveAction.TargetId);
    }

    private static BattleSession CreateBattle(BattleAssets assets, byte savedFlags, byte angeloFlags)
    {
        NativeLimitProgress progress = new(new string('A', 64), 0, 0, angeloFlags, 0, Enumerable.Repeat((byte)0, 8).ToImmutableArray());
        BattleSession battle = new(new BattleDefinition { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeActions = assets.NativeActions.ToArray(), NativeSavedBattleFlags = savedFlags, NativeSavedBattleFlagsKnownMask = 255, LimitProgress = progress, NativeLimitsDisabled = false, Combatants = [new() { Id = "rinoa", Name = "Rinoa", IsPlayer = true, ModelId = 4, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(4) }, new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("rinoa", 4)] });
    }

    private static BattleAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return new BattleAssets(directory);
    }
}
