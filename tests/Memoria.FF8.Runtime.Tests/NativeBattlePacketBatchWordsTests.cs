using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePacketBatchWordsTests
{
    [Fact]
    public void Effect_packet_retains_current_high_UV_words_in_physical_and_logical_owners()
    {
        const int cursor = 16;
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(cursor + 38, new byte[] { 0xA5, 0xB6 }).Write(cursor + 50, new byte[] { 0xC7, 0xD8 });
        NativeBattleGfGeometryAttempt attempt = Quad();
        ImmutableArray<NativeBattleGfPacketByteState> mask = attempt.WriteMask.SetItem(38, NativeBattleGfPacketByteState.Untouched).SetItem(39, NativeBattleGfPacketByteState.Untouched).SetItem(50, NativeBattleGfPacketByteState.Untouched).SetItem(51, NativeBattleGfPacketByteState.Untouched);
        NativeBattleGfGeometryBatch source = new([attempt with { WriteMask = mask }], 52);
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(source, cursor, NativeBattleEffectArena.Length, address => arena.Read(address, 1)[0]);
        Assert.Equal(0xB6A5u, resolved.Attempts[0].Primitive!.Words[9] >> 16);
        Assert.Equal(0xD8C7u, resolved.Attempts[0].Primitive!.Words[12] >> 16);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena }, cursor, NativeBattleLogicalOrdering.Clear(), resolved);
        Assert.Equal(cursor + 52, written.Cursor);
        Assert.Equal(new byte[] { 0xA5, 0xB6 }, written.Memory.Arena!.Read(cursor + 38, 2));
        Assert.Equal(new byte[] { 0xC7, 0xD8 }, written.Memory.Arena.Read(cursor + 50, 2));
        Assert.Equal(resolved.Attempts[0].Primitive!.Words.ToArray(), Assert.Single(written.Ordering.Packets).Words.ToArray());
        Assert.Equal(new int[] { 11, 22, 33, 44 }, Assert.Single(written.Ordering.Proxies).VertexPayload4.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Logical_packet_reads_the_ordered_preceding_store_instead_of_older_backing_bytes(bool known)
    {
        NativeBattleGfGeometryAttempt quad = Quad();
        byte[] prefix = new byte[52];
        prefix[4] = 0x7D;
        var prefixMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 52).ToImmutableArray().SetItem(4, known ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Unknown);
        var retainedMask = quad.WriteMask.SetItem(4, NativeBattleGfPacketByteState.Untouched);
        NativeBattleGfGeometryBatch batch = new([new(0, prefix.ToImmutableArray(), prefixMask, null), quad with { WriteMask = retainedMask }], 52);
        NativeBattleGfGeometryBatch Resolve() => NativeBattlePacketBatchWords.Resolve(batch, 16, 128, _ => throw new InvalidOperationException("The preceding ordered store owns this consumed byte."));
        if (!known)
        {
            Assert.Throws<NotSupportedException>(() => Resolve());
            return;
        }
        Assert.Equal(0x7Du, Resolve().Attempts[1].Primitive!.Words[1] & 0xFF);
    }

    private static NativeBattleGfGeometryAttempt Quad()
    {
        byte[] bytes = new byte[52];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x0C000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x3E112233);
        ImmutableArray<uint> words = Enumerable.Range(0, 13).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        return new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 52).ToImmutableArray(), new(17, words, [11, 22, 33, 44]));
    }
}
