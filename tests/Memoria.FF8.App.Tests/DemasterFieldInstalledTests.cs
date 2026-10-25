using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImageMagick;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class DemasterFieldInstalledTests
{
    [Fact]
    public void InstalledCheckpointPresentationChangesAndRestoresWithoutAdvancingGameplay()
    {
        string? checkpoint = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_STATE");
        Assert.SkipUnless(File.Exists(checkpoint), "Set MEMORIA_FF8_FIELD_STATE to a concrete standalone field save.");
        using InstalledPackages fixture = new();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        string originalFileHash = FileHash(checkpoint!);
        GameSession session = new(GameSession.Load(checkpoint!));
        using GameWorld world = new(session, TestResources.GameDirectory, resources: fixture.Host.Resources, resolveGameFile: fixture.Host.ResolveGameFile);
        Assert.True(world.EnsureField());
        world.SetRenderSize(640, 480);
        Assert.NotNull(world.Field);
        Assert.Contains(world.Field!.Background, tile => tile.Texture?.Value.IsReplacement == true);
        string stateHash = StateHash(session, world);
        byte[] enabled = world.Render(TimeSpan.Zero).ToArray();
        world.Render(TimeSpan.Zero, triangleRenderer: gpu);
        Color[] enabledGpu = GpuTestDevice.Read(gpu.Target);
        Capture("field-checkpoint-enabled-cpu.png", enabled, 640, 480);
        Capture("field-checkpoint-enabled-gpu.png", enabledGpu, 640, 480);
        fixture.EnableAll(false);
        byte[] disabled = world.Render(TimeSpan.Zero).ToArray();
        world.Render(TimeSpan.Zero, triangleRenderer: gpu);
        Color[] disabledGpu = GpuTestDevice.Read(gpu.Target);
        Assert.NotEqual(Hash(enabled), Hash(disabled));
        Assert.False(enabledGpu.SequenceEqual(disabledGpu));
        Capture("field-checkpoint-disabled-cpu.png", disabled, 640, 480);
        Capture("field-checkpoint-disabled-gpu.png", disabledGpu, 640, 480);
        fixture.EnableAll(true);
        Assert.Equal(enabled, world.Render(TimeSpan.Zero));
        world.Render(TimeSpan.Zero, triangleRenderer: gpu);
        Assert.Equal(enabledGpu, GpuTestDevice.Read(gpu.Target));
        Assert.Equal(stateHash, StateHash(session, world));
        Assert.Equal(originalFileHash, FileHash(checkpoint!));
        Evidence("field-checkpoint-rendering.json", new { session.State.FieldId, frame = world.Events.Frame, enabledCpu = Hash(enabled), disabledCpu = Hash(disabled), enabledGpu = Hash(enabledGpu), disabledGpu = Hash(disabledGpu), stateHash, checkpointUnchanged = true });
    }

    [Fact]
    public void InstalledAddonWinsChangedNativeFieldPageAndDisablingRestoresBaseRendering()
    {
        using InstalledPackages fixture = new();
        Assert.True(fixture.PackageIds.Length >= 2, "The acceptance catalog must list a base package followed by an addon.");
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using GameAssets assets = new(TestResources.GameDirectory, resources: fixture.Host.Resources);
        string[] names = Encoding.ASCII.GetString(assets.ReadResource(new("field/mapdata/maplist"))).Replace("\r", "").Split('\n');
        string? selectedField = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PATCH_FIELD");
        int? selectedId = int.TryParse(selectedField, out int explicitId) ? explicitId : null;
        string baseId = fixture.PackageIds[0];
        string addonId = fixture.PackageIds[^1];
        ModFile[] changedFiles = fixture.Host.Mods.Catalog.Files.Where(file => file.PackageId == addonId && file.RelativePath.StartsWith("textures/field_bg/", StringComparison.Ordinal) && Path.GetExtension(file.RelativePath).ToLowerInvariant() is ".dds" or ".png").Where(file =>
        {
            ModFile? original = fixture.Host.Mods.Catalog.FindAll(ModFormat.Demaster, file.RelativePath).FirstOrDefault(candidate => candidate.PackageId == baseId);
            return original is not null && FileHash(file) != FileHash(original);
        }).ToArray();
        Assert.All(changedFiles, file => Assert.Equal(addonId, fixture.Host.Mods.Catalog.FindAll(ModFormat.Demaster, file.RelativePath)[0].PackageId));
        string[] changedMaps = changedFiles.Select(file => file.RelativePath.Split('/')).Where(parts => parts.Length == 5).Select(parts => parts[3]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.NotEmpty(changedMaps);
        bool verified = false;
        foreach (string name in changedMaps)
        {
            int fieldId = Array.FindIndex(names, candidate => candidate.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (fieldId < 0 || selectedId is int id && id != fieldId)
            {
                continue;
            }
            FieldAssets field = assets.LoadField(fieldId);
            BackgroundTile[] addonTiles = field.Background.Where(tile => tile.Texture?.Value.Provenance.StartsWith(addonId + "/", StringComparison.Ordinal) == true).ToArray();
            if (addonTiles.Length == 0)
            {
                continue;
            }
            Dictionary<int, int> states = field.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => tile.AnimationId).ToDictionary(group => group.Key, group => group.First().AnimationState);
            // Prefer the source MAP state addressed by the actual addon, when animated.
            foreach (BackgroundTile tile in addonTiles.Where(tile => tile.Draw && tile.AnimationId != 255))
            {
                states[tile.AnimationId] = tile.AnimationState;
            }
            FieldRenderer renderer = new(field, 640, 480);
            byte[] addon = renderer.Render(states, [], TimeSpan.Zero).ToArray();
            renderer.TriangleRenderer = gpu;
            renderer.Render(states, [], TimeSpan.Zero);
            Color[] addonGpu = GpuTestDevice.Read(gpu.Target);
            fixture.Enable(addonId, false);
            Assert.Contains(addonTiles, tile => tile.Texture!.Value.Provenance.StartsWith(baseId + "/", StringComparison.Ordinal));
            renderer.TriangleRenderer = null;
            byte[] original = renderer.Render(states, [], TimeSpan.Zero).ToArray();
            renderer.TriangleRenderer = gpu;
            renderer.Render(states, [], TimeSpan.Zero);
            Color[] originalGpu = GpuTestDevice.Read(gpu.Target);
            fixture.Enable(addonId, true);
            if (Hash(addon) == Hash(original) || addonGpu.SequenceEqual(originalGpu))
            {
                continue;
            }
            renderer.TriangleRenderer = null;
            Assert.Equal(addon, renderer.Render(states, [], TimeSpan.Zero));
            renderer.TriangleRenderer = gpu;
            renderer.Render(states, [], TimeSpan.Zero);
            Assert.Equal(addonGpu, GpuTestDevice.Read(gpu.Target));
            Capture("field-addon-enabled-cpu.png", addon, 640, 480);
            Capture("field-addon-base-cpu.png", original, 640, 480);
            Capture("field-addon-enabled-gpu.png", addonGpu, 640, 480);
            Capture("field-addon-base-gpu.png", originalGpu, 640, 480);
            Evidence("field-addon-rendering.json", new { fieldId, fieldName = name, addonCpu = Hash(addon), baseCpu = Hash(original), addonGpu = Hash(addonGpu), baseGpu = Hash(originalGpu), states, addonTileCount = addonTiles.Length });
            verified = true;
            break;
        }
        Assert.True(verified, "No configured changed addon field produced a visible native MAP tile difference in both CPU and GPU rendering.");
    }

    private sealed class InstalledPackages : IDisposable
    {
        private readonly Dictionary<string, byte[]?> configurations = [];
        public GameResourceHost Host { get; }
        public string[] PackageIds { get; }

        public InstalledPackages()
        {
            string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_TEXTURE_PACKAGES");
            Assert.SkipUnless(!string.IsNullOrWhiteSpace(input), "Set MEMORIA_FF8_FIELD_TEXTURE_PACKAGES to an external JSON package directory catalog.");
            bool catalogFile = File.Exists(input);
            string[] directories = JsonSerializer.Deserialize<string[]>(catalogFile ? File.ReadAllText(input!) : input!) ?? throw new InvalidDataException("Expected a JSON package directory array.");
            Assert.NotEmpty(directories);
            string relativeBase = catalogFile ? Path.GetDirectoryName(Path.GetFullPath(input!))! : Environment.CurrentDirectory;
            directories = directories.Select(path => Path.GetFullPath(path, relativeBase)).ToArray();
            DirectoryInfo demaster = Directory.GetParent(directories[0]) ?? throw new InvalidDataException("Missing Demaster package parent.");
            Assert.Equal("Demaster", demaster.Name, ignoreCase: true);
            Assert.All(directories, path => Assert.Equal(demaster.FullName, Directory.GetParent(path)!.FullName, ignoreCase: true));
            string mods = demaster.Parent?.FullName ?? throw new InvalidDataException("Missing Mods directory.");
            GameResourceHost? openedHost = null;
            try
            {
                for (int index = 0; index < directories.Length; index++)
                {
                    string config = Path.Combine(directories[index], "mod.ini");
                    configurations.Add(config, File.Exists(config) ? File.ReadAllBytes(config) : null);
                    File.WriteAllText(config, $"enabled = 1{Environment.NewLine}priority = {index}{Environment.NewLine}");
                }
                openedHost = new(TestResources.GameDirectory, watch: false, modsDirectory: mods);
                Host = openedHost;
                PackageIds = directories.Select(directory => Assert.Single(Host.Mods.Catalog.Packages, package => Path.GetFullPath(package.RootDirectory).Equals(directory, StringComparison.OrdinalIgnoreCase)).Id).ToArray();
                Assert.All(PackageIds, id => Assert.Equal(ModEffectiveStatus.Active, Assert.Single(Host.Mods.Catalog.Packages, package => package.Id == id).Status));
            }
            catch
            {
                try
                {
                    openedHost?.Dispose();
                }
                finally
                {
                    Restore();
                }
                throw;
            }
        }

        public void Enable(string id, bool enabled)
        {
            Assert.True(Host.Mods.SetEnabled(id, enabled).Success);
            Assert.True(Host.Pump(force: true));
            Assert.Equal(enabled ? ModEffectiveStatus.Active : ModEffectiveStatus.Disabled, Assert.Single(Host.Mods.Catalog.Packages, package => package.Id == id).Status);
        }

        public void EnableAll(bool enabled)
        {
            foreach (string id in PackageIds)
            {
                Assert.True(Host.Mods.SetEnabled(id, enabled).Success);
            }
            Assert.True(Host.Pump(force: true));
        }

        public void Dispose()
        {
            try
            {
                Host.Dispose();
            }
            finally
            {
                Restore();
            }
        }

        private void Restore()
        {
            foreach ((string path, byte[]? bytes) in configurations)
            {
                if (bytes is null)
                {
                    File.Delete(path);
                }
                else
                {
                    File.WriteAllBytes(path, bytes);
                }
            }
        }
    }

    private static string StateHash(GameSession session, GameWorld world)
    {
        using SHA256 hash = SHA256.Create();
        using CryptoStream stream = new(Stream.Null, hash, CryptoStreamMode.Write);
        JsonSerializer.Serialize(stream, new { session.State, Events = world.Events.Capture() });
        stream.FlushFinalBlock();
        return Convert.ToHexString(hash.Hash!);
    }

    private static string FileHash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string FileHash(ModFile file)
    {
        using SHA256 hash = SHA256.Create();
        using CryptoStream stream = new(Stream.Null, hash, CryptoStreamMode.Write);
        file.CopyTo(stream);
        stream.FlushFinalBlock();
        return Convert.ToHexString(hash.Hash!);
    }

    private static string Hash(byte[] pixels) => Convert.ToHexString(SHA256.HashData(pixels));
    private static string Hash(Color[] pixels) => Hash(pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray());

    private static void Capture(string name, Color[] pixels, int width, int height) => Capture(name, pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray(), width, height);

    private static void Capture(string name, byte[] pixels, int width, int height)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            using MagickImage image = new(pixels, new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.RGBA));
            image.Write(Path.Combine(directory, name), MagickFormat.Png);
        }
    }

    private static void Evidence(string name, object value)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
