using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect9PresentationTests
{
    [Fact]
    public void Specialized_body_capture_preserves_native_writes_registers_and_physical_temporary_storage()
    {
        NativeBattleEffect9BodyDraw plain = Draw(false, false);
        NativeBattleEffect9BodyDraw captured = Draw(true, false);
        NativeBattleGfGeometryAttempt a = Assert.Single(plain.Batch.Attempts);
        NativeBattleGfGeometryAttempt b = Assert.Single(captured.Batch.Attempts);
        Assert.Equal(a.Data.ToArray(), b.Data.ToArray());
        Assert.Equal(a.WriteMask.ToArray(), b.WriteMask.ToArray());
        Assert.Equal(a.Primitive!.Words, b.Primitive!.Words);
        Assert.Equal(a.Primitive.VertexPayload4, b.Primitive.VertexPayload4);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain.Frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured.Frame)));
        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(b.Primitive.PilotGeometry);
        Assert.Equal((byte)3, visual.CornerCount);
        Assert.True(visual.UseSourceDepths);
        Assert.Equal(new System.Numerics.Vector3(0, 0, 1024), visual.A);
        Assert.Equal(new System.Numerics.Vector3(128, 0, 1024), visual.B);
        Assert.Equal(new System.Numerics.Vector3(0, 128, 1024), visual.C);
        Assert.Equal(31, visual.Identity.ResourceGeneration);
    }

    [Fact]
    public void Rejected_specialized_quad_does_not_read_its_absent_fourth_vertex_for_capture()
    {
        Assert.Empty(Draw(true, true).Batch.Attempts);
    }

    private static NativeBattleEffect9BodyDraw Draw(bool capture, bool rejected)
    {
        byte[] face = new byte[rejected ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), rejected ? (ushort)2 : (ushort)1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), rejected ? (ushort)1 : (ushort)2);
        if (rejected)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 0xFFF);
        }
        NativeBattleRawMesh mesh = new([new(new(0, 0, 0), 0), new(new(128, 0, 0), 0), new(new(0, 128, 0), 0)], [new(rejected, face.ToImmutableArray())]);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleActionMatrix matrix = identity with { Z = 1024 };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleOriginalEffectState effect = new(9, 2, new(9, NativeBattleEffect9Controller.Entry, 0x8C0360), new(0, 0, 0, 0, 0, 0), 0);
        NativeBattleEffect9Frame frame = new(memory, effect, new(1), NativeBattleEffect9State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        return NativeBattleEffect9Body.Draw(frame, [new(mesh, new([]), true) { AuthoredMesh = 3 }], [identity], matrix, new(identity, 216, 160, 108), -10, 200, 128, 1, capture ? new NativeBattlePilotVisualIdentity(9, 0x2500000, 0, 7, 0) { ResourceGeneration = 31 } : null);
    }
}
