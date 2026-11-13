using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignBodyOperationTests
{
    private sealed record Material : INativeBattleForeignMaterial;
    private static readonly NativeBattleActionMatrix Identity = NativeBattleForeignModelGeometry.Identity;
    private static readonly NativeBattlePilotVisualIdentity Visual = new(317, 1, 0, 1, 0);
    private const uint CacheAddress = NativeBattleEffectNodeMemory.ArenaAddress + 4096;
    private const uint ScratchAddress = 0x1000000;
    private const uint GeometryAddress = 0x2000000;
    private static NativeBattleCameraProjection Projection => new(Identity, 256, 160, 108);

    [Fact]
    public void Snapshot_consumes_original_indexed_vertices_once_including_unreferenced_vertices_and_restores_CRT_continuation()
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        byte[] raw = Raw(geometry);
        NativeBattleAcid317SnapshotStep native = NativeBattleAcid317Snapshot.Capture(Memory(), new(1), SnapshotContext(), Read, GeometryAddress, [NativeBattlePackedMatrix32.FromMatrix(geometry.World, null)], CacheAddress, 256);
        NativeBattleAcid317SnapshotStep foreign = NativeBattleAcid317Snapshot.CaptureForeign(Memory(), new(1), SnapshotContext(), geometry, CacheAddress, 256);
        Assert.Equal(native.Random, foreign.Random);
        Assert.Equal(native.Memory.Arena!.Read(4096, geometry.Vertices.Length), foreign.Memory.Arena!.Read(4096, geometry.Vertices.Length));
        Assert.Equal(CacheAddress + (uint)geometry.Vertices.Length, Dword(foreign.Temporary12, 8));
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Memory), NativeBattleTestSnapshots.Json(foreign.Memory));
        NativeBattleAcid317SnapshotStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleAcid317SnapshotStep>(NativeBattleTestSnapshots.Json(foreign))!;
        NativeBattleAcid317SnapshotStep Next(NativeBattleAcid317SnapshotStep current) => NativeBattleAcid317Snapshot.CaptureForeign(current.Memory, current.Random, current.Temporary12, geometry, CacheAddress, 256);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(foreign)), NativeBattleTestSnapshots.Json(Next(restored)));
        Assert.Throws<InvalidDataException>(() => NativeBattleAcid317Snapshot.CaptureForeign(Memory(), new(1), SnapshotContext(), geometry, CacheAddress, geometry.Vertices.Length - 1));

        byte[] Read(int offset, int length) => raw.AsSpan(offset, length).ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    public void Deformation_reuses_shared_vertex_coefficients_and_native_unsigned_cache_projection_packets_and_FIFO(short fraction)
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        byte[] raw = Raw(geometry);
        NativeBattleEffectMemory NativeMemory() => Memory() with { Arena = NativeBattleEffectArena.Clear().Write(4096, [255, 2, 3, 4, 5]) };
        NativeBattleDeformedBodyStep native = NativeBattleDeformedBodyDrawing.DrawBody(NativeMemory(), DeformedContext(fraction), NativeBattleEffectArena.CreateUnknown(40), Read, GeometryAddress, [NativeBattlePackedMatrix32.FromMatrix(geometry.World, null)], NativeBattlePackedMatrix32.FromMatrix(Identity, null), Projection, NativeMemory().ScriptedGraphics!, ScratchAddress, 40, CacheAddress, 256);
        NativeBattleDeformedBodyStep foreign = NativeBattleDeformedBodyDrawing.DrawForeign(NativeMemory(), DeformedContext(fraction), NativeBattleEffectArena.CreateUnknown(40), geometry, NativeBattlePackedMatrix32.FromMatrix(Identity, null), Projection, NativeMemory().ScriptedGraphics!, CacheAddress, 256, Visual);
        AssertPackets(native.Batch, foreign.Batch);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Memory), NativeBattleTestSnapshots.Json(foreign.Memory));
        Assert.Equal(native.VertexScratch.Read(0, 40), foreign.VertexScratch.Read(0, 40));
        Assert.Equal(CacheAddress + 5, Dword(foreign.Temporary132, 0x2C));
        NativeBattleGeometryPrimitive first = foreign.Batch.Attempts[0].Primitive!;
        NativeBattleGeometryPrimitive second = foreign.Batch.Attempts[1].Primitive!;
        Assert.Same(geometry.Faces[0].Surface, first.PilotGeometry!.ForeignSurface);
        Assert.Same(geometry.Faces[1].Surface, second.PilotGeometry!.ForeignSurface);
        Assert.Equal(first.PilotGeometry.B, second.PilotGeometry.A);
        Assert.Equal(first.PilotGeometry.C, second.PilotGeometry.C);

        byte[] Read(int offset, int length) => raw.AsSpan(offset, length).ToArray();
    }

    [Fact]
    public void Incomplete_topology_and_cache_capacity_fail_before_coefficient_publication()
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        NativeBattleEffectMemory memory = Memory() with { Arena = NativeBattleEffectArena.Clear().Write(4096, [91, 92, 93, 94, 95]) };
        Assert.Throws<NotSupportedException>(() => NativeBattleAcid317Snapshot.CaptureForeign(memory, new(1), SnapshotContext(), geometry with { Vertices = [] }, CacheAddress, 256));
        Assert.Throws<InvalidDataException>(() => NativeBattleAcid317Snapshot.CaptureForeign(memory, new(1), SnapshotContext(), geometry with { Faces = geometry.Faces.SetItem(0, geometry.Faces[0] with { VertexIndices = [0, 1, 99] }) }, CacheAddress, 256));
        Assert.Throws<InvalidDataException>(() => NativeBattleAcid317Snapshot.CaptureForeign(memory, new(1), SnapshotContext(), geometry, CacheAddress, 4));
        Assert.Equal(new byte[] { 91, 92, 93, 94, 95 }, memory.Arena!.Read(4096, 5));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Radial_plane_deformation_uses_native_LIGHT_IR_and_post_deformation_material_geometry(bool deform)
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        byte[] raw = Raw(geometry);
        NativeBattleEffectArena Context()
        {
            NativeBattleEffectArena context = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes(ScratchAddress)).Write(0x1C, BitConverter.GetBytes(0x808080u)).Write(0x20, BitConverter.GetBytes(uint.MaxValue));
            return context.Write(0x74, BitConverter.GetBytes((short)0)).Write(0x76, BitConverter.GetBytes((short)4096)).Write(0x78, BitConverter.GetBytes((short)0)).Write(0x7A, BitConverter.GetBytes(deform ? (short)1000 : (short)-10000)).Write(0x34, BitConverter.GetBytes(-4096)).Write(0x3C, BitConverter.GetBytes(1000)).Write(0x40, BitConverter.GetBytes(6250000)).Write(0x8A, BitConverter.GetBytes((ushort)1));
        }
        NativeBattleAcid170ModelStep native = NativeBattleAcid170ModelDrawing.DrawBody(Memory(), Context(), NativeBattleEffectArena.CreateUnknown(40), Read, GeometryAddress, [NativeBattlePackedMatrix32.FromMatrix(geometry.World, null)], NativeBattlePackedMatrix32.FromMatrix(Identity, null), Projection, Memory().ScriptedGraphics!, ScratchAddress, 40, pilotIdentity: Visual);
        NativeBattleAcid170ModelStep foreign = NativeBattleAcid170ModelDrawing.DrawForeign(Memory(), Context(), NativeBattleEffectArena.CreateUnknown(40), geometry, NativeBattlePackedMatrix32.FromMatrix(Identity, null), Projection, Memory().ScriptedGraphics!, Visual);
        AssertPackets(native.Batch, foreign.Batch);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Memory), NativeBattleTestSnapshots.Json(foreign.Memory));
        Assert.Equal(native.VertexScratch.Read(0, 40), foreign.VertexScratch.Read(0, 40));
        for (int index = 0; index < foreign.Batch.Attempts.Length; index++)
        {
            NativeBattleGeometryPrimitive expected = native.Batch.Attempts[index].Primitive!;
            NativeBattleGeometryPrimitive actual = foreign.Batch.Attempts[index].Primitive!;
            Assert.Equal(expected.PilotGeometry!.A, actual.PilotGeometry!.A);
            Assert.Equal(expected.PilotGeometry.B, actual.PilotGeometry.B);
            Assert.Equal(expected.PilotGeometry.C, actual.PilotGeometry.C);
            Assert.Same(geometry.Faces[index].Surface, actual.PilotGeometry.ForeignSurface);
        }

        byte[] Read(int offset, int length) => raw.AsSpan(offset, length).ToArray();
    }

    [Fact]
    public void Petrification_keeps_dither_face_order_and_uses_original_stone_material_only_for_selected_faces()
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        byte[] data = Enumerable.Repeat((byte)255, 512).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(data, 0);
        ImmutableArray<byte> dither = data.ToImmutableArray();
        NativeBattleBreak117BodyResult foreign = NativeBattleForeignModelDrawing.Break(geometry, Projection, 0x808080, dither, 100, Visual);
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(Projection.View!, geometry.World);
        NativeBattleBreak117BodyResult native = NativeBattleBreak117Body.Draw([geometry.Faces[0].Triangle(), geometry.Faces[1].Triangle()], [camera], Projection, 0x808080, uint.MaxValue, dither, 100, null, 0);
        Assert.Equal((byte)2, foreign.DitherIndex);
        Assert.Equal(native.Primitives.Length, foreign.Primitives.Length);
        for (int index = 0; index < native.Primitives.Length; index++)
        {
            Assert.Equal(native.Primitives[index].Words, foreign.Primitives[index].Words);
        }
        Assert.Null(foreign.Primitives[0].PilotGeometry!.ForeignSurface);
        Assert.Same(geometry.Faces[1].Surface, foreign.Primitives[1].PilotGeometry!.ForeignSurface);
        Assert.Equal(0x3D54u, foreign.Primitives[0].Words[3] >> 16);
    }

    private static void AssertPackets(NativeBattleGfGeometryBatch expected, NativeBattleGfGeometryBatch actual)
    {
        Assert.Equal(expected.AllocationBytes, actual.AllocationBytes);
        Assert.Equal(expected.Attempts.Length, actual.Attempts.Length);
        for (int index = 0; index < expected.Attempts.Length; index++)
        {
            Assert.Equal(expected.Attempts[index].ByteOffsetRelative, actual.Attempts[index].ByteOffsetRelative);
            Assert.Equal(expected.Attempts[index].Data, actual.Attempts[index].Data);
            Assert.Equal(expected.Attempts[index].WriteMask, actual.Attempts[index].WriteMask);
            Assert.Equal(expected.Attempts[index].Primitive!.Words, actual.Attempts[index].Primitive!.Words);
            Assert.Equal(expected.Attempts[index].Primitive!.VertexPayload4, actual.Attempts[index].Primitive!.VertexPayload4);
            Assert.Equal(expected.Attempts[index].Primitive!.Bucket, actual.Attempts[index].Primitive!.Bucket);
        }
    }

    private static NativeBattleForeignModelGeometry Geometry()
    {
        NativeBattleForeignSurface surface = new(new Material(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        ImmutableArray<NativeBattleVector> vertices = [new(-400, -400, 0), new(400, -400, 0), new(-400, 100, 0), new(400, 100, 0), new(17, -200, 0)];
        return new(Identity with { Z = 2000 }, new(default, default, 0, new(default, default)), [new(vertices[0], vertices[1], vertices[2], surface) { VertexIndices = [0, 1, 2] }, new(vertices[1], vertices[3], vertices[2], surface) { VertexIndices = [1, 3, 2] }]) { Vertices = vertices };
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess() with { Dqa = 0, Dqb = 0, Zsf3 = 341, Zsf4 = 256 } };
    private static NativeBattleEffectArena SnapshotContext() => NativeBattleEffectArena.Clear().Write(8, BitConverter.GetBytes(CacheAddress));
    private static NativeBattleEffectArena DeformedContext(short fraction) => NativeBattleEffectArena.Clear().Write(0x24, BitConverter.GetBytes(ScratchAddress)).Write(0x2C, BitConverter.GetBytes(CacheAddress)).Write(0x44, BitConverter.GetBytes((short)320)).Write(0x46, BitConverter.GetBytes((short)216)).Write(0x48, BitConverter.GetBytes(0x808080u)).Write(0x4C, BitConverter.GetBytes(uint.MaxValue)).Write(0x80, BitConverter.GetBytes(fraction));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static byte[] Raw(NativeBattleForeignModelGeometry geometry)
    {
        int countOffset = (14 + geometry.Vertices.Length * 6 + 3) & ~3;
        byte[] bytes = new byte[countOffset + 12 + geometry.Faces.Length * 16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), checked((short)geometry.Vertices.Length));
        for (int index = 0; index < geometry.Vertices.Length; index++)
        {
            NativeBattleVector vertex = geometry.Vertices[index];
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 6), vertex.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16 + index * 6), vertex.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18 + index * 6), vertex.Z);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(countOffset), checked((ushort)geometry.Faces.Length));
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[index];
            byte[] polygon = face.Triangle().Polygons[0].Bytes.ToArray();
            for (int corner = 0; corner < 3; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(corner * 2), checked((ushort)face.VertexIndices[corner]));
            }
            polygon.CopyTo(bytes, countOffset + 12 + index * 16);
        }
        return bytes;
    }
}
