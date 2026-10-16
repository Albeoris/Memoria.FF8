using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class NativeSavedBattleFlagTests
{
    [Theory]
    [InlineData(0xF2, 0xFC)]
    [InlineData(0x01, 0x0D)]
    public void Phoenix_and_completion_masks_compose_in_either_native_order(byte before, byte expected)
    {
        byte phoenixFirst = NativeSavedBattleFlags.ApplyBattleEffects(before, 4, 0);
        byte completionFirst = NativeSavedBattleFlags.ApplyBattleEffects(before, 8, 2);

        Assert.Equal(expected, NativeSavedBattleFlags.ApplyBattleEffects(phoenixFirst, 8, 2));
        Assert.Equal(expected, NativeSavedBattleFlags.ApplyBattleEffects(completionFirst, 4, 0));
        NativeBattleScriptState receipt = new() { SavedBattleFlagsBefore = before, SavedBattleFlagsAfter = expected, SavedBattleFlagsSetMask = 12, SavedBattleFlagsClearMask = 2 };
        receipt.Validate();
        string snapshot = JsonSerializer.Serialize(receipt);
        Assert.Equal(snapshot, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleScriptState>(snapshot)));
        Assert.Throws<InvalidDataException>(() => (receipt with { SavedBattleFlagsClearMask = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => NativeSavedBattleFlags.ApplyBattleEffects(before, 0x10, 0));
    }

    [Fact]
    public void Opcode54_clears_bit1_and_sets_bit3_without_erasing_other_saved_flags()
    {
        NativeSavedBattleFlags flags = new(NativeInputConfiguration.OriginalInitHash, 0xF7);

        NativeSavedBattleFlags completed = flags.CompleteBattle();

        Assert.Equal((byte)0xFD, completed.Value);
        Assert.Equal((byte)0xF7, flags.Value);
        Assert.Equal(completed, JsonSerializer.Deserialize<NativeSavedBattleFlags>(JsonSerializer.Serialize(completed)));
    }

    [Fact]
    public void Dream_alias_changes_only_saved_bit0()
    {
        NativeSavedBattleFlags flags = new(NativeInputConfiguration.OriginalInitHash, 0xFA);

        Assert.Equal((byte)0xFB, flags.WithDreamEnabled(true).Value);
        Assert.Equal((byte)0xFA, flags.WithDreamEnabled(true).WithDreamEnabled(false).Value);
    }

    [Fact]
    public void Existing_states_without_the_full_byte_keep_its_unknown_owner_absent()
    {
        GameState state = new() { PendingBattleEncounter = null };

        GameState restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!;

        Assert.Null(restored.SavedBattleFlags);
    }

    [Fact]
    public void Opcode54_changes_are_visible_to_later_conditions_and_survive_restore()
    {
        BattleSession battle = CreateBattle(255);

        Assert.Equal((byte)0xF7, battle.NativeScriptState.SavedBattleFlagsBefore);
        Assert.Equal((byte)0xFD, battle.NativeScriptState.SavedBattleFlagsAfter);
        Assert.Equal(new uint[] { 1, 1 }, battle.Capture().NativeAiVariables!.Take(2));
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(snapshot, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!).Capture()));
        BattleSnapshot tampered = battle.Capture() with { NativeScriptState = battle.NativeScriptState with { SavedBattleFlagsBefore = 0xF6, SavedBattleFlagsAfter = 0xFC } };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(tampered));
    }

    [Fact]
    public void Opcode54_rejects_an_unknown_full_byte_before_publishing_an_effect()
    {
        Assert.Contains("complete saved1CFE97A BYTE", Assert.Throws<NotSupportedException>(() => CreateBattle(1)).Message);
    }

    private static BattleSession CreateBattle(byte knownMask)
    {
        byte[] initial = [54, 2, 18, 0, 0, 3, 0, 3, 0, 15, 96, 1, 2, 18, 0, 3, 1, 0, 3, 0, 15, 97, 1, 0];
        NativeBattleAiDefinition ai = new([initial, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new BattleDefinition { NativeSavedBattleFlags = 0xF7, NativeSavedBattleFlagsKnownMask = knownMask, Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", NativeAi = ai }] }, BattleAtbMode.Normal);
        battle.Tick();
        return battle;
    }
}
