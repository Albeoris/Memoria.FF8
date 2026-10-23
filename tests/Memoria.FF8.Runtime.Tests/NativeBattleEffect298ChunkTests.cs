using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect298Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect298ChunkTests
{
    [Theory]
    [InlineData(4, 64, true)]
    [InlineData(8, 128, false)]
    public void Chunk_GPF_scales_RGB_without_FAR_and_links_exact_native_compound(int life, byte color, bool compound)
    {
        var frame = Frame();
        uint main = ArenaAddress + MainOffset;
        uint chunk = main + 0x2EC;
        frame = WriteBytes(frame, chunk, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)10)).Write(2, BitConverter.GetBytes((short)20)).Write(4, BitConverter.GetBytes((short)30)).Write(6, BitConverter.GetBytes((short)life)).Write(14, BitConverter.GetBytes((short)4096)).Write(24, BitConverter.GetBytes(MeshAddresses[0])), 172);
        for (int face = 0; face < 12; face++)
        {
            var material = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x3A001122u + (uint)face)).Write(4, BitConverter.GetBytes(0x00B63344u + (uint)face)).Write(8, BitConverter.GetBytes((ushort)(0x5566 + face)));
            frame = WriteBytes(frame, chunk + 28 + (uint)(face * 12), material, 10);
        }
        (frame, uint temporary) = Allocate(frame, 104);
        frame = PublishContext(frame, temporary, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 104).ToArray()), 104);
        frame = Release(frame, 104);
        int deliveries = 0;
        var actual = DrawChunks6725F0(frame, main, NativeBattleEffect298StateTests.Resources(), Inputs((current, operation) =>
        {
            var delivery = Assert.IsType<NativeBattleEffect298DeliverBatch>(operation);
            Assert.Equal(CursorGlobal, delivery.CursorGlobal);
            Assert.Equal(12 * (compound ? 60 : 32), delivery.Batch.AllocationBytes);
            Assert.Equal(12 * (compound ? 3 : 1), delivery.Batch.Attempts.Length);
            for (int face = 0; face < 12; face++)
            {
                var packet = delivery.Batch.Attempts[face * (compound ? 3 : 1)];
                var polygon = Assert.IsType<NativeBattleGeometryPrimitive>(packet.Primitive);
                Assert.Equal(0x07000000u, polygon.Words[0]);
                Assert.Equal((compound ? 0x26000000u : 0x24000000u) | unchecked((uint)(color * 0x010101)), polygon.Words[1]);
                Assert.Equal(new[] { 2030, 2030, 2030, 0 }, polygon.VertexPayload4);
                Assert.Equal(32, packet.Data.Length);
                Assert.Equal(0x3A001122u + (uint)face, polygon.Words[3]);
                Assert.Equal(0x00B63344u + (uint)face, polygon.Words[5]);
                Assert.Equal((uint)(0x5566 + face), polygon.Words[7]);
                Assert.All(packet.WriteMask.Skip(20).Take(4), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[30]);
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[31]);
                if (compound)
                {
                    var glow = Assert.IsType<NativeBattleGeometryPrimitive>(delivery.Batch.Attempts[face * 3 + 1].Primitive);
                    var mode = Assert.IsType<NativeBattleGeometryPrimitive>(delivery.Batch.Attempts[face * 3 + 2].Primitive);
                    Assert.Equal(0x04000000u, glow.Words[0]);
                    Assert.Equal(0x22000000u | unchecked((uint)(color * 0x010101)), glow.Words[1]);
                    Assert.Equal(new uint[] { 0x01000000, 0xE1000240 }, mode.Words);
                    Assert.Equal(new[] { 0, 0, 0, 0 }, mode.VertexPayload4);
                    Assert.Equal(polygon.Bucket, glow.Bucket);
                    Assert.Equal(polygon.Bucket, mode.Bucket);
                }
            }
            int cursor = FirstPacketOffset;
            var memory = current.Memory with { Arena = current.Memory.Arena!.Write(cursor, Enumerable.Repeat((byte)0xE9, delivery.Batch.AllocationBytes).ToArray()) };
            var resolved = NativeBattlePacketBatchWords.Resolve(delivery.Batch, cursor, NativeBattleEffectArena.Length, offset => memory.Arena!.Read(offset, 1)[0]);
            var written = NativeBattleEffectPacketArena.Append(memory, cursor, NativeBattleLogicalOrdering.Clear(), resolved);
            Assert.Equal(cursor + delivery.Batch.AllocationBytes, written.Cursor);
            for (int face = 0; face < 12; face++)
            {
                int address = cursor + face * (compound ? 60 : 32);
                Assert.Equal(0x00B63344u + (uint)face, BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena!.Read(address + 20, 4)));
                Assert.Equal(0xE9E90000u | (uint)(0x5566 + face), BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena.Read(address + 28, 4)));
            }
            deliveries++;
            return current;
        }));
        Assert.Equal(1, deliveries);
        Assert.Equal(frame.Memory.SharedEffectRandom, actual.Memory.SharedEffectRandom);
        Assert.Equal(frame.State.ReadDword(SeedGlobal), actual.State.ReadDword(SeedGlobal));
        var retained = Context(actual, temporary, 104);
        Assert.Equal(new byte[] { color, color, color, 0xA5 }, retained.Read(96, 4));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, retained.Read(18, 2));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Fact]
    public void Rejected_RTPT_keeps_color_prefix_writes_without_reserving_or_linking_packets()
    {
        var frame = Frame();
        uint main = ArenaAddress + MainOffset;
        frame = WriteBytes(frame, main + 0x2EC, NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((short)4)).Write(14, BitConverter.GetBytes((short)4096)).Write(24, BitConverter.GetBytes(MeshAddresses[0])), 172);
        int calls = 0;
        _ = DrawChunks6725F0(frame, main, NativeBattleEffect298StateTests.Resources(), Inputs((current, operation) =>
        {
            var delivery = Assert.IsType<NativeBattleEffect298DeliverBatch>(operation);
            Assert.Equal(0, delivery.Batch.AllocationBytes);
            Assert.True(delivery.Batch.NativeUnlinkedAllocation);
            Assert.Equal(12, delivery.Batch.Attempts.Length);
            foreach (var packet in delivery.Batch.Attempts)
            {
                Assert.Null(packet.Primitive);
                Assert.Equal(0, packet.ByteOffsetRelative);
                Assert.All(packet.WriteMask.Take(4), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
                Assert.Equal(new[] { NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Unknown }, packet.WriteMask.Skip(4).Take(4));
                Assert.All(packet.WriteMask.Skip(8), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
            }
            calls++;
            return current;
        }, 1));
        Assert.Equal(1, calls);
    }

    private static NativeBattleEffect298Frame Frame()
    {
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity().ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), SharedEffectRandom = new(123) };
        var effect = new NativeBattleOriginalEffectState(298, 297, new(298, Entry, Initializer), null, 0) { Pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray() };
        return new(memory, effect, NativeBattleEffect298State.FromColdProcess().WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect298Inputs Inputs(Func<NativeBattleEffect298Frame, NativeBattleEffect298Operation, NativeBattleEffect298Frame> execute, int z = 2000) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Identity(z), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), _ => NativeBattleActionPacketsState.Fresh(), (_, _) => throw new InvalidOperationException(), execute, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
    private static NativeBattlePackedMatrix32 Identity(int z = 0) => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096), null);
}
