using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleCompactPilotPresentationTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Compact_model_pages_match_rich_geometry_vertices_depth_and_submission_digests(bool screenSpace, bool discrete, bool replacement)
    {
        NativeBattlePreparedPresentation richFirst = Frame(1, 0, false, screenSpace, discrete, false);
        NativeBattlePreparedPresentation richNext = Frame(2, 10, false, screenSpace, discrete, replacement);
        NativeBattlePreparedPresentation compactFirst = Frame(1, 0, true, screenSpace, discrete, false);
        NativeBattlePreparedPresentation compactNext = Frame(2, 10, true, screenSpace, discrete, replacement);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture, PilotDiagnosticsEnabled = true };
        foreach (float alpha in new[] { 0f, 0.5f, 1f })
        {
            renderer.ObservePilotSource(richFirst, richNext, alpha, true);
            renderer.RenderNativePilotPrimitiveFrame(richFirst, richNext, alpha);
            TriangleVertex[] expected = capture.Vertices.ToArray();
            NativeBattlePilotSubmission digest = renderer.PilotSubmission;
            Assert.NotEmpty(expected);
            renderer.ObservePilotSource(compactFirst, compactNext, alpha, true);
            renderer.RenderNativePilotPrimitiveFrame(compactFirst, compactNext, alpha);
            Assert.Equal(expected, capture.Vertices.ToArray());
            Assert.Equal(digest, renderer.PilotSubmission);
            Assert.Equal(0, renderer.LastPilotFallbackCount);
        }
        Assert.All(compactFirst.Primitives.LastCommands, resolved => Assert.True(resolved.Command.Pilot.IsModel));
    }

    [Fact]
    public void Renderer_and_diagnostics_read_compact_pilot_references_without_compatibility_materialization()
    {
        NativeBattlePreparedPresentation richFirst = Frame(1, 0, false, false, false, false);
        NativeBattlePreparedPresentation richNext = Frame(2, 10, false, false, false, false);
        NativeBattlePreparedPresentation compactFirst = Frame(1, 0, true, false, false, false);
        NativeBattlePreparedPresentation compactNext = Frame(2, 10, true, false, false, false);
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = new Capture(), PilotDiagnosticsEnabled = true };
        Func<byte[]> rich = () => renderer.RenderNativePilotPrimitiveFrame(richFirst, richNext, 0.5f);
        Func<byte[]> compact = () => renderer.RenderNativePilotPrimitiveFrame(compactFirst, compactNext, 0.5f);
        for (int index = 0; index < 3; index++)
        {
            GC.KeepAlive(rich());
            GC.KeepAlive(compact());
        }
        long richBytes = Measure(rich);
        long compactBytes = Measure(compact);
        Assert.True(compactBytes <= richBytes + 1024, $"Compact rendering allocated {compactBytes} bytes; rich rendering allocated {richBytes} bytes. Compatibility geometry must not be materialized per submitted polygon.");
        Assert.Equal(64 * 3, renderer.PilotSubmission.CoveredContinuousCorners);
    }

    private static long Measure(Func<byte[]> render)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        byte[] pixels = render();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(pixels);
        return bytes;
    }

    private static NativeBattlePreparedPresentation Frame(long callback, float x, bool compact, bool screenSpace, bool discrete, bool replacement)
    {
        NativeBattlePilotVisualIdentity identity = new(2, 0x502D40, 11, 3, 0) { ResourceGeneration = replacement ? 8 : 7 };
        Vector3 a = screenSpace ? new(160 + x, 108, 100) : new(x, 0, 100);
        Vector3 b = a + new Vector3(10, 0, 0);
        Vector3 c = a + new Vector3(0, 10, 0);
        NativeBattlePilotGeometryReference pilot;
        if (compact)
        {
            NativeBattleModelPrimitiveBatch.Builder builder = new();
            int emission = builder.BeginEmission(new(null, null, null, identity, !discrete, screenSpace));
            pilot = builder.AddPilot(new(a, b, c, default, identity.Primitive, emission, 3));
            builder.Freeze();
        }
        else
        {
            pilot = new NativeBattlePilotVisualGeometry(identity, a, b, c, default, 3, !discrete) { ScreenSpace = screenSpace };
        }
        NativeBattleLogicalCommand command = new(new("pilot", 0, 0), 1, 0x20, [0x200000FF, Point((int)(160 + x), 108), Point((int)(170 + x), 108), Point((int)(160 + x), 118)], 0, [100, 100, 100, 0], null)
        {
            OrderingBucket = 50,
            Pilot = pilot
        };
        NativeBattlePreparedPrimitiveFrame primitives = new(NativeBattleResolvedCommandList.From(Enumerable.Repeat(new NativeBattleLogicalResolvedCommand(command, new(0), 1), 64)), [])
        {
            LastDescriptorPerspective = true,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        return new(callback, callback, callback, false, primitives, null, null, null, new(new(0, 0, 0), new(0, 0, 100), 100, 0), [], null, null, [], null)
        {
            PilotEffect = 2,
            PilotGeneration = 1,
            PilotCamera = new(new(0, 0, 0), new(0, 0, 100), 100, 0),
            PilotProjection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108)
        };
    }

    private static uint Point(int x, int y)
    {
        return unchecked((ushort)(x * 8) | ((uint)(ushort)(y * 8) << 16));
    }

    private sealed class Capture : ITriangleRenderer
    {
        internal List<TriangleVertex> Vertices { get; } = new(256);

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
