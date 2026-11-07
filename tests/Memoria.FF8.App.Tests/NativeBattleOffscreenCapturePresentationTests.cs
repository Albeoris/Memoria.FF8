using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleOffscreenCapturePresentationTests(ITestOutputHelper testOutput)
{
    [Theory]
    [InlineData(184, 0, 320, 216)]
    [InlineData(184, 1, 320, 216)]
    [InlineData(184, 0, 1920, 1080)]
    [InlineData(184, 1, 1920, 1080)]
    [InlineData(154, 0, 320, 216)]
    [InlineData(154, 1, 320, 216)]
    [InlineData(154, 0, 1920, 1080)]
    [InlineData(154, 1, 1920, 1080)]
    public void Original_resources_clear_the_full_capture_and_present_stage_pixels(ushort effect, byte buffer, int width, int height)
    {
        string? gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? outputRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_CAPTURE_PRESENTATION_OUTPUT");
        Assert.SkipUnless(gameDirectory is not null && outputRoot is not null, "Select installed resources and a fresh external diagnostic output directory.");
        string output = Path.GetFullPath(Path.Combine(outputRoot!, $"effect{effect}", $"buffer{buffer}", $"{width}x{height}"));
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        string fixture = Path.Combine(output, "input");
        NativeEffectFixtureRequest request = effect switch
        {
            184 => new("scene184", 184, 15, 1, 3, 800, 240) { EncounterId = 368, PartyModelId = 3, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, CrisisLevel = 1, ControlledContactValue = 7 },
            154 => new("scene154", 154, 8, 98, 0, 800, 240) { EncounterId = 36, PartyModelId = 0, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 },
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, gameDirectory!, fixture);
        Assert.NotEqual("blocked", exported.Status);
        BattleSnapshot queued = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json"));
        foreach (NativeEffectFixtureArtifact artifact in exported.Artifacts)
        {
            string path = Path.GetFullPath(Path.Combine(fixture, artifact.File));
            Assert.StartsWith(fixture + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
            File.Delete(path);
        }
        using GameAssets game = new(gameDirectory!);
        using BattleAssets assets = new(gameDirectory!);
        NativeBattleEffectMemory memory = queued.NativeEffectMemory!;
        BattleSession battle = BattleSession.Restore(queued with { NativeEffectMemory = memory with { PrimitiveProcess = memory.PrimitiveProcess! with { Buffer80 = buffer } } });
        battle.AttachNativeEffectResourceResolver(selected => game.ReadNativeEffectCatalogForEffects([selected])[selected - 1]);
        PixelBounds projection = width == 320 ? new(0, 0, 320, 216) : ViewportLayout.Fit(320, 240, width, height);
        BattleSceneVisibleArea captureArea = BattleSceneVisibleArea.FromViewport(width, height, projection.X, projection.Y, projection.Width, projection.Height);
        Assert.Equal(width > 320, captureArea.Left < 0);
        using IDisposable visibleArea = BattleSceneVisibleArea.Begin(captureArea);
        List<object> observations = [];
        ClearProbe? clearProof = null;
        int coloredCaptureCount = 0;
        BattleSnapshot prior = battle.Capture();
        for (int callback = 0; callback < 512; callback++)
        {
            battle.Tick();
            BattleSnapshot snapshot = battle.Capture();
            NativeBattleSoftwarePrimitiveState? softwareBeforeTick = prior.NativeEffectMemory?.PrimitiveProcess?.Software;
            prior = snapshot;
            if (snapshot.NativeGraphics?.OriginalEffect?.Binding.Effect != effect || snapshot.NativeGraphics.PrimitiveFrame is not { } primitive)
            {
                continue;
            }
            NativeBattleLogicalResolvedCommand[] grid = primitive.LastCommands.Where(item => item.Command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.Image } && (item.Command.Opcode & 0xE4) == 0x24 && (item.Pass == 1 && (item.Command.Opcode & 2) == 0 || item.Pass == 2 && (item.Command.Opcode & 2) != 0)).ToArray();
            if (grid.Length == 0)
            {
                continue;
            }
            battle.RefreshPreparedPresentation();
            NativeBattlePreparedPresentation prepared = battle.PreparedPresentation!;
            NativeBattlePreparedPrimitiveFrame original = prepared.Primitives;
            Assert.Contains(original.LastCommands, item => item.Pass > 0 && item.Command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.Source } && (item.Command.Opcode & 0xE4) == 0x24);
            Assert.All(grid, item =>
            {
                Assert.Equal(2, item.DrawMode.TexturePage!.Value >> 7 & 3);
                Assert.NotNull(item.DrawMode.TexturePixelSnapshot);
            });
            string nativeWords = WordHash(original);
            string retainedWords = SnapshotHash(original);
            NativeBattleSceneCapture capture = grid[0].Command.SceneCapture!;
            NativeBattlePcTextureWindowPixels[] rawPages = grid.Select(item => item.DrawMode.TexturePixelSnapshot!.Value).Distinct().Select(index => original.LastTextureSnapshots!.Value[index]).ToArray();
            NativeBattlePcDirectTextureFormat format = queued.Definition.NativeGraphics!.PrimitiveCapabilities!.DirectTextureFormat!.Value;
            byte[] captured = ReadCapturedWords(rawPages, capture, format, out CaptureWords retained);
            CaptureWords actualVram = SummarizeWords(capture, format, (x, y) => prepared.Textures!.ReadVramWord(x, y));
            Assert.Equal(nativeWords, WordHash(original));
            Assert.Equal(retainedWords, SnapshotHash(original));
            object[] rawRegions = grid.Select(item => item.DrawMode.TexturePixelSnapshot!.Value).Distinct().Select(index => DescribeRegion(index, original.LastTextureSnapshots!.Value[index], capture)).ToArray();
            string blackBank = effect == 184 ? "D34868" : "DBAEE0";
            NativeBattleLogicalResolvedCommand black = Assert.Single(original.LastCommands.Where(item => item.Pass == 1 && item.Command.Opcode == 0x28 && item.Command.Packet.Bank == blackBank));
            NativeBattleLogicalResolvedCommand? sourceEnvironmentCommand = original.LastCommands.LastOrDefault(item => item.Command.Opcode == 0xE3 && (item.Command.Words[0] & 0x3FF) == capture.X && (item.Command.Words[0] >> 10 & 0x1FF) == capture.Y);
            NativeBattleSceneCapture? actualSourceEnvironment = sourceEnvironmentCommand?.Command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.SourceEnvironment } actual ? actual : null;
            object source = DescribeSource(snapshot, original, capture);
            object environmentTrace = DescribeEnvironmentTrace(original, softwareBeforeTick!, black.Command.Packet.ByteOffset / 24, blackBank);
            observations.Add(new { snapshot.Frame, callback = snapshot.NativeGraphics.Callback, buffer = snapshot.NativeEffectMemory!.PrimitiveProcess!.Buffer80, retained, actualVram, source, environmentTrace, blackCapture = black.Command.SceneCapture, black.Command.ScreenCoverage, sourceEnvironmentMetadata = sourceEnvironmentCommand?.Command.SceneCapture, actualSourceEnvironment, nativeWordsSha256 = nativeWords, retainedWordsSha256 = retainedWords });
            File.WriteAllText(Path.Combine(output, "capture.json"), JsonSerializer.Serialize(new { effect, width, height, captureArea, observations }, new JsonSerializerOptions { WriteIndented = true }));
            if (clearProof is null)
            {
                clearProof = ProbePhysicalClear(black.Command, queued.Definition.NativeGraphics!.SoftwarePrimitiveTable!, capture, actualSourceEnvironment);
            }
            coloredCaptureCount += retained.NonblackColors > 1 ? 1 : 0;
            bool finalCapture = retained.NonblackColors > 1 || observations.Count == 35;
            if (observations.Count == 1 || finalCapture)
            {
                SavePng(Path.Combine(output, observations.Count == 1 ? "captured-first.png" : "captured-words.png"), captured, capture.Width, capture.Height);
                BattleRenderer renderer = new(width, height, projection);
                byte[] rgba = renderer.RenderNativePrimitiveFrame(original, prepared.Textures, capabilities: queued.Definition.NativeGraphics!.PrimitiveCapabilities, textureWindowCache: prepared.TextureWindows, uiFrame: prepared.UiFrame, uiTexture: prepared.UiFrame is null ? null : assets.ReadNativePcUiIcons().Resolve, effectTextureSource: assets.EffectTextureSource);
                SavePng(Path.Combine(output, "frame.png"), rgba, width, height);
                Assert.Equal(nativeWords, WordHash(original));
                Assert.Equal(retainedWords, SnapshotHash(original));
            }
            var report = new { effect, width, height, captureArea, clearProof, coloredCaptureCount, commands = original.LastCommands.Length, gridCommands = grid.Length, rawRegions, observations, interpretation = "Actual black packet metadata is preserved, including null capture metadata. Final VRAM is observed after the complete OT; command metadata does not measure per-command software dispatch." };
            File.WriteAllText(Path.Combine(output, "capture.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Assert.Null(black.Command.ScreenCoverage);
            Assert.Equal(buffer * 24, black.Command.Packet.ByteOffset);
            Assert.Equal((byte)0, black.Command.Packet.Buffer);
            Assert.Equal(0x28000000u, black.Command.Words[0]);
            Assert.Equal(0u, black.Command.Words[1]);
            Assert.Equal(320u, black.Command.Words[2]);
            Assert.Equal(232u << 16, black.Command.Words[3]);
            Assert.Equal(320u | 232u << 16, black.Command.Words[4]);
            AssertCaptureSetupOrder(original, blackBank, capture);
            if (observations.Count == 1)
            {
                Assert.Equal(0, clearProof.UnclearedWords);
                Assert.Equal(0, clearProof.OutsideChangedWords);
            }
            if (finalCapture)
            {
                testOutput.WriteLine($"Effect {effect}: {observations.Count} capture observations, first/final raw words and selected main frame written to {output}.");
                Assert.True(retained.NonblackColors > 1, "The retained original-resource capture must contain multiple nonblack stage colors within its 35-capture window; see capture.json for retained/current VRAM and source-stage state.");
                BattleSnapshot independent = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot)) ?? throw new InvalidDataException("The capture continuation snapshot is null.");
                BattleSession cold = BattleSession.Restore(independent);
                cold.AttachNativeEffectResourceResolver(selected => game.ReadNativeEffectCatalogForEffects([selected])[selected - 1]);
                battle.Tick();
                cold.Tick();
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
                return;
            }
        }
        Assert.True(coloredCaptureCount > 0, $"The original-resource component produced {observations.Count} genuine captures without multiple nonblack stage colors within the callback budget; see capture.json.");
        Assert.Fail("The original-resource component did not reach its genuine direct16 warp grid within the callback budget.");
    }

    private sealed record ClearProbe(int X, int Y, int Width, int Height, int UnclearedWords, int UnclearedLeftEdge, int UnclearedRightEdge, int OutsideChangedWords, string PacketSha256, NativeBattleSceneCapture? CommandCapture, NativeBattleSceneCapture? SourceEnvironment);

    private static ClearProbe ProbePhysicalClear(NativeBattleLogicalCommand black, NativeSoftwarePrimitiveTable table, NativeBattleSceneCapture capture, NativeBattleSceneCapture? sourceEnvironment = null)
    {
        Assert.Equal(0u, black.Words[0] & 0xFFFFFF);
        string packetHash = WordHash(new([new(black, new(0), 1)], []));
        byte[] image = new byte[1024 * 512 * 2];
        for (int offset = 0; offset < image.Length; offset += 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset, 2), 0x03E0);
        }
        for (int y = capture.Y; y < capture.Y + capture.Height; y++)
        {
            for (int x = capture.X; x < capture.X + capture.Width; x++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(2 * (y * 1024 + x), 2), 0x7C1F);
            }
        }
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(new((short)capture.X, (short)capture.Y, (uint)(capture.X + capture.Width - 1), (uint)(capture.Y + capture.Height - 1), (short)capture.X, (short)capture.Y, 0, 0, 0, null));
        Assert.Equal(3u, environment.Derived!.Mode);
        NativeBattleSoftwarePrimitiveState software = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer() with { Environment = environment, SceneCapture = sourceEnvironment };
        software.DrawPolygon(image, black, table);
        int uncleared = 0;
        int leftEdge = 0;
        int rightEdge = 0;
        for (int y = capture.Y; y < capture.Y + capture.Height; y++)
        {
            for (int x = capture.X; x < capture.X + capture.Width; x++)
            {
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(2 * (y * 1024 + x), 2));
                if (word != 0)
                {
                    uncleared++;
                    leftEdge += x == capture.X ? 1 : 0;
                    rightEdge += x == capture.X + capture.Width - 1 ? 1 : 0;
                }
            }
        }
        int outsideChanged = 0;
        for (int y = 0; y < 512; y++)
        {
            for (int x = 0; x < 1024; x++)
            {
                if (x < capture.X || x >= capture.X + capture.Width || y < capture.Y || y >= capture.Y + capture.Height)
                {
                    outsideChanged += BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(2 * (y * 1024 + x), 2)) != 0x03E0 ? 1 : 0;
                }
            }
        }
        Assert.Equal(packetHash, WordHash(new([new(black, new(0), 1)], [])));
        return new(capture.X, capture.Y, capture.Width, capture.Height, uncleared, leftEdge, rightEdge, outsideChanged, packetHash, black.SceneCapture, sourceEnvironment);
    }

    private static void SavePng(string path, byte[] rgba, int width, int height)
    {
        using var bitmap = PortableRaster.FromRgba(rgba, width, height);
        bitmap.SavePng(path);
    }

    private sealed record WordFrequency(ushort Word, int Count);
    private sealed record CaptureWords(int NonzeroWords, int DistinctWords, int NonblackColors, WordFrequency[] MostFrequentWords);

    private static byte[] ReadCapturedWords(NativeBattlePcTextureWindowPixels[] pages, NativeBattleSceneCapture capture, NativeBattlePcDirectTextureFormat format, out CaptureWords summary)
    {
        byte[] rgba = new byte[capture.Width * capture.Height * 4];
        Dictionary<ushort, int> histogram = [];
        for (int y = 0; y < capture.Height; y++)
        {
            for (int x = 0; x < capture.Width; x++)
            {
                int physicalX = capture.X + x;
                int physicalY = capture.Y + y;
                NativeBattlePcTextureWindowPixels[] owners = pages.Where(page => physicalX >= page.X && physicalX < page.X + page.Width && physicalY >= page.Y && physicalY < page.Y + page.Height).ToArray();
                Assert.NotEmpty(owners);
                ushort word = owners[0].ReadWord(physicalX, physicalY);
                Assert.All(owners, owner => Assert.Equal(word, owner.ReadWord(physicalX, physicalY)));
                uint color = NativeBattlePcDirectTexture.DecodeArgb(word, format);
                histogram[word] = histogram.GetValueOrDefault(word) + 1;
                int offset = (y * capture.Width + x) * 4;
                rgba[offset] = (byte)(color >> 16);
                rgba[offset + 1] = (byte)(color >> 8);
                rgba[offset + 2] = (byte)color;
                rgba[offset + 3] = (byte)(color >> 24);
            }
        }
        summary = SummarizeHistogram(histogram, format);
        return rgba;
    }

    private static CaptureWords SummarizeWords(NativeBattleSceneCapture capture, NativeBattlePcDirectTextureFormat format, Func<int, int, ushort> read)
    {
        Dictionary<ushort, int> histogram = [];
        for (int y = capture.Y; y < capture.Y + capture.Height; y++)
        {
            for (int x = capture.X; x < capture.X + capture.Width; x++)
            {
                ushort word = read(x, y);
                histogram[word] = histogram.GetValueOrDefault(word) + 1;
            }
        }
        return SummarizeHistogram(histogram, format);
    }

    private static CaptureWords SummarizeHistogram(Dictionary<ushort, int> histogram, NativeBattlePcDirectTextureFormat format)
    {
        int nonblack = histogram.Keys.Select(word => NativeBattlePcDirectTexture.DecodeArgb(word, format) & 0xFFFFFF).Where(color => color != 0).Distinct().Count();
        return new(histogram.Where(item => item.Key != 0).Sum(item => item.Value), histogram.Count, nonblack, histogram.OrderByDescending(item => item.Value).ThenBy(item => item.Key).Take(8).Select(item => new WordFrequency(item.Key, item.Value)).ToArray());
    }

    private static object DescribeSource(BattleSnapshot snapshot, NativeBattlePreparedPrimitiveFrame frame, NativeBattleSceneCapture capture)
    {
        var commands = frame.LastCommands.Select((item, order) => new { item, order }).Where(entry => entry.item.Command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.Source or NativeBattleSceneCaptureKind.SourceEnvironment }).ToArray();
        var groups = commands.GroupBy(entry => new { entry.item.Pass, entry.item.Command.Opcode, entry.item.Command.Flags20, entry.item.Command.Packet.Bank, Color = entry.item.Command.Words[0] & 0xFFFFFF }).ToArray();
        var commandGroups = groups.Take(24).Select(group => new
        {
            group.Key,
            Count = group.Count(),
            FirstOrder = group.First().order,
            LastOrder = group.Last().order,
            SampleColors = SourceColors(group.First().item.Command),
            NormalViewport = group.First().item.DrawMode.NormalViewport,
            TexturePage = group.First().item.DrawMode.TexturePage,
            TexturePixelSnapshot = group.First().item.DrawMode.TexturePixelSnapshot
        }).ToArray();
        var environments = commands.Where(entry => entry.item.Pass == 0 && entry.item.Command.Opcode is >= 0xE3 and <= 0xE5).Take(12).Select(entry => new { entry.order, entry.item.Command.Opcode, entry.item.Command.Words }).ToArray();
        NativeBattleEffectTextureState textures = snapshot.NativeEffectMemory!.Textures!;
        return new
        {
            Commands = commands.Length,
            CommandGroups = groups.Length,
            commandGroups,
            environments,
            Target = new { capture.X, capture.Y, capture.Width, capture.Height, capture.Source },
            FinalSoftwareEnvironment = snapshot.NativeEffectMemory.PrimitiveProcess!.Software!.Environment,
            Stage = snapshot.NativeStage!.Groups.Select((group, index) => new { index, group.Flags, group.ColorFlags, group.ColorDepth, group.Animation, group.Counter }).ToArray(),
            textures.Count,
            LiveTextureSources = textures.LiveTextureSources?.Length ?? 0,
            PixelSources = textures.LiveTextureSources?.Count(source => source.Origin.Role == NativeBattleTextureRole.Pixels) ?? 0,
            TextureRegistrations = textures.TextureRegistrations?.Length ?? 0,
            RetainedTextureSnapshots = frame.LastTextureSnapshots?.Length ?? 0
        };
    }

    private static uint[] SourceColors(NativeBattleLogicalCommand command)
    {
        if ((command.Opcode & 0xE0) != 0x20 || (command.Opcode & 16) == 0)
        {
            return [command.Words[0] & 0xFFFFFF];
        }
        int stride = (command.Opcode & 4) != 0 ? 3 : 2;
        int count = (command.Opcode & 8) != 0 ? 4 : 3;
        return Enumerable.Range(0, count).Select(index => command.Words[index * stride] & 0xFFFFFF).ToArray();
    }

    private static object DescribeEnvironmentTrace(NativeBattlePreparedPrimitiveFrame frame, NativeBattleSoftwarePrimitiveState before, int buffer, string blackBank)
    {
        NativeSoftwareDrawingEnvironmentState environment = before.EnvironmentBuffers!.Apply45B660(buffer ^ 1, before.Environment).Environment;
        NativeSoftwareDrawingEnvironmentState initial = environment;
        List<object> changes = [];
        Dictionary<uint, int> sourceModes = [];
        foreach (var entry in frame.LastCommands.Select((item, order) => new { item, order }).Where(entry => entry.item.Pass == 0))
        {
            NativeBattleLogicalCommand command = entry.item.Command;
            environment = command.Opcode switch
            {
                0xE3 => NativeSoftwareDrawingEnvironment.WriteE345BA30(environment, command.Words[0]),
                0xE4 => NativeSoftwareDrawingEnvironment.WriteE445BA60(environment, command.Words[0]).State,
                0xE5 => NativeSoftwareDrawingEnvironment.WriteE545BAD0(environment, command.Words[0]),
                _ => environment
            };
            uint mode = environment.Derived!.Mode;
            if (command.Opcode is >= 0xE3 and <= 0xE5 || command.Packet.Bank == blackBank)
            {
                changes.Add(new { entry.order, command.OrderingBucket, command.Opcode, command.Packet, command.Words, command.Flags20, command.SceneCapture, mode, environment.TopLeftX, environment.TopLeftY, environment.RightX, environment.BottomY, environment.OffsetX, environment.OffsetY });
            }
            if (command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.Source } && (command.Opcode & 0xE0) == 0x20)
            {
                sourceModes[mode] = sourceModes.GetValueOrDefault(mode) + 1;
            }
        }
        return new { interpretation = "Deterministic environment reduction of actual retained pass0 commands from prior software state and actual buffer publication; not an additional OT execution.", initial, changes, sourceModes };
    }

    private static void AssertCaptureSetupOrder(NativeBattlePreparedPrimitiveFrame frame, string blackBank, NativeBattleSceneCapture capture)
    {
        NativeBattleLogicalResolvedCommand[] firstPass = frame.LastCommands.Where(item => item.Pass == 0).ToArray();
        var setup = firstPass.Select((item, order) => new { item.Command, order }).Where(entry => entry.Command.OrderingBucket == 4385).ToArray();
        Assert.Equal(new byte[] { 0xE5, 0xE3, 0xE4, 0x28 }, setup.Select(entry => entry.Command.Opcode).ToArray());
        Assert.Equal(blackBank, setup[3].Command.Packet.Bank);
        Assert.Equal((uint)capture.X, setup[1].Command.Words[0] & 0x3FF);
        Assert.Equal((uint)capture.Y, setup[1].Command.Words[0] >> 10 & 0x1FF);
        int firstSource = Array.FindIndex(firstPass, item => item.Command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.Source } && (item.Command.Opcode & 0xE0) == 0x20);
        Assert.True(firstSource > setup[3].order, "The physical capture setup and black clear must precede the actual source stage packets.");
    }

    private static object DescribeRegion(int index, NativeBattlePcTextureWindowPixels pixels, NativeBattleSceneCapture capture)
    {
        int left = Math.Max(pixels.X, capture.X);
        int top = Math.Max(pixels.Y, capture.Y);
        int right = Math.Min(pixels.X + pixels.Width, capture.X + capture.Width);
        int bottom = Math.Min(pixels.Y + pixels.Height, capture.Y + capture.Height);
        int nonzero = 0;
        HashSet<ushort> colors = [];
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                ushort word = pixels.ReadWord(x, y);
                if (word != 0)
                {
                    nonzero++;
                }
                colors.Add(word);
            }
        }
        var provenance16 = (pixels.LiveTextureSources ?? []).Where(source => source.Origin.Role == NativeBattleTextureRole.Pixels && source.Origin.BitsPerPixel == 16 && source.X < right && source.X + source.WidthWords > left && source.Y < bottom && source.Y + source.Height > top).Select(source => new { source.X, source.Y, source.WidthWords, source.Height, source.Origin.Owner, source.Origin.Member }).ToArray();
        return new { index, pixels.X, pixels.Y, pixels.Width, pixels.Height, intersection = new { left, top, right, bottom }, nonzeroWords = nonzero, distinctWords = colors.Count, dataSha256 = Convert.ToHexString(SHA256.HashData(pixels.Data.AsSpan())), provenance16 };
    }

    private static string WordHash(NativeBattlePreparedPrimitiveFrame frame)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> wordBytes = stackalloc byte[4];
        foreach (NativeBattleLogicalResolvedCommand command in frame.LastCommands)
        {
            foreach (uint word in command.Command.Words)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(wordBytes, word);
                hash.AppendData(wordBytes);
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string SnapshotHash(NativeBattlePreparedPrimitiveFrame frame)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (NativeBattlePcTextureWindowPixels pixels in frame.LastTextureSnapshots ?? [])
        {
            hash.AppendData(pixels.Data.AsSpan());
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
