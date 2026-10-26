using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePcUiImmediateTests
{
    private static readonly NativeBattlePcUiRules Rules = new(0.001f, BitConverter.Int32BitsToSingle(0x37874CD5), 0.5f, 256);
    private static readonly NativeBattlePcUiProfile Device = new(640, 480, 2, 2, 0, 16);
    private static readonly NativeBattlePcUiIconProfile Ordinary = new(1, 1, false, 256, 224, 256, 256, 1, 1);
    private static readonly NativeBattlePcUiIconProfile HighResolution = new(2, 2, true, 256, 224, 256, 256, 2, 2);

    [Fact]
    public void String_groups_all_parts_at_one_depth_and_advances_once_per_string()
    {
        NativeBattlePcUiImmediateState state = Fresh();
        state = state.DrawString4A93D0([0x18, 0x19, 0], 0, 0xFFFFFF, 256, Encode, Ordinary, Ordinary, Rules);
        Assert.Equal(2, state.Opaque.Length);
        Assert.All(state.Opaque, quad => Assert.All(quad.Vertices, vertex => Assert.Equal(Rules.InitialDepth, vertex.Z)));
        Assert.False(state.Grouped);
        Assert.Equal((float)((double)Rules.InitialDepth + Rules.DepthStep), state.Depth);
        Assert.Equal(48, state.FormattedPackets.Length);
        Assert.Equal(15.5f, state.Opaque[0].Vertices[0].Y);
        Assert.Equal(-0.5f, state.Opaque[0].Vertices[0].X);
        Assert.Equal(15.5f, state.Opaque[1].Vertices[0].X);
    }

    [Fact]
    public void Frame_reset_keeps_the_actual_mid_group_toggle()
    {
        NativeBattlePcUiImmediateState state = Fresh().Toggle49B080(Rules);
        NativeBattlePcUiImmediateState restored = JsonSerializer.Deserialize<NativeBattlePcUiImmediateState>(JsonSerializer.Serialize(state))!;
        restored = restored.BeginFrame49B120(Rules);
        Assert.True(restored.Grouped);
        Assert.Equal(Rules.InitialDepth, restored.Depth);
        restored = Submit(restored, false);
        Assert.Equal(Rules.InitialDepth, restored.Depth);
    }

    [Fact]
    public void Suppressed_backend_does_not_consume_depth_or_create_geometry()
    {
        NativeBattlePcUiImmediateState state = Fresh() with { Suppressed = true };
        NativeBattlePcUiImmediateState next = Submit(state, false);
        Assert.Same(state, next);
    }

    [Fact]
    public void Opaque_and_semi_have_separate_batches_and_viewport_epochs()
    {
        NativeBattlePcUiImmediateState state = Submit(Submit(Fresh(), false), true);
        state = state.SetViewport49D590(new(10, 20, 100, 120));
        state = Submit(state, true);
        (state, NativeBattlePcUiFrame frame) = state.Publish499EA0();
        Assert.Single(frame.Opaque);
        Assert.Equal(2, frame.Semi.Length);
        Assert.Equal(new NativeBattlePcUiViewport(0, 0, 640, 480), frame.Semi[0].Viewport);
        Assert.Equal(new NativeBattlePcUiViewport(10, 20, 100, 120), frame.Semi[1].Viewport);
        Assert.Equal(0, frame.Semi[0].Epoch);
        Assert.Equal(1, frame.Semi[1].Epoch);
        Assert.Empty(state.Opaque);
        Assert.Empty(state.CurrentSemi);
        Assert.Empty(state.ClosedSemi);
    }

    [Theory]
    [InlineData(126, 0, 1, 0)]
    [InlineData(128, 0, 1, 1)]
    [InlineData(126, 128, 1, 2)]
    [InlineData(128, 128, 1, 3)]
    [InlineData(127, 127, 4, 0)]
    public void Opaque_selected_texture_pages_follow_scaled_UV_crossings(int u, int v, int count, int firstPage)
    {
        byte[] packet = Packet(false, (byte)u, (byte)v, 2, 2);
        NativeBattlePcUiImmediateState state = NativeBattlePcUiIconDrawing.Submit49BB30(Fresh(), packet, HighResolution, Ordinary, Rules);
        Assert.Equal(count, state.Opaque.Length);
        Assert.Equal(firstPage, state.Opaque[0].Page);
        Assert.All(state.Opaque, quad => Assert.All(quad.Vertices, vertex => Assert.InRange(vertex.U, 0, 1)));
        Assert.All(state.Opaque, quad => Assert.All(quad.Vertices, vertex => Assert.InRange(vertex.V, 0, 1)));
    }

    [Fact]
    public void Semi_endpoint_adds_retained_translation_a_second_time()
    {
        NativeBattlePcUiImmediateState state = Fresh() with { TranslationX = 3, TranslationY = 5 };
        NativeBattlePcUiQuad opaque = Submit(state, false).Opaque[0];
        NativeBattlePcUiQuad semi = Submit(state, true).CurrentSemi[0];
        Assert.Equal(opaque.Vertices[0].X, semi.Vertices[0].X);
        Assert.Equal(opaque.Vertices[0].Y, semi.Vertices[0].Y);
        Assert.Equal(opaque.Vertices[3].X + 3, semi.Vertices[3].X);
        Assert.Equal(opaque.Vertices[3].Y + 5, semi.Vertices[3].Y);
    }

    [Fact]
    public void Signed_CLUT_origin_mismatch_keeps_the_additional_sixteen_palettes()
    {
        byte[] packet = Packet(false);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(18), 0x3811);
        NativeBattlePcUiQuad quad = NativeBattlePcUiIconDrawing.Submit49BB30(Fresh(), packet, Ordinary, Ordinary, Rules).Opaque[0];
        Assert.Equal(16, quad.Palette);
        Assert.Equal(0x7FFFFFFFu, quad.DiffuseArgb);
    }

    [Fact]
    public void Unknown_retained_inputs_remain_unsupported()
    {
        Assert.Throws<NotSupportedException>(() => Submit(NativeBattlePcUiImmediateState.FromColdProcess(), false));
        Assert.Throws<NotSupportedException>(() => Submit(Fresh() with { Grouped = null }, false));
        Assert.Throws<NotSupportedException>(() => Submit(Fresh() with { Suppressed = null }, false));
    }

    private static NativeBattlePcUiImmediateState Fresh() => NativeBattlePcUiImmediateState.FromColdProcess().Initialize4972A0(Device).BeginFrame49B120(Rules);

    private static NativeBattlePcUiImmediateState Submit(NativeBattlePcUiImmediateState state, bool semi) => NativeBattlePcUiIconDrawing.Submit49BB30(state, Packet(semi), Ordinary, Ordinary, Rules);

    private static ImmutableArray<byte> Encode(byte glyph, int x, int y, uint color)
    {
        return NativeMenuIconPackets.Encode4B76A0([0, 0, 0, 0, 8, 0, 8, 0], x, y, color, 0);
    }

    private static byte[] Packet(bool semi, byte u = 0, byte v = 0, byte width = 8, byte height = 8)
    {
        byte[] packet = Encode(0x18, 0, 0, semi ? 0x04FFFFFFu : 0xFFFFFFu).ToArray();
        packet[16] = u;
        packet[17] = v;
        packet[20] = width;
        packet[22] = height;
        return packet;
    }
}
