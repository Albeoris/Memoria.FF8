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
public sealed class DemasterFieldModelInstalledTests
{
    [Fact]
    public void RegisteredInstalledModelMaterialsComposePackagesAndRestoreCpuGpuAndStandaloneState()
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
        NativeFieldRegistrationState registration = Assert.IsType<NativeFieldRegistrationState>(world.Events.Scene.NativeFieldRegistration);
        List<(int Model, ResourceTextureList Textures)> models = registration.Resources.Select(resource => (resource.ModelId, Assert.IsType<ResourceTextureList>(field.Models[resource.ModelId].Textures))).ToList();
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
        Evidence("field-model-native-materials.json", NativeMaterialEvidence(field, registration, world.Events.Scene.NativeEyeProcess));
        foreach (string package in fixture.PackageIds)
        {
            Assert.True(models.Any(model => Enumerable.Range(0, model.Textures.Count).Any(index => model.Textures.GetTexture(index).IsReplacement && model.Textures.GetTexture(index).Provenance.Contains(package + "/", StringComparison.Ordinal))), $"No registered material consumes configured package {package}.");
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
        Assert.All(models, model => Assert.All(Enumerable.Range(0, model.Textures.Count), index => Assert.False(model.Textures.GetTexture(index).IsReplacement)));
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
        VerifyEyeCopies(fixture.Host, field, registration, world.Events.Scene.NativeEyeProcess);
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
        Evidence("field-model-rendering.json", new { fieldId = field.Id, frame = world.Events.Frame, materials, layers, enabledCpu = Hash(enabled), disabledCpu = Hash(disabled), enabledGpu = Hash(enabledGpu), disabledGpu = Hash(disabledGpu), stateHash, checkpointUnchanged = true, restoredWaits = 1 });

        void CaptureGpu(string filename)
        {
            if (EvidenceDirectory() is { } directory)
            {
                using FileStream stream = File.Create(Path.Combine(directory, filename));
                gpu.Target.SaveAsPng(stream, 640, 480);
            }
        }
    }

