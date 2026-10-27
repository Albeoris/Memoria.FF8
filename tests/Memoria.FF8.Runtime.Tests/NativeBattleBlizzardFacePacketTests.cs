using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlizzardFacePacketTests
{
    private static readonly NativeBattleSummonColors Colors = new(0, 0, 1f / 4096, 4503601774854144d);
    private static NativeBattleBlizzardFacePacketContext Context => new(0x24808080, 0, null, null, 0, new(0, 128, 128, 128), new(0, 0, 0, 0), 2, 2);
    private static ImmutableArray<NativeBattleModuleProjectedVertex> Corners => [new(Screen(100, 100), 1024, 0), new(Screen(200, 100), 1024, 0), new(Screen(100, 200), 1024, 0)];

    [Fact]
    public void Ordinary_face_uses_fixed_neutral_color_and_exact_primary_uv_words()
    {
        NativeBattleGfGeometryBatch batch = Emit(0, 1);
        Assert.Equal(32, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(65, primitive.Bucket);
        Assert.Equal(new uint[] { 0x07000000, 0x24808080, Screen(100, 100), 0x3DE71211, Screen(200, 100), 0x003D3433, Screen(100, 200), 0x7A79 }, primitive.Words.ToArray());
        Assert.Equal(new[] { 1024, 1024, 1024, 0 }, primitive.VertexPayload4.ToArray());
    }

    [Fact]
    public void Paired_face_is_one_tag9_composite_without_padding_and_with_zero_proxy_depths()
    {
        NativeBattleGfGeometryBatch batch = Emit(1, 1);
        Assert.Equal(72, batch.AllocationBytes);
        Assert.Equal(2, batch.Attempts.Length);
        NativeBattleGfGeometryAttempt paired = batch.Attempts[1];
        Assert.Equal(32, paired.ByteOffsetRelative);
        Assert.Equal(40, paired.PacketLength);
        Assert.All(paired.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(new uint[] { 0x09000000, 0xE2080210, 0x26808080, Screen(100, 100), 0x12340A09, Screen(200, 100), 0x01421413, Screen(100, 200), 0x1E1D, 0xE2000000 }, paired.Primitive!.Words.ToArray());
        Assert.Equal(64, paired.Primitive.Bucket);
        Assert.Equal(new int[4], paired.Primitive.VertexPayload4.ToArray());
        Assert.Equal(NativeBattleEffectTextureWindows.Encode(Context.Before), paired.Primitive.Words[1]);
        Assert.Equal(NativeBattleEffectTextureWindows.Encode(Context.After), paired.Primitive.Words[9]);
    }

    [Theory]
    [InlineData(-32768, 32767, 32)]
    [InlineData(-1, 0, 32)]
    [InlineData(0, 0, 72)]
    [InlineData(32767, -32768, 72)]
    public void Paired_admission_compares_the_already_updated_signed_age_and_duration(int age, int duration, int allocation)
    {
        Assert.Equal(allocation, Emit((short)age, (short)duration).AllocationBytes);
    }

    [Fact]
    public void Near_rejection_keeps_only_native_prefix_without_reading_fade_or_partner()
    {
        ImmutableArray<NativeBattleModuleProjectedVertex> corners = Corners.SetItem(1, Corners[1] with { Flags = 0x20000 });
        NativeBattleGfGeometryBatch batch = NativeBattleBlizzardFacePackets.EmitFace61B9A0(Primary, default, corners, 1, 1, Context with { Flags = 0x41, Before = null!, After = null! });
        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0x26808080u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan()[4..]));
        for (int offset = 0; offset < 32; offset++)
        {
            bool written = offset < 8 || offset is >= 12 and < 16 or >= 20 and < 24 or >= 28;
            Assert.Equal(written ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[offset]);
        }
    }

    [Fact]
    public void Negative_backface_and_collective_outside_screen_are_admitted_without_generic_mesh_gates()
    {
        ImmutableArray<NativeBattleModuleProjectedVertex> backface = [Corners[0], Corners[2], Corners[1]];
        Assert.True(NativeBattleModulePackets.Nclip(backface[0].Screen, backface[1].Screen, backface[2].Screen) < 0);
        Assert.Equal(72, NativeBattleBlizzardFacePackets.EmitFace61B9A0(Primary, Partner, backface, 1, 1, Context).AllocationBytes);
        ImmutableArray<NativeBattleModuleProjectedVertex> outside = Corners.Select(vertex => vertex with { Screen = Screen(-100, -100), Flags = 0x80006000 }).ToImmutableArray();
        Assert.Equal(72, NativeBattleBlizzardFacePackets.EmitFace61B9A0(Primary, Partner, outside, 1, 1, Context).AllocationBytes);
    }

    [Theory]
    [InlineData(0u, 0x24808080u)]
    [InlineData(1u, 0x26808080u)]
    [InlineData(4u, 0x24808080u)]
    [InlineData(5u, 0x26808080u)]
    [InlineData(0xFFFFFFBEu, 0x24808080u)]
    public void Only_native_flag1_changes_primary_semi_transparency(uint flags, uint expected)
    {
        NativeBattleGfGeometryBatch batch = Emit(1, 1, Context with { Flags = flags });
        Assert.Equal(expected, batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0x26808080u, batch.Attempts[1].Primitive!.Words[2]);
    }

    [Fact]
    public void Accepted_fade_uses_existing_interpolation_and_selected_unknowns_fail()
    {
        NativeBattleBlizzardFacePacketContext context = Context with { Flags = 0xC3, Fade = 2048, FarColor = 0 };
        NativeBattleGfGeometryBatch batch = Emit(1, 1, context, Colors);
        Assert.Equal(0x26404040u, batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0x26404040u, batch.Attempts[1].Primitive!.Words[2]);
        Assert.Throws<NotSupportedException>(() => Emit(1, 1, context));
        Assert.Throws<NotSupportedException>(() => Emit(1, 1, context with { Fade = null }, Colors));
        Assert.Throws<NotSupportedException>(() => Emit(1, 1, context with { FarColor = null }, Colors));
        Assert.Throws<NotSupportedException>(() => NativeBattleBlizzardFacePackets.EmitFace61B9A0(Primary, default, Corners, 1, 1, Context));
        Assert.Equal(32, NativeBattleBlizzardFacePackets.EmitFace61B9A0(Primary, default, Corners, 0, 1, Context with { Before = null!, After = null! }).AllocationBytes);
    }

    [Fact]
    public void Transition_callback_retains_the_controllers_moving_gray_command_even_when_updated_age_reaches_duration()
    {
        NativeBattleGfGeometryBatch moving = Emit(7, 7, Context with { PrimaryColor = 0x26606060 });
        Assert.Equal(72, moving.AllocationBytes);
        Assert.Equal(0x26606060u, moving.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0x26606060u, moving.Attempts[1].Primitive!.Words[2]);
        NativeBattleGfGeometryBatch stationary = Emit(7, 7);
        Assert.Equal(0x24808080u, stationary.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0x26808080u, stationary.Attempts[1].Primitive!.Words[2]);
    }

    [Theory]
    [InlineData(240, 16, 0, 16, 128, 160, 144)]
    [InlineData(0, 16, 32, -16, 112, 128, 144)]
    [InlineData(0, 128, 255, 0, 0, 128, 255)]
    [InlineData(0, 128, 255, -256, 128, 0, 127)]
    public void Composite_signed_v_offset_adjusts_every_vertex_once_and_preserves_u_and_material(int a, int b, int c, int offset, int expectedA, int expectedB, int expectedC)
    {
        byte[] partner = Partner.ToArray();
        partner[13] = (byte)a;
        partner[17] = (byte)b;
        partner[11] = (byte)c;
        NativeBattleGfGeometryBatch batch = NativeBattleBlizzardFacePackets.EmitFace61B9A0(Primary, partner.ToImmutableArray(), Corners, 1, 1, Context with { VOffset = offset });
        ImmutableArray<uint> words = batch.Attempts[1].Primitive!.Words;
        Assert.Equal((uint)expectedA, words[4] >> 8 & 255);
        Assert.Equal((uint)expectedB, words[6] >> 8 & 255);
        Assert.Equal((uint)expectedC, words[8] >> 8 & 255);
        Assert.Equal(0x12340009u, words[4] & 0xFFFF00FFu);
        Assert.Equal(0x01420013u, words[6] & 0xFFFF00FFu);
        Assert.Equal(0x1Du, words[8] & 0xFFFF00FFu);
    }

    [Fact]
    public void Shared_unsigned_offset_wrapper_preserves_its_existing_aggregate_rule()
    {
        byte[] coordinates = [0, 127, 255];
        foreach (ushort offset in new ushort[] { 0, 1, 127, 255, 256, 65535 })
        {
            foreach (byte dimension in new byte[] { 0, 1, 128, 255 })
            {
                int[] values = coordinates.Select(coordinate => coordinate + offset).ToArray();
                bool wrap = values.Aggregate(0, (combined, value) => combined | value) > 255;
                byte[] expected = values.Select(value => unchecked((byte)(value - (wrap ? dimension : 0)))).ToArray();
                Assert.Equal(expected, NativeBattleEffectTextureWindows.OffsetAxis(coordinates, offset, dimension).ToArray());
            }
        }
        Assert.Equal(new byte[] { 128, 129 }, NativeBattleEffectTextureWindows.OffsetAxisSigned([0, 1], int.MinValue, -128).ToArray());
    }

    [Theory]
    [InlineData(4, new byte[] { 0xE2, 0x26, 0xE2, 0x24 })]
    [InlineData(1024, new byte[] { 0x24, 0xE2, 0x26, 0xE2 })]
    public void Composite_keeps_physical_lifo_order_and_logical_inner_command_depth_after_json(int depth, byte[] opcodes)
    {
        ImmutableArray<NativeBattleModuleProjectedVertex> corners = Corners.Select(vertex => vertex with { Depth = (ushort)depth }).ToImmutableArray();
        NativeBattleGfGeometryBatch batch = NativeBattleBlizzardFacePackets.EmitFace61B9A0(Primary, Partner, corners, 1, 1, Context);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(128, Enumerable.Repeat((byte)0xA5, 80).ToArray()) };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 128, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(200, written.Cursor);
        Assert.Equal(new[] { 128, 160 }, written.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset));
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5 }, written.Memory.Arena!.Read(200, 8));
        Assert.Equal((byte)7, written.Memory.Arena.Read(131, 1)[0]);
        Assert.Equal((byte)9, written.Memory.Arena.Read(163, 1)[0]);
        byte[] lengths = new byte[256];
        lengths[0x24] = 7;
        lengths[0x26] = 7;
        lengths[0xE2] = 1;
        ImmutableArray<NativeBattleLogicalCommand> commands = written.Ordering.Traverse(lengths.ToImmutableArray());
        Assert.Equal(opcodes, commands.Select(command => command.Opcode));
        Assert.All(commands.Where(command => command.Packet.ByteOffset == 160), command => Assert.Equal(new int[4], command.VertexPayload4.ToArray()));
        NativeBattleLogicalCommand primary = Assert.Single(commands, command => command.Packet.ByteOffset == 128);
        Assert.Equal(new[] { depth, depth, depth, 0 }, primary.VertexPayload4.ToArray());
        NativeBattleEffectPacketWrite restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectPacketWrite>(NativeBattleTestSnapshots.Json(written))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(commands), NativeBattleTestSnapshots.Json(restored.Ordering.Traverse(lengths.ToImmutableArray())));
    }

    [Fact]
    public void Rejected_prefix_reuses_the_cursor_and_does_not_erase_unwritten_screen_words()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(128, Enumerable.Repeat((byte)0xA5, 40).ToArray()) };
        ImmutableArray<NativeBattleModuleProjectedVertex> corners = Corners.SetItem(0, Corners[0] with { Flags = 0x40000 });
        NativeBattleGfGeometryBatch rejected = NativeBattleBlizzardFacePackets.EmitFace61B9A0(Primary, default, corners, 1, 1, Context);
        NativeBattleEffectPacketWrite first = NativeBattleEffectPacketArena.Append(memory, 128, NativeBattleLogicalOrdering.Clear(), rejected);
        Assert.Equal(128, first.Cursor);
        Assert.Empty(first.Ordering.Proxies);
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, first.Memory.Arena!.Read(136, 4));
        Assert.Equal(0x3DE71211u, BinaryPrimitives.ReadUInt32LittleEndian(first.Memory.Arena.Read(140, 4)));
        NativeBattleEffectPacketWrite accepted = NativeBattleEffectPacketArena.Append(first.Memory, first.Cursor, first.Ordering, Emit(0, 1));
        Assert.Equal(160, accepted.Cursor);
        Assert.Equal(Screen(100, 100), BinaryPrimitives.ReadUInt32LittleEndian(accepted.Memory.Arena!.Read(136, 4)));
        Assert.Single(accepted.Ordering.Proxies);
    }

    private static NativeBattleGfGeometryBatch Emit(short age, short duration, NativeBattleBlizzardFacePacketContext? context = null, NativeBattleSummonColors? colors = null) => NativeBattleBlizzardFacePackets.EmitFace61B9A0(Primary, Partner, Corners, age, duration, context ?? Context, colors);
    private static ImmutableArray<byte> Primary => Face(0xDEADBEEF, 0x3DE71211, 0x003D3433, 0x7A79);
    private static ImmutableArray<byte> Partner => Face(0x10203040, 0x12340A09, 0x01421413, 0x1E1D);

    private static ImmutableArray<byte> Face(uint color, uint uv0, uint uv1, ushort uv2)
    {
        byte[] face = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, color);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), uv2);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), uv0);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), uv1);
        return face.ToImmutableArray();
    }

    private static uint Screen(int x, int y) => unchecked((ushort)x) | unchecked((uint)y << 16);
}
