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
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void Culled_neighbor_keeps_its_shared_vertices_on_the_same_interpolated_model_pose(bool compact, bool separateMeshes, bool entirelyCulled)
    {
        NativeBattleRawMesh mesh = new([new(new(0, 0, 100), 0), new(new(10, 0, 100), 0), new(new(0, 10, 100), 0), new(new(10, 10, 100), 0)], [new(false, [0, 0, 1, 0, 2, 0]), new(false, [1, 0, 3, 0, 2, 0])]);
        NativeBattlePreparedPresentation earlier = Prepare(1, 0, 2);
        NativeBattlePreparedPresentation later = Prepare(2, 20, entirelyCulled ? 0 : 1);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        renderer.RenderNativePilotPrimitiveFrame(earlier, later, 0.5f);
        Assert.Equal(6, capture.Vertices.Count);
        Assert.Equal(entirelyCulled ? 170 : 180, capture.Vertices[1].Position.X, 3);
        Assert.Equal(capture.Vertices[1].Position, capture.Vertices[3].Position);
        Assert.Equal(capture.Vertices[2].Position, capture.Vertices[5].Position);
        renderer.RenderNativePrimitiveFrame(earlier.Primitives, null);
        TriangleVertex[] first = capture.Vertices.ToArray();
        renderer.RenderNativePilotPrimitiveFrame(earlier, later, 0);
        Assert.Equal(first, capture.Vertices.ToArray());
        renderer.RenderNativePrimitiveFrame(later.Primitives, null);
        TriangleVertex[] last = capture.Vertices.ToArray();
        renderer.RenderNativePilotPrimitiveFrame(earlier, later, 1);
        Assert.Equal(last, capture.Vertices.ToArray());
        Assert.Equal(entirelyCulled ? 0 : 3, last.Length);

        NativeBattlePreparedPresentation Prepare(long callback, short x, int admitted)
        {
            NativeBattlePreparedPresentation frame = Frame(callback, x, false, false, false, false);
            NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = x };
            NativeBattlePilotVisualIdentity identity = new(2, 0x502D40, 11, 3, 0);
            ImmutableArray<NativeBattleRawMesh> meshes = separateMeshes ? [mesh with { Polygons = [mesh.Polygons[0]] }, mesh with { Polygons = [mesh.Polygons[1]] }] : [mesh];
            NativeBattlePilotModelPose pose = NativeBattlePilotModelPose.Capture(meshes, [matrix], frame.PilotProjection!, identity, 1, false, true);
            NativeBattlePilotGeometryReference[] polygons = pose.Geometry().ToArray();
            List<NativeBattleLogicalResolvedCommand> commands = [];
            NativeBattleModelPrimitiveBatch.Builder builder = new();
            for (int index = 0; index < admitted; index++)
            {
                NativeBattlePilotGeometryReference geometry = polygons[index];
                int emission = builder.BeginEmission(new(null, null, null, geometry.Identity, true, false) { ModelPose = pose });
                NativeBattlePilotGeometryReference pilot = compact ? builder.AddPilot(new(geometry.Corner(0), geometry.Corner(1), geometry.Corner(2), default, geometry.Identity.Primitive, emission, 3)) : geometry.ToGeometry()! with { ModelPose = pose };
                NativeBattleLogicalResolvedCommand original = frame.Primitives.LastCommands[0];
                uint Coordinate(int corner) => Point((int)(160 + geometry.Corner(corner).X), (int)(108 + geometry.Corner(corner).Y));
                NativeBattleLogicalCommand command = original.Command.ToCommand() with { Pilot = pilot, Words = [0x200000FF, Coordinate(0), Coordinate(1), Coordinate(2)] };
                commands.Add(original with { Command = command });
            }
            builder.Freeze();
            return frame with { Primitives = frame.Primitives with { LastCommands = NativeBattleResolvedCommandList.From(commands) } };
        }
    }

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
