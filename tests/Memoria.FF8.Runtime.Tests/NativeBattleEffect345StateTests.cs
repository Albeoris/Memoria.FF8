using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect345StateTests
{
    [Fact]
    public void Sparse_current_owners_exclude_shared_views_and_preserve_unknown_payloads()
    {
        NativeBattleEffect345State cold = NativeBattleEffect345State.FromColdProcess();
        cold.Validate();
        Assert.Equal(0u, cold.ReadDword(0x255EA94));
        Assert.Equal(new byte[8], cold.Read(0x2557770, 8));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(0x2793E58));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(0xB8B9A8));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(0x1D999C4));
        Assert.Throws<NotSupportedException>(() => cold.ReadDword(0x1643698));
        NativeBattleEffect345State unknown = NativeBattleEffect345State.FromUnknown();
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(0x255EA94));
        NativeBattleEffectArena view = unknown.ReadView(0x2557770, 16);
        unknown = unknown.WriteView(0x2557770, view.Write(0, new byte[8]), 16);
        Assert.Equal(new byte[8], unknown.Read(0x2557770, 8));
        Assert.Throws<NotSupportedException>(() => unknown.Read(0x2557778, 8));
    }

    [Fact]
    public void Adjacent_scalar_and_projection_owners_roundtrip_coalesced_knownness_without_admitting_gaps()
    {
        NativeBattleEffect345State state = NativeBattleEffect345State.FromUnknown();
        state.Validate();
        byte[] value = [1, 2, 3, 4, 5, 6, 7, 8];
        state = state.Write(0x255DC0C, value);
        state.Validate();
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state));
        NativeBattleEffect345State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect345State>(json)!);
        restored.Validate();
        Assert.Equal(value, restored.Read(0x255DC0C, 8));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(0x255DC14));
        Assert.Throws<InvalidDataException>(() => restored.Read(0x255E57C, 8));
        Assert.Throws<InvalidDataException>(() => restored.ReadDword(0x255EA98));
    }

    [Fact]
    public void Physical_bank_transfer_and_json_preserve_exclusive_ownership()
    {
        NativeBattleEffect345State state = NativeBattleEffect345State.FromUnknown();
        NativeBattleEffectArena bank = state.ReadBank(2).Write(12, [0xA7]);
        state = state.DetachBank(2);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(2));
        state = state.AttachBank(2, bank).WriteWord(0x255EA78, 91);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(2, bank));
        Assert.Equal((byte)0xA7, state.ReadBank(2).Read(12, 1)[0]);
        Assert.Throws<NotSupportedException>(() => state.ReadBank(2).Read(13, 1));
        state = state.DetachBank(7) with { OwnedModelSourceBound = true, OwnedModels = ImmutableDictionary<uint, NativeBattleEffectOwnedModelState>.Empty.Add(0x255E670, new(0x255E670, NativeBattleEffect345Resources.ModelAddress, null)) };
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state));
        NativeBattleEffect345State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect345State>(json)!);
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Equal((ushort)91, restored.ReadWord(0x255EA78));
        Assert.Null(restored.BankStorage[7]);
        Assert.True(restored.OwnedModelSourceBound);
    }

    [Fact]
    public void Source_origin_and_model_binding_cannot_be_inferred_from_older_json()
    {
        foreach (string property in new[] { "ColdSourceOrigin", "OwnedModelSourceBound", "NodeData", "OwnedModels" })
        {
            JsonObject old = JsonNode.Parse(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect345State.FromColdProcess())))!.AsObject();
            Assert.True(old.Remove(property));
            Assert.Throws<JsonException>(() => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect345State>(old.ToJsonString())));
        }
    }
}
