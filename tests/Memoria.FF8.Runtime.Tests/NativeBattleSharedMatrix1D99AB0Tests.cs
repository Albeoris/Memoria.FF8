using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSharedMatrix1D99AB0Tests
{
    [Fact]
    public void Effect_light_publication_is_the_same_camera_matrix_and_preserves_unread_padding()
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 7, Y = -8, Z = 9 }, null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().PublishEffectLight1D99AB0(packed);
        Assert.Equal(packed.Bytes, memory.SharedCustomMatrix!.Bytes);
        Assert.Equal(packed.Known, memory.SharedCustomMatrix.Known);
        Assert.Equal(NativeBattleTestSnapshots.Json(packed.ReadMatrix()), NativeBattleTestSnapshots.Json(memory.SharedCustomMatrix.ReadMatrix50D790()));
        Assert.False(memory.CaptureEffectLight1D99AB0().Known[18]);
        Assert.False(memory.CaptureEffectLight1D99AB0().Known[19]);
    }

    [Fact]
    public void Camera_matrix_publication_is_immediately_visible_to_effect_light_reads()
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = -123, Z = 456 }, 0xBEEF);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { SharedCustomMatrix = new(packed.Bytes, packed.Known) };
        Assert.Equal(NativeBattleTestSnapshots.Json(packed), NativeBattleTestSnapshots.Json(memory.CaptureEffectLight1D99AB0()));
        Assert.Equal(packed.Bytes, memory.EffectLight1D99AB0);
        Assert.Null(memory.EffectLightKnown1D99AB0);
        string json = NativeBattleTestSnapshots.Json(memory);
        JsonObject document = JsonNode.Parse(json)!.AsObject();
        Assert.True(document.ContainsKey("SharedCustomMatrix"));
        Assert.False(document.ContainsKey("EffectLight1D99AB0"));
        Assert.False(document.ContainsKey("EffectLightKnown1D99AB0"));
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(json)!;
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
        Assert.Equal(NativeBattleTestSnapshots.Json(packed), NativeBattleTestSnapshots.Json(restored.CaptureEffectLight1D99AB0()));
    }

    [Fact]
    public void Task_world_copy_keeps_actual_knownness_before_later_position_stores()
    {
        NativeBattlePackedMatrix32 world = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 11, Y = 22, Z = 33 }, null);
        NativeBattleModulePoolTask task = new(1, 0, null, 0x7C4B50, new byte[308].ToImmutableArray());
        task = task.WriteBytes(0x70, world.Bytes.AsSpan()).MarkPayloadUnknown(0x82, 2);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().PublishEffectLight1D99AB0(NativeBattleEffectOwnedModelRendering.CaptureTaskMatrix(task, 0x70));
        task = task.WriteDword(0x84, 111).WriteDword(0x88, 222).WriteDword(0x8C, 333);
        Assert.Equal(world.Bytes.ToArray(), memory.CaptureEffectLight1D99AB0().Bytes.ToArray());
        Assert.Equal(world.Known.ToArray(), memory.CaptureEffectLight1D99AB0().Known.ToArray());
        Assert.Equal(11, memory.SharedCustomMatrix!.ReadMatrix50D790().X);
        Assert.Equal(111u, task.ReadDword(0x84));
        Assert.False(memory.SharedCustomMatrix.Known[18]);
    }

    [Theory]
    [InlineData("EffectLight1D99AB0")]
    [InlineData("EffectLightKnown1D99AB0")]
    public void Separate_legacy_owners_require_explicit_migration_instead_of_selecting_one_copy(string property)
    {
        JsonObject document = JsonNode.Parse(NativeBattleTestSnapshots.Json(NativeBattleEffectMemory.FromColdProcess()))!.AsObject();
        document[property] = property == "EffectLight1D99AB0" ? JsonSerializer.SerializeToNode(new byte[32].ToImmutableArray()) : JsonSerializer.SerializeToNode(new bool[32].ToImmutableArray());
        Assert.Throws<JsonException>(() => NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(document.ToJsonString()));
    }

    [Fact]
    public void Cold_missing_and_required_unknown_lanes_are_not_reconstructed()
    {
        NativeBattleEffectMemory cold = NativeBattleEffectMemory.FromColdProcess();
        Assert.Null(cold.SharedCustomMatrix);
        Assert.All(cold.CaptureEffectLight1D99AB0().Known, known => Assert.False(known));
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        NativeBattleEffectMemory unknown = cold.PublishEffectLight1D99AB0(packed with { Known = packed.Known.SetItem(20, false) });
        Assert.Throws<NotSupportedException>(() => unknown.SharedCustomMatrix!.ReadMatrix50D790());
        Assert.False(unknown.CaptureEffectLight1D99AB0().Known[20]);
    }
}
