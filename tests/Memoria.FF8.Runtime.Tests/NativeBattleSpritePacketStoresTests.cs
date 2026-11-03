using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSpritePacketStoresTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedSpriteRetainsUvPaddingAndExtendedTailWhileLinkingInNativeOrder(bool extended)
    {
        NativeBattleModulePacketBatch batch = Emit(extended, false);
        NativeBattlePrimitiveProcess initial = Process(true);
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(initial, NativeBattleLogicalOrdering.Clear(), batch.CapturedWrites!);

        Assert.Equal(extended ? 52 : 40, written.Process.ByteCursor);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.Process.ReadCommonPacket(30, 2));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.Process.ReadCommonPacket(38, 2));
        Assert.Equal(new byte[] { 0xFF, 0x80, 0x40, 0x2E }, written.Process.ReadCommonPacket(4, 4));
        Assert.Equal(new byte[] { 0xFA, 0xFE, 0x80, 0x37 }, written.Process.ReadCommonPacket(12, 4));
        Assert.Equal(new byte[] { 0xFF, 0xFE, 0x42, 1 }, written.Process.ReadCommonPacket(20, 4));
        Assert.Equal(extended ? new[] { 40, 0 } : new[] { 0 }, batch.CapturedWrites!.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.ByteOffsetRelative).ToArray());
        if (extended)
        {
            Assert.Equal(new byte[] { 0x20, 2, 0, 0xE1 }, written.Process.ReadCommonPacket(44, 4));
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 4), written.Process.ReadCommonPacket(48, 4));
        }
        NativeBattlePrimitiveProcess restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePrimitiveProcess>(NativeBattleTestSnapshots.Json(written.Process))!;
        NativeBattleLogicalOrdering restoredOrdering = NativeBattleTestSnapshots.RestoreJson<NativeBattleLogicalOrdering>(NativeBattleTestSnapshots.Json(written.Ordering))!;
        NativeBattleCommonPacketWrite next = NativeBattleCommonPacketArena.Append(written.Process, written.Ordering, Emit(false, false).CapturedWrites!);
        NativeBattleCommonPacketWrite cold = NativeBattleCommonPacketArena.Append(restored, restoredOrdering, Emit(false, false).CapturedWrites!);
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(cold));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedSpritePublishesOnlyTheActualPreclipStoresWithoutAllocationOrLinks(bool known)
    {
        NativeBattleModulePacketBatch batch = Emit(true, true);
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(Process(known), NativeBattleLogicalOrdering.Clear(), batch.CapturedWrites!);

        Assert.Empty(batch.Primitives);
        Assert.Equal(0, written.Process.ByteCursor);
        Assert.Equal(0, written.Ordering.ProxyCursor);
        Assert.Equal(new byte[] { 0, 0, 0, 9 }, written.Process.ReadCommonPacket(0, 4));
        Assert.Equal(new byte[] { 0xFF, 0x80, 0x40, 0x2E }, written.Process.ReadCommonPacket(4, 4));
        Assert.Equal(new byte[] { 0xFA, 0xFE, 0x80, 0x37 }, written.Process.ReadCommonPacket(12, 4));
        Assert.Equal(new byte[] { 0xFF, 0xFE }, written.Process.ReadCommonPacket(20, 2));
        Assert.Equal(new byte[] { 0xFA }, written.Process.ReadCommonPacket(28, 1));
        Assert.Equal(new byte[] { 0xFF }, written.Process.ReadCommonPacket(36, 1));
        foreach (int offset in new[] { 8, 16, 22, 24, 29, 32, 37, 40, 48 })
        {
            if (known)
            {
                Assert.Equal(new byte[] { 0xA5 }, written.Process.ReadCommonPacket(offset, 1));
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => written.Process.ReadCommonPacket(offset, 1));
            }
        }
        NativeBattlePrimitiveProcess restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePrimitiveProcess>(NativeBattleTestSnapshots.Json(written.Process))!;
        NativeBattleCommonPacketWrite continued = NativeBattleCommonPacketArena.Append(restored, written.Ordering, Emit(false, false).CapturedWrites!);
        Assert.Equal(40, continued.Process.ByteCursor);
        Assert.Equal(1, continued.Ordering.ProxyCursor);
    }

    private static NativeBattlePrimitiveProcess Process(bool known)
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        return known ? process.ApplyCommonWrite(0, Enumerable.Repeat((byte)0xA5, 128).ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 128).ToImmutableArray()) : process;
    }

    private static NativeBattleModulePacketBatch Emit(bool extended, bool rejected)
    {
        byte[] quad = new byte[20];
        quad[0] = 8;
        quad[1] = 128;
        quad[2] = 4;
        quad[3] = 0x2E;
        quad[4] = 250;
        quad[5] = 254;
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(6), 0x3800);
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(14), 0x1142);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Anchor = new(0, 0, 0, 0),
            Scale = 4096,
            SpriteFlags = 0xC,
            SpriteColor = 0x4080FF,
            Resource = new(0, 1, 0, 0, [0, 0, 0, 0, 0, 254, 0, 0]),
            SpriteFrame = new(0, extended, quad.ToImmutableArray())
        };
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, rejected ? 0 : 1024, 4096);
        NativeBattleCameraProjection projection = new(matrix, 256, 160, 108);
        return NativeBattleModulePackets.EmitSprite571DC0(draw, projection, 0, 17, 2, matrix);
    }
}
