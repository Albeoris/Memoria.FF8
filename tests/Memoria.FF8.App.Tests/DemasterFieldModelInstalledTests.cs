using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class DemasterFieldModelInstalledTests
{
    [Fact]
    public void InstalledModelMaterialsComposePackagesAndRestoreCpuGpuAndStandaloneState()
    {
        string? checkpoint = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_MODEL_STATE");
        Assert.SkipUnless(File.Exists(checkpoint), "Set MEMORIA_FF8_FIELD_MODEL_STATE to an optional concrete standalone field save.");
        using InstalledPackages fixture = new();
        string originalFileHash = FileHash(checkpoint!);
        GameSession session = new(GameSession.Load(checkpoint!));
        using GameWorld world = new(session, GameDirectory(), resources: fixture.Host.Resources, resolveGameFile: fixture.Host.ResolveGameFile);
        Assert.True(world.EnsureField());
        world.SetRenderSize(640, 480);
        FieldAssets field = Assert.IsType<FieldAssets>(world.Field);

        IEnumerable<int> modelIds = world.Events.Actors.Where(actor => actor.Active && actor.ModelId >= 0).Select(actor => actor.ModelId).Distinct();
        List<(int Model, ResourceTextureList Textures)> models = modelIds.Select(modelId => (modelId, Assert.IsType<ResourceTextureList>(field.Models[modelId].Textures))).ToList();
        Assert.NotEmpty(models);
        List<object> materials = [];

        foreach (var model in models)
        {
            foreach (int index in Enumerable.Range(0, model.Textures.Count))
            {
                TextureValue value = model.Textures.GetTexture(index);
                materials.Add(new { model = model.Model, name = field.Models[model.Model].TextureResourceName, field.Models[model.Model].MainCharacterTextures, texture = index, value.IsReplacement, value.Provenance, value.LogicalWidth, value.LogicalHeight, physicalWidth = value.Image.Width, physicalHeight = value.Image.Height });
            }
        }
        Evidence("field-model-materials.json", new { field.Id, field.Name, materials });
        foreach (string package in fixture.PackageIds)
        {
            Assert.True(models.Any(model => Enumerable.Range(0, model.Textures.Count).Any(index => model.Textures.GetTexture(index).IsReplacement && model.Textures.GetTexture(index).Provenance.Contains(package + "/", StringComparison.Ordinal))), $"No actual participant material consumes configured package {package}.");
        }
        string stateHash = StateHash(session, world);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        byte[] enabled = world.Render(TimeSpan.Zero).ToArray();
        world.Render(TimeSpan.Zero, triangleRenderer: gpu);
        Color[] enabledGpu = GpuTestDevice.Read(gpu.Target);
        Capture("field-model-enabled-cpu.png", enabled);
        CaptureGpu("field-model-enabled-gpu.png");
        List<object> layers = [];
        foreach (string package in fixture.PackageIds.Reverse())
        {
            var selected = models.SelectMany(model => Enumerable.Range(0, model.Textures.Count).Select(index => (model.Model, Index: index, Textures: model.Textures, Value: model.Textures.GetTexture(index)))).Where(item => item.Value.Provenance.Contains(package + "/", StringComparison.Ordinal)).ToArray();
            fixture.Enable(package, false);
            Assert.All(selected, item => Assert.DoesNotContain(package + "/", item.Textures.GetTexture(item.Index).Provenance, StringComparison.Ordinal));
            layers.Add(new { package, replacedMaterials = selected.Length, fallbacks = selected.Select(item => new { model = item.Model, texture = item.Index, provenance = item.Textures.GetTexture(item.Index).Provenance }).ToArray() });
            fixture.Enable(package, true);
            Assert.All(selected, item => Assert.Equal(item.Value.Provenance, item.Textures.GetTexture(item.Index).Provenance));
        }
        fixture.EnableAll(false);
        Assert.All(models, model => Assert.All(Enumerable.Range(0, model.Textures.Count), index => Assert.DoesNotContain(fixture.PackageIds, package => model.Textures.GetTexture(index).Provenance.Contains(package + "/", StringComparison.Ordinal))));
        byte[] disabled = world.Render(TimeSpan.Zero).ToArray();
        world.Render(TimeSpan.Zero, triangleRenderer: gpu);
        Color[] disabledGpu = GpuTestDevice.Read(gpu.Target);
        Assert.NotEqual(Hash(enabled), Hash(disabled));
        Assert.False(enabledGpu.SequenceEqual(disabledGpu));
        Capture("field-model-disabled-cpu.png", disabled);
        CaptureGpu("field-model-disabled-gpu.png");
        fixture.EnableAll(true);
        Assert.Equal(enabled, world.Render(TimeSpan.Zero));
        world.Render(TimeSpan.Zero, triangleRenderer: gpu);
        Assert.Equal(enabledGpu, GpuTestDevice.Read(gpu.Target));
        Assert.Equal(stateHash, StateHash(session, world));
        Evidence("field-model-eye-copies.json", new { fieldId = field.Id, performed = false, reason = "Actual Remastered native field eye-plane selection and timing are not established by this material/render acceptance." });
        session.SetEventState(world.Events.Capture());
        using GameWorld restored = new(new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!), GameDirectory(), resources: fixture.Host.Resources, resolveGameFile: fixture.Host.ResolveGameFile);
        Assert.True(restored.EnsureField());
        restored.SetRenderSize(640, 480);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), restored.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(originalFileHash, FileHash(checkpoint!));
        Assert.DoesNotContain(fixture.Host.Mods.Diagnostics, diagnostic => diagnostic.Code is "replacement-failed" or "demaster-import-failed");
        session.SetEventState(world.Events.Capture());
        if (EvidenceDirectory() is { } directory)
        {
            session.Save(Path.Combine(directory, "field-model-validated.json"), compact: true);
        }
        Evidence("field-model-rendering.json", new { edition = fixture.Host.Resources.Source.Edition.ToString(), fieldId = field.Id, frame = world.Events.Frame, nativeRegistration = world.Events.Scene.NativeFieldRegistration is not null, materials, layers, enabledCpu = Hash(enabled), disabledCpu = Hash(disabled), enabledGpu = Hash(enabledGpu), disabledGpu = Hash(disabledGpu), stateHash, checkpointUnchanged = true, restoredWaits = 1 });

        void CaptureGpu(string filename)
        {
            if (EvidenceDirectory() is { } directory)
            {
                using FileStream stream = File.Create(Path.Combine(directory, filename));
                gpu.Target.SaveAsPng(stream, 640, 480);
            }
        }
    }

    private sealed class InstalledPackages : IDisposable
    {
        private readonly Dictionary<string, byte[]?> configurations = [];
        public GameResourceHost Host { get; }
        public string[] PackageIds { get; }

        public InstalledPackages()
        {
            string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_MODEL_TEXTURE_PACKAGES");
            Assert.SkipUnless(!string.IsNullOrWhiteSpace(input), "Set MEMORIA_FF8_FIELD_MODEL_TEXTURE_PACKAGES to an optional JSON package directory catalog.");
            bool catalogFile = File.Exists(input);
            string[] directories = JsonSerializer.Deserialize<string[]>(catalogFile ? File.ReadAllText(input!) : input!) ?? throw new InvalidDataException("Expected a JSON package directory array.");
            Assert.NotEmpty(directories);
            string relativeBase = catalogFile ? Path.GetDirectoryName(Path.GetFullPath(input!))! : Environment.CurrentDirectory;
            directories = directories.Select(path => Path.GetFullPath(path, relativeBase)).ToArray();
            DirectoryInfo demaster = Directory.GetParent(directories[0]) ?? throw new InvalidDataException("Missing Demaster package parent.");
            Assert.Equal("Demaster", demaster.Name, ignoreCase: true);
            Assert.All(directories, path => Assert.Equal(demaster.FullName, Directory.GetParent(path)!.FullName, ignoreCase: true));
            GameResourceHost? opened = null;
            try
            {
                for (int index = 0; index < directories.Length; index++)
                {
                    string config = Path.Combine(directories[index], "mod.ini");
                    configurations.Add(config, File.Exists(config) ? File.ReadAllBytes(config) : null);
                    File.WriteAllText(config, $"enabled = 1{Environment.NewLine}priority = {index}{Environment.NewLine}");
                }
                opened = new(GameDirectory(), watch: false, modsDirectory: demaster.Parent!.FullName);
                Host = opened;
                PackageIds = directories.Select(directory => Assert.Single(Host.Mods.Catalog.Packages, package => package.RootDirectory.Equals(directory, StringComparison.OrdinalIgnoreCase)).Id).ToArray();
                Assert.All(PackageIds, id => Assert.Equal(ModEffectiveStatus.Active, Assert.Single(Host.Mods.Catalog.Packages, package => package.Id == id).Status));
                Assert.DoesNotContain(Host.Mods.Catalog.Packages, package => package.Status == ModEffectiveStatus.Active && !PackageIds.Contains(package.Id) && package.Format is ModFormat.Demaster or ModFormat.Junction or ModFormat.Tonberry);
            }
            catch
            {
                opened?.Dispose();
                Restore();
                throw;
            }
        }

        public void Enable(string id, bool enabled)
        {
            Assert.True(Host.Mods.SetEnabled(id, enabled).Success);
            Assert.True(Host.Pump(force: true));
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

    private static string GameDirectory()
    {
        string? configured = Environment.GetEnvironmentVariable("FF8_GAME_DIR");
        return !string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured) ? configured : TestResources.GameDirectory;
    }

    private static string StateHash(GameSession session, GameWorld world) => Hash(JsonSerializer.SerializeToUtf8Bytes(new { session.State, Events = world.Events.Capture() }));
    private static string FileHash(string path) => Hash(File.ReadAllBytes(path));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Hash(Color[] pixels) => Hash(pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray());

    private static string? EvidenceDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void Evidence(string filename, object value)
    {
        if (EvidenceDirectory() is { } directory)
        {
            File.WriteAllText(Path.Combine(directory, filename), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static void Capture(string filename, byte[] pixels)
    {
        if (EvidenceDirectory() is { } directory)
        {
            using var image = GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 640, 480);
            image.Save(Path.Combine(directory, filename));
        }
    }
}
