using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect314PresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Morph_capture_preserves_detached_native_bytes_and_actual_deformed_world_depths(bool quad)
    {
        byte[] source = NativeBattleEffect301BodyTests.Geometry(quad);
        NativeBattleEffect314MorphBodyStep Draw(NativeBattlePilotVisualIdentity? identity)
        {
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
            NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes(0x1000u)).Write(0x1C, BitConverter.GetBytes(1u)).Write(0x10, BitConverter.GetBytes((short)320)).Write(0x12, BitConverter.GetBytes((short)216)).Write(0x14, BitConverter.GetBytes(0x123456u));
            return NativeBattleEffect314Morph.DrawBodyWithPresentation(memory, temporary, NativeBattleEffectArena.Clear().MarkUnknown(0, 32), (address, length) => NativeBattleEffectArena.Clear().Write(0, source.AsSpan(checked((int)(address - 0x2000)), length)), 0x2000, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null)], NativeBattleEffectArena.Clear(), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), 0x1000, 32, pilotIdentity: identity);
        }
        var plain = Draw(null);
        string frozen = JsonSerializer.Serialize(new { Memory = NativeBattleStorageSnapshots.Capture(plain.Memory), Temporary156 = NativeBattleStorageSnapshots.Capture(plain.Temporary156), VertexScratch = NativeBattleStorageSnapshots.Capture(plain.VertexScratch) });
        var captured = Draw(new(314, 0x123400, 1, 11, 0) { ResourceGeneration = 37 });
        Assert.Equal(frozen, JsonSerializer.Serialize(new { Memory = NativeBattleStorageSnapshots.Capture(captured.Memory), Temporary156 = NativeBattleStorageSnapshots.Capture(captured.Temporary156), VertexScratch = NativeBattleStorageSnapshots.Capture(captured.VertexScratch) }));
        NativeBattleGfGeometryAttempt expected = Assert.Single(plain.Batch.Attempts);
        NativeBattleGfGeometryAttempt actual = Assert.Single(captured.Batch.Attempts);
        Assert.Equal(expected.Data.ToArray(), actual.Data.ToArray());
        Assert.Equal(expected.WriteMask.ToArray(), actual.WriteMask.ToArray());
        Assert.Equal(expected.Primitive!.Words.ToArray(), actual.Primitive!.Words.ToArray());
        Assert.Equal(expected.Primitive.VertexPayload4.ToArray(), actual.Primitive.VertexPayload4.ToArray());
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(actual.Primitive.PilotGeometry);
        Assert.Equal((ushort)314, geometry.Identity.Effect);
        Assert.Equal(quad ? 4 : 3, geometry.CornerCount);
        Assert.Equal(new System.Numerics.Vector3(-100, -100, 1000), geometry.A);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(actual.Primitive.VertexPayload4[0], geometry.SourceDepths!.Value.X);
    }
}
