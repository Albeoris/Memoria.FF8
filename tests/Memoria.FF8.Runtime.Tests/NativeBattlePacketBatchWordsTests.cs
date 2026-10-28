using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePacketBatchWordsTests
{
    [Fact]
    public void Effect_packet_preserves_physical_UV_padding_without_consuming_it_in_logical_words()
    {
        const int cursor = 16;
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(cursor + 38, new byte[] { 0xA5, 0xB6 }).Write(cursor + 50, new byte[] { 0xC7, 0xD8 });
        NativeBattleGfGeometryAttempt attempt = Quad();
        ImmutableArray<NativeBattleGfPacketByteState> mask = attempt.WriteMask.SetItem(38, NativeBattleGfPacketByteState.Untouched).SetItem(39, NativeBattleGfPacketByteState.Untouched).SetItem(50, NativeBattleGfPacketByteState.Untouched).SetItem(51, NativeBattleGfPacketByteState.Untouched);
        NativeBattleGfGeometryBatch source = new([attempt with { WriteMask = mask }], 52);
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(source, cursor, NativeBattleEffectArena.Length, address => arena.Read(address, 1)[0]);
        Assert.Equal(0u, resolved.Attempts[0].Primitive!.Words[9] >> 16);
        Assert.Equal(0u, resolved.Attempts[0].Primitive!.Words[12] >> 16);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena }, cursor, NativeBattleLogicalOrdering.Clear(), resolved);
        Assert.Equal(cursor + 52, written.Cursor);
        Assert.Equal(new byte[] { 0xA5, 0xB6 }, written.Memory.Arena!.Read(cursor + 38, 2));
        Assert.Equal(new byte[] { 0xC7, 0xD8 }, written.Memory.Arena.Read(cursor + 50, 2));
        Assert.Equal(resolved.Attempts[0].Primitive!.Words.ToArray(), Assert.Single(written.Ordering.Packets).Words.ToArray());
        Assert.Equal(new int[] { 11, 22, 33, 44 }, Assert.Single(written.Ordering.Proxies).VertexPayload4.ToArray());
    }

    [Theory]
    [InlineData(0x20)]
    [InlineData(0x24)]
    [InlineData(0x28)]
    [InlineData(0x2C)]
    [InlineData(0x30)]
    [InlineData(0x34)]
    [InlineData(0x38)]
    [InlineData(0x3C)]
    public void Polygon_padding_can_be_unknown_but_every_consumed_byte_requires_a_known_owner(int opcode)
    {
        bool gouraud = (opcode & 16) != 0;
        bool textured = (opcode & 4) != 0;
        int corners = (opcode & 8) != 0 ? 4 : 3;
        int stride = (gouraud ? 1 : 0) + 1 + (textured ? 1 : 0);
        int length = 4 * (1 + corners * stride + (gouraud ? 0 : 1));
        byte[] bytes = Enumerable.Repeat((byte)0xA5, length).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)(length / 4 - 1) << 24);
        bytes[7] = (byte)opcode;
        int[] padding = Enumerable.Range(0, corners).SelectMany(corner =>
        {
            List<int> offsets = [];
            if (textured && corner >= 2)
            {
                offsets.Add(4 * (3 + corner * stride) + 2);
                offsets.Add(4 * (3 + corner * stride) + 3);
            }
            if (gouraud && corner > 0)
            {
                offsets.Add(4 * (1 + corner * stride) + 3);
            }
            return offsets;
        }).ToArray();
        ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4 * index))).ToImmutableArray();
        var known = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, length).ToImmutableArray();
        NativeBattleGfGeometryAttempt source = new(0, bytes.ToImmutableArray(), known, new(17, words, [11, 22, 33, 44]));
        foreach (NativeBattleGfPacketByteState state in new[] { NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Unknown })
        {
            var mask = known.ToBuilder();
            foreach (int offset in padding)
            {
                mask[offset] = state;
            }
            var batch = new NativeBattleGfGeometryBatch([source with { WriteMask = mask.ToImmutable() }], length);
            var resolved = NativeBattlePacketBatchWords.Resolve(batch, 0, length, _ => throw new InvalidOperationException("Ignored padding must not read retained memory."));
            Assert.Equal(batch.Attempts[0].Data, resolved.Attempts[0].Data);
            Assert.Equal(batch.Attempts[0].WriteMask, resolved.Attempts[0].WriteMask);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0, length) }, 0, NativeBattleLogicalOrdering.Clear(), resolved);
            foreach (int offset in padding)
            {
                Assert.Throws<NotSupportedException>(() => written.Memory.Arena!.Read(offset, 1));
                Assert.Equal(0u, resolved.Attempts[0].Primitive!.Words[offset / 4] >> (8 * (offset % 4)) & 0xFF);
            }
        }
        foreach (int offset in Enumerable.Range(4, length - 4).Except(padding))
        {
            var batch = new NativeBattleGfGeometryBatch([source with { WriteMask = known.SetItem(offset, NativeBattleGfPacketByteState.Unknown) }], length);
            Assert.Throws<NotSupportedException>(() => NativeBattlePacketBatchWords.Resolve(batch, 0, length, _ => 0));
        }
    }

    [Fact]
    public void Composite_environment_and_polygon_commands_use_their_own_padding_offsets()
    {
        NativeBattleGfGeometryAttempt quad = Quad();
        byte[] bytes = new byte[quad.PacketLength + 4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x0D000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0xE2000000);
        quad.Data.AsSpan().Slice(4).CopyTo(bytes.AsSpan(8));
        var mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray().SetItem(42, NativeBattleGfPacketByteState.Unknown).SetItem(43, NativeBattleGfPacketByteState.Unknown).SetItem(54, NativeBattleGfPacketByteState.Unknown).SetItem(55, NativeBattleGfPacketByteState.Unknown);
        var words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        var batch = new NativeBattleGfGeometryBatch([new(0, bytes.ToImmutableArray(), mask, quad.Primitive! with { Words = words })], bytes.Length);
        var resolved = NativeBattlePacketBatchWords.Resolve(batch, 0, bytes.Length, _ => throw new InvalidOperationException());
        Assert.Equal(0xE2000000u, resolved.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0u, resolved.Attempts[0].Primitive!.Words[10] >> 16);
        Assert.Equal(0u, resolved.Attempts[0].Primitive!.Words[13] >> 16);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Knownness_reader_requires_retained_composite_color_but_ignores_unknown_UV_padding(bool colorKnown)
    {
        const int cursor = 16;
        NativeBattleGfGeometryAttempt quad = Quad();
        byte[] bytes = new byte[quad.PacketLength + 4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x0D000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0xE1000000);
        quad.Data.AsSpan().Slice(4).CopyTo(bytes.AsSpan(8));
        int[] retained = [8, 42, 43, 54, 55];
        var mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray();
        foreach (int offset in retained)
        {
            mask = mask.SetItem(offset, NativeBattleGfPacketByteState.Untouched);
        }
        var words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGfGeometryBatch batch = new([new(0, bytes.ToImmutableArray(), mask, quad.Primitive! with { Words = words })], bytes.Length);
        List<int> reads = [];
        NativeBattleGfGeometryBatch Resolve() => NativeBattlePacketBatchWords.ResolveWithKnownness(batch, cursor, 128, address =>
        {
            reads.Add(address);
            return address == cursor + 8 ? ((byte)0x7D, colorKnown) : ((byte)0xA5, false);
        });
        if (!colorKnown)
        {
            Assert.Throws<NotSupportedException>(() => Resolve());
            Assert.Equal(new[] { cursor + 8 }, reads);
            return;
        }
        NativeBattleGfGeometryBatch resolved = Resolve();
        Assert.Equal(new[] { cursor + 8 }, reads);
        Assert.Equal(0xE1000000u, resolved.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0x7Du, resolved.Attempts[0].Primitive!.Words[2] & 0xFF);
        Assert.Equal(0u, resolved.Attempts[0].Primitive!.Words[10] >> 16);
        Assert.Equal(0u, resolved.Attempts[0].Primitive!.Words[13] >> 16);
        Assert.Equal(batch.Attempts[0].Data, resolved.Attempts[0].Data);
        Assert.Equal(batch.Attempts[0].WriteMask, resolved.Attempts[0].WriteMask);
    }

    [Fact]
    public void Lazy_prefix_materialization_observes_later_rejected_rewrites_after_first_retained_read()
    {
        NativeBattleGfGeometryAttempt quad = Quad();
        NativeBattleGfGeometryAttempt inherited = quad with { WriteMask = quad.WriteMask.SetItem(4, NativeBattleGfPacketByteState.Untouched) };
        NativeBattleGfGeometryAttempt rewrite = quad with { Data = quad.Data.SetItem(4, 0x7D), Primitive = null };
        NativeBattleGfGeometryBatch batch = new([inherited, rewrite, inherited], 52);
        List<int> reads = [];

        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, 16, 128, address =>
        {
            reads.Add(address);
            return ((byte)0x5B, true);
        });

        Assert.Equal(new[] { 20 }, reads);
        Assert.Equal(0x5Bu, resolved.Attempts[0].Primitive!.Words[1] & 0xFF);
        Assert.Null(resolved.Attempts[1].Primitive);
        Assert.Equal(0x7Du, resolved.Attempts[2].Primitive!.Words[1] & 0xFF);
        for (int index = 0; index < batch.Attempts.Length; index++)
        {
            Assert.Equal(batch.Attempts[index].Data, resolved.Attempts[index].Data);
            Assert.Equal(batch.Attempts[index].WriteMask, resolved.Attempts[index].WriteMask);
        }

        NativeBattleGfGeometryBatch unknownRewrite = batch with { Attempts = batch.Attempts.SetItem(1, rewrite with { WriteMask = rewrite.WriteMask.SetItem(4, NativeBattleGfPacketByteState.Unknown) }) };
        Assert.Throws<NotSupportedException>(() => NativeBattlePacketBatchWords.ResolveWithKnownness(unknownRewrite, 16, 128, _ => ((byte)0x5B, true)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deferred_observation_does_not_defer_invalid_mask_validation_or_read_future_attempts(bool retainedReadFirst)
    {
        NativeBattleGfGeometryAttempt quad = Quad();
        NativeBattleGfGeometryAttempt inherited = quad with { WriteMask = quad.WriteMask.SetItem(4, NativeBattleGfPacketByteState.Untouched) };
        NativeBattleGfGeometryAttempt invalid = quad with { Primitive = null, WriteMask = quad.WriteMask.SetItem(0, (NativeBattleGfPacketByteState)255) };
        NativeBattleGfGeometryBatch batch = new(retainedReadFirst ? [inherited, invalid] : [invalid, inherited], 52);
        List<int> reads = [];

        Assert.Throws<InvalidDataException>(() => NativeBattlePacketBatchWords.ResolveWithKnownness(batch, 16, 128, address =>
        {
            reads.Add(address);
            return ((byte)0x5B, true);
        }));

        Assert.Equal(retainedReadFirst ? new[] { 20 } : Array.Empty<int>(), reads);
    }

    [Fact]
    public void Fully_authored_commands_do_not_allocate_a_per_byte_observation_map_for_rejected_prefixes()
    {
        NativeBattleGfGeometryAttempt quad = Quad();
        NativeBattleGfGeometryAttempt rejected = new(0, new byte[2048].ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 2048).ToImmutableArray(), null);
        NativeBattleGfGeometryBatch ordinary = new([quad], 52);
        NativeBattleGfGeometryBatch withPrefix = new([rejected, quad], 52);
        for (int index = 0; index < 16; index++)
        {
            Resolve(ordinary);
            Resolve(withPrefix);
        }

        long ordinaryBytes = Allocated(ordinary);
        long prefixBytes = Allocated(withPrefix);

        Assert.True(prefixBytes - ordinaryBytes < 16 * 1024, $"A rejected prefix added {prefixBytes - ordinaryBytes} bytes across 16 resolutions.");
        Assert.Equal(quad.Primitive!.Words, Resolve(withPrefix).Attempts[1].Primitive!.Words);

        static NativeBattleGfGeometryBatch Resolve(NativeBattleGfGeometryBatch batch) => NativeBattlePacketBatchWords.ResolveWithKnownness(batch, 0, 4096, _ => throw new InvalidOperationException("Authored commands must not consume retained bytes."));
        static long Allocated(NativeBattleGfGeometryBatch batch)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 16; index++)
            {
                Resolve(batch);
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
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
