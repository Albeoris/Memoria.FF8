using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGfGeometryPacketsTests
{
    private static NativeBattleGfGeometryContext Context => new(0, 0, 0, null, null, 0, 17, 2);
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(Identity, 216, 160, 108);

    [Fact]
    public void Original_FT3_layout_uses_AVSZ3_context_bias_and_four_DWORD_proxy_payload()
    {
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(NativeBattleGfGeometryPackets.EmitTriangle7049A0(Face(), Corners(), Context with { DepthBias = 9 }));
        Assert.Equal(275, primitive.Bucket);
        Assert.Equal(new uint[] { 0x7000000, 0x26404040, XY(100, 100), 0x10000201, XY(200, 100), 0x00040302, XY(100, 200), 0x0605 }, primitive.Words.ToArray());
        Assert.Equal(new int[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4.ToArray());
    }

    [Theory]
    [InlineData(1, 0x24u, 0x26u)]
    [InlineData(4, 0x26u, 0x24u)]
    [InlineData(5, 0x24u, 0x24u)]
    public void Semitransparency_set_precedes_clear(uint flags, uint inputCommand, uint outputCommand)
    {
        byte[] face = Face();
        BinaryPrimitives.WriteUInt32LittleEndian(face, inputCommand << 24 | 0x123456);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(NativeBattleGfGeometryPackets.EmitTriangle7049A0(face, Corners(), Context with { Flags = flags }));
        Assert.Equal(outputCommand << 24 | 0x123456, primitive.Words[1]);
    }

    [Theory]
    [InlineData(0x300u, 0x22220302u, 0x33330201u)]
    [InlineData(0xC00u, 0x22260302u, 0x43330201u)]
    [InlineData(0xF00u, 0x22260302u, 0x43330201u)]
    public void Material_add_precedes_override_and_preserves_UV_low_WORD(uint flags, uint expectedPage, uint expectedClut)
    {
        NativeBattleGfGeometryContext context = Context with { Flags = flags, TexturePage = 0x2222, Clut = 0x3333 };
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(NativeBattleGfGeometryPackets.EmitTriangle7049A0(Face(), Corners(), context));
        Assert.Equal(expectedPage, primitive.Words[5]);
        Assert.Equal(expectedClut, primitive.Words[3]);
        Assert.Throws<NotSupportedException>(() => NativeBattleGfGeometryPackets.EmitTriangle7049A0(Face(), Corners(), context with { TexturePage = null }));
    }

    [Fact]
    public void Backfaces_require_flag10_while_native_degenerate_triangles_remain_admitted()
    {
        NativeBattleModuleProjectedVertex[] backwards = [Corners()[0], Corners()[2], Corners()[1]];
        Assert.Null(NativeBattleGfGeometryPackets.EmitTriangle7049A0(Face(), backwards, Context));
        Assert.NotNull(NativeBattleGfGeometryPackets.EmitTriangle7049A0(Face(), backwards, Context with { Flags = 0x10 }));
        NativeBattleModuleProjectedVertex[] degenerate = [Corners()[0], Corners()[0], Corners()[0]];
        Assert.NotNull(NativeBattleGfGeometryPackets.EmitTriangle7049A0(Face(), degenerate, Context));
    }

    [Fact]
    public void Near_flags_and_native_all_outside_tests_cull_without_fabricating_depth()
    {
        NativeBattleModuleProjectedVertex[] near = Corners();
        near[1] = near[1] with { Flags = 0x80020000 };
        Assert.Null(NativeBattleGfGeometryPackets.EmitTriangle7049A0(Face(), near, Context));
        NativeBattleModuleProjectedVertex[] outside = [new(XY(-1, 1), 200, 0), new(XY(2561, 2), 300, 0), new(XY(-4, 3), 400, 0)];
        Assert.Null(NativeBattleGfGeometryPackets.EmitTriangle7049A0(Face(), outside, Context with { Flags = 0x10 }));
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(NativeBattleGfGeometryPackets.EmitTriangle7049A0(Face(), Corners(), Context with { DepthBias = -2000 }));
        Assert.Equal(17, primitive.Bucket);
        Assert.Equal(new int[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4.ToArray());
    }

    [Fact]
    public void Context_depth_cue_uses_actual_far_RGB_and_preserves_command_BYTE()
    {
        NativeBattleSummonColors colors = new(0, 0, 1f / 4096, 4503601774854144.0);
        NativeBattleGfGeometryContext context = Context with { Flags = 0x40, FarColor = 0x00804020, DepthCue = 2048 };
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(NativeBattleGfGeometryPackets.EmitTriangle7049A0(Face(), Corners(), context, colors));
        Assert.Equal(0x26604030u, primitive.Words[1]);
        Assert.Throws<NotSupportedException>(() => NativeBattleGfGeometryPackets.EmitTriangle7049A0(Face(), Corners(), context));
    }

    [Fact]
    public void Dispatcher_reads_native_word_times_four_offsets_and_advances_exactly_twenty_bytes_per_FT3()
    {
        (ImmutableArray<byte> source, ImmutableArray<byte> vertices) = Geometry();
        NativeBattleGfGeometryContext context = Context with { Flags = 0x2000 };
        ImmutableArray<NativeBattleGeometryPrimitive> emitted = NativeBattleGfGeometryPackets.Emit7043B0(source, 16, vertices, Identity, Projection, context);
        NativeBattleGeometryPrimitive primitive = Assert.Single(emitted);
        Assert.Equal(272, primitive.Bucket);
        NativeBattleModuleProjectedVertex expected = NativeBattleModuleProjection.Project45FE10(Identity, new(-64, -64, 0), Projection);
        Assert.Equal(expected.Screen, primitive.Words[2]);
        Assert.Equal(new int[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4.ToArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleGfGeometryPackets.Emit7043B0(source, 16, default, Identity, Projection, context));
        Assert.Throws<InvalidDataException>(() => NativeBattleGfGeometryPackets.Emit7043B0(source, 16, ImmutableArray<byte>.Empty, Identity, Projection, context));
    }

    [Fact]
    public void Native_default_vertex_pointer_is_geometry_plus_eight_and_truncated_groups_fail()
    {
        (ImmutableArray<byte> source, ImmutableArray<byte> vertices) = Geometry();
        byte[] bytes = source.ToArray();
        vertices.AsSpan().CopyTo(bytes.AsSpan(24));
        Assert.Single(NativeBattleGfGeometryPackets.Emit7043B0(bytes.ToImmutableArray(), 16, default, Identity, Projection, Context));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(80), 1);
        Assert.Throws<InvalidDataException>(() => NativeBattleGfGeometryPackets.Emit7043B0(bytes.AsSpan(0, 84).ToArray().ToImmutableArray(), 16, default, Identity, Projection, Context));
    }

    [Fact]
    public void RTPT_rounds_rotation_before_translation_and_retains_native_near_boundary()
    {
        NativeBattleActionMatrix matrix = Identity with { Rotation = [2048, 0, 0, 0, 4096, 0, 0, 0, 4096], X = 1 };
        NativeBattleCameraProjection projection = Projection with { FocalLength = 4096, OffsetX = 0, OffsetY = 0 };
        NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(matrix, new(1, 0, 0), projection);
        Assert.Equal(XY(8, 0), projected.Screen);
        NativeBattleModuleProjectedVertex boundary = NativeBattleModuleProjection.Project45FE10(Identity with { Z = 108 }, new(0, 0, 0), Projection);
        Assert.Equal(0x80020000u, boundary.Flags);
        Assert.Equal((ushort)108, boundary.Depth);
    }

    [Theory]
    [InlineData(0, 0x04000000u, 5, 0x20u)]
    [InlineData(1, 0x05000000u, 6, 0x28u)]
    [InlineData(2, 0x07000000u, 8, 0x24u)]
    [InlineData(3, 0x09000000u, 10, 0x2Cu)]
    [InlineData(4, 0x06000000u, 7, 0x30u)]
    [InlineData(5, 0x08000000u, 9, 0x38u)]
    [InlineData(6, 0x09000000u, 10, 0x34u)]
    [InlineData(7, 0x0C000000u, 13, 0x3Cu)]
    public void Eight_original_groups_emit_exact_native_packet_sizes_and_color_layouts(int group, uint tag, int wordCount, uint command)
    {
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(NativeBattleGfGeometryPackets.EmitPolygon7043B0(Face(group), Corners(group), group, Context));
        Assert.Equal(tag, primitive.Words[0]);
        Assert.Equal(wordCount, primitive.Words.Length);
        Assert.Equal(command << 24 | 0x404040u, primitive.Words[1]);
        Assert.Equal(XY(100, 100), primitive.Words[2]);
        bool textured = (group & 2) != 0;
        bool gouraud = (group & 4) != 0;
        bool quad = (group & 1) != 0;
        if (textured)
        {
            Assert.Equal(0x10000201u, primitive.Words[3]);
            Assert.Equal(0x00040302u, primitive.Words[gouraud ? 6 : 5]);
            Assert.Equal(quad ? 0x0807u : 0x0605u, primitive.Words[^1]);
        }
        if (gouraud)
        {
            Assert.Equal(0x11402010u, primitive.Words[textured ? 4 : 3]);
            Assert.Equal(0x22804020u, primitive.Words[textured ? 7 : 5]);
        }
        Assert.Equal(quad ? 273 : 272, primitive.Bucket);
        Assert.Equal(quad ? new int[] { 4096, 4096, 4096, 4096 } : [4096, 4096, 4096, 0], primitive.VertexPayload4.ToArray());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Gouraud_semi_backface_and_depthcue_flags_use_their_distinct_native_bits(int group)
    {
        NativeBattleModuleProjectedVertex[] backwards = Corners(group);
        (backwards[1], backwards[2]) = (backwards[2], backwards[1]);
        Assert.Null(NativeBattleGfGeometryPackets.EmitPolygon7043B0(Face(group), backwards, group, Context with { Flags = 0x10 }));
        NativeBattleSummonColors colors = new(0, 0, 1f / 4096, 4503601774854144.0);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(NativeBattleGfGeometryPackets.EmitPolygon7043B0(Face(group), backwards, group, Context with { Flags = 0xA2, DepthCue = 2048 }, colors));
        uint command = (uint)(group switch { 4 => 0x32, 5 => 0x3A, 6 => 0x36, _ => 0x3E });
        Assert.Equal(command << 24 | 0x202020u, primitive.Words[1]);
        bool textured = (group & 2) != 0;
        uint fifoCommand = (group & 1) != 0 ? 0x33000000u : command << 24;
        Assert.Equal(fifoCommand | 0x201008u, primitive.Words[textured ? 4 : 3]);
        Assert.Equal(fifoCommand | 0x402010u, primitive.Words[textured ? 7 : 5]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public void Quad_fourth_RTPS_flags_are_unchecked_but_screen_and_AVSZ4_include_fourth_vertex(int group)
    {
        NativeBattleModuleProjectedVertex[] vertices = Corners(group);
        vertices[3] = vertices[3] with { Flags = 0x80060000, Depth = 0 };
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(NativeBattleGfGeometryPackets.EmitPolygon7043B0(Face(group), vertices, group, Context));
        Assert.Equal(209, primitive.Bucket);
        Assert.Equal(new int[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4.ToArray());
        vertices[0] = vertices[0] with { Flags = 0x20000 };
        Assert.Null(NativeBattleGfGeometryPackets.EmitPolygon7043B0(Face(group), vertices, group, Context));
    }

    [Fact]
    public void Dispatcher_walks_all_eight_original_record_strides_in_native_group_order()
    {
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] source = new byte[80 + 32 + lengths.Sum()];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(16), 64);
        int cursor = 80;
        for (int group = 0; group < lengths.Length; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(cursor), 1);
            cursor += 4;
            Face(group).CopyTo(source, cursor);
            cursor += lengths[group];
        }
        (_, ImmutableArray<byte> triangle) = Geometry();
        byte[] vertices = new byte[32];
        triangle.AsSpan().CopyTo(vertices);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(24), 64);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(26), 64);
        ImmutableArray<NativeBattleGeometryPrimitive> result = NativeBattleGfGeometryPackets.Emit7043B0(source.ToImmutableArray(), 16, vertices.ToImmutableArray(), Identity, Projection, Context with { Flags = 0x2000 });
        Assert.Equal(new uint[] { 0x20, 0x28, 0x24, 0x2C, 0x30, 0x38, 0x34, 0x3C }, result.Select(primitive => primitive.Words[1] >> 24).ToArray());
        Assert.Equal(source.Length, cursor);
    }

    [Fact]
    public void Quad_dispatcher_does_not_read_fourth_vertex_before_native_first_three_admission()
    {
        byte[] source = new byte[136];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(16), 64);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(92), 1);
        Face(3).CopyTo(source, 96);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(106), ushort.MaxValue);
        (_, ImmutableArray<byte> vertices) = Geometry();
        NativeBattleGfGeometryContext context = Context with { Flags = 0x2000 };
        Assert.Empty(NativeBattleGfGeometryPackets.Emit7043B0(source.ToImmutableArray(), 16, vertices, Identity with { Z = 108 }, Projection, context));
        Assert.Throws<InvalidDataException>(() => NativeBattleGfGeometryPackets.Emit7043B0(source.ToImmutableArray(), 16, vertices, Identity, Projection, context));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Attempt_masks_retain_exact_native_near_NCLIP_screen_and_accepted_writes(int group)
    {
        bool quad = (group & 1) != 0;
        bool textured = (group & 2) != 0;
        bool gouraud = (group & 4) != 0;
        int count = quad ? 4 : 3;
        HashSet<int> prefix = [0, 1];
        if (textured)
        {
            for (int corner = 0; corner < count; corner++)
            {
                prefix.Add(ScreenWord(corner) + 1);
            }
        }
        NativeBattleModuleProjectedVertex[] near = Corners(group);
        near[0] = near[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryAttempt nearAttempt = NativeBattleGfGeometryPackets.EmitAttempt7043B0(Face(group), near, group, Context with { Flags = 0xF00 });
        Assert.Null(nearAttempt.Primitive);
        CheckWords(nearAttempt, prefix);
        if (textured)
        {
            Assert.Equal(0x00040302u, Word(nearAttempt, ScreenWord(1) + 1));
        }
        NativeBattleModuleProjectedVertex[] backwards = Corners(group);
        (backwards[1], backwards[2]) = (backwards[2], backwards[1]);
        NativeBattleGfGeometryContext material = Context with { Flags = 0xF00, TexturePage = 1, Clut = 2 };
        NativeBattleGfGeometryAttempt windingAttempt = NativeBattleGfGeometryPackets.EmitAttempt7043B0(Face(group), backwards, group, material);
        Assert.Null(windingAttempt.Primitive);
        CheckWords(windingAttempt, prefix);
        if (textured)
        {
            Assert.Equal(0x00050302u, Word(windingAttempt, ScreenWord(1) + 1));
            Assert.Equal(0x10020201u, Word(windingAttempt, ScreenWord(0) + 1));
        }
        NativeBattleModuleProjectedVertex[] outside = Corners(group);
        for (int corner = 0; corner < count; corner++)
        {
            outside[corner] = outside[corner] with { Screen = XY(-1, (short)(100 + corner * 100)) };
        }
        NativeBattleGfGeometryAttempt screenAttempt = NativeBattleGfGeometryPackets.EmitAttempt7043B0(Face(group), outside, group, material);
        Assert.Null(screenAttempt.Primitive);
        HashSet<int> screenWords = [.. prefix];
        for (int corner = 0; corner < count; corner++)
        {
            screenWords.Add(ScreenWord(corner));
        }
        CheckWords(screenAttempt, screenWords);
        NativeBattleGfGeometryAttempt accepted = NativeBattleGfGeometryPackets.EmitAttempt7043B0(Face(group), Corners(group), group, Context);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(accepted.Primitive);
        Assert.All(accepted.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(new byte[] { 0, 0, 0 }, accepted.Data.AsSpan().Slice(0, 3).ToArray());
        for (int word = 0; word < primitive.Words.Length; word++)
        {
            Assert.Equal(primitive.Words[word], Word(accepted, word));
        }

        int ScreenWord(int corner) => gouraud ? 2 + corner * (textured ? 3 : 2) : 2 + corner * (textured ? 2 : 1);

        static uint Word(NativeBattleGfGeometryAttempt attempt, int word) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(word * 4));

        static void CheckWords(NativeBattleGfGeometryAttempt attempt, HashSet<int> expectedWords)
        {
            Assert.Equal(attempt.PacketLength, attempt.WriteMask.Length);
            for (int index = 0; index < attempt.WriteMask.Length; index++)
            {
                Assert.Equal(expectedWords.Contains(index / 4) ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[index]);
            }
        }
    }

    [Fact]
    public void Rejected_attempts_reuse_cursor_and_preserve_unwritten_prior_arena_bytes_after_JSON_restore()
    {
        byte[] source = new byte[172];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(16), 64);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(88), 3);
        Face().CopyTo(source, 92);
        Face().CopyTo(source, 112);
        Face().CopyTo(source, 132);
        SetNear(92, 0x26ABBCCD);
        SetNear(132, 0x26112233);
        (_, ImmutableArray<byte> triangle) = Geometry();
        byte[] vertices = new byte[32];
        triangle.AsSpan().CopyTo(vertices);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(28), -4096);
        NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.ToImmutableArray(), 16, vertices.ToImmutableArray(), Identity, Projection, Context with { Flags = 0x2000 });
        Assert.Equal(new int[] { 0, 0, 32 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        Assert.Equal(new bool[] { false, true, false }, batch.Attempts.Select(attempt => attempt.Primitive is not null).ToArray());
        Assert.Equal(32, batch.AllocationBytes);
        NativeBattleGfGeometryBatch restored = JsonSerializer.Deserialize<NativeBattleGfGeometryBatch>(JsonSerializer.Serialize(batch))!;
        byte[] expected = Apply(batch);
        Assert.Equal(expected, Apply(restored));
        Assert.Equal(0x7000000u, BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(32)));
        Assert.Equal(0x26112233u, BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(36)));
        Assert.All(expected.AsSpan(40, 4).ToArray(), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(0x10000201u, BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(44)));
        Assert.All(expected.Skip(64), value => Assert.Equal((byte)0xA5, value));
        NativeBattleGeometryPrimitive accepted = Assert.IsType<NativeBattleGeometryPrimitive>(batch.Attempts[1].Primitive);
        for (int index = 0; index < accepted.Words.Length; index++)
        {
            Assert.Equal(accepted.Words[index], BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(index * 4)));
        }

        void SetNear(int start, uint command)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(start), command);
            for (int corner = 0; corner < 3; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(start + 4 + corner * 2), 6);
            }
        }

        static byte[] Apply(NativeBattleGfGeometryBatch attempts)
        {
            byte[] prior = Enumerable.Repeat((byte)0xA5, 80).ToArray();
            foreach (NativeBattleGfGeometryAttempt attempt in attempts.Attempts)
            {
                for (int index = 0; index < attempt.PacketLength; index++)
                {
                    if (attempt.WriteMask[index] == NativeBattleGfPacketByteState.Known)
                    {
                        prior[attempt.ByteOffsetRelative + index] = attempt.Data[index];
                    }
                    else
                    {
                        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[index]);
                    }
                }
            }
            return prior;
        }
    }

    private static NativeBattleModuleProjectedVertex[] Corners() => [new(XY(100, 100), 4096, 0), new(XY(200, 100), 4096, 0), new(XY(100, 200), 4096, 0)];
    private static NativeBattleModuleProjectedVertex[] Corners(int group) => (group & 1) == 0 ? Corners() : [.. Corners(), new(XY(200, 200), 4096, 0)];
    private static uint XY(short x, short y) => unchecked((ushort)x) | unchecked((uint)y << 16);

    private static byte[] Face()
    {
        byte[] face = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x26404040);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(8), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x0605);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x10000201);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x00040302);
        return face;
    }

    private static byte[] Face(int group)
    {
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        uint[] commands = [0x20, 0x28, 0x24, 0x2C, 0x30, 0x38, 0x34, 0x3C];
        byte[] face = new byte[lengths[group]];
        BinaryPrimitives.WriteUInt32LittleEndian(face, commands[group] << 24 | 0x404040);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(8), 4);
        bool quad = (group & 1) != 0;
        bool textured = (group & 2) != 0;
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 6);
        }
        if (textured)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x10000201);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x00040302);
            if (quad)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x08070605);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x0605);
            }
        }
        if ((group & 4) != 0)
        {
            int offset = textured ? (quad ? 24 : 20) : 12;
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(offset), 0x11402010);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(offset + 4), 0x22804020);
            if (quad)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(offset + 8), 0x33C06030);
            }
        }
        return face;
    }

    private static (ImmutableArray<byte> Source, ImmutableArray<byte> Vertices) Geometry()
    {
        byte[] source = new byte[132];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(16), 64);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(88), 1);
        Face().CopyTo(source, 92);
        byte[] vertices = new byte[24];
        Put(0, -64, -64);
        Put(8, 64, -64);
        Put(16, -64, 64);
        return (source.ToImmutableArray(), vertices.ToImmutableArray());

        void Put(int offset, short x, short y)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(offset), x);
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(offset + 2), y);
        }
    }
}
