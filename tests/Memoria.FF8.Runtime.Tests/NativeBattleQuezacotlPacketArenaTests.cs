using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlPacketArenaTests
{
    [Fact]
    public void OrderedRejectedAndAcceptedWritesPreserveUntouchedBytesAndUseTheEffectArenaCursor()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        memory = memory with { Arena = memory.Arena!.Write(128, Enumerable.Repeat((byte)0xA5, 80).ToArray()) };
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { DrawArenaOffset = 128 };
        NativeBattleGeometryPrimitive primitive = new(20, [0x7000000, 0x24010203, 0x12345678, 0x00001122, 0x12345679, 0x10003344, 0x1234567A, 0x00005566], [100, 110, 120, 0]);
        NativeBattleGfGeometryAttempt accepted = Known(0, primitive);
        NativeBattleGfGeometryAttempt rejected = Rejected(32, 40);
        NativeBattleGfGeometryBatch batch = new([Rejected(0, 32), accepted, rejected], 32);
        NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlPacketArena.Append(memory, effect, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(160, written.Effect.DrawArenaOffset);
        Assert.Equal(memory.PrimitiveProcess!.ByteCursor, written.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Equal(new NativeBattlePrimitiveReference("20DFAB8", 0, 128), Assert.Single(written.Ordering.Packets).Reference);
        Assert.Equal((ushort)0, Assert.Single(written.Ordering.Proxies).Flags20);
        Assert.Throws<NotSupportedException>(() => written.Memory.Arena!.Read(128, 4));
        Assert.Equal(accepted.Data.AsSpan().Slice(3).ToArray(), written.Memory.Arena!.Read(131, 29));
        Assert.Equal(rejected.Data.AsSpan().Slice(0, 8).ToArray(), written.Memory.Arena.Read(160, 8));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 32), written.Memory.Arena.Read(168, 32));
        NativeBattleQuezacotlPacketWrite restored = JsonSerializer.Deserialize<NativeBattleQuezacotlPacketWrite>(JsonSerializer.Serialize(written))!;
        Assert.Throws<NotSupportedException>(() => restored.Memory.Arena!.Read(128, 3));
        Assert.Equal(written.Memory.Arena.Read(131, 69), restored.Memory.Arena!.Read(131, 69));
    }

    [Fact]
    public void ExhaustedProxyBankLeavesTheInitialZeroTagPointerKnownButStillAdvancesTheNativePacketCursor()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { DrawArenaOffset = 128 };
        NativeBattleLogicalOrdering full = NativeBattleLogicalOrdering.Clear() with { Proxies = Enumerable.Range(0, NativeBattlePrimitiveOrdering.ProxyCapacity).Select(index => new NativeBattleLogicalProxy(new("filled-native-proxy", 0, index * 4), 0, [1, 1, 1, 0], null)).ToImmutableArray() };
        NativeBattleGeometryPrimitive primitive = new(20, [0x4000000, 0x20010203, 0, 0, 0], [1, 1, 1, 0]);
        NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlPacketArena.Append(memory, effect, full, new([Known(0, primitive)], 20));
        Assert.Equal(148, written.Effect.DrawArenaOffset);
        Assert.Equal(0x4000000u, BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena!.Read(128, 4)));
        Assert.Empty(written.Memory.Arena.UnknownRegions);
        Assert.Empty(written.Ordering.Packets);
    }

    private static NativeBattleGfGeometryAttempt Known(int offset, NativeBattleGeometryPrimitive primitive)
    {
        byte[] bytes = new byte[primitive.Words.Length * 4];
        for (int index = 0; index < primitive.Words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), primitive.Words[index]);
        }
        return new(offset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive);
    }

    private static NativeBattleGfGeometryAttempt Rejected(int offset, int length)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, ((uint)length / 4 - 1) << 24);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x26010203);
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
        mask.AsSpan(0, 8).Fill(NativeBattleGfPacketByteState.Known);
        return new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), null);
    }
}
