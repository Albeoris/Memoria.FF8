using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRemainingBodySmoothPresentationTests
{
    [Fact]
    public void Break_body_captures_selected_bone_vertices_without_changing_native_writes_or_dither()
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        NativeBattleRawMesh mesh = new([new(new(-100, -100, 0), 0), new(new(100, -100, 0), 0), new(new(-100, 100, 0), 0)], [new(false, polygon.ToImmutableArray())]);
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { X = 50, Z = 4096 };
        NativeBattleActionMatrix bone = view with { X = 70 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattlePilotVisualIdentity identity = Identity(117);
        NativeBattleBreak117BodyResult plain = NativeBattleBreak117Body.Draw([mesh], [bone], projection, 0x123456, 1, new byte[512].ToImmutableArray(), 32768, null, 19);
        NativeBattleBreak117BodyResult captured = NativeBattleBreak117Body.Draw([mesh], [bone], projection, 0x123456, 1, new byte[512].ToImmutableArray(), 32768, null, 19, identity);
        NativeBattleGeometryPrimitive expected = Assert.Single(plain.Primitives);
        NativeBattleGeometryPrimitive actual = Assert.Single(captured.Primitives);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual with { PilotGeometry = null }));
        Assert.Equal(plain.Projected.ToArray(), captured.Projected.ToArray());
        Assert.Equal(plain.DitherIndex, captured.DitherIndex);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(actual.PilotGeometry);
        Assert.Equal(new Vector3(-80, -100, 0), geometry.A);
        Assert.Equal(new Vector3(120, -100, 0), geometry.B);
        Assert.Equal(4, geometry.DepthQuantum);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void Pain_glow_captures_selected_vertices_and_keeps_packet_mode_and_masks(int group)
    {
        (ImmutableArray<byte> mesh, ImmutableArray<byte> vertices) = GlowMesh(group);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleGfGeometryBatch plain = NativeBattlePain34Drawing.Emit8DE9D0(mesh, 0, vertices, matrix, projection, 0x2430, 0, 0, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGfGeometryBatch captured = NativeBattlePain34Drawing.Emit8DE9D0(mesh, 0, vertices, matrix, projection, 0x2430, 0, 0, NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: Identity(34));
        NativeBattleGfGeometryAttempt expected = Assert.Single(plain.Attempts);
        NativeBattleGfGeometryAttempt actual = Assert.Single(captured.Attempts);
        Assert.Equal(expected.Data.ToArray(), actual.Data.ToArray());
        Assert.Equal(expected.WriteMask.ToArray(), actual.WriteMask.ToArray());
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Primitive), NativeBattleTestSnapshots.Json(actual.Primitive! with { PilotGeometry = null }));
        Assert.Equal(plain.AllocationBytes, captured.AllocationBytes);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(actual.Primitive.PilotGeometry);
        Assert.Equal(new Vector3(-100, -100, 1000), geometry.A);
        Assert.Equal(new Vector3(100, 100, 1000), geometry.D);
        Assert.Equal(0xE1000220u, actual.Primitive.Words[^1]);
    }

    [Fact]
    public void Break_cut_uses_actual_intersection_vertices_and_changes_identity_when_topology_changes()
    {
        byte[] source = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(source, 8);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 3);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(20), 0x24123456);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(26), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(28), 4);
        byte[] vertices = new byte[24];
        BinaryPrimitives.WriteInt16LittleEndian(vertices, -100);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(2), -100);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(8), 100);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(10), -100);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(18), 100);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4096 };
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattlePilotVisualIdentity identity = Identity(117);
        NativeBattleBreak117CutResult plain = Draw(0, null);
        NativeBattleBreak117CutResult captured = Draw(0, identity);
        NativeBattleGeometryPrimitive actual = Assert.Single(captured.Packets.Attempts).Primitive!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Assert.Single(plain.Packets.Attempts).Primitive), NativeBattleTestSnapshots.Json(actual with { PilotGeometry = null }));
        Assert.Equal(plain.Projected.ToArray(), captured.Projected.ToArray());
        NativeBattlePilotVisualGeometry geometry = actual.PilotGeometry!;
        Assert.Equal((byte)4, geometry.CornerCount);
        Assert.Equal(0, geometry.C.Y);
        Assert.Equal(0, geometry.D.Y);
        NativeBattlePilotVisualGeometry uncut = Assert.Single(Draw(100, identity).Packets.Attempts).Primitive!.PilotGeometry!;
        Assert.NotEqual(geometry.Identity, uncut.Identity);

        NativeBattleBreak117CutResult Draw(int cut, NativeBattlePilotVisualIdentity? pilotIdentity) => NativeBattleBreak117Cut.Draw(source.ToImmutableArray(), 0, vertices.ToImmutableArray(), matrix, projection, cut, new byte[24].ToImmutableArray(), pilotIdentity);
    }

    [Fact]
    public void Death_actor_mesh_metadata_preserves_native_context_registers_and_accepted_packets()
    {
        (ImmutableArray<byte> selected, ImmutableArray<byte> vertices) = GlowMesh(3);
        byte[] mesh = selected.ToArray();
        vertices.AsSpan().CopyTo(mesh.AsSpan(8));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleEffectArena context = NativeBattleActorWaveMesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 104), 0x100000, 0, 0, null);
        NativeBattleActorWaveMeshStep plain = Draw(null);
        NativeBattleActorWaveMeshStep captured = Draw(Identity(14));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Registers), NativeBattleTestSnapshots.Json(captured.Registers));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Context104), NativeBattleTestSnapshots.Json(captured.Context104));
        Assert.Equal(plain.Batch.AllocationBytes, captured.Batch.AllocationBytes);
        NativeBattleGeometryPrimitive expected = Assert.Single(plain.Batch.Attempts).Primitive!;
        NativeBattleGeometryPrimitive actual = Assert.Single(captured.Batch.Attempts).Primitive!;
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual with { PilotGeometry = null }));
        Assert.Equal(new Vector3(-100, -100, 1000), actual.PilotGeometry!.A);
        Assert.Equal(new Vector3(100, 100, 1000), actual.PilotGeometry.D);

        NativeBattleActorWaveMeshStep Draw(NativeBattlePilotVisualIdentity? identity) => NativeBattleActorWaveMesh.EmitBatch(mesh.ToImmutableArray(), 0x100000, matrix, projection, context, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(null, null, null, null, null), 0, 0, 256, NativeBattleGteColorInterpolation.CodeConstants45F270, zsf3: 341, pilotIdentity: identity);
    }

    private static NativeBattlePilotVisualIdentity Identity(ushort effect)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 64, 1);
        (pool, int slot) = pool.Append(0x3000);
        return NativeBattlePilotVisualLifetimes.Identity(effect, pool.Slots[slot]);
    }

    private static (ImmutableArray<byte> Mesh, ImmutableArray<byte> Vertices) GlowMesh(int selectedGroup)
    {
        int faceLength = selectedGroup == 3 ? 24 : 36;
        byte[] mesh = new byte[44 + 32 + faceLength];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 44);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 4);
        int cursor = 44;
        for (int group = 0; group < 8; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(cursor), group == selectedGroup ? 1 : 0);
            cursor += 4;
            if (group != selectedGroup)
            {
                continue;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(cursor), selectedGroup == 3 ? 0x2C808080u : 0x3C808080u);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(cursor + 4 + corner * 2), checked((ushort)(corner * 2)));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(cursor + 12), 0x12340201);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(cursor + 16), 0xABCD0403);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(cursor + 20), 0x08070605);
            if (selectedGroup == 7)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(cursor + 24 + corner * 4), 0x00808080);
                }
            }
            cursor += faceLength;
        }
        byte[] vertices = new byte[32];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(corner * 8), (short)((corner & 1) == 0 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(corner * 8 + 2), (short)(corner < 2 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(corner * 8 + 4), 1000);
        }
        return (mesh.ToImmutableArray(), vertices.ToImmutableArray());
    }
}
