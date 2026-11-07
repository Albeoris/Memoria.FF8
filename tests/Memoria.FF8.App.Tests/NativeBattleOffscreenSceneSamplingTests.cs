using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleOffscreenSceneSamplingTests(ITestOutputHelper testOutput)
{
    [Theory]
    [InlineData(0, 320, 216)]
    [InlineData(1, 320, 216)]
    [InlineData(0, 1920, 1080)]
    [InlineData(1, 1920, 1080)]
    public void Retained_scene_sample_renders_the_produced_framebuffer_instead_of_distinct_atlas_words_after_json_restore(byte buffer, int width, int height)
    {
        PixelBounds projection = ViewportLayout.Fit(320, 240, width, height);
        BattleSceneVisibleArea area = BattleSceneVisibleArea.FromViewport(width, height, projection.X, projection.Y, projection.Width, projection.Height);
        NativeBattleLogicalCommand red = new(new("source", buffer, 0), 1, 0x28, [0x280000FF, 0, 320, 216u << 16, 320u | 216u << 16], 1, [0, 0, 0, 0], null) { OrderingBucket = 17, ScreenCoverage = NativeBattleScreenCoverage.Full };
        NativeBattleLogicalCommand green = red with { Packet = new("source-edge", buffer, 0), Words = [0x2800FF00, 0, 320, 216u << 16, 320u | 216u << 16], ScreenCoverage = new(0, 0, 0.1f, 1) };
        NativeBattlePreparedPrimitiveFrame sourceFrame = Frame([new(red, new(0), 1), new(green, new(0), 1)]);
        NativeBattleSceneReplaySource source = new(sourceFrame, NativeBattleEffectTextureState.FromColdProcess(), area) { Callback = 1, Buffer = buffer };
        NativeBattleSceneCapture capture = new(area, NativeBattleSceneCaptureKind.Image) { X = 384, Y = 256, ReplaySourceId = 1 };
        NativeBattlePcTextureWindowPixels first = BlueWords(384, 256);
        NativeBattlePcTextureWindowPixels second = BlueWords(640, 64);
        NativeBattleLogicalCommand consumer = new(new("consumer", buffer, 0), 1, 0x2C, [0x2C808080, 0, 0, 320, 0x011600FF, 216u << 16, 0x0000FF00, 320u | 216u << 16, 0x0000FFFF], 1, [0, 0, 0, 0], null) { OrderingBucket = 17, SceneCapture = capture };
        NativeBattlePrimitiveDrawMode mode = new(0x116) { TextureWindowBits = 0, SceneCaptureSample = new(capture, [0, 1]) };
        NativeBattlePreparedPrimitiveFrame frame = Frame([new(consumer, mode, 1)]) with { LastTextureSnapshots = [first, second], SceneReplaySources = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty.Add(1, source) };
        NativeBattlePreparedPrimitiveFrame frozen = NativeBattleStorageSnapshots.Capture(frame);
        NativeBattlePreparedPrimitiveFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattlePreparedPrimitiveFrame>(JsonSerializer.Serialize(frozen))!);
        Capture backend = new();
        BattleRenderer renderer = new(width, height, projection) { TriangleRenderer = backend };
        renderer.RenderNativePrimitiveFrame(restored, NativeBattleEffectTextureState.FromColdProcess(), capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8());
        PixelImage image = Assert.Single(backend.Textures.Distinct());
        Assert.Equal((byte)255, image[0, 100].G);
        Assert.Equal((byte)0, image[0, 100].B);
        Assert.Equal((byte)255, image[160, 100].R);
        Assert.Equal((byte)0, image[160, 100].B);
        renderer.RenderNativePrimitiveFrame(restored, NativeBattleEffectTextureState.FromColdProcess(), capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8());
        Assert.Equal(image.Pixels, Assert.Single(backend.Textures.Distinct()).Pixels);
    }

    [Fact]
    public void Completed_display_replay_excludes_offscreen_source_receipts()
    {
        NativeBattleSceneCapture receipt = new(BattleSceneVisibleArea.Native, NativeBattleSceneCaptureKind.Source) { X = 384, Y = 256, ReplaySourceId = 1 };
        NativeBattleLogicalCommand green = new(new("visible", 0, 0), 1, 0x28, [0x2800FF00, 0, 320, 216u << 16, 320u | 216u << 16], 1, [0, 0, 0, 0], null) { OrderingBucket = 17, ScreenCoverage = NativeBattleScreenCoverage.Full };
        NativeBattleLogicalCommand red = green with { Packet = new("offscreen", 0, 0), Words = [0x280000FF, 0, 320, 216u << 16, 320u | 216u << 16], SceneCapture = receipt };
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleSceneReplaySource offscreen = new(Frame([new(red with { SceneCapture = receipt with { ReplaySourceId = null } }, new(0), 1)]), textures, BattleSceneVisibleArea.Native);
        NativeBattleSceneReplaySource completed = new(Frame([new(green, new(0), 1), new(red, new(0), 1)]), textures, BattleSceneVisibleArea.Native);
        NativeBattleSceneCapture imageCapture = receipt with { Kind = NativeBattleSceneCaptureKind.Image, ReplaySourceId = 2 };
        NativeBattleLogicalCommand consumer = new(new("consumer", 0, 0), 1, 0x2C, [0x2C808080, 0, 0, 320, 0x011600FF, 216u << 16, 0x0000FF00, 320u | 216u << 16, 0x0000FFFF], 1, [0, 0, 0, 0], null) { OrderingBucket = 17, SceneCapture = imageCapture };
        NativeBattlePrimitiveDrawMode mode = new(0x116) { TextureWindowBits = 0, SceneCaptureSample = new(imageCapture, [0, 1]) };
        NativeBattlePreparedPrimitiveFrame frame = Frame([new(consumer, mode, 1)]) with { LastTextureSnapshots = [BlueWords(384, 256), BlueWords(640, 64)], SceneReplaySources = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty.Add(1, offscreen).Add(2, completed) };
        Capture backend = new();
        new BattleRenderer(320, 216, new PixelBounds(0, 0, 320, 216)) { TriangleRenderer = backend }.RenderNativePrimitiveFrame(frame, textures, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8());
        PixelImage sampled = Assert.Single(backend.Textures.Distinct());
        Assert.All(sampled.Pixels, pixel =>
        {
            Assert.Equal((byte)0, pixel.R);
            Assert.Equal((byte)255, pixel.G);
            Assert.Equal((byte)0, pixel.B);
        });
    }

    [Theory]
    [InlineData(184, 0, true)]
    [InlineData(184, 1, true)]
    [InlineData(154, 0, true)]
    [InlineData(154, 1, true)]
    [InlineData(184, 0, false)]
    [InlineData(184, 1, false)]
    [InlineData(154, 0, false)]
    [InlineData(154, 1, false)]
    public void Original_resource_warp_grid_retains_selected_stage_source_sampling_and_cold_next_callback(ushort effect, byte buffer, bool scoped)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? outputRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCENE_COPY_OUTPUT");
        Assert.SkipUnless(directory is not null && outputRoot is not null, "Select installed resources and a fresh external component output directory.");
        string output = Path.Combine(outputRoot!, $"effect{effect}-buffer{buffer}-scoped{scoped}");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        NativeEffectFixtureRequest request = effect switch
        {
            184 => new("scene184", 184, 15, 1, 3, 800, 240) { EncounterId = 368, PartyModelId = 3, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, CrisisLevel = 1, ControlledContactValue = 7 },
            154 => new("scene154", 154, 8, 98, 0, 800, 240) { EncounterId = 36, PartyModelId = 0, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 },
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory!, output);
        Assert.NotEqual("blocked", exported.Status);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "queued-action104.battle.json"));
        NativeBattleEffectMemory memory = source.NativeEffectMemory!;
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess! with { Buffer80 = buffer };
        process.Validate();
        source = source with { NativeEffectMemory = memory with { PrimitiveProcess = process } };
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(selected => game.ReadNativeEffectCatalogForEffects([selected])[selected - 1]);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        bool observed = false;
        using IDisposable? visibleArea = scoped ? BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native) : null;
        Assert.Equal(scoped, BattleSceneVisibleArea.IsScoped);
        for (int callback = 0; callback < 512 && !observed; callback++)
        {
            battle.Tick();
            BattleSnapshot snapshot = battle.Capture();
            if (snapshot.NativeGraphics?.OriginalEffect?.Binding.Effect != effect || snapshot.NativeGraphics.PrimitiveFrame is not { } primitive)
            {
                continue;
            }
            NativeBattleLogicalResolvedCommand[] grid = primitive.LastCommands.Where(command => (command.Pass == 1 && (command.Command.Opcode & 2) == 0 || command.Pass == 2 && (command.Command.Opcode & 2) != 0) && command.Command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.Image } && (command.Command.Opcode & 0xE4) == 0x24).ToArray();
            if (grid.Length == 0)
            {
                continue;
            }
            HashSet<long> checkedSources = [];
            foreach (NativeBattleLogicalResolvedCommand command in grid)
            {
                NativeBattleSceneCaptureSample sample = Assert.IsType<NativeBattleSceneCaptureSample>(command.DrawMode.SceneCaptureSample);
                long id = Assert.IsType<long>(sample.Capture.ReplaySourceId);
                Assert.Equal(id, command.Command.SceneCapture!.ReplaySourceId);
                if (!checkedSources.Add(id))
                {
                    continue;
                }
                NativeBattleSceneReplaySource retained = primitive.SceneReplaySources![id];
                Assert.Equal(snapshot.NativeGraphics.Callback, retained.Callback);
                Assert.Equal((byte)(buffer ^ 1), retained.Buffer);
                Assert.NotEmpty(retained.Frame.LastCommands);
                Assert.Null(retained.Frame.SceneReplaySources);
                Assert.All(retained.Frame.LastCommands, item =>
                {
                    Assert.Equal(NativeBattleSceneCaptureKind.Source, item.Command.SceneCapture!.Kind);
                    Assert.Null(item.Command.SceneCapture.ReplaySourceId);
                });
                string captureBank = effect == 184 ? "D34868" : "DBAEE0";
                NativeBattleLogicalResolvedCommand backdrop = Assert.Single(retained.Frame.LastCommands.Where(item => item.Pass == 1 && item.Command.Opcode == 0x28 && item.Command.Packet.Bank == captureBank));
                Assert.Equal(buffer * 24, backdrop.Command.Packet.ByteOffset);
                Assert.Equal((byte)0, backdrop.Command.Packet.Buffer);
                string stageBank = effect == 184 ? "22DB3C4" : "24224D4";
                Assert.Contains(retained.Frame.LastCommands, item => item.Command.Packet.Bank == stageBank);
                Assert.Equal(BattleSceneVisibleArea.Native, retained.VisibleArea);
                NativeBattlePrimitiveCapabilities capabilities = source.Definition.NativeGraphics!.PrimitiveCapabilities!;
                Assert.Equal(2, command.DrawMode.TexturePage!.Value >> 7 & 3);
                byte[] produced = new BattleRenderer(320, 216, new PixelBounds(0, 0, 320, 216)).RenderNativePrimitiveFrame(retained.Frame, retained.Textures, capabilities: capabilities);
                HashSet<uint> stageColors = [];
                for (int pixel = 0; pixel < 320 * 216; pixel++)
                {
                    int offset = pixel * 4;
                    uint rgb = (uint)(produced[offset] << 16 | produced[offset + 1] << 8 | produced[offset + 2]);
                    if (rgb != 0)
                    {
                        stageColors.Add(rgb);
                    }
                }
                Assert.True(stageColors.Count > 1, "The independently rendered original stage must contain multiple nonblack colors.");
                ImmutableArray<NativeBattlePcTextureWindowPixels>.Builder sentinels = primitive.LastTextureSnapshots!.Value.ToBuilder();
                foreach (int index in sample.PixelSnapshots)
                {
                    NativeBattlePcTextureWindowPixels original = sentinels[index];
                    sentinels[index] = BlueWords(original.X, original.Width);
                }
                NativeBattlePreparedPrimitiveFrame isolated = NativeBattlePreparedPrimitiveFrame.From(primitive) with { LastCommands = [command], LastTextureSnapshots = sentinels.ToImmutable(), LastPcRenderTail = null };
                Capture sampledBackend = new();
                new BattleRenderer(320, 216, new PixelBounds(0, 0, 320, 216)) { TriangleRenderer = sampledBackend }.RenderNativePrimitiveFrame(isolated, snapshot.NativeEffectMemory!.Textures!, capabilities: capabilities);
                PixelImage sampled = Assert.Single(sampledBackend.Textures.Distinct());
                Assert.Equal(320, sampled.Width);
                Assert.Equal(216, sampled.Height);
                for (int pixel = 0; pixel < 320 * 216; pixel++)
                {
                    int offset = pixel * 4;
                    ushort word = (ushort)((produced[offset] >> 3) | (produced[offset + 1] >> 3) << 5 | (produced[offset + 2] >> 3) << 10);
                    uint expected = NativeBattlePcDirectTexture.DecodeArgb(word, capabilities.DirectTextureFormat!.Value);
                    var actual = sampled.Pixels[pixel];
                    uint actualArgb = (uint)(actual.A << 24 | actual.R << 16 | actual.G << 8 | actual.B);
                    Assert.Equal(expected, actualArgb);
                }

            }
            BattleSnapshot restoredSnapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!;
            BattleSession cold = BattleSession.Restore(restoredSnapshot);
            cold.AttachNativeEffectResourceResolver(selected => game.ReadNativeEffectCatalogForEffects([selected])[selected - 1]);
            Assert.Equal(scene.Render(battle, 320, 216).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 216).ToArray());
            BattleSession direct = BattleSession.Restore(snapshot);
            direct.AttachNativeEffectResourceResolver(selected => game.ReadNativeEffectCatalogForEffects([selected])[selected - 1]);
            cold.Tick();
            direct.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            if (!scoped)
            {
                Assert.False(BattleSceneVisibleArea.IsScoped);
                BattleSnapshot next = direct.Capture();
                NativeBattlePrimitiveFrame nextFrame = next.NativeGraphics!.PrimitiveFrame!;
                long[] nextIds = nextFrame.LastCommands.Where(item => item.Command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.Image, ReplaySourceId: not null }).Select(item => item.Command.SceneCapture!.ReplaySourceId!.Value).Distinct().ToArray();
                Assert.NotEmpty(nextIds);
                Assert.All(nextIds, id => Assert.DoesNotContain(id, checkedSources));
                foreach (long oldId in checkedSources)
                {
                    string? reference = FindSourceReference(next, oldId);
                    Assert.True(nextFrame.SceneReplaySources?.ContainsKey(oldId) != true || reference is not null, $"Unscoped next warp callback retained orphaned offscreen source {oldId}.");
                    if (nextFrame.SceneReplaySources?.ContainsKey(oldId) == true)
                    {
                        testOutput.WriteLine($"Offscreen source {oldId} remains live through {reference}.");
                    }
                }
            }
            observed = true;
        }
        Assert.True(observed, "The independently selected native buffer must reach its original-resource warp grid.");
    }

    private static string? FindSourceReference(BattleSnapshot snapshot, long target)
    {
        NativeBattlePrimitiveFrame frame = snapshot.NativeGraphics!.PrimitiveFrame!;
        Queue<(long Id, string Path)> pending = new();
        HashSet<long> visited = [];
        for (int slot = 0; slot < (frame.SceneSources?.Length ?? 0); slot++)
        {
            Add(frame.SceneSources!.Value[slot], $"SceneSources[{slot}]");
        }
        AddFrame(NativeBattlePreparedPrimitiveFrame.From(frame), "CurrentFrame");
        AddRegions(snapshot.NativeEffectMemory!.Textures!.SceneCopies, "Textures.SceneCopies");
        foreach (long id in frame.Pending.ReferencedSceneReplaySources())
        {
            Add(id, "Pending");
        }
        NativeBattlePcTextureWindowCache? windows = snapshot.NativeEffectMemory.PrimitiveProcess?.TextureWindowCache;
        for (int row = 0; row < (windows?.Rows.Length ?? 0); row++)
        {
            for (int region = 0; region < windows!.Rows[row].Regions.Length; region++)
            {
                AddRegions(windows.Rows[row].Regions[region].RetainedPixels?.SceneCopies, $"TextureWindows[{row}].Regions[{region}].SceneCopies");
            }
        }
        while (pending.TryDequeue(out var reference))
        {
            if (reference.Id == target)
            {
                return reference.Path;
            }
            if (!visited.Add(reference.Id) || frame.SceneReplaySources is not { } pool || !pool.TryGetValue(reference.Id, out NativeBattleSceneReplaySource? source))
            {
                continue;
            }
            AddFrame(source.Frame, $"{reference.Path}->Source[{reference.Id}].Frame");
            AddRegions(source.Textures.SceneCopies, $"{reference.Path}->Source[{reference.Id}].Textures.SceneCopies");
        }
        return null;

        void Add(long? id, string path)
        {
            if (id is long value)
            {
                pending.Enqueue((value, path));
            }
        }

        void AddFrame(NativeBattlePreparedPrimitiveFrame value, string path)
        {
            for (int index = 0; index < value.LastCommands.Length; index++)
            {
                Add(value.LastCommands[index].Command.SceneCapture?.ReplaySourceId, $"{path}.LastCommands[{index}]");
            }
            for (int index = 0; index < (value.LastTextureSnapshots?.Length ?? 0); index++)
            {
                AddRegions(value.LastTextureSnapshots!.Value[index].SceneCopies, $"{path}.LastTextureSnapshots[{index}].SceneCopies");
            }
        }

        void AddRegions(ImmutableArray<NativeBattleSceneCopyRegion>? regions, string path)
        {
            for (int index = 0; index < (regions?.Length ?? 0); index++)
            {
                Add(regions!.Value[index].Capture.ReplaySourceId, $"{path}[{index}]");
            }
        }
    }

    private static NativeBattlePcTextureWindowPixels BlueWords(int x, int width)
    {
        byte[] data = new byte[width * 216 * 2];
        for (int index = 1; index < data.Length; index += 2)
        {
            data[index] = 0x7C;
        }
        return new(x, 256, width, 216, data.ToImmutableArray());
    }

    private static NativeBattlePreparedPrimitiveFrame Frame(ImmutableArray<NativeBattleLogicalResolvedCommand> commands)
    {
        return new(commands, []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };
    }

    private sealed class Capture : ITriangleRenderer
    {
        internal List<PixelImage> Textures { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Textures.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            if (material.Texture is { } image)
            {
                Textures.Add(image);
            }
        }

        public void End()
        {
        }
    }
}
