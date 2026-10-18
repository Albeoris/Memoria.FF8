using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePcRenderRequestTests
{
    [Fact]
    public void Request_stub_overwrites_pending_DWORD_and_does_not_interpret_ignored_color_arguments()
    {
        NativeBattlePcRenderRequest cold = NativeBattlePcRenderRequest.FromColdProcess();
        Assert.Equal(0u, cold.Requested);
        Assert.Null(cold.PublishedTail);
        NativeBattlePcRenderRequest first = cold.Request47CF50(0, 255, 255, 255, 63);
        NativeBattlePcRenderRequest latest = first.Request47CF50(0, -1, 2, 3, int.MaxValue);
        Assert.Equal(1u, latest.Requested);
        Assert.Null(latest.PublishedTail);
        Assert.Equal(0u, cold.Requested);
        Assert.Equal(2u, latest.Request47CF50(1, 0, 0, 0, 0).Requested);
        Assert.Equal(0u, latest.Request47CF50(-1, 0, 0, 0, 0).Requested);
        Assert.Equal(0x80000000u, latest.Request47CF50(int.MaxValue, 0, 0, 0, 0).Requested);
        Assert.Equal(0x80000001u, latest.Request47CF50(int.MinValue, 0, 0, 0, 0).Requested);
    }

    [Fact]
    public void Pause_DE9_retains_request_and_publishes_no_tail_without_reading_viewport_or_calling_service()
    {
        NativeBattlePcRenderTailReceipt previous = new(2, new(10, 20, 640, 480));
        NativeBattlePcRenderRequest request = new(1, previous);
        NativeBattlePcRenderRequest paused = request.Tail47CF60(true, null, _ => throw new InvalidOperationException());
        Assert.Equal(1u, paused.Requested);
        Assert.Null(paused.PublishedTail);
        NativeBattlePcRenderRequest empty = new NativeBattlePcRenderRequest(0, previous).Tail47CF60(false, null, null);
        Assert.Equal(0u, empty.Requested);
        Assert.Null(empty.PublishedTail);
        Assert.Same(previous, request.PublishedTail);
    }

    [Fact]
    public void Active_tail_calls_exact_current_viewport_then_clears_request_even_when_texture_allocation_returns_early()
    {
        NativeBattlePcRenderRequest request = NativeBattlePcRenderRequest.FromColdProcess().Request47CF50(0, 1, 2, 3, 4);
        NativeBattlePcRenderViewport viewport = new(17, -29, 641, 479);
        List<NativeBattlePcRenderTailReceipt> calls = [];
        NativeBattlePcRenderRequest completed = request.Tail47CF60(false, viewport, receipt =>
        {
            Assert.Equal(1u, request.Requested);
            Assert.Same(viewport, receipt.Viewport);
            Assert.Equal(0x3AC49BA6u, receipt.DepthBits);
            Assert.Equal(0x55FFFFFFu, receipt.ColorArgb);
            Assert.Equal(0u, receipt.ExpandArgument);
            calls.Add(receipt);
            //The native resource-allocation failure path returns normally without capture or draw.
        });
        Assert.Same(Assert.Single(calls), completed.PublishedTail);
        Assert.Equal(0u, completed.Requested);
        Assert.Equal(1u, request.Requested);
        NativeBattlePcRenderRequest next = completed.Tail47CF60(false, null, _ => throw new InvalidOperationException());
        Assert.Null(next.PublishedTail);
        Assert.Equal(0u, next.Requested);
    }

    [Theory]
    [InlineData(1u, 0, 16, -30, 656, 448)]
    [InlineData(2u, 1, 0, -46, 672, 464)]
    [InlineData(3u, 0, 16, -30, 656, 448)]
    public void Textured_quad_uses_exact_native_vertex_order_depth_UV_and_request_two_expansion(uint requested, uint expand, int left, int top, int right, int bottom)
    {
        NativeBattlePcRenderTailReceipt receipt = new(requested, new(17, -29, 641, 479));
        NativeBattlePcRenderVertex[] vertices = receipt.QuadVertices56D5F0().ToArray();
        Assert.Equal(expand, receipt.ExpandArgument);
        Assert.Equal(new float[] { left, right, left, right }, vertices.Select(vertex => vertex.X));
        Assert.Equal(new float[] { top, top, bottom, bottom }, vertices.Select(vertex => vertex.Y));
        Assert.Equal(new float[] { 0, 1, 0, 1 }, vertices.Select(vertex => vertex.U));
        Assert.Equal(new float[] { 0, 0, 1, 1 }, vertices.Select(vertex => vertex.V));
        Assert.All(vertices, vertex =>
        {
            Assert.Equal(0x3AC49BA6, BitConverter.SingleToInt32Bits(vertex.Z));
            Assert.Equal(1f, vertex.ReciprocalW);
            Assert.Equal(0x55FFFFFFu, vertex.ColorArgb);
        });
    }

    [Fact]
    public void Quad_coordinate_arithmetic_wraps_as_signed_DWORD_before_single_float_conversion()
    {
        NativeBattlePcRenderTailReceipt receipt = new(2, new(int.MinValue, int.MaxValue, -1, 3));
        NativeBattlePcRenderVertex[] vertices = receipt.QuadVertices56D5F0().ToArray();
        int left = unchecked(int.MinValue - 1 - 16);
        int top = unchecked(int.MaxValue - 1 - 16);
        int right = unchecked(int.MinValue - 1 - 2 + 16);
        int bottom = unchecked(int.MaxValue + 3 - 2 + 16);
        Assert.Equal(new float[] { left, right, left, right }, vertices.Select(vertex => vertex.X));
        Assert.Equal(new float[] { top, top, bottom, bottom }, vertices.Select(vertex => vertex.Y));
        NativeBattlePcRenderTailReceipt rounding = new(1, new(16777218, 1, 3, 2));
        Assert.Equal(16777216f, rounding.QuadVertices56D5F0()[0].X);
        Assert.Equal(16777220f, rounding.QuadVertices56D5F0()[1].X);
    }

    [Fact]
    public void JSON_preserves_pending_request_separately_from_current_published_call_and_has_no_default_hydration()
    {
        NativeBattlePcRenderRequest first = NativeBattlePcRenderRequest.FromColdProcess().Request47CF50(0, 0, 0, 0, 0).Tail47CF60(false, new(4, 5, 6, 7), _ => { });
        NativeBattlePcRenderRequest pending = first.Request47CF50(1, 1, 2, 3, 4);
        Assert.Equal(2u, pending.Requested);
        Assert.Equal(1u, pending.PublishedTail!.Requested);
        NativeBattlePcRenderRequest restored = JsonSerializer.Deserialize<NativeBattlePcRenderRequest>(JsonSerializer.Serialize(pending))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(pending), JsonSerializer.Serialize(restored));
        NativeBattlePcRenderRequest expected = pending.Tail47CF60(false, new(10, 20, 30, 40), _ => { });
        NativeBattlePcRenderRequest actual = restored.Tail47CF60(false, new(10, 20, 30, 40), _ => { });
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        JsonObject missing = JsonNode.Parse(JsonSerializer.Serialize(pending))!.AsObject();
        missing.Remove(nameof(NativeBattlePcRenderRequest.Requested));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattlePcRenderRequest>(missing.ToJsonString()));
        Assert.Throws<NotSupportedException>(() => pending.Tail47CF60(false, null, _ => { }));
        Assert.Throws<NotSupportedException>(() => pending.Tail47CF60(false, new(1, 2, 3, 4), null));
        Assert.Throws<InvalidDataException>(() => new NativeBattlePcRenderTailReceipt(0, new(1, 2, 3, 4)).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeBattlePcRenderTailReceipt(1, null!).Validate());
    }
}
