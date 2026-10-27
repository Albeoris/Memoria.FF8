using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22ActorPacketTests
{
    private static NativeBattleActionMatrix Identity => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleCameraProjection Projection => new(null, 128, 0, 0);

    [Theory]
    [InlineData(1024, 4096, 64, 0)]
    [InlineData(0, -4096, 0, -64)]
    [InlineData(3072, 7, 0, 0)]
    public void Wave_uses_signed_amplitude_and_262144_truncation(short phase, short amplitude, short dx, short dz)
    {
        Assert.Equal(new NativeBattleVector((short)(10 + dx), 0, (short)(20 + dz)), NativeBattleBio22ActorPackets.DeformVertex(new(10, 0, 20), phase, amplitude));
        Assert.Equal(NativeBattleBio22ActorPackets.DeformVertex(new(10, 0, 20), phase, amplitude), NativeBattleBio22ActorPackets.DeformVertex(new(10, 1, 20), unchecked((short)(phase - 12)), amplitude) with { Y = 0 });
    }

    [Fact]
    public void Work_stores_only_XYZ_at_four_and_packets_use_actual_AVSZ_controls_and_partial_UV()
    {
        NativeBattleEffectArena work = NativeBattleEffectArena.Clear().MarkUnknown(0x2000, 80);
        NativeBattleBio22ActorDraw draw = Emit(Mesh(), work);
        Assert.Equal(72, draw.Packets.AllocationBytes);
        Assert.Equal(new[] { 0, 32 }, draw.Packets.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(29, draw.Packets.Attempts[0].Primitive!.Bucket);
        Assert.Equal(33, draw.Packets.Attempts[1].Primitive!.Bucket);
        Assert.Equal(0x26102030u, draw.Packets.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0x2C102030u, draw.Packets.Attempts[1].Primitive!.Words[1]);
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(draw.Work.Read(0x2000 + index * 16 + 8, 2)));
            Assert.Throws<NotSupportedException>(() => draw.Work.Read(0x2000 + index * 16, 4));
            Assert.Throws<NotSupportedException>(() => draw.Work.Read(0x2000 + index * 16 + 10, 6));
        }
        Assert.Throws<NotSupportedException>(() => draw.Work.Read(0x2040, 1));
        Assert.All(draw.Packets.Attempts[0].WriteMask.Skip(30), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.All(draw.Packets.Attempts[1].WriteMask.Skip(30).Take(2), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.All(draw.Packets.Attempts[1].WriteMask.Skip(38), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        NativeBattleEffectMemory memory = new(NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 72).ToArray()), 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 0, NativeBattleLogicalOrdering.Clear(), draw.Packets);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.Memory.Arena!.Read(30, 2));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.Memory.Arena.Read(70, 2));
    }

    [Fact]
    public void Rejected_quad_does_not_read_fourth_selector_or_allocate_any_prefix()
    {
        byte[] face = Face(true).Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 0xFFF);
        NativeBattleRawMesh mesh = Mesh() with { Polygons = [new(true, face.ToImmutableArray())] };
        NativeBattleBio22ActorDraw draw = Emit(mesh, NativeBattleEffectArena.Clear());
        Assert.Empty(draw.Packets.Attempts);
        Assert.Equal(0, draw.Packets.AllocationBytes);
        Assert.Equal((short)64, BinaryPrimitives.ReadInt16LittleEndian(draw.Work.Read(0x2014, 2)));
        Assert.Throws<InvalidDataException>(() => Emit(mesh with { Polygons = [new(true, Face(true).Bytes.SetItem(6, 0xFF).SetItem(7, 0x0F))] }, NativeBattleEffectArena.Clear()));
    }

    [Fact]
    public void No_near_or_viewport_rejection_is_added_after_positive_NCLIP()
    {
        NativeBattleBio22ActorDraw draw = NativeBattleBio22ActorPackets.Emit89D780(Mesh() with { Polygons = [Face(false)] }, [Identity], Identity with { X = 500, Z = -1 }, Projection, 0x123456, 0, 0, 256, 256, NativeBattleEffectArena.Clear(), 0x2000);
        NativeBattleGeometryPrimitive primitive = Assert.Single(draw.Packets.Attempts).Primitive!;
        Assert.Equal(17, primitive.Bucket);
        Assert.Equal(new[] { 0, 0, 0, 0 }, primitive.VertexPayload4);
        Assert.True(unchecked((short)primitive.Words[2]) > 2560);
    }

    [Fact]
    public void Saturated_bone_is_wrapped_by_wave_and_smaller_object_reuses_work_with_JSON_next()
    {
        NativeBattleRawMesh mesh = new([new(new(100, 0, 0), 0)], []);
        NativeBattleBio22ActorDraw draw = NativeBattleBio22ActorPackets.Emit89D780(mesh, [Identity with { X = 40000 }], Identity, Projection, 0, 1024, 4096, 256, 256, NativeBattleEffectArena.Clear(), 0x2000);
        Assert.Equal(unchecked((short)(short.MaxValue + 64)), BinaryPrimitives.ReadInt16LittleEndian(draw.Work.Read(0x2004, 2)));
        NativeBattleBio22ActorDraw full = Emit(Mesh(), draw.Work);
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(full.Work))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Emit(mesh, full.Work)), NativeBattleTestSnapshots.Json(Emit(mesh, restored)));
        Assert.Equal(full.Work.Read(0x2014, 6), Emit(mesh, full.Work).Work.Read(0x2014, 6));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rejected_faces_do_not_consume_unexecuted_material_relocations(bool quad)
    {
        byte[] face = Face(quad).Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 224 << 6);
        NativeBattleModelTextureBinding missing = new(ImmutableArray.CreateRange(new byte?[16]));
        NativeBattleRawMesh mesh = Mesh() with { Polygons = [new(quad, face.ToImmutableArray())] };
        Assert.Throws<NotSupportedException>(() => Draw(mesh));
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 1);
        NativeBattleBio22ActorDraw rejected = Draw(mesh with { Polygons = [new(quad, face.ToImmutableArray())] });
        Assert.Empty(rejected.Packets.Attempts);
        Assert.Equal(0, rejected.Packets.AllocationBytes);

        NativeBattleBio22ActorDraw Draw(NativeBattleRawMesh selected) => NativeBattleBio22ActorPackets.Emit89D780(selected, [Identity], Identity with { Z = 1024 }, Projection, 0xFF102030, 0, 0, 256, 256, NativeBattleEffectArena.Clear(), 0x2000, missing);
    }

    private static NativeBattleBio22ActorDraw Emit(NativeBattleRawMesh mesh, NativeBattleEffectArena work) => NativeBattleBio22ActorPackets.Emit89D780(mesh, [Identity], Identity with { Z = 1024 }, Projection, 0xFF102030, 0, 0, 256, 256, work, 0x2000);
    private static NativeBattleRawMesh Mesh() => new([new(new(0, 0, 0), 0), new(new(64, 0, 0), 0), new(new(0, 64, 0), 0), new(new(64, 64, 0), 0)], [Face(true), Face(false)]);
    private static NativeBattleRawPolygon Face(bool quad)
    {
        byte[] bytes = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), quad ? (ushort)3 : (ushort)0x0908);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), quad ? 0x00560605u : 0x02560605u);
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 0x0908);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 0x0B0A);
        }
        return new(quad, bytes.ToImmutableArray());
    }
}
