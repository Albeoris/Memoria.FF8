using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect298Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect298SmokeTests
{
    [Fact]
    public void Empty_scan_releases_one_context_and_only_initializes_the_native_WORDs()
    {
        var frame = Frame();
        var harness = new Harness();
        var actual = DrawSmoke672C40(frame, Main, Resources(), harness.Inputs());
        Assert.Empty(harness.Batches);
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        var context = Context(actual, NativeBattleTemporaryMemory.Address, 180);
        foreach (int offset in new[] { 0x92, 0x90, 0x8E, 0x8A, 0x88, 0x86, 0x60, 0x58, 0x50, 0x48 })
        {
            Assert.Equal(new byte[2], context.Read(offset, 2));
        }
        Assert.Throws<NotSupportedException>(() => context.Read(0x4A, 2));
        Assert.Throws<NotSupportedException>(() => context.Read(0x96, 2));
    }

    [Theory]
    [InlineData((byte)0, (short)12, 0x104EA64u)]
    [InlineData((byte)1, (short)11, 0x104E74Cu)]
    [InlineData((byte)255, (short)11, 0x104E74Cu)]
    public void Reverse_phase_selects_the_raw_first_directory_entry_and_updates_after_drawing(byte type, short life, uint source)
    {
        var frame = Row(Frame(), life, type, 32766, -32767, 2000, 1, -2, -128);
        var harness = new Harness();
        var actual = DrawSmoke672C40(frame, Main, Resources(), harness.Inputs());
        var command = Assert.Single(Assert.Single(harness.Batches).Batch.Attempts).Primitive!;
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, command.VertexPayload4);
        var row = ReadBytes(actual, RowAddress, 12);
        Assert.Equal(new short[] { -32766, 32761, 1488, (short)(life - 1) }, Enumerable.Range(0, 4).Select(index => W(row, index * 2)));
        var context = Context(actual, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(source, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0, 4)));
        Assert.Equal(source + 68, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x2C, 4)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(context.Read(0x30, 4)));
        Assert.Equal(frame.Memory.SharedEffectRandom, actual.Memory.SharedEffectRandom);
        Assert.Equal(frame.State.ReadDword(SeedGlobal), actual.State.ReadDword(SeedGlobal));
    }

    [Fact]
    public void FT4_keeps_source_color_and_CLUT_clamps_UV_and_retains_unused_high_WORDs()
    {
        var frame = Row(Frame(), 12);
        var harness = new Harness();
        var actual = DrawSmoke672C40(frame, Main, Resources(), harness.Inputs());
        var batch = Assert.Single(harness.Batches);
        Assert.Equal(CursorGlobal, batch.CursorGlobal);
        Assert.Equal(40, batch.Batch.AllocationBytes);
        var attempt = Assert.Single(batch.Batch.Attempts);
        var command = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(19, command.Bucket);
        Assert.Equal(0x09000000u, U(attempt.Data, 0));
        Assert.Equal(10, command.Words.Length);
        Assert.Equal(0x09000000u, command.Words[0]);
        Assert.Equal(0x2C080710u, command.Words[1]);
        Assert.Equal(0x1234FBFAu, command.Words[3]);
        Assert.Equal(0x01CDFBFFu, command.Words[5]);
        Assert.Equal(0xFFFAu, command.Words[7] & 0xFFFF);
        Assert.Equal(0xFFFFu, command.Words[9] & 0xFFFF);
        foreach (int index in new[] { 30, 31, 38, 39 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[index]);
        }
        Assert.Equal(123u, actual.Memory.GeometryRegisters!.Otz);
        Assert.All(command.VertexPayload4, value => Assert.Equal(2000, value));
        Assert.Throws<NotSupportedException>(() => Context(actual, NativeBattleTemporaryMemory.Address, 180).Read(0x4A, 2));
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)1)]
    public void First_RTPT_rejection_retains_only_reached_packet_prefixes_and_still_ages_the_row(short z)
    {
        var frame = Row(Frame(), 12, z: z);
        var harness = new Harness();
        var actual = DrawSmoke672C40(frame, Main, Resources(), harness.Inputs());
        var batch = Assert.Single(harness.Batches).Batch;
        Assert.Equal(0, batch.AllocationBytes);
        var attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, attempt.ByteOffsetRelative);
        int[] written = [0, 1, 2, 3, 4, 5, 6, 7, 12, 13, 14, 15, 20, 21, 28, 36];
        for (int index = 0; index < 40; index++)
        {
            Assert.Equal(written.Contains(index) ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[index]);
        }
        Assert.Equal((short)11, W(ReadBytes(actual, RowAddress, 12), 6));
        Assert.NotEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(Context(actual, NativeBattleTemporaryMemory.Address, 180).Read(0x40, 4)) & 0x60000u);
    }

    [Theory]
    [InlineData((short)-4096, 160)]
    [InlineData((short)4096, 800)]
    public void Negative_winding_and_offscreen_coordinates_do_not_add_native_culling(short scaleX, int offsetX)
    {
        var harness = new Harness { OffsetX = offsetX };
        _ = DrawSmoke672C40(Row(Frame(), 12), Main, Resources(scaleX: scaleX), harness.Inputs());
        Assert.NotNull(Assert.Single(Assert.Single(harness.Batches).Batch.Attempts).Primitive);
    }

    [Fact]
    public void Fourth_RTps_flag_is_not_a_second_admission_gate_and_FIFO_is_current()
    {
        var frame = Frame();
        (frame, uint temporary) = Allocate(frame, 180);
        var context = NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectArena.Length);
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            context = context.Write(offset, new byte[2]);
        }
        var parent = NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, -4096, -4096, 4096], 0, 0, 33, 4096), null);
        context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectArena.Clear().Write(0, parent.Bytes.AsSpan()), 0, context, 0x84, 32);
        frame = PublishContext(frame, temporary, context, 180);
        var resources = Resources(width: 2, height: 2, sourceX: -1, sourceY: -1);
        var harness = new Harness { H = 60 };
        var actual = DrawSmokeQuads672DB0(frame, temporary, resources.ReadSprite(0x104EA64), 68, 1, harness.Inputs());
        var command = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(Assert.Single(harness.Batches).Batch.Attempts).Primitive);
        Assert.Equal(new[] { 65, 33, 33, 1 }, command.VertexPayload4);
        Assert.NotEqual(0u, actual.Memory.ScriptedColorRegisters!.Flags!.Value & 0x60000u);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(Context(actual, temporary, 180).Read(0x40, 4)) & 0x60000u);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Signed_nonpositive_quad_count_skips_packet_work_but_not_lifetime_or_velocity(int count)
    {
        var harness = new Harness();
        var actual = DrawSmoke672C40(Row(Frame(), 12, vx: -3), Main, Resources(count: count), harness.Inputs());
        Assert.Empty(harness.Batches);
        var row = ReadBytes(actual, RowAddress, 12);
        Assert.Equal((short)-12, W(row, 0));
        Assert.Equal((short)11, W(row, 6));
    }

    [Fact]
    public void Last_life_draws_before_zero_and_a_cold_restored_next_scan_skips_it()
    {
        var firstHarness = new Harness();
        var first = DrawSmoke672C40(Row(Frame(), 1), Main, Resources(), firstHarness.Inputs());
        Assert.Single(firstHarness.Batches);
        Assert.Equal((short)0, W(ReadBytes(first, RowAddress, 12), 6));
        var cold = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect298Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        var secondHarness = new Harness();
        var second = DrawSmoke672C40(cold, Main, Resources(), secondHarness.Inputs());
        Assert.Empty(secondHarness.Batches);
        Assert.Equal(ReadBytes(first, RowAddress, 12).Read(0, 12), ReadBytes(second, RowAddress, 12).Read(0, 12));
    }

    [Fact]
    public void Two_quads_reserve_contiguous_packets_in_one_delivery_and_one_reused_context()
    {
        var harness = new Harness();
        var actual = DrawSmoke672C40(Row(Frame(), 12), Main, Resources(count: 2), harness.Inputs());
        var batch = Assert.Single(harness.Batches).Batch;
        Assert.Equal(80, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 40 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.All(batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Last_row_uses_the_same_temporary_owner_and_retains_actual_vertex_W_bytes()
    {
        var frame = Frame();
        uint last = RowAddress + 127 * 12;
        var row = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes((short)2000)).Write(6, BitConverter.GetBytes((short)12));
        frame = WriteBytes(frame, last, row, 12);
        var context = NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectArena.Length);
        foreach (int offset in new[] { 0x4A, 0x52, 0x5A, 0x62 })
        {
            context = context.Write(offset, new byte[] { 0x5A, 0xA5 });
        }
        frame = PublishContext(frame, NativeBattleTemporaryMemory.Address, context, 180);
        var harness = new Harness();
        var actual = DrawSmoke672C40(frame, Main, Resources(), harness.Inputs());
        Assert.Single(harness.Batches);
        Assert.Equal((short)11, W(ReadBytes(actual, last, 12), 6));
        Assert.Equal((short)0, W(ReadBytes(actual, RowAddress, 12), 6));
        var retained = Context(actual, NativeBattleTemporaryMemory.Address, 180);
        foreach (int offset in new[] { 0x4A, 0x52, 0x5A, 0x62 })
        {
            Assert.Equal(new byte[] { 0x5A, 0xA5 }, retained.Read(offset, 2));
        }
    }

    [Fact]
    public void Complete_physical_tag_and_nine_GPU_words_append_with_retained_UV_padding()
    {
        var harness = new Harness();
        var frame = DrawSmoke672C40(Row(Frame(), 12), Main, Resources(), harness.Inputs());
        var batch = Assert.Single(harness.Batches).Batch;
        int cursor = FirstPacketOffset;
        var memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(cursor, Enumerable.Repeat((byte)0xA5, 40).ToArray()) };
        var resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => memory.Arena!.Read(offset, 1)[0]);
        var written = NativeBattleEffectPacketArena.Append(memory, cursor, NativeBattleLogicalOrdering.Clear(), resolved);
        Assert.Equal(cursor + 40, written.Cursor);
        var packet = Assert.Single(written.Ordering.Packets);
        Assert.Equal(10, packet.Words.Length);
        Assert.Equal(9u, packet.Words[0] >> 24);
        Assert.Equal(0x2Cu, packet.Words[1] >> 24);
        Assert.Equal(0x0000FFFAu, packet.Words[7]);
        Assert.Equal(0x0000FFFFu, packet.Words[9]);
        foreach (int offset in new[] { 30, 31, 38, 39 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, Assert.Single(batch.Attempts).WriteMask[offset]);
        }
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.Memory.Arena!.Read(cursor + 30, 2));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.Memory.Arena.Read(cursor + 38, 2));
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, Assert.Single(written.Ordering.Proxies).VertexPayload4);
    }

    private const uint Main = ArenaAddress + MainOffset;
    private const uint RowAddress = Main + 0x83EC;

    private static NativeBattleEffect298Frame Frame()
    {
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null) with { Otz = 123 }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), SharedEffectRandom = new(123) };
        var effect = new NativeBattleOriginalEffectState(298, 297, new(298, Entry, Initializer), null, 0);
        return new(memory, effect, NativeBattleEffect298State.FromColdProcess().WriteDword(ArenaGlobal, ArenaAddress)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect298Frame Row(NativeBattleEffect298Frame frame, short life, byte type = 0, short x = 0, short y = 0, short z = 2000, sbyte vx = 0, sbyte vy = 0, sbyte vz = 0)
    {
        var row = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z)).Write(6, BitConverter.GetBytes(life)).Write(8, [unchecked((byte)vx), unchecked((byte)vy), unchecked((byte)vz), type]);
        return WriteBytes(frame, RowAddress, row, 12);
    }

    private static NativeBattleEffect298Resources Resources(int count = 1, short scaleX = 4096, byte width = 16, byte height = 8, short sourceX = 0, short sourceY = 0)
    {
        var resources = NativeBattleEffect298StateTests.Resources();
        var sprites = resources.Sprites;
        foreach (uint address in new[] { 0x104EA64u, 0x104E74Cu })
        {
            int index = Enumerable.Range(0, sprites.Length).Single(candidate => sprites[candidate].Address == address);
            var source = sprites[index];
            byte[] bytes = source.Bytes.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(64), count);
            for (int face = 0; face < Math.Max(count, 0); face++)
            {
                int at = 68 + face * 20;
                bytes[at] = width;
                bytes[at + 1] = 7;
                bytes[at + 2] = height;
                bytes[at + 3] = 0x2C;
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 4), 0x1234FBFA);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 8), sourceX);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 10), sourceY);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 14), 0xABCD);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 16), scaleX);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 18), 4096);
            }
            sprites = sprites.SetItem(index, source with { Bytes = bytes.ToImmutableArray() });
        }
        return resources with { Sprites = sprites };
    }

    private static short W(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.Read(offset, 2));
    private static uint U(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));

    private sealed class Harness
    {
        public List<NativeBattleEffect298DeliverBatch> Batches { get; } = [];
        public int H { get; init; } = 100;
        public int OffsetX { get; init; } = 160;

        public NativeBattleEffect298Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, checked((ushort)H), checked((short)OffsetX), 108), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (frame, operation) =>
        {
            Batches.Add(Assert.IsType<NativeBattleEffect298DeliverBatch>(operation));
            return frame;
        }, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
    }
}
