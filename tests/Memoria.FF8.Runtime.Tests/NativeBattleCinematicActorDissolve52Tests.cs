using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorDissolve52Tests
{
    [Fact]
    public void Radius_uses_updated_velocity_before_amplitude_and_retains_signed32_multiplication()
    {
        Assert.Equal(new[] { 8192, 8211, 8233 }, NativeBattleCinematicActorDissolve52.BuildRadiusTable(-2, 2, 1, 3, 1024, 0).ToArray());
        Assert.Equal(new[] { 0, 4096, 0 }, NativeBattleCinematicActorDissolve52.BuildRadiusTable(-2, 1, 0, 0, 0, 1024).ToArray());
        Assert.Equal(new[] { -4096 }, NativeBattleCinematicActorDissolve52.BuildRadiusTable(0, 32767, 0, 0, 1024, 0).ToArray());
        Assert.Empty(NativeBattleCinematicActorDissolve52.BuildRadiusTable(1, 1, 1, 1, 1, 1));
    }

    [Fact]
    public void Positive_Y_sets_clip_before_clamping_and_indexes_radius_after_reset_to_zero()
    {
        int observedIndex = -1;
        NativeBattleCinematicActorDissolve52Deformation result = NativeBattleCinematicActorDissolve52.DeformVertex(new(10, 5, 0), -2, 4, index =>
        {
            observedIndex = index;
            return 8233;
        }, (x, z) =>
        {
            Assert.Equal((short)10, x);
            Assert.Equal((short)0, z);
            return 1024;
        });
        Assert.Equal(2, observedIndex);
        Assert.Equal(new NativeBattleVector(12, 0, 0), result.Vertex);
        Assert.Equal((byte)1, result.Clipped);
        Assert.Equal(2, result.RadiusIndex);
        Assert.Equal(1024, result.Angle);
    }

    [Fact]
    public void Boundary_vertex_skips_radius_and_angle_while_deformed_coordinates_wrap_signed_WORDs()
    {
        NativeBattleCinematicActorDissolve52Deformation untouched = NativeBattleCinematicActorDissolve52.DeformVertex(new(-4, -2, 8), -2, -3, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        Assert.Equal(new NativeBattleVector(-4, -2, 8), untouched.Vertex);
        Assert.Equal((byte)1, untouched.Clipped);
        Assert.Null(untouched.RadiusIndex);
        NativeBattleCinematicActorDissolve52Deformation wrapped = NativeBattleCinematicActorDissolve52.DeformVertex(new(short.MaxValue, 1, 0), 0, 1, _ => 65536, (_, _) => 1024);
        Assert.Equal(new NativeBattleVector(-32753, 0, 0), wrapped.Vertex);
        Assert.Equal((byte)0, wrapped.Clipped);
        NativeBattleCinematicActorDissolve52Deformation z = NativeBattleCinematicActorDissolve52.DeformVertex(new(0, -1, 10), -2, 0, index =>
        {
            Assert.Equal(1, index);
            return 8192;
        }, (_, _) => 0);
        Assert.Equal(new NativeBattleVector(0, -1, 12), z.Vertex);
    }

    [Theory]
    [InlineData(false, 0x1000u, 0x4FFCu)]
    [InlineData(true, 0x1000u, 0x4FFCu)]
    [InlineData(false, 0xFFFFFFF0u, 0x3FECu)]
    [InlineData(true, 0xFFFFFFF0u, 0x3FECu)]
    public void Inactive_unclipped_faces_keep_original_white_page_and_unsigned_depth_ordering(bool quad, uint orderingBase, uint expectedOrdering)
    {
        List<int> indices = [];
        NativeBattleCinematicActorDissolve52Emission result = NativeBattleCinematicActorDissolve52.EmitFace(Face(quad), quad, 255, 5, index =>
        {
            indices.Add(index);
            return Vertex(index, 0, false, false);
        }, _ => throw new InvalidOperationException("Unclipped inactive faces do not read a dissolve palette."), orderingBase);
        Assert.Equal(quad ? new[] { 0, 1, 2, 3 } : new[] { 0, 1, 2 }, indices);
        Assert.Equal((byte)255, result.Counter);
        Assert.Equal(expectedOrdering, result.OrderingAddress);
        Assert.Equal(quad ? 40 : 32, result.Packets.AllocationBytes);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Packets.Attempts);
        Assert.NotNull(packet.Primitive);
        Assert.Equal(quad ? 0x2C808080u : 0x24808080u, Dword(packet, 4));
        Assert.Equal((ushort)0x8847, Word(packet, 22));
        Assert.Equal((ushort)0x2211, Word(packet, 12));
        Assert.Equal((ushort)0x4433, Word(packet, 14));
        Assert.Equal((ushort)0x6655, Word(packet, 20));
        Assert.Equal((ushort)0xAA99, Word(packet, 28));
        if (quad)
        {
            Assert.Equal((ushort)0xCCBB, Word(packet, 36));
        }
        Assert.Equal(quad ? 33 : 27, packet.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
        foreach (int offset in quad ? new[] { 0, 1, 2, 30, 31, 38, 39 } : new[] { 0, 1, 2, 30, 31 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[offset]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dissolve_counter_updates_before_negative_Nclip_and_rejected_packet_preserves_only_actual_stores(bool quad)
    {
        int selected = -1;
        NativeBattleCinematicActorDissolve52Emission result = NativeBattleCinematicActorDissolve52.EmitFace(Face(quad), quad, 255, 5, index => Vertex(index, 1, true, false), index =>
        {
            selected = index;
            return 0x00112233;
        }, 0x1000);
        Assert.Equal(5, selected);
        Assert.Equal((byte)4, result.Counter);
        Assert.Equal(0, result.Packets.AllocationBytes);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Packets.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(quad ? 0x2E112233u : 0x26112233u, Dword(packet, 4));
        Assert.Equal((ushort)0x8867, Word(packet, 22));
        Assert.Equal(18, packet.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
        foreach (int offset in new[] { 0, 1, 2, 3, 12, 13, 14, 15, 20, 21, 28, 29 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[offset]);
        }
        if (quad)
        {
            Assert.All(packet.WriteMask.Skip(32), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Positive_counter_dissolves_without_clip_and_zero_Nclip_is_accepted_then_counter_zero_skips(bool quad)
    {
        NativeBattleCinematicActorDissolve52Emission result = NativeBattleCinematicActorDissolve52.EmitFace(Face(quad), quad, 1, 5, index => Vertex(index, 0, false, true), index =>
        {
            Assert.Equal(1, index);
            return 0x00224466;
        }, 0x1000);
        Assert.Equal((byte)0, result.Counter);
        Assert.Equal(quad ? 40 : 32, result.Packets.AllocationBytes);
        Assert.NotNull(Assert.Single(result.Packets.Attempts).Primitive);
        NativeBattleCinematicActorDissolve52Emission skipped = NativeBattleCinematicActorDissolve52.EmitFace(Face(quad), quad, result.Counter, 5, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), 0x1000);
        Assert.Empty(skipped.Packets.Attempts);
        Assert.Equal(0, skipped.Packets.AllocationBytes);
        Assert.Equal(0x1000u, skipped.OrderingAddress);
    }

    [Fact]
    public void Negative_counter_selects_signed_initial_palette_index_and_wraps_decrement_BYTE()
    {
        NativeBattleCinematicActorDissolve52Emission result = NativeBattleCinematicActorDissolve52.EmitFace(Face(false), false, 128, -1, index => Vertex(index, 1, false, false), index =>
        {
            Assert.Equal(-1, index);
            return 0;
        }, 0);
        Assert.Equal((byte)254, result.Counter);
    }

    private static NativeBattleCinematicActorDissolve52Vertex Vertex(int index, byte clipped, bool reverse, bool collinear)
    {
        (short x, short y) = collinear ? ((short)index, (short)index) : index switch
        {
            0 => ((short)0, (short)0),
            1 => reverse ? ((short)0, (short)10) : ((short)10, (short)0),
            2 => reverse ? ((short)10, (short)0) : ((short)0, (short)10),
            _ => ((short)10, (short)10)
        };
        uint screen = unchecked((uint)(ushort)x | (uint)(ushort)y << 16);
        return new(screen, ushort.MaxValue, clipped);
    }

    private static ImmutableArray<byte> Face(bool quad)
    {
        ushort[] words = quad ? [0xF000, 0xF001, 0xF002, 0xF003, 0x2211, 0x4433, 0x6655, 0x8847, 0xAA99, 0xCCBB] : [0xF000, 0xF001, 0xF002, 0xAA99, 0x2211, 0x4433, 0x6655, 0x8847];
        byte[] bytes = new byte[words.Length * 2];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        }
        return bytes.ToImmutableArray();
    }

    private static uint Dword(NativeBattleGfGeometryAttempt packet, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan().Slice(offset));
    }

    private static ushort Word(NativeBattleGfGeometryAttempt packet, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(offset));
    }
}
