using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGravija214MeshContext88Tests
{
    [Theory]
    [InlineData(3, false)]
    [InlineData(5, false)]
    [InlineData(7, false)]
    [InlineData(3, true)]
    [InlineData(5, true)]
    [InlineData(7, true)]
    public void Group16_programme_capture_keeps_selected_loads_and_native_rejection(int group, bool near)
    {
        List<int> nativeReads = [];
        List<int> capturedReads = [];
        NativeBattleVector Read(int offset, List<int> reads)
        {
            reads.Add(offset);
            if (near)
            {
                Assert.NotEqual(24, offset);
            }
            return new((short)(offset == 0 || offset == 16 ? -400 : 400), (short)(offset < 16 ? -400 : 400), (short)(near ? -1999 : 0));
        }
        var native = Draw(Mesh(group), Context(0x2030), offset => (Read(offset, nativeReads), null));
        NativeBattlePilotVisualIdentity identity = new(214, 123, 0, 42, 71);
        var captured = Draw(Mesh(group), Context(0x2030), offset => (Read(offset, capturedReads), null), identity);
        Assert.Equal(nativeReads, capturedReads);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        if (near)
        {
            Assert.Null(Assert.Single(captured.Batch.Attempts).Primitive);
        }
        else
        {
            var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Batch.Attempts).Primitive!.PilotGeometry);
            Assert.Equal(identity.Generation, geometry.Identity.Generation);
            Assert.Equal(new System.Numerics.Vector3(-400, -400, 2000), geometry.A);
            Assert.Equal(new System.Numerics.Vector3(400, 400, 2000), geometry.D);
        }
    }

    [Theory]
    [InlineData(3, 0x2030u)]
    [InlineData(3, 0x20F0u)]
    [InlineData(5, 0x2030u)]
    [InlineData(5, 0x20F0u)]
    [InlineData(7, 0x2030u)]
    [InlineData(7, 0x20F0u)]
    public void Selected_programme_preserves_source_material_and_uses_signed_bias(int group, uint flags)
    {
        var context = Context(flags);
        var mesh = Mesh(group);
        var actual = Draw(mesh, context);
        var attempt = Assert.Single(actual.Batch.Attempts);
        var primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(110, primitive.Bucket);
        Assert.Equal(372u, Dword(actual.Context88, 0x2C));
        Assert.Equal(unchecked((uint)-128), Dword(actual.Context88, 0x18));
        Assert.Equal(0x20000000u, Dword(actual.Context88, 4));
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, primitive.VertexPayload4);
        if (group is 3 or 7)
        {
            Assert.Equal(0x12345678u, primitive.Words[3]);
        }
        Assert.Equal(flags == 0x20F0 ? 2048u : 0u, actual.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, Dword(actual.Context88, 0x20));
    }

    [Fact]
    public void First_near_rejection_keeps_bias_and_cue_and_does_not_read_fourth()
    {
        var actual = Draw(Mesh(7), Context(0x20F0).MarkUnknown(8, 8), offset =>
        {
            Assert.NotEqual(24, offset);
            return (new((short)offset, 0, -1999), null);
        });
        Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Equal(unchecked((uint)-128), Dword(actual.Context88, 0x18));
        Assert.NotEqual(0u, Dword(actual.Context88, 0x30) & 0x60000);
    }

    [Fact]
    public void Untextured_G4_does_not_consume_retained_TPAGE_or_UV_operands()
    {
        var context = Context(0x2030).MarkUnknown(0x10, 8);
        var actual = Draw(Mesh(5), context);
        Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Throws<NotSupportedException>(() => actual.Context88.Read(0x10, 8));
    }

    private static NativeBattleGravija214MeshContext88Step Draw(NativeBattleStaticMeshResource mesh, NativeBattleEffectArena context, Func<int, (NativeBattleVector Vertex, short? W)>? read = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleVector[] points = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        var memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return NativeBattleGravija214MeshContext88.DrawLoaded(memory, mesh, new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, read ?? (offset => (points[offset / 8], null)), pilotIdentity);
    }

    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(0x10000000u)).Write(4, BitConverter.GetBytes(0x20000000u)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(2048)).Write(24, BitConverter.GetBytes(-128)).Write(28, BitConverter.GetBytes(flags));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        int length = group == 7 ? 36 : 24;
        byte[] bytes = new byte[32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 0);
        int face = group * 4 + 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), (group == 3 ? 0x2E112233u : group == 5 ? 0x3A112233u : 0x3E112233u));
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x9ABCDEF0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x11112222);
        if (group == 7)
        {
            for (int corner = 0; corner < 3; corner++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 24 + corner * 4), 0x00445566);
            }
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
