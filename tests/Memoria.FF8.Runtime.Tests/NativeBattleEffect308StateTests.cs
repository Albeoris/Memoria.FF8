using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect308StateTests
{
    [Fact]
    public void Original_programmes_bind_once_and_do_not_reset_warm_counter_edits()
    {
        ImmutableDictionary<uint, ImmutableArray<byte>> data = ScriptRanges.ToImmutableDictionary(range => range.Address, range => Enumerable.Repeat((byte)0x2A, range.Length).ToImmutableArray());
        NativeBattleEffect308Resources resources = new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, 0, null!, data);
        NativeBattleEffect308State state = NativeBattleEffect308State.FromColdProcess().BindOriginalScriptSources(resources);
        Assert.True(state.ScriptSourcesBound);
        Assert.Equal(data[ScriptRanges[0].Address].ToArray(), state.Read(ScriptRanges[0].Address, ScriptRanges[0].Length));
        state = state.WriteWord(ScriptRanges[0].Address + 6, 1);
        Assert.Same(state, state.BindOriginalScriptSources(resources));
        Assert.Equal((ushort)1, state.ReadWord(ScriptRanges[0].Address + 6));
    }

    [Fact]
    public void Sparse_globals_and_mutable_programmes_have_separate_explicit_owners()
    {
        NativeBattleEffect308State cold = NativeBattleEffect308State.FromColdProcess();
        NativeBattleEffect308State unknown = NativeBattleEffect308State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(28, OwnedRanges.Sum(range => range.Length));
        Assert.False(cold.ScriptSourcesBound);
        Assert.Null(unknown.ScriptSourcesBound);
        Assert.Empty(cold.ScriptStorage);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], cold.Read(address, length));
            Assert.Throws<NotSupportedException>(() => unknown.Read(address, length));
        }
        Assert.Throws<InvalidDataException>(() => cold.Read(0x25074DC, 1));
        Assert.Throws<InvalidDataException>(() => cold.Read(RootBank, 1));
        Assert.Throws<InvalidDataException>(() => cold.Read(ScriptDescriptor + 8, 1));
        Assert.Throws<NotSupportedException>(() => cold.Read(ScriptRanges[0].Address, 2));
        Assert.Throws<NotSupportedException>(() => unknown.BindOriginalScriptSources(null!));
    }

    [Fact]
    public void Warm_programme_counter_and_unknown_padding_survive_JSON_without_rebinding()
    {
        NativeBattleEffect308State state = NativeBattleEffect308State.FromColdProcess() with
        {
            ScriptSourcesBound = true,
            ScriptStorage = ScriptRanges.ToImmutableDictionary(range => range.Address, range => NativeBattleEffectArena.Clear().MarkUnknown(0, range.Length))
        };
        uint address = ScriptRanges[0].Address + 16;
        state = state.WriteWord(address, 17);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect308State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Same(state, state.BindOriginalScriptSources(null!));
        Assert.Equal((ushort)17, state.ReadWord(address));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(address + 2));
        Assert.Throws<InvalidDataException>(() => (state with { ScriptSourcesBound = false }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ScriptStorage = state.ScriptStorage.Remove(ScriptRanges[1].Address) }).Validate());
    }

    [Fact]
    public void Fixed_bank_and_COMMON_queues_transfer_exclusively()
    {
        NativeBattleEffect308State state = NativeBattleEffect308State.FromUnknown();
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
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect308State.FromColdProcess())));
        Dictionary<string, JsonElement> fields = document.RootElement.EnumerateObject().Where(property => property.Name != nameof(NativeBattleEffect308State.ScriptSourcesBound)).ToDictionary(property => property.Name, property => property.Value.Clone());
        Assert.Throws<JsonException>(() => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect308State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fields)))));
    }
}