    private static object[] NativeMaterialEvidence(FieldAssets field, NativeFieldRegistrationState registration, NativeFieldEyeProcess? process)
    {
        if (process?.Memory.EffectMemory?.Textures is not { } textures)
        {
            return [];
        }
        byte[] bank = textures.ReadVram();
        List<object> report = [];
        foreach (NativeFieldRegisteredResource resource in registration.Resources)
        {
            FieldModel model = field.Models[resource.ModelId];
            if (model.NativeEyes is not { Texture: { Depth: 1 } original } eyes || resource.Textures.Length == 0 || model.NativeTexturePalettes.Count == 0)
            {
                continue;
            }
            NativeFieldRegisteredTexture registered = resource.Textures[0];
            byte[] actual = new byte[registered.WidthWords * registered.Height * 2];
            for (int row = 0; row < registered.Height; row++)
            {
                bank.AsSpan(((registered.Y + row) * 1024 + registered.X) * 2, registered.WidthWords * 2).CopyTo(actual.AsSpan(row * registered.WidthWords * 2));
            }
            int mismatch = Enumerable.Range(0, Math.Min(actual.Length, original.Words.Length) / 2).FirstOrDefault(word => !actual.AsSpan(word * 2, 2).SequenceEqual(original.Words.AsSpan(word * 2, 2)), -1);
            byte[] palette = bank.AsSpan((registered.PaletteY * 1024 + registered.PaletteX) * 2, 512).ToArray();
            NativeEyeCopy? open = eyes.Copy(false);
            NativeEyeCopy? closed = eyes.Copy(true);
            report.Add(new
            {
                resource.ModelId,
                model.TextureResourceName,
                registered,
                originalWordHash = Hash(original.Words),
                actualWordHash = Hash(actual),
                originalPaletteHash = Hash(model.NativeTexturePalettes[0].AsSpan().Slice(0, 512).ToArray()),
                actualPaletteHash = Hash(palette),
                firstWordMismatch = mismatch,
                firstMismatchXWords = mismatch < 0 ? -1 : mismatch % registered.WidthWords,
                firstMismatchY = mismatch < 0 ? -1 : mismatch / registered.WidthWords,
                open = open is null ? null : new { open.SourceXWords, open.SourceY, open.DestinationXWords, open.DestinationY, open.WidthWords, open.Height },
                closed = closed is null ? null : new { closed.SourceXWords, closed.SourceY, closed.DestinationXWords, closed.DestinationY, closed.WidthWords, closed.Height }
            });
        }
        return report.ToArray();
    }
    private static void VerifyEyeCopies(GameResourceHost host, FieldAssets field, NativeFieldRegistrationState registration, NativeFieldEyeProcess? process)
    {
        Assert.NotNull(process);
        using GameAssets assets = new(GameDirectory(), resources: host.Resources);
        bool verified = false;
        foreach (NativeFieldRegisteredInstance instance in registration.Instances.Where(instance => instance.ModelId >= 0))
        {
            FieldModel model = field.Models[instance.ModelId];
            if (model.Textures is not ResourceTextureList textures || !Enumerable.Range(0, textures.Count).Any(index => textures.GetTexture(index).Variants?.ContainsKey("eyes-closed") == true) || model.NativeEyes?.Copy(true) is not { } patch || model.NativeEyes.Copy(false) is null)
            {
                continue;
            }
            NativeBattleFieldResourceReloadResult open = NativeFieldRegistrationMachine.SetEyes532340(registration, process!.Memory, instance.ObjectId, 0x100000, null, process.CrtRandom);
            NativeBattleFieldResourceReloadResult closed = NativeFieldRegistrationMachine.SetEyes532340(registration, open.Memory, instance.ObjectId, 0x200000, null, open.CrtRandomAfter);
            PixelImage[] openImages = Images(open.Memory);
            PixelImage[] closedImages = Images(closed.Memory);
            Assert.Equal(openImages.Length, closedImages.Length);
            Assert.False(openImages[0].Pixels.AsSpan().SequenceEqual(closedImages[0].Pixels));
            NativeEyeTexture nativeTexture = model.NativeEyes.Texture!;
            int nativeWidth = nativeTexture.WidthWords * 2;
            Assert.True(openImages[0].Width > nativeWidth && closedImages[0].Width > nativeWidth, "Both original eye command states must retain HD texture density.");
            Assert.Equal(openImages[0].Width, closedImages[0].Width);
            Assert.Equal(openImages[0].Height, closedImages[0].Height);
            for (int y = 0; y < openImages[0].Height; y++)
            {
                for (int x = 0; x < openImages[0].Width; x++)
                {
                    if (openImages[0][x, y].Equals(closedImages[0][x, y]))
                    {
                        continue;
                    }
                    int nativeX = x * nativeWidth / openImages[0].Width;
                    int nativeY = y * nativeTexture.Height / openImages[0].Height;
                    Assert.InRange(nativeX, patch.DestinationXWords * 2, (patch.DestinationXWords + patch.WidthWords) * 2 - 1);
                    Assert.InRange(nativeY, patch.DestinationY, patch.DestinationY + patch.Height - 1);
                }
            }
            Assert.All(Enumerable.Range(1, openImages.Length - 1), index => Assert.Equal(openImages[index].Pixels, closedImages[index].Pixels));
            NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(closed.Memory))!;
            PixelImage[] restoredImages = Images(restored);
            Assert.All(Enumerable.Range(0, closedImages.Length), index => Assert.Equal(closedImages[index].Pixels, restoredImages[index].Pixels));
            NativeBattleFieldResourceReloadResult next = NativeFieldRegistrationMachine.SetEyes532340(registration, restored, instance.ObjectId, 0x100000, null, closed.CrtRandomAfter);
            PixelImage[] nextImages = Images(next.Memory);
            Assert.All(Enumerable.Range(0, openImages.Length), index => Assert.Equal(openImages[index].Pixels, nextImages[index].Pixels));
            Assert.Equal(open.CrtRandomAfter, closed.CrtRandomAfter);
            Assert.Equal(closed.CrtRandomAfter, next.CrtRandomAfter);
            Evidence("field-model-eye-copies.json", new { fieldId = field.Id, instance.ObjectId, instance.ModelId, model.TextureResourceName, patch.SourceXWords, patch.SourceY, patch.DestinationXWords, patch.DestinationY, patch.WidthWords, patch.Height, nativeSourceHash = patch.ModelHash, producer = "Registered original 532340 eye commands with actual VRAM copy and JSON restore" });
            verified = true;
            break;

            PixelImage[] Images(NativeBattleRetainedMemory memory)
            {
                FieldModel bound = assets.BindNativeRegisteredTextures(field, registration, memory.EffectMemory!.Textures!, memory.FieldTextureAnimations).Models[instance.ModelId];
                return bound.Textures.ToArray();
            }
        }
        Assert.True(verified, "The configured packages and standalone field must supply a registered participant with HD eye planes.");
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
