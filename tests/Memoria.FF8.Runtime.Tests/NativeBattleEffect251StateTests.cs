using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect251StateTests
{
    [Fact]
    public void Callback_stack_word_has_no_cold_default_and_retains_current_value_through_JSON()
    {
        NativeBattleEffect251State state = NativeBattleEffect251State.FromColdProcess();
        Assert.Null(state.CallbackStackWord58);
        Assert.Null(NativeBattleEffect251State.FromUnknown().CallbackStackWord58);
        Assert.Throws<NotSupportedException>(() => state.ReadCallbackStackWord58());
        state = state with { CallbackStackWord58 = 0xF13D };
        state = JsonSerializer.Deserialize<NativeBattleEffect251State>(JsonSerializer.Serialize(state))!;
        Assert.Equal((ushort)0xF13D, state.ReadCallbackStackWord58());
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(state));
        Dictionary<string, JsonElement> fields = document.RootElement.EnumerateObject().Where(property => property.Name != nameof(NativeBattleEffect251State.CallbackStackWord58)).ToDictionary(property => property.Name, property => property.Value.Clone());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleEffect251State>(JsonSerializer.Serialize(fields)));
    }
    [Fact]
    public void Original_programmes_bind_once_and_do_not_reset_warm_counter_edits()
    {
        ImmutableDictionary<uint, ImmutableArray<byte>> data = ScriptRanges.ToImmutableDictionary(range => range.Address, range => Enumerable.Repeat((byte)0x2A, range.Length).ToImmutableArray());
        NativeBattleEffect251Resources resources = new(null!, null!, null!, null!, null!, null!, data);
        NativeBattleEffect251State state = NativeBattleEffect251State.FromColdProcess().BindOriginalScriptSources(resources);
        Assert.True(state.ScriptSourcesBound);
        Assert.Equal(data[ScriptRanges[0].Address].ToArray(), state.Read(ScriptRanges[0].Address, ScriptRanges[0].Length));
        state = state.WriteWord(ScriptRanges[0].Address + 6, 1);
        Assert.Same(state, state.BindOriginalScriptSources(resources));
        Assert.Equal((ushort)1, state.ReadWord(ScriptRanges[0].Address + 6));
    }

    [Fact]
    public void Sparse_globals_and_mutable_programmes_have_separate_explicit_owners()
    {
        NativeBattleEffect251State cold = NativeBattleEffect251State.FromColdProcess();
        NativeBattleEffect251State unknown = NativeBattleEffect251State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(56, OwnedRanges.Sum(range => range.Length));
        Assert.False(cold.ScriptSourcesBound);
        Assert.Null(unknown.ScriptSourcesBound);
        Assert.Empty(cold.ScriptStorage);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], cold.Read(address, length));
            Assert.Throws<NotSupportedException>(() => unknown.Read(address, length));
        }
        Assert.Throws<InvalidDataException>(() => cold.Read(0x255639C, 1));
        Assert.Throws<InvalidDataException>(() => cold.Read(RootBank, 1));
        Assert.Throws<InvalidDataException>(() => cold.Read(PauseAddress, 4));
        Assert.Throws<InvalidDataException>(() => cold.Read(TrailDescriptor + 8, 1));
        Assert.Throws<NotSupportedException>(() => cold.Read(ScriptRanges[0].Address, 2));
        Assert.Throws<NotSupportedException>(() => unknown.BindOriginalScriptSources(null!));
    }

    [Fact]
    public void Warm_programme_counter_and_unknown_padding_survive_JSON_without_rebinding()
    {
        NativeBattleEffect251State state = NativeBattleEffect251State.FromColdProcess() with
        {
            ScriptSourcesBound = true,
            ScriptStorage = ScriptRanges.ToImmutableDictionary(range => range.Address, range => NativeBattleEffectArena.Clear().MarkUnknown(0, range.Length))
        };
        uint address = ScriptRanges[0].Address + 16;
        state = state.WriteWord(address, 17);
        state = JsonSerializer.Deserialize<NativeBattleEffect251State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Same(state, state.BindOriginalScriptSources(null!));
        Assert.Equal((ushort)17, state.ReadWord(address));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(address + 2));
        Assert.Throws<InvalidDataException>(() => (state with { ScriptSourcesBound = false }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ScriptStorage = state.ScriptStorage.Remove(ScriptRanges[0].Address) }).Validate());
    }

    [Fact]
    public void Fixed_bank_and_COMMON_queues_transfer_exclusively()
    {
        NativeBattleEffect251State state = NativeBattleEffect251State.FromUnknown();
        NativeBattleEffectArena root = state.ReadBank(0).Write(14, [0xAB]);
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, root);
        Assert.Equal(new byte[] { 0xAB }, state.ReadBank(0).Read(14, 1));
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            NativeBattleEffectArena queue = state.ReadCommonQueue(shape.Descriptor);
            state = state.DetachCommonQueue(shape.Descriptor);
            Assert.Throws<InvalidDataException>(() => state.ReadCommonQueue(shape.Descriptor));
            state = state.AttachCommonQueue(shape.Descriptor, queue);
            Assert.Throws<InvalidDataException>(() => state.AttachCommonQueue(shape.Descriptor, queue));
        }
        state.Validate();
    }

    [Fact]
    public void JSON_requires_programme_binding_history()
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(NativeBattleEffect251State.FromColdProcess()));
        Dictionary<string, JsonElement> fields = document.RootElement.EnumerateObject().Where(property => property.Name != nameof(NativeBattleEffect251State.ScriptSourcesBound)).ToDictionary(property => property.Name, property => property.Value.Clone());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleEffect251State>(JsonSerializer.Serialize(fields)));
    }
}
