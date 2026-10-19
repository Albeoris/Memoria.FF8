using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBerserk108Tests
{
    [Fact]
    public void Ray_fades_only_RGB_and_stops_before_motion_when_all_channels_die()
    {
        byte[] ray = Ray();
        ray[8] = 1;
        ray[12] = 2;
        ray[11] = 0xA7;
        W(ray, 34, 900);
        ImmutableArray<byte> next = NativeBattleBerserk108Geometry.UpdateRay(ray.ToImmutableArray());
        Assert.Equal(0, R(next, 6));
        Assert.Equal(900, R(next, 34));
        Assert.Equal(0xA7, next[11]);
    }

    [Fact]
    public void Ray_length_death_preserves_width_motion_and_pad()
    {
        byte[] ray = Ray();
        ray[8] = 255;
        W(ray, 34, 1);
        W(ray, 36, -10);
        W(ray, 40, 101);
        W(ray, 42, 77);
        W(ray, 46, 0x6172);
        ImmutableArray<byte> next = NativeBattleBerserk108Geometry.UpdateRay(ray.ToImmutableArray());
        Assert.Equal(0, R(next, 6));
        Assert.Equal(101, R(next, 40));
        Assert.Equal(77, R(next, 42));
        Assert.Equal(0x6172, R(next, 46));
    }

    [Fact]
    public void Ray_projection_rejection_keeps_early_SXY_stores_without_allocation()
    {
        int calls = 0;
        NativeBattleGfGeometryBatch batch = NativeBattleBerserk108Geometry.RayPackets(Ray().ToImmutableArray(), _ => ++calls == 1 ? new(0x12345678, 500, 0) : new(0x10203040, 0, 0));
        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(8)));
        Assert.Equal(0x10203040u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(16)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[0]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[19]);
    }

    [Fact]
    public void Rays_count_pre_update_survivors_and_keep_two_packet_physical_order()
    {
        byte[] all = new byte[32 * 48];
        byte[] ray = Ray();
        W(ray, 34, 160);
        W(ray, 40, 160);
        ray.CopyTo(all, 0);
        int calls = 0;
        NativeBattleBerserk108RayStep step = NativeBattleBerserk108Geometry.Rays(all.ToImmutableArray(), _ => ++calls == 1 ? new(0, 500, 0) : new(100, 600, 0));
        Assert.Equal(1, step.Alive);
        Assert.Equal(0, R(step.Rays, 6));
        Assert.Equal(64, step.Packets.AllocationBytes);
        Assert.Equal(new[] { 0, 28 }, step.Packets.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.All(step.Packets.Attempts, attempt => Assert.Equal(new[] { 0, 0, 0, 0 }, attempt.Primitive!.VertexPayload4));
    }

    private static byte[] Ray()
    {
        byte[] bytes = new byte[48];
        W(bytes, 6, 1);
        return bytes;
    }
    private static void W(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    private static short R(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset));
}
