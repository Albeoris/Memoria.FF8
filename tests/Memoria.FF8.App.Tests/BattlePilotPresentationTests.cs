using System.Numerics;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattlePilotPresentationTests
{
    [Fact]
    public void Timeline_retains_one_completed_interval_and_resets_at_gaps_modes_and_spell_generations()
    {
        BattlePilotPresentationTimeline timeline = new();
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattlePreparedPresentation second = Frame(2, 10);
        Assert.Null(timeline.Select(first, true, 0).Next);
        var sample = timeline.Select(second, true, 0.25f);
        Assert.Same(first, sample.Frame);
        Assert.Same(second, sample.Next);
        Assert.Equal(0.25f, sample.Alpha);
        Assert.Same(first, timeline.Select(second, true, 0.75f).Frame);
        Assert.Null(timeline.Select(second with { Callback = 4 }, true, 0.5f).Next);
        Assert.Null(timeline.Select(second with { Callback = 5, PilotGeneration = 2 }, true, 0.5f).Next);
        Assert.Null(timeline.Select(second with { Callback = 6, PilotGeneration = 2, PilotEffect = 105 }, true, 0.5f).Next);
        Assert.Null(timeline.Select(second, false, 0.5f).Next);
        Assert.Null(timeline.Select(second, true, 0.5f).Next);
    }

    [Theory]
    [InlineData((ushort)2)]
    [InlineData((ushort)3)]
    [InlineData((ushort)144)]
    public void Renderer_samples_world_geometry_to_fractional_pixels_and_holds_native_material_until_next_endpoint(ushort effect)
    {
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        NativeBattlePreparedPresentation first = Frame(1, 0, effect);
        NativeBattlePreparedPresentation second = Frame(2, 10, effect);
        NativeBattleLogicalResolvedCommand nextCommand = second.Primitives.LastCommands[0];
        nextCommand = nextCommand with { Command = nextCommand.Command with { Words = [0x2000FF00, Point(170, 108), Point(180, 108), Point(170, 118)] } };
        second = second with { Primitives = second.Primitives with { LastCommands = [nextCommand] } };
        renderer.RenderNativePrimitiveFrame(first.Primitives, null);
        TriangleVertex original = capture.Vertices[0];
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0.525f);
        TriangleVertex sampled = capture.Vertices[0];
        Assert.Equal(165.25f, sampled.Position.X, 3);
        Assert.Equal(108, sampled.Position.Y);
        Assert.Equal(original.Color, sampled.Color);
        Assert.Equal(original.Uv, sampled.Uv);
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0);
        Assert.Equal(original, capture.Vertices[0]);
        renderer.RenderNativePilotPrimitiveFrame(first, second, 1);
        Assert.Equal(170, capture.Vertices[0].Position.X);
        Assert.NotEqual(original.Color, capture.Vertices[0].Color);
        NativeBattleLogicalResolvedCommand heldCommand = first.Primitives.LastCommands[0];
        heldCommand = heldCommand with { Command = heldCommand.Command with { PilotGeometry = heldCommand.Command.PilotGeometry! with { Continuous = false } } };
        NativeBattlePreparedPresentation discrete = first with { Primitives = first.Primitives with { LastCommands = [heldCommand] } };
        renderer.RenderNativePilotPrimitiveFrame(discrete, second, 0.525f);
        Assert.Equal(160, capture.Vertices[0].Position.X);
        Assert.Equal(original.Color, capture.Vertices[0].Color);
    }

    private static NativeBattlePreparedPresentation Frame(long callback, float x, ushort effect = 2)
    {
        NativeBattlePilotVisualIdentity identity = new(effect, 1, 0, 1, 0);
        NativeBattleLogicalCommand command = new(new("pilot", 0, 0), 1, 0x20, [0x200000FF, Point((int)(160 + x), 108), Point((int)(170 + x), 108), Point((int)(160 + x), 118)], 0, [], null)
        {
            OrderingBucket = 50,
            PilotGeometry = new(identity, new(x, 0, 100), new(x + 10, 0, 100), new(x, 10, 100), default, 3)
        };
        NativeBattlePreparedPrimitiveFrame primitives = new([new(command, new(0), 1)], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        return new(callback, callback, callback, false, primitives, null, null, null, new(new(0, 0, 0), new(0, 0, 100), 100, 0), [], null, null, [], null)
        {
            PilotEffect = effect,
            PilotGeneration = 1,
            PilotCamera = new(new(0, 0, 0), new(0, 0, 100), 100, 0),
            PilotProjection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108)
        };
    }

    private static uint Point(int x, int y) => unchecked((ushort)(x * 8) | ((uint)(ushort)(y * 8) << 16));

    private sealed class Capture : ITriangleRenderer
    {
        internal List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Vertices.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
        }

        public void End()
        {
        }
    }
}
