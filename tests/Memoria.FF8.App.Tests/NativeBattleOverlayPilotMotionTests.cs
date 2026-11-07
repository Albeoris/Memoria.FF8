using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleOverlayPilotMotionTests
{
    [Theory]
    [InlineData((ushort)4, false)]
    [InlineData((ushort)4, true)]
    [InlineData((ushort)21, false)]
    [InlineData((ushort)21, true)]
    public void Original_overlay_packets_keep_native_anchor_and_extent_at_fractional_motion(ushort effect, bool normalViewport)
    {
        NativeBattleModulePoolTask task = Task(effect);
        string checkpoint = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(task));
        NativeBattleModulePoolTask cold = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(checkpoint)!);
        NativeBattlePreparedPresentation first = Frame(effect, task, 1, normalViewport);
        NativeBattlePreparedPresentation restored = Frame(effect, cold, 1, normalViewport);
        NativeBattlePilotVisualGeometry originalGeometry = first.Primitives.LastCommands[0].Command.PilotGeometry!;
        NativeBattlePilotVisualGeometry coldGeometry = restored.Primitives.LastCommands[0].Command.PilotGeometry!;
        Assert.Equal(originalGeometry.Identity, coldGeometry.Identity);
        Assert.Equal(originalGeometry.A, coldGeometry.A);
        Assert.Equal(originalGeometry.ScreenOffsets.ToArray(), coldGeometry.ScreenOffsets.ToArray());
        NativeBattleModulePoolTask nextTask = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(task));
        NativeBattlePreparedPresentation next = Frame(effect, nextTask.WriteWord(0x1C, 125), 2, normalViewport);
        Capture capture = new();
        BattleRenderer renderer = new(normalViewport ? 640 : 320, normalViewport ? 432 : 216) { TriangleRenderer = capture, PilotDiagnosticsEnabled = true };
        float scale = normalViewport ? 2 : 1;
        float offsetX = normalViewport ? 3 : 0;
        float offsetY = normalViewport ? 5 : 0;
        foreach (float alpha in new[] { 0f, 0.5f, 1f })
        {
            NativeBattlePreparedPresentation endpoint = alpha == 1 ? next : first;
            renderer.RenderNativePrimitiveFrame(endpoint.Primitives, endpoint.Textures);
            TriangleVertex[] native = capture.Vertices.ToArray();
            renderer.RenderNativePilotPrimitiveFrame(first, next, alpha);
            Assert.NotEmpty(capture.Vertices);
            Assert.Equal((140 + 27 * alpha) * scale + offsetX, capture.Vertices.Min(vertex => vertex.Position.X), 3);
            Assert.Equal((180 + 27 * alpha) * scale + offsetX, capture.Vertices.Max(vertex => vertex.Position.X), 3);
            Assert.Equal(92 * scale + offsetY, capture.Vertices.Min(vertex => vertex.Position.Y), 3);
            Assert.Equal(108 * scale + offsetY, capture.Vertices.Max(vertex => vertex.Position.Y), 3);
            if (alpha is 0 or 1)
            {
                Assert.Equal(native, capture.Vertices.ToArray());
            }
            else
            {
                Assert.Equal(native[0].Color, capture.Vertices[0].Color);
                Assert.Equal(native[0].Uv, capture.Vertices[0].Uv);
                renderer.RenderNativePilotPrimitiveFrame(restored, next, alpha);
                Assert.Equal((140 + 27 * alpha) * scale + offsetX, capture.Vertices.Min(vertex => vertex.Position.X), 3);
            }
        }
        NativeBattleLogicalResolvedCommand changed = next.Primitives.LastCommands[0];
        changed = changed with { Command = changed.Command with { PilotGeometry = changed.Command.PilotGeometry! with { Identity = changed.Command.PilotGeometry!.Identity with { Generation = 2 } } } };
        NativeBattlePreparedPresentation reused = next with { Primitives = next.Primitives with { LastCommands = [changed] } };
        renderer.RenderNativePilotPrimitiveFrame(first, reused, 0.5f);
        Assert.Equal(140 * scale + offsetX, capture.Vertices.Min(vertex => vertex.Position.X), 3);
        Assert.Equal((ushort)0, task.ReadWord(0x1C));
        Assert.Equal(checkpoint, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(task)));
    }

    private static NativeBattlePreparedPresentation Frame(ushort effect, NativeBattleModulePoolTask task, long callback, bool normalViewport)
    {
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096), 216, 160, 108);
        NativeBattlePilotVisualIdentity identity = new(effect, task.Callback, 0, 1, 0);
        NativeBattleGfGeometryBatch Emit(NativeBattlePilotVisualIdentity? visual) => effect == 4 ? NativeBattleEffectOverlayPackets.Emit8D6790(task, projection, pilotIdentity: visual) : NativeBattleEffectOverlayPackets.Emit89F950(task, projection, pilotIdentity: visual);
        NativeBattleGfGeometryBatch batch = Emit(identity);
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
        NativeBattleGeometryPrimitive raw = Assert.Single(Emit(null).Attempts).Primitive!;
        Assert.Equal(raw.Words.ToArray(), primitive.Words.ToArray());
        Assert.Equal(raw.PhysicalWriteMask.HasValue, primitive.PhysicalWriteMask.HasValue);
        if (raw.PhysicalWriteMask is { } rawMask && primitive.PhysicalWriteMask is { } presentedMask)
        {
            Assert.Equal(rawMask.ToArray(), presentedMask.ToArray());
        }
        Assert.Equal(raw.VertexPayload4.ToArray(), primitive.VertexPayload4.ToArray());
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeBattleEffectMemory.FromColdProcess().BeginLoad(effect), 0x6000, NativeBattleLogicalOrdering.Clear(), batch, flags20: 1);
        ushort flags = Assert.Single(written.Ordering.Proxies).Flags20;
        Assert.Equal((ushort)1, flags);
        ushort page = (ushort)(primitive.Words[5] >> 16);
        ushort clut = (ushort)(primitive.Words[3] >> 16);
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(page, clut).Prepare464BD0().Cache;
        NativeBattlePrimitiveDrawMode mode = new(page) { TextureWindowBits = 0, TextureSelection = cache.Resolve465CE0(page, clut, 0).Selection };
        if (normalViewport)
        {
            NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(new(0, 0, 319, 215, 0, 0, 0, 0, 0, null));
            NativeSoftwareViewportInputs inputs = new(640, 432, 3, 5);
            mode = mode with { NormalViewport = NativeSoftwareNormalViewport.Calculate45B6F0(environment, inputs), ViewportInputs = inputs };
        }
        NativeBattleLogicalCommand command = new(new("overlay", 0, 0), 1, 0x2F, primitive.Words.RemoveAt(0), flags, primitive.VertexPayload4, null) { OrderingBucket = 0, PilotGeometry = primitive.PilotGeometry };
        NativeBattlePreparedPrimitiveFrame frame = new([new(command, mode, 2)], []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };
        NativeBattleCameraPose camera = new(new(0, 0, -1000), new(0, 0, 0), 216, 0);
        return new(callback, callback, callback, false, frame, NativeBattleEffectTextureState.FromColdProcess(), cache, null, camera, [], null, null, [], null) { PilotEffect = effect, PilotGeneration = 1, PilotProjection = projection, PilotCamera = camera };
    }

    private static NativeBattleModulePoolTask Task(ushort effect)
    {
        return new NativeBattleModulePoolTask(1, 0, null, effect == 4 ? 0x8D6790u : 0x89F950u, new byte[68].ToImmutableArray()).WriteWord(0x40, 0x240).WriteWord(0x42, 0x120).WriteWord(0x44, 0x140).WriteWord(0x46, 0xF4).WriteWord(0x48, 0).WriteWord(0x4A, 0x20).WriteWord(0x4C, 40).WriteWord(0x4E, 16);
    }

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
