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
            Assert.True(path.StartsWith(fixture + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
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
        for (int callback = 0; callback < 512; callback++)
        {
            battle.Tick();
            BattleSnapshot snapshot = battle.Capture();
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
            BattleRenderer renderer = new(width, height, projection);
            byte[] rgba = renderer.RenderNativePrimitiveFrame(original, prepared.Textures, capabilities: queued.Definition.NativeGraphics!.PrimitiveCapabilities, textureWindowCache: prepared.TextureWindows, uiFrame: prepared.UiFrame, uiTexture: prepared.UiFrame is null ? null : assets.ReadNativePcUiIcons().Resolve, effectTextureSource: assets.EffectTextureSource);
            SavePng(Path.Combine(output, "frame.png"), rgba, width, height);
            NativeBattleSceneCapture capture = grid[0].Command.SceneCapture!;
            NativeBattlePcTextureWindowPixels[] rawPages = grid.Select(item => item.DrawMode.TexturePixelSnapshot!.Value).Distinct().Select(index => original.LastTextureSnapshots!.Value[index]).ToArray();
            byte[] captured = ReadCapturedWords(rawPages, capture, queued.Definition.NativeGraphics!.PrimitiveCapabilities!.DirectTextureFormat!.Value);
            SavePng(Path.Combine(output, "captured-words.png"), captured, capture.Width, capture.Height);
            Assert.Equal(nativeWords, WordHash(original));
            Assert.Equal(retainedWords, SnapshotHash(original));
            object[] rawRegions = grid.Select(item => item.DrawMode.TexturePixelSnapshot!.Value).Distinct().Select(index => DescribeRegion(index, original.LastTextureSnapshots!.Value[index], capture)).ToArray();
            string blackBank = effect == 184 ? "D34868" : "DBAEE0";
            NativeBattleLogicalResolvedCommand black = Assert.Single(original.LastCommands.Where(item => item.Pass == 1 && item.Command.Opcode == 0x28 && item.Command.Packet.Bank == blackBank));
            Assert.Equal(captureArea, black.Command.SceneCapture!.Source);
            Assert.Equal(NativeBattleScreenCoverage.Full, black.Command.ScreenCoverage);
            Assert.Equal(buffer * 24, black.Command.Packet.ByteOffset);
            Assert.Equal((byte)0, black.Command.Packet.Buffer);
            Assert.Equal(0x28000000u, black.Command.Words[0]);
            Assert.Equal(0u, black.Command.Words[1]);
            Assert.Equal(320u, black.Command.Words[2]);
            Assert.Equal(232u << 16, black.Command.Words[3]);
            Assert.Equal(320u | 232u << 16, black.Command.Words[4]);
            var clearProof = ProbePhysicalClear(black.Command, queued.Definition.NativeGraphics!.SoftwarePrimitiveTable!);
            ClearProbe? holyClearProof = effect == 184 ? ProbeHolyProducer(snapshot, game.ReadNativeHoly175Resources(), captureArea, buffer) : null;
            var report = new { effect, width, height, captureArea, clearProof, holyClearProof, snapshot.Frame, callback = snapshot.NativeGraphics.Callback, buffer = snapshot.NativeEffectMemory!.PrimitiveProcess!.Buffer80, nativeWordsSha256 = nativeWords, retainedWordsSha256 = retainedWords, commands = original.LastCommands.Length, gridCommands = grid.Length, rawRegions, frameRgbaSha256 = Convert.ToHexString(SHA256.HashData(rgba)), captureRgbaSha256 = Convert.ToHexString(SHA256.HashData(captured)), interpretation = "Original-resource completed frame and its unmodified retained framebuffer words; capture scope matches the selected output viewport." };
            File.WriteAllText(Path.Combine(output, "capture.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            testOutput.WriteLine($"Effect {effect}: frame, raw captured words and compact metadata written to {output}.");
            Assert.Equal(0, clearProof.UnclearedWords);
            Assert.Equal(0, clearProof.OutsideChangedWords);
            if (holyClearProof is not null)
            {
                Assert.Equal(0, holyClearProof.UnclearedWords);
                Assert.Equal(0, holyClearProof.OutsideChangedWords);
            }
            return;
        }
        Assert.Fail("The original-resource component did not reach its genuine direct16 warp grid within the callback budget.");
    }

    private static ClearProbe ProbeHolyProducer(BattleSnapshot snapshot, NativeBattleHoly175Resources resources, BattleSceneVisibleArea area, byte buffer)
    {
        NativeBattlePrimitiveProcess process = snapshot.NativeEffectMemory!.PrimitiveProcess!.Reset500900(4);
        NativeBattleStageState stage = snapshot.NativeStage!;
        var geometry = stage.MutableGeometry ?? snapshot.Definition.NativeStageSource!.Geometry!.Value;
        NativeBattleCameraProjection projection = snapshot.NativeCamera!.Projection!;
        NativeBattleGraphicsDefinition definition = snapshot.Definition.NativeGraphics!;
        NativeBattleHoly175CaptureWrite written = NativeBattleHoly175CapturePackets.Write5D29E0(process, NativeBattleLogicalOrdering.Clear(buffer), stage, geometry, projection.View!, projection, definition.Rules.SummonColors!, resources, new(resources.CaptureBlackPackets!.Data, 0, 0));
        NativeBattleLogicalCommand black = Assert.Single(written.Ordering.SelectTable(buffer).Traverse(definition.PrimitiveTables!.WordLengths).Where(item => item.Packet.Bank == NativeBattleHoly175CaptureMemory.PacketBank && item.Opcode == 0x28));
        Assert.Equal(NativeBattleScreenCoverage.Full, black.ScreenCoverage);
        Assert.Equal(area, black.SceneCapture!.Source);
        Assert.Equal(232, black.SceneCapture.Height);
        Assert.Equal(8, black.SceneCapture.PaddingTop);
        Assert.Equal(8, black.SceneCapture.PaddingBottom);
        Assert.Equal(buffer * 24, black.Packet.ByteOffset);
        Assert.Equal(buffer, black.Packet.Buffer);
        Assert.Equal(5, black.Words.Length);
        for (int word = 0; word < black.Words.Length; word++)
        {
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(resources.CaptureBlackPackets!.Data.AsSpan(buffer * 24 + (word + 1) * 4, 4)), black.Words[word]);
        }
        return ProbePhysicalClear(black, definition.SoftwarePrimitiveTable!);
    }

    private sealed record ClearProbe(int X, int Y, int Width, int Height, int UnclearedWords, int UnclearedLeftEdge, int UnclearedRightEdge, int OutsideChangedWords, string PacketSha256);

    private static ClearProbe ProbePhysicalClear(NativeBattleLogicalCommand black, NativeSoftwarePrimitiveTable table)
    {
        NativeBattleSceneCapture capture = black.SceneCapture!;
        Assert.Equal(NativeBattleSceneCaptureKind.Source, capture.Kind);
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
        NativeBattleSoftwarePrimitiveState software = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer() with { Environment = environment };
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
        return new(capture.X, capture.Y, capture.Width, capture.Height, uncleared, leftEdge, rightEdge, outsideChanged, packetHash);
    }

    private static void SavePng(string path, byte[] rgba, int width, int height)
    {
        using var bitmap = PortableRaster.FromRgba(rgba, width, height);
        bitmap.SavePng(path);
    }

    private static byte[] ReadCapturedWords(NativeBattlePcTextureWindowPixels[] pages, NativeBattleSceneCapture capture, NativeBattlePcDirectTextureFormat format)
    {
        byte[] rgba = new byte[capture.Width * capture.Height * 4];
        HashSet<uint> nonblackColors = [];
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
                if ((color & 0xFFFFFF) != 0)
                {
                    nonblackColors.Add(color);
                }
                int offset = (y * capture.Width + x) * 4;
                rgba[offset] = (byte)(color >> 16);
                rgba[offset + 1] = (byte)(color >> 8);
                rgba[offset + 2] = (byte)color;
                rgba[offset + 3] = (byte)(color >> 24);
            }
        }
        Assert.True(nonblackColors.Count > 1, "The retained original-resource capture must contain multiple nonblack stage colors.");
        return rgba;
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
