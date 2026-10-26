using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class JunctionVisualInstalledTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    [Fact]
    public void CoherentOptionCoveragePreservesWinnersAndDecodesEverySelectedPhysicalTextureOnce()
    {
        using Inputs input = new();
        HashSet<string> decoded = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> selected = new(StringComparer.OrdinalIgnoreCase);
        List<object> images = [];
        List<object> configurations = [];
        foreach (CoverageCase configuration in input.Cases)
        {
            JunctionModPackage package = JunctionModImporter.Open(input.Archive, configuration.Options);
            Dictionary<string, ExpectedFile> expected = configuration.Rows.Where(row => row.Winner).ToDictionary(row => Normalize(row.Logical), StringComparer.OrdinalIgnoreCase);
            foreach (JunctionReplacement replacement in package.Files)
            {
                ExpectedFile file = expected[Normalize(replacement.RelativePath)];
                Assert.Equal(Normalize(file.Source), Normalize(replacement.SourcePath));
                selected.Add(Normalize(replacement.SourcePath));
                if (!IsTexture(replacement.RelativePath) || !decoded.Add(Normalize(replacement.SourcePath)))
                {
                    continue;
                }
                byte[] bytes = Read(replacement);
                Assert.Equal(file.Bytes, bytes.Length);
                ModFile modFile = new("candidate", ModFormat.Junction, replacement.RelativePath, 1, () => bytes);
                DecodedModImage image = ModImageDecoder.Decode(modFile);
                Assert.True(image.Image.Width > 0 && image.Image.Height > 0);
                Assert.NotEmpty(image.MipLevels);
                images.Add(new { replacement.SourcePath, replacement.RelativePath, bytes = bytes.Length, sha256 = Hash(bytes), image.Image.Width, image.Image.Height, mipLevels = image.MipLevels.Count });
            }
            Assert.Equal(expected.Count, package.Files.Count);
            configurations.Add(new { configuration.Options, selected = package.Files.Count, package.Diagnostics });
        }
        HashSet<string> expectedTextures = input.Cases.SelectMany(configuration => configuration.Rows).Where(row => row.Winner && IsTexture(row.Logical)).Select(row => Normalize(row.Source)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.True(expectedTextures.SetEquals(decoded));
        Evidence("option-texture-decode.json", new { cases = configurations, physicalFiles = selected.Count, textureFiles = decoded.Count, images, scope = "Every coherent selected physical texture decoded once; filename recognition alone is not native consumer acceptance." });
    }

    [Fact]
    public void EachOptionSelectionPublishesThroughHostAndExistingNativeArtworkConsumers()
    {
        using Inputs input = new();
        NativeMenuArtwork original = NativeMenuArtwork.Load(input.GameDirectory);
        string baseline = ArtworkSignature(original);
        List<object> observations = [];
        foreach (CoverageCase configuration in input.Cases)
        {
            input.Select(configuration.Options);
            using GameResourceHost host = input.Open();
            ModPackage package = Assert.Single(host.Mods.Catalog.Packages, package => package.Id == input.PackageId);
            Assert.Equal(ModEffectiveStatus.Active, package.Status);
            AssertOnlyExpectedPackages(host, input.PackageId);
            Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "junction-import-failed" or "ffnx-unsupported-target" or "Junction.InvalidEncodedResource" or "Junction.InvalidFieldResource");
            Assert.All(host.Mods.Diagnostics.Where(diagnostic => diagnostic.Code == "ffnx-unused-native-target" && diagnostic.PackageId == input.PackageId), diagnostic => Assert.False(string.IsNullOrWhiteSpace(diagnostic.RelativePath)));
            Assert.True(JunctionEncodedResources.TryPrepare(host.Mods.Catalog, out IReadOnlyList<EncodedResource> encoded, out IReadOnlyList<ModDiagnostic> diagnostics));
            Assert.Empty(diagnostics);
            foreach (EncodedResource resource in encoded.Where(resource => resource.Content.Provenance.StartsWith(input.PackageId + ":", StringComparison.Ordinal)))
            {
                Assert.Equal(resource.Fingerprint, Hash(host.Resources.Read(resource.Key)));
            }
            NativeMenuArtwork artwork = NativeMenuArtwork.Load(input.GameDirectory, host.Resources);
            string fingerprint = ArtworkSignature(artwork);
            bool hasNativeArtwork = host.Mods.Catalog.Files.Any(file => file.PackageId == input.PackageId && (file.RelativePath.Contains("/hires/face_b", StringComparison.OrdinalIgnoreCase) || file.RelativePath.Contains("/hires/iconfl", StringComparison.OrdinalIgnoreCase)));
            if (hasNativeArtwork)
            {
                Assert.NotEqual(baseline, fingerprint);
            }
            else
            {
                Assert.Equal(baseline, fingerprint);
            }
            observations.Add(new { configuration.Options, package.Status, encoded = encoded.Select(resource => new { resource.Key.Path, resource.Fingerprint }), nativeArtwork = fingerprint, changedNativeArtwork = fingerprint != baseline, artwork.HasControllerArtwork, diagnostics = host.Mods.Diagnostics });
        }
        Evidence("option-host-native-artwork.json", new { observations, scope = "Actual portraits, icon and controller native consumers. Unused title/font domains are not counted as displayed artwork." });
    }

    [Fact]
    public void DefaultFieldBackgroundAndMainCharacterConsumersUseSelectedResources()
    {
        using Inputs input = new();
        input.Select(input.Cases[0].Options);
        using GameResourceHost host = input.Open();
        using GameAssets assets = new(input.GameDirectory, resources: host.Resources);
        ModFile background = host.Mods.Catalog.Files.First(file => file.PackageId == input.PackageId && BackgroundField(file.RelativePath) is not null);
        string fieldName = BackgroundField(background.RelativePath)!;
        FieldLogicResources logic = assets.ReadFieldLogicResources(fieldName);
        FieldAssets field = assets.LoadField(logic.Id);
        TextureValue[] tiles = field.Background.Select(tile => tile.Texture!.Value).ToArray();
        Assert.Contains(tiles, texture => texture.IsReplacement && texture.Provenance.Contains(input.PackageId + "/", StringComparison.Ordinal));
        List<object> models = [];
        foreach (ModFile file in host.Mods.Catalog.Files.Where(file => file.PackageId == input.PackageId && JunctionEncodedResources.Address(file.RelativePath) is ResourceKey key && key.Path.StartsWith("field/main_chr/", StringComparison.Ordinal)))
        {
            ResourceKey key = JunctionEncodedResources.Address(file.RelativePath)!.Value;
            Assert.Equal(file.ReadBytes(), host.Resources.Read(key));
            string name = Path.GetFileNameWithoutExtension(key.Path);
            var textures = assets.LoadFieldMainCharacterTextures(name);
            List<object> materials = [];
            for (int texture = 0; texture < textures.Count; texture++)
            {
                PixelImage image = textures[texture];
                Assert.True(image.Width > 0 && image.Height > 0);
                materials.Add(new { texture, image.Width, image.Height });
            }
            models.Add(new { key.Path, materials });
        }
        Assert.NotEmpty(models);
        Evidence("native-field-consumers.json", new { field.Id, field.Name, tiles = tiles.Length, replacements = tiles.Count(texture => texture.IsReplacement), background.RelativePath, models, diagnostics = host.Mods.Diagnostics, scope = "Native field asset/material initialization only; no gameplay or glyph/title rendering." });
    }

    [Fact]
    public void OptionalStandaloneRendersCpuGpuAndFreshHostRestoreWithoutAdvancing()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_VISUAL_STANDALONE");
        Assert.SkipUnless(File.Exists(source), "Configure an optional concrete standalone visual checkpoint.");
        using Inputs input = new();
        Dictionary<string, int> renderOptions = new(input.Cases[0].Options, StringComparer.OrdinalIgnoreCase);
        string? configuredOptions = Environment.GetEnvironmentVariable("MEMORIA_FF8_VISUAL_RENDER_OPTIONS");
        if (!string.IsNullOrWhiteSpace(configuredOptions))
        {
            foreach ((string key, int value) in JsonSerializer.Deserialize<Dictionary<string, int>>(configuredOptions)!)
            {
                renderOptions[key] = value;
            }
        }
        input.Select(renderOptions);
        string sourceHash = FileHash(source!);
        string? evidenceRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        string? output = string.IsNullOrWhiteSpace(evidenceRoot) ? null : Path.Combine(evidenceRoot, "VisualPackage", "Standalone");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
        }
        string phase = "initial native field restore";
        try
        {
            GameSession session = new(GameSession.Load(source!));
            Assert.Equal(SceneKind.Field, session.State.Scene);
            int fieldId = session.State.FieldId;
            string fieldName = session.State.FieldName;
            byte[] software;
            Color[] hardware;
            JsonElement events;
            long frame;
            using GpuTestDevice device = new();
            using GpuTriangleRenderer gpu = new(device.Device);
            using (GameResourceHost host = input.Open())
            using (GameAssets assets = new(input.GameDirectory, resources: host.Resources, modelOverrides: host))
            using (GameWorld world = new(session, input.GameDirectory, resources: host.Resources, resolveGameFile: host.ResolveGameFile, sharedAssets: assets, readSceneMods: () => host.SceneMods, modelOverrides: host))
            {
                Assert.True(world.EnsureField());
                world.SetRenderSize(640, 480);
                FieldAssets nativeField = Assert.IsType<FieldAssets>(world.Field);
                Assert.Contains(nativeField.Background, tile => tile.Texture?.Value is { IsReplacement: true } texture && texture.Provenance.Contains(input.PackageId + "/", StringComparison.Ordinal));
                frame = world.Events.Frame;
                events = world.Events.Capture();
                phase = "CPU and GPU preview";
                software = world.Render(TimeSpan.Zero).ToArray();
                world.Render(TimeSpan.Zero, triangleRenderer: gpu);
                hardware = GpuTestDevice.Read(gpu.Target);
                Assert.Equal(software, world.Render(TimeSpan.Zero));
                Assert.True(JsonElement.DeepEquals(events, world.Events.Capture()));
                Assert.Equal(frame, world.Events.Frame);
                Assert.Equal(fieldId, session.State.FieldId);
                Assert.Equal(fieldName, session.State.FieldName);
                Assert.Contains(hardware, pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
                long error = 0;
                for (int index = 0; index < hardware.Length; index++)
                {
                    error += Math.Abs(hardware[index].R - software[index * 4]);
                    error += Math.Abs(hardware[index].G - software[index * 4 + 1]);
                    error += Math.Abs(hardware[index].B - software[index * 4 + 2]);
                }
                Assert.InRange(error / (double)(hardware.Length * 3), 0, 2);
                if (output is not null)
                {
                    using var image = GameUi.BattlePreviewCanvas.BitmapFromRgba(software, 640, 480);
                    image.Save(Path.Combine(output, "enabled-cpu.png"));
                    using FileStream stream = File.Create(Path.Combine(output, "enabled-gpu.png"));
                    gpu.Target.SaveAsPng(stream, 640, 480);
                }
            }
            phase = "fresh host restore of the same preserved standalone";
            GameSession restored = new(GameSession.Load(source!));
            using (GameResourceHost host = input.Open())
            using (GameAssets assets = new(input.GameDirectory, resources: host.Resources, modelOverrides: host))
            using (GameWorld world = new(restored, input.GameDirectory, resources: host.Resources, resolveGameFile: host.ResolveGameFile, sharedAssets: assets, readSceneMods: () => host.SceneMods, modelOverrides: host))
            {
                Assert.True(world.EnsureField());
                world.SetRenderSize(640, 480);
                Assert.Equal(frame, world.Events.Frame);
                Assert.True(JsonElement.DeepEquals(events, world.Events.Capture()));
                Assert.Equal(software, world.Render(TimeSpan.Zero));
                world.Render(TimeSpan.Zero, triangleRenderer: gpu);
                Assert.Equal(hardware, GpuTestDevice.Read(gpu.Target));
                Assert.True(JsonElement.DeepEquals(events, world.Events.Capture()));
            }
            phase = "baseline source preview with an empty external mod selection";
            GameSession baseline = new(GameSession.Load(source!));
            byte[] baselineSoftware;
            using (GameResourceHost host = input.OpenOriginal())
            using (GameAssets assets = new(input.GameDirectory, resources: host.Resources, modelOverrides: host))
            using (GameWorld world = new(baseline, input.GameDirectory, resources: host.Resources, resolveGameFile: host.ResolveGameFile, sharedAssets: assets, readSceneMods: () => host.SceneMods, modelOverrides: host))
            {
                Assert.True(world.EnsureField());
                world.SetRenderSize(640, 480);
                Assert.Equal(fieldId, baseline.State.FieldId);
                Assert.Equal(fieldName, baseline.State.FieldName);
                Assert.Equal(frame, world.Events.Frame);
                JsonElement baselineEvents = world.Events.Capture();
                baselineSoftware = world.Render(TimeSpan.Zero).ToArray();
                Assert.True(JsonElement.DeepEquals(baselineEvents, world.Events.Capture()));
                if (output is not null)
                {
                    using var image = GameUi.BattlePreviewCanvas.BitmapFromRgba(baselineSoftware, 640, 480);
                    image.Save(Path.Combine(output, "baseline-cpu.png"));
                }
            }
            Evidence("standalone-render-restore.json", new { source, sourceHash, options = renderOptions, fieldId, fieldName, frame, framesAdvanced = 0, cpuSha256 = Hash(software), baselineCpuSha256 = Hash(baselineSoftware), baselineUsesEmptyExternalMods = true, freshHostCpuGpuEqual = true, sourceUnchanged = true, output, scope = "Render and fresh-host restoration of one existing standalone; no input action, VM step, replay or source-save write. Baseline is comparison evidence without a pixel-difference acceptance threshold." });
        }
        catch (Exception exception)
        {
            Evidence("standalone-render-boundary.json", new { source, sourceHash, options = renderOptions, phase, exception = exception.ToString(), sourceUnchanged = FileHash(source!) == sourceHash, policy = "No resource state, native flags or checkpoint bytes changed to bypass incompatibility." });
            throw;
        }
        finally
        {
            Assert.Equal(sourceHash, FileHash(source!));
        }
    }

    [Fact]
    public void OptionalLogicalModelSnapshotContinuesAndRestoresWithReplacementEnabledAndDisabled()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_VISUAL_STANDALONE");
        string? evidenceRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        Assert.SkipUnless(File.Exists(source) && !string.IsNullOrWhiteSpace(evidenceRoot), "Configure an optional standalone and output directory for model replacement continuation.");
        using Inputs input = new();
        input.Select(input.Cases[0].Options);
        string output = Path.Combine(evidenceRoot!, "VisualPackage", "logical-model-continuation.ff8");
        Assert.False(File.Exists(output), "Preserve an earlier standalone; select a fresh evidence output directory.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string sourceHash = FileHash(source!);
        long frame;
        JsonElement snapshot;
        using (GameResourceHost host = input.Open())
        using (GameAssets assets = new(input.GameDirectory, resources: host.Resources, modelOverrides: host))
        {
            GameSession session = new(GameSession.Load(source!));
            using GameWorld world = new(session, input.GameDirectory, resources: host.Resources, resolveGameFile: host.ResolveGameFile, sharedAssets: assets, readSceneMods: () => host.SceneMods, modelOverrides: host);
            Assert.True(world.EnsureField());
            frame = world.Events.Frame;
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.Equal(frame + 1, world.Events.Frame);
            Assert.Null(world.Events.LastFault);
            session.SetEventState(world.Events.Capture());
            session.Save(output, compact: true);
            snapshot = world.Events.Capture();
        }
        foreach (bool enabled in new[] { true, false })
        {
            using GameResourceHost host = enabled ? input.Open() : input.OpenOriginal();
            using GameAssets assets = new(input.GameDirectory, resources: host.Resources, modelOverrides: host);
            GameSession restored = new(GameSession.Load(output));
            using GameWorld world = new(restored, input.GameDirectory, resources: host.Resources, resolveGameFile: host.ResolveGameFile, sharedAssets: assets, readSceneMods: () => host.SceneMods, modelOverrides: host);
            Assert.True(world.EnsureField());
            Assert.Equal(frame + 1, world.Events.Frame);
            Assert.True(JsonElement.DeepEquals(snapshot, world.Events.Capture()));
            Assert.NotEmpty(world.Render(TimeSpan.Zero));
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.Equal(frame + 2, world.Events.Frame);
            Assert.Null(world.Events.LastFault);
        }
        Assert.Equal(sourceHash, FileHash(source!));
        Evidence("logical-model-continuation.json", new { source, sourceHash, output, outputHash = FileHash(output), startFrame = frame, savedFrame = frame + 1, restoredFrame = frame + 1, continuedFrame = frame + 2, options = input.Cases[0].Options, enabledAndDisabledRestored = true, sourceUnchanged = true });
    }

    private static string? BackgroundField(string path)
    {
        string[] parts = Normalize(path).Split('/');
        return parts.Length == 6 && parts[0] == "mods" && parts[1] == "textures" && parts[2] == "field" && parts[3] == "mapdata" && Path.GetFileNameWithoutExtension(parts[5]) == parts[4] ? parts[4] : null;
    }

    private static string ArtworkSignature(NativeMenuArtwork artwork)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IEnumerable<PixelImage> images = Enumerable.Range(0, NativeMenuArtwork.CharacterCount).Select(artwork.GetCharacterPortrait)
            .Concat(Enumerable.Range(0, NativeMenuArtwork.GuardianForceCount).Select(artwork.GetGuardianForcePortrait))
            .Concat(Enumerable.Range(128, 12).Select(artwork.GetControllerButtonIcon));
        foreach (PixelImage image in images)
        {
            hash.AppendData(BitConverter.GetBytes(image.Width));
            hash.AppendData(BitConverter.GetBytes(image.Height));
            hash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(image.Pixels.AsSpan()));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AssertOnlyExpectedPackages(GameResourceHost host, string packageId)
    {
        string bundled = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "BundledMods")) + Path.DirectorySeparatorChar;
        Assert.All(host.Mods.Catalog.Packages, package => Assert.True(package.Id == packageId || Path.GetFullPath(package.RootDirectory).StartsWith(bundled, StringComparison.OrdinalIgnoreCase)));
    }

    private static byte[] Read(JunctionReplacement replacement)
    {
        using Stream stream = replacement.OpenRead();
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static bool IsTexture(string path) => Normalize(path).StartsWith("mods/textures/", StringComparison.Ordinal) && Path.GetExtension(path).ToLowerInvariant() is ".dds" or ".png";
    private static string Normalize(string path) => path.Replace('\\', '/').ToLowerInvariant();
    private static string FileHash(string path)
    {
        using Stream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void Evidence(string filename, object value)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(root))
        {
            string directory = Path.Combine(root, "VisualPackage");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, filename), JsonSerializer.Serialize(value, JsonOptions));
        }
    }

    private sealed record CoverageCase(Dictionary<string, int> Options, ExpectedFile[] Rows);
    private sealed record ExpectedFile(string Source, string Logical, bool Winner, int Bytes);

    private sealed class Inputs : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-visual-" + Guid.NewGuid().ToString("N"));
        private readonly IsolatedFieldGame game;
        private readonly string archiveHash;
        private readonly string workingArchive;
        public string Archive { get; }
        public string PackageId => "Junction/candidate.iroj";
        public string GameDirectory => game.GameDirectory;
        public CoverageCase[] Cases { get; }

        public Inputs()
        {
            string? catalogPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_VISUAL_PACKAGE_FIXTURE");
            Assert.SkipUnless(File.Exists(catalogPath) && Directory.Exists(TestResources.GameDirectory), "Configure an optional visual package fixture catalog and installed game source.");
            Dictionary<string, string> catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalogPath!))!;
            string fixtureRoot = Path.GetDirectoryName(Path.GetFullPath(catalogPath!))!;
            Archive = Path.GetFullPath(catalog["junction-visual-package"], fixtureRoot);
            archiveHash = FileHash(Archive);
            Cases = JsonSerializer.Deserialize<CoverageCase[]>(File.ReadAllText(Path.GetFullPath(catalog["junction-visual-coverage"], fixtureRoot)), JsonOptions)!;
            Assert.NotEmpty(Cases);
            workingArchive = Path.Combine(root, "Junction", "candidate.iroj");
            Directory.CreateDirectory(Path.GetDirectoryName(workingArchive)!);
            File.Copy(Archive, workingArchive);
            game = new IsolatedFieldGame();
        }

        public void Select(Dictionary<string, int> options) => File.WriteAllText(workingArchive + ".ini", "enabled = 1" + Environment.NewLine + string.Join(Environment.NewLine, options.Select(option => $"option.{option.Key} = {option.Value}")));
        public GameResourceHost Open() => new(GameDirectory, watch: false, modsDirectory: root);

        public GameResourceHost OpenOriginal()
        {
            string empty = Path.Combine(root, "empty-mods");
            Directory.CreateDirectory(empty);
            return new(GameDirectory, watch: false, modsDirectory: empty);
        }

        public void Dispose()
        {
            try
            {
                Assert.Equal(archiveHash, FileHash(Archive));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, true);
                    }
                }
                finally
                {
                    game.Dispose();
                }
            }
        }
    }
}
