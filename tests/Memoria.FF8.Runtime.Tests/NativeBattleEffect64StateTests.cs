using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect64StateTests
{
    [Fact]
    public void Cold_scalar_and_root_owners_exclude_COMMON_descriptors_and_gaps()
    {
        NativeBattleEffect64State state = NativeBattleEffect64State.FromColdProcess();
        state.Validate();
        Assert.NotNull(NativeBattleEffectMemory.FromColdProcess().Effect64);
        Assert.True(state.ColdSourceOrigin);
        Assert.Empty(state.ScriptSources);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(RootBank, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2545090, 1));
        NativeBattleEffectArena retained = state.ReadBank(0).Write(18, [0xFE, 0xCA]);
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, retained);
        Assert.Equal(new byte[] { 0xFE, 0xCA }, state.ReadBank(0).Read(18, 2));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, retained));
        Assert.Throws<InvalidDataException>(() => state.ReadBank(1));
    }

    [Fact]
    public void Current_script_counter_is_bound_once_and_retained_across_entries()
    {
        NativeBattleEffect64Resources resources = Sources();
        uint source = ScriptAddresses[0];
        NativeBattleEffect64State state = NativeBattleEffect64State.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => state.ReadWord(source));
        state = state.BindScriptSource(resources, source).WriteWord(source + 4, 7).BindScriptSource(resources, source);
        Assert.Equal((ushort)7, state.ReadWord(source + 4));
        Assert.Single(state.ScriptSources);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect64State.FromUnknown().BindScriptSource(resources, source));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect64State.FromUnknown().ReadDword(ArenaGlobal));
    }

    [Fact]
    public void Bound_unknown_script_bytes_are_not_replaced_by_original_input()
    {
        uint source = ScriptAddresses[1];
        NativeBattleEffectArena unknown = NativeBattleEffectArena.Clear().MarkUnknown(4, 2);
        NativeBattleEffect64State state = NativeBattleEffect64State.FromColdProcess().ReplaceScriptSource(source, unknown).BindScriptSource(Sources(), source);
        Assert.Throws<NotSupportedException>(() => state.ReadWord(source + 4));
        Assert.Throws<InvalidDataException>(() => state.ReplaceScriptSource(source, NativeBattleEffectArena.Clear().Write(ScriptLengths[source], [1])));
        Assert.Throws<InvalidDataException>(() => state.WriteWord(source + (uint)ScriptLengths[source] - 1, 1));
    }

    [Fact]
    public void Json_retains_partial_script_knownness_and_exclusive_root_ownership()
    {
        uint source = ScriptAddresses[0];
        NativeBattleEffect64State state = NativeBattleEffect64State.FromColdProcess().BindScriptSource(Sources(), source).WriteDword(CursorGlobal, 0x2100000).DetachBank(0);
        state = state.WriteView(source + 6, NativeBattleEffectArena.Clear().MarkUnknown(0, 2).Write(0, [0xA7]), 2);
        NativeBattleEffect64State restored = JsonSerializer.Deserialize<NativeBattleEffect64State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Null(restored.RootBankStorage);
        Assert.Equal(new byte[] { 0xA7 }, restored.Read(source + 6, 1));
        Assert.Throws<NotSupportedException>(() => restored.Read(source + 7, 1));
        Assert.Equal(0x2100000u, restored.ReadDword(CursorGlobal));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    private static NativeBattleEffect64Resources Sources() => new(null!, null!, null!, 0, [], ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, [], ScriptLengths.ToImmutableDictionary(pair => pair.Key, pair => Enumerable.Repeat((byte)0x3C, pair.Value).ToImmutableArray()));
}
