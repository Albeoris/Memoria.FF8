using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect338PuffPacketsTests
{
    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 48)]
    public void Current_extended_flag_controls_allocation_and_effect_ordering_base_after_JSON(bool extended, int expectedBytes)
    {
        uint temporary = NativeBattleTemporaryMemory.Address;
        byte[] context = new byte[180];
        BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(44), 0x1371310);
        BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(48), 1);
        context[37] = extended ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(132), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(140), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(148), 2048);
        BinaryPrimitives.WriteInt32LittleEndian(context.AsSpan(160), 4096);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(temporary, context);
        staging = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(staging)))!);
        byte[] quad = new byte[20];
        quad[0] = 4;
        quad[1] = 128;
        quad[2] = 8;
        quad[3] = 0x2C;
        BinaryPrimitives.WriteInt16LittleEndian(quad.AsSpan(16), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(quad.AsSpan(18), 4096);
        NativeBattlePuffPacketStep result = NativeBattlePuffPackets6FC9E0.Draw(staging, temporary, new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 256, 160, 108), (address, length) =>
        {
            Assert.Equal(0x1371310u, address);
            Assert.Equal(20, length);
            return quad;
        }, 17);
        Assert.Equal(expectedBytes, result.Packets.AllocationBytes);
        Assert.All(result.Packets.Attempts, attempt => Assert.Equal(17, attempt.Primitive!.Bucket));
        NativeBattleGfGeometryAttempt textured = result.Packets.Attempts.Last();
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, textured.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, textured.WriteMask[38]);
        Assert.Equal((byte)(extended ? 1 : 0), result.Staging.Read(temporary + 37, 1)[0]);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }
}
