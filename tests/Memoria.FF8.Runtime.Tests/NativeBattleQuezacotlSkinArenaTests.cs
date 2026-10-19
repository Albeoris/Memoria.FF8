using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlSkinArenaTests
{
    [Fact]
    public void InterleavedDualBankStoresKeepScratchAndPaddingAndAdvanceOnlyTheirOwnAcceptedAllocations()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        memory = memory with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { ByteCursor = 64 }, Arena = memory.Arena!.Write(128, Enumerable.Repeat((byte)0xA5, 40).ToArray()) };
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { DrawArenaOffset = 128 };
        NativeBattleGeometryPrimitive primitive = new(20, [0x04000000, 0x20010203, 1, 2, 3], [100, 110, 120, 0]);
        NativeBattleGfGeometryAttempt stored = Packet(primitive) with { Primitive = null };
        NativeBattleGfGeometryAttempt linked = Packet(primitive) with { WriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 20).ToImmutableArray() };
        NativeBattleGfGeometryAttempt rejected = stored with { ByteOffsetRelative = 20, WriteMask = [NativeBattleGfPacketByteState.Known, .. Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 19)] };
        NativeBattleGfGeometryAttempt scratch = new(0, Enumerable.Repeat((byte)0x5A, 12).ToImmutableArray(), [.. Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 9), NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known], null);
        NativeBattleQuezacotlSkinBatch batch = new([new(NativeBattleQuezacotlSkinBank.VertexScratch, scratch), new(NativeBattleQuezacotlSkinBank.Common, stored), new(NativeBattleQuezacotlSkinBank.Effect, Packet(primitive)), new(NativeBattleQuezacotlSkinBank.Common, linked), new(NativeBattleQuezacotlSkinBank.Effect, rejected)], 20, 20);
        NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlSkinArena.Append(memory, effect, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(84, written.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Equal(148, written.Effect.DrawArenaOffset);
        Assert.Equal(new[] { "20DFAB8", "off_B6D070" }, written.Ordering.Proxies.Select(proxy => proxy.Packet.Bank));
        Assert.Equal(new[] { 128, 64 }, written.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset));
        Assert.Equal(3, written.Memory.PrimitiveProcess.ReadCommonPacket(64, 3).Length);
        Assert.Equal(stored.Data.AsSpan().Slice(3).ToArray(), written.Memory.PrimitiveProcess.ReadCommonPacket(67, 17));
        Assert.Throws<NotSupportedException>(() => written.Memory.PrimitiveProcess.ReadCommonPacket(84, 1));
        Assert.Throws<NotSupportedException>(() => written.Memory.Arena!.Read(0x8D9, 1));
        Assert.Equal(new byte[] { 0x5A, 0x5A }, written.Memory.Arena!.Read(0x8DA, 2));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 19), written.Memory.Arena.Read(149, 19));
        NativeBattleQuezacotlPacketWrite restored = JsonSerializer.Deserialize<NativeBattleQuezacotlPacketWrite>(JsonSerializer.Serialize(written))!;
        Assert.Equal(JsonSerializer.Serialize(written), JsonSerializer.Serialize(restored));
        Assert.Equal(3, restored.Memory.PrimitiveProcess!.ReadCommonPacket(64, 3).Length);
    }

    [Fact]
    public void Whole_skin_producer_matches_interleaved_scalar_stores_without_copying_each_packet_bank_per_store()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        memory = memory with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), Arena = memory.Arena!.Write(0, new byte[0x20000]) };
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { DrawArenaOffset = 0x10000 };
        NativeBattleQuezacotlPacketWrite scalar = new(memory, effect, NativeBattleLogicalOrdering.Clear());
        ImmutableArray<NativeBattleQuezacotlSkinWrite>.Builder writes = ImmutableArray.CreateBuilder<NativeBattleQuezacotlSkinWrite>();
        List<NativeBattleQuezacotlSkinBatch> scalarBatches = [];
        int common = 0;
        int draw = 0;
        for (int index = 0; index < 64; index++)
        {
            NativeBattleQuezacotlSkinBank bank = index % 3 == 0 ? NativeBattleQuezacotlSkinBank.VertexScratch : index % 3 == 1 ? NativeBattleQuezacotlSkinBank.Common : NativeBattleQuezacotlSkinBank.Effect;
            NativeBattleGeometryPrimitive primitive = new(20 + index % 7, [0x04000000, 0x20010203, (uint)index, 2, 3], [100, 110, 120, 0]);
            NativeBattleGfGeometryAttempt attempt = bank == NativeBattleQuezacotlSkinBank.VertexScratch
                ? new(0, Enumerable.Repeat((byte)index, 12).ToImmutableArray(), [.. Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 9), NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Known], null)
                : Packet(primitive);
            int commonBytes = bank == NativeBattleQuezacotlSkinBank.Common ? attempt.PacketLength : 0;
            int effectBytes = bank == NativeBattleQuezacotlSkinBank.Effect ? attempt.PacketLength : 0;
            scalarBatches.Add(new([new(bank, attempt)], commonBytes, effectBytes));
            int offset = bank == NativeBattleQuezacotlSkinBank.Common ? common : bank == NativeBattleQuezacotlSkinBank.Effect ? draw : 0;
            writes.Add(new(bank, attempt with { ByteOffsetRelative = offset }));
            common += commonBytes;
            draw += effectBytes;
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        foreach (NativeBattleQuezacotlSkinBatch scalarBatch in scalarBatches)
        {
            scalar = NativeBattleQuezacotlSkinArena.Append(scalar.Memory, scalar.Effect, scalar.Ordering, scalarBatch);
        }
        long scalarBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        NativeBattleQuezacotlSkinBatch batch = new(writes.ToImmutable(), common, draw);
        before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattleQuezacotlPacketWrite result = NativeBattleQuezacotlSkinArena.Append(memory, effect, NativeBattleLogicalOrdering.Clear(), batch);
        long batchBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(JsonSerializer.Serialize(scalar), JsonSerializer.Serialize(result));
        Assert.True(batchBytes < scalarBytes / 8, $"Scalar allocated {scalarBytes} bytes; batch allocated {batchBytes} bytes.");
        Assert.Equal(new byte[12], memory.Arena.Read(0x8D0, 12));
    }

    private static NativeBattleGfGeometryAttempt Packet(NativeBattleGeometryPrimitive primitive)
    {
        byte[] bytes = new byte[primitive.Words.Length * 4];
        for (int index = 0; index < primitive.Words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), primitive.Words[index]);
        }
        return new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive);
    }
}
