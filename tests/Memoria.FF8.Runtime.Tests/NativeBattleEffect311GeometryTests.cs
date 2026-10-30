using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect311GeometryTests
{
    [Fact]
    public void Group20_screen_capture_keeps_exact_copied_pixel_coordinates_asymmetric_clamps_and_zero_payload()
    {
        byte[] mesh = Mesh(true);
        SetPoint(mesh, 0, -8, -16);
        SetPoint(mesh, 1, 2560, 2000);
        SetPoint(mesh, 2, 8, 2000);
        SetPoint(mesh, 3, 8, -8);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(60 + corner * 2), (ushort)(corner * 3));
        }
        var matrix = new NativeBattleActionMatrix([4096, 0, 0, 0, 4096, 0, 0, 0, 0], 0, 0, 0, 4096);
        var native = NativeBattleEffect311Geometry.Emit665D50(mesh.ToImmutableArray(), matrix, 123, 0, Colors);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var identity = new NativeBattlePilotVisualIdentity(311, 0x25073E8, 0, 123, 0x10000) { ResourceGeneration = 0x101B0E8 };
        var captured = NativeBattleEffect311Geometry.Emit665D50(mesh.ToImmutableArray(), matrix, 123, 0, Colors, identity);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Null(Assert.Single(captured.Segments[0].Batch.Attempts).Primitive!.PilotGeometry);
        Assert.Null(Assert.Single(captured.Segments[2].Batch.Attempts).Primitive!.PilotGeometry);
        var geometry = Assert.Single(captured.Segments[1].Batch.Attempts).Primitive!.PilotGeometry!;
        Assert.True(geometry.ScreenSpace);
        Assert.Equal(8, geometry.ScreenScale);
        Assert.Equal((uint)1, captured.Segments[1].Flags20);
        var nativePrimitive = Assert.Single(native.Segments[1].Batch.Attempts).Primitive!;
        for (int corner = 0; corner < 4; corner++)
        {
            uint xy = nativePrimitive.Words[corner * 2 + 2];
            var sampled = NativeBattlePilotVisualGeometry.SampleCorner(geometry, geometry, corner, 0.5f);
            Assert.Equal(unchecked((short)xy), sampled.X * geometry.ScreenScale * 0.125f);
            Assert.Equal(unchecked((short)(xy >> 16)), sampled.Y * geometry.ScreenScale * 0.125f);
        }
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(System.Numerics.Vector4.Zero, geometry.SourceDepths!.Value);
        Assert.Equal(new System.Numerics.Vector3(0, -2, 0), geometry.A);
        Assert.Equal(new System.Numerics.Vector3(319, 250, 0), geometry.B);
        Assert.Equal(new System.Numerics.Vector3(1, 215, 0), geometry.C);
        Assert.Equal(new System.Numerics.Vector3(1, 0, 0), geometry.D);
        Assert.Equal(identity.Generation, geometry.Identity.Generation);
    }

    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static NativeBattleSummonColors Colors => new(0, 0, 1f / 4096, 4503601774854144d);

    [Fact]
    public void MeshInitializationUsesNativeLookupWidthsAndClearsZeroVectorPackedData()
    {
        byte[] mesh = Mesh(false);
        SetPoint(mesh, 0, 0, 0);
        SetPoint(mesh, 1, 4, 0);
        SetPoint(mesh, 2, 0, 4);
        SetPoint(mesh, 3, -4, 0);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(12 + index * 8), 0xDEADBEEF);
        }
        List<uint> reads = [];
        ImmutableArray<byte> original = mesh.ToImmutableArray();
        ImmutableArray<byte> result = NativeBattleEffect311Geometry.InitializeMesh(original, Read);
        Assert.Equal(0u, U32(result, 12));
        Assert.Equal(0x200200u, U32(result, 20));
        Assert.Equal(0x280000u, U32(result, 28));
        Assert.Equal(0x200200u, U32(result, 36));
        Assert.Equal(0xDEADBEEFu, U32(original, 12));
        Assert.Equal(new uint[] { 0x1021048, 0x10219C6, 0x1021048, 0x10219C6, 0x1021048, 0x10219C6 }, reads);

        ImmutableArray<byte> Read(uint address, int length)
        {
            Assert.Equal(2, length);
            reads.Add(address);
            return address < 0x10211C8 ? [0, 16] : [0, 8];
        }
    }

    [Theory]
    [InlineData(0, new int[] { 0, 1, 2, 3 })]
    [InlineData(1, new int[] { 1, 3, 0, 2 })]
    [InlineData(2, new int[] { 2, 0, 3, 1 })]
    [InlineData(3, new int[] { 3, 2, 1, 0 })]
    public void NativeQuadReorderingPreservesCornerColorsAndConvertsScratchStride(int minimum, int[] order)
    {
        byte[] mesh = Mesh(true);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            SetPoint(mesh, vertex, (short)(vertex == minimum ? -4 : 4), 0);
        }
        ImmutableArray<byte> result = NativeBattleEffect311Geometry.InitializeMesh(mesh.ToImmutableArray(), (_, _) => [0, 16]);
        Assert.Equal(order.Select(index => (ushort)(index * 3)), Enumerable.Range(0, 4).Select(corner => U16(result, 60 + corner * 2)));
        Assert.Equal(order.Select(index => 0x101010u + (uint)index), Enumerable.Range(0, 4).Select(corner => U32(result, 56 + ColorOffset(corner))));
    }

    [Fact]
    public void NativeTriangleReorderingUsesFirstStrictMinimumAndRotatesColors()
    {
        byte[] mesh = Mesh(false);
        SetPoint(mesh, 0, 4, 0);
        SetPoint(mesh, 1, -4, 0);
        SetPoint(mesh, 2, -4, 0);
        ImmutableArray<byte> result = NativeBattleEffect311Geometry.InitializeMesh(mesh.ToImmutableArray(), (_, _) => [0, 16]);
        Assert.Equal(new ushort[] { 3, 6, 0 }, Enumerable.Range(0, 3).Select(corner => U16(result, 56 + corner * 2)));
        Assert.Equal(new uint[] { 0x101011, 0x101012, 0x101010 }, Enumerable.Range(0, 3).Select(corner => U32(result, 52 + ColorOffset(corner))));
    }

    [Fact]
    public void TargetBoundsUseNativeScaleOffsetAndSignedDepthAdmission()
    {
        NativeBattleGraphicsModel model = new(0, 2) { Transform = new(new(0, 0, 0), new(0, 0, 0), 9999, 0, 0) };
        NativeBattleEffect311Bounds bounds = NativeBattleEffect311Geometry.IncludeBounds6661D0(NativeBattleEffect311Bounds.Initial, model, View, Projection);
        Assert.Equal(new NativeBattleEffect311Bounds(157, 94, 163, 108, 4096, true), bounds);
        NativeBattleEffect311Bounds behindSignedDepth = NativeBattleEffect311Geometry.IncludeBounds6661D0(NativeBattleEffect311Bounds.Initial, model, View with { Z = 40000 }, Projection);
        Assert.Equal(NativeBattleEffect311Bounds.Initial, behindSignedDepth);
    }

    [Fact]
    public void ScreenMeshUsesPixelProxiesAndExactAsymmetricClampBranches()
    {
        byte[] mesh = Mesh(true);
        SetPoint(mesh, 0, -8, -16);
        SetPoint(mesh, 1, 2560, 2000);
        SetPoint(mesh, 2, 8, 2000);
        SetPoint(mesh, 3, 8, -8);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(60 + corner * 2), (ushort)(corner * 3));
        }
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 0], 0, 0, 0, 4096);
        NativeBattleEffect311ScreenMeshBatch batch = NativeBattleEffect311Geometry.Emit665D50(mesh.ToImmutableArray(), matrix, 123, 0, Colors);
        Assert.Equal(new ushort[] { 0, 1, 0 }, batch.Segments.Select(segment => segment.Flags20));
        Assert.Equal(0xE1000220u, batch.Segments[0].Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0xE1000240u, batch.Segments[2].Batch.Attempts[0].Primitive!.Words[1]);
        NativeBattleGfGeometryAttempt body = Assert.Single(batch.Segments[1].Batch.Attempts);
        Assert.Equal(36, batch.Segments[1].Batch.AllocationBytes);
        Assert.Equal(123, body.Primitive!.Bucket);
        Assert.Equal(new uint[] { 0x8000000, 0x3A101010, XY(0, -2), 0x101011, XY(319, 250), 0x101012, XY(1, 215), 0x101013, XY(1, 0) }, body.Primitive.Words);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, body.Primitive.VertexPayload4);
        Assert.All(body.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Fact]
    public void ScreenMeshInterpolatesAllCornerColorsAndStillEmitsDegenerateFaces()
    {
        byte[] mesh = Mesh(false);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            SetPoint(mesh, vertex, 0, 0);
        }
        for (int corner = 0; corner < 3; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(56 + corner * 2), (ushort)(corner * 3));
        }
        NativeBattleActionMatrix matrix = new([0, 0, 0, 0, 0, 0, 0, 0, 0], 80, 80, 0, 4096);
        NativeBattleGfGeometryAttempt body = Assert.Single(NativeBattleEffect311Geometry.Emit665D50(mesh.ToImmutableArray(), matrix, 17, 4096, Colors).Segments[1].Batch.Attempts);
        Assert.Equal(new uint[] { 0x6000000, 0x32000000, XY(10, 10), 0, XY(10, 10), 0, XY(10, 10) }, body.Primitive!.Words);
    }

    private static byte[] Mesh(bool quad)
    {
        byte[] mesh = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 32);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 4);
        int count = quad ? 52 : 48;
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(count), 1);
        int face = count + 4;
        for (int corner = 0; corner < (quad ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(face + ColorOffset(corner)), 0x101010u + (uint)corner);
        }
        return mesh;
    }

    private static void SetPoint(byte[] mesh, int index, short x, short y)
    {
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + index * 8), x);
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(10 + index * 8), y);
    }

    private static int ColorOffset(int corner) => corner == 0 ? 0 : 8 + corner * 4;
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort U16(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static uint XY(int x, int y) => unchecked((ushort)x) | unchecked((uint)y << 16);
}
