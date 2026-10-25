using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("Installed Junction resources")]
public sealed class DemasterBattleModelInstalledTests
{
    [Fact]
    public void InstalledModelSheetsReachNativeBodyAndLinkedWeaponMaterialsAndSurviveRestore()
    {
        string? state = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_MODEL_STATE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(state) && File.Exists(state), "Set MEMORIA_FF8_BATTLE_MODEL_STATE to an optional standalone battle state.");
        using InstalledPackages packages = new();
        GameResourceHost host = packages.Host;
        GameSession session = new(GameSession.Load(state!));
        using GameAssets game = new(GameDirectory(), resources: host.Resources);
        using GameWorld world = new(session, GameDirectory(), resources: host.Resources, resolveGameFile: host.ResolveGameFile, modelOverrides: host);
        world.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Bind(battle);
        battle.Tick();
        using BattleAssets assets = new(GameDirectory(), host.Resources);
        BattleSnapshot snapshot = battle.Capture();
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(battle.NativeGraphics);
        List<(BattleModel Model, NativeBattleModelTextureBinding Binding, bool Weapon)> materials = [];
        for (int index = 0; index < graphics.ActorIds.Length; index++)
        {
            if (graphics.ActorIds[index] is not string actorId || graphics.Models[index].BodyTextureSlots is not { } bodyBinding)
            {
                continue;
            }
            BattleCombatantDefinition actor = battle.GetCombatantDefinition(actorId);
            BattleModel body = actor.IsPlayer ? assets.LoadPlayer(actor.ModelId, actor.Costume) : assets.LoadEnemy(actor.ModelId);
            materials.Add((body, bodyBinding, false));
            if (actor.IsPlayer && actor.WeaponId is int weaponId && assets.ResolveWeaponModel(actor.ModelId, weaponId, body) is { } weapon && graphics.Models[index].LinkedTextureSlots is { } weaponBinding)
            {
                materials.Add((weapon, weaponBinding, true));
            }
        }
        Assert.Contains(materials, material => !material.Weapon);
        Assert.Contains(materials, material => material.Weapon);
        List<object> targetReport = [];
        int replaced = 0;
        foreach (var material in materials)
        {
            ResourceTextureList textures = Assert.IsType<ResourceTextureList>(material.Model.Textures);
            TextureValue[] enabled = Enumerable.Range(0, textures.Count).Select(textures.GetTexture).ToArray();
            NativeBattleTextureReplacement[] bindings = NativeBattleTextureReplacements.Bind(material.Model, material.Binding, material.Weapon).ToArray();
            bool hasReplacement = enabled.Any(value => value.IsReplacement);
            if (hasReplacement)
            {
                replaced++;
                Assert.NotEmpty(bindings);
                Assert.All(enabled.Where(value => value.IsReplacement), value => Assert.Contains(packages.PackageIds, id => value.Provenance.Contains(id, StringComparison.OrdinalIgnoreCase)));
            }
            packages.EnableAll(false);
            Assert.All(Enumerable.Range(0, textures.Count), index =>
            {
                TextureValue original = textures.GetTexture(index);
                Assert.False(original.IsReplacement);
                Assert.Equal(enabled[index].LogicalWidth, original.LogicalWidth);
                Assert.Equal(enabled[index].LogicalHeight, original.LogicalHeight);
                if (!enabled[index].IsReplacement)
                {
                    Assert.Equal(Signature(enabled[index].Image), Signature(original.Image));
                }
            });
            Assert.Empty(NativeBattleTextureReplacements.Bind(material.Model, material.Binding, material.Weapon));
            packages.EnableAll(true);
            Assert.All(Enumerable.Range(0, textures.Count), index => Assert.Equal(Signature(enabled[index].Image), Signature(textures.GetTexture(index).Image)));
            targetReport.Add(new { model = material.Model.Name, material.Weapon, replacement = hasReplacement, nativeBindings = bindings.Length, provenance = enabled.Where(value => value.IsReplacement).Select(value => value.Provenance).ToArray() });
        }
        Assert.True(replaced > 0, "The configured packages must replace an actual participant's body or linked weapon in the supplied standalone battle.");
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        byte[] enabledPixels = scene.Render(battle, 640, 448);
        string cpuEnabled = Signature(enabledPixels);
        CaptureCpu("battle-model-enabled-cpu.png", enabledPixels);
        Assert.Equal(cpuEnabled, Signature(scene.Render(battle, 640, 448)));
        packages.EnableAll(false);
        byte[] disabledPixels = scene.Render(battle, 640, 448);
        string cpuDisabled = Signature(disabledPixels);
        CaptureCpu("battle-model-disabled-cpu.png", disabledPixels);
        Assert.NotEqual(cpuDisabled, cpuEnabled);
        packages.EnableAll(true);
        Assert.Equal(cpuEnabled, Signature(scene.Render(battle, 640, 448)));
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        scene.Render(battle, 640, 448, gpu);
        string gpuEnabled = GpuSignature();
        Assert.Contains(GpuTestDevice.Read(gpu.Target), pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
        CaptureGpu("battle-model-enabled-gpu.png");
        packages.EnableAll(false);
        scene.Render(battle, 640, 448, gpu);
        string gpuDisabled = GpuSignature();
        Assert.NotEqual(gpuDisabled, gpuEnabled);
        CaptureGpu("battle-model-disabled-gpu.png");
        packages.EnableAll(true);
        scene.Render(battle, 640, 448, gpu);
        Assert.Equal(gpuEnabled, GpuSignature());
        BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Bind(clone);
        BattleScenePresentation restored = new(assets, snapshot.Definition, true);
        Assert.Equal(scene.Render(battle, 640, 448), restored.Render(clone, 640, 448));
        for (int tick = 0; tick < 8; tick++)
        {
            battle.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(battle, 640, 448), restored.Render(clone, 640, 448));
        }
        Assert.True(battle.NativeGraphics!.Callback > graphics.Callback, "The restored standalone battle must execute its next native callbacks.");
        Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "replacement-failed" or "demaster-import-failed");
        world.CaptureBattleState();
        string? evidence = EvidenceDirectory();
        if (evidence is not null)
        {
            session.Save(Path.Combine(evidence, "battle-model-validated.json"), compact: true);
            File.WriteAllText(Path.Combine(evidence, "battle-model-render.json"), JsonSerializer.Serialize(new { targets = targetReport, cpuEnabled, cpuDisabled, gpuEnabled, gpuDisabled, resumedTicks = 8, callbackBefore = graphics.Callback, callbackAfter = battle.NativeGraphics.Callback }, new JsonSerializerOptions { WriteIndented = true }));
        }

        void Bind(BattleSession selected)
        {
            selected.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        }

        string GpuSignature() => Signature(GpuTestDevice.Read(gpu.Target).SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray());

        void CaptureGpu(string filename)
        {
            if (EvidenceDirectory() is { } directory)
            {
                using FileStream stream = File.Create(Path.Combine(directory, filename));
                gpu.Target.SaveAsPng(stream, 640, 448);
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
            string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_MODEL_TEXTURE_PACKAGES");
            Assert.SkipUnless(!string.IsNullOrWhiteSpace(input), "Set MEMORIA_FF8_BATTLE_MODEL_TEXTURE_PACKAGES to an optional JSON package directory catalog.");
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
                    string settings = Path.Combine(directories[index], "mod.ini");
                    configurations.Add(settings, File.Exists(settings) ? File.ReadAllBytes(settings) : null);
                    File.WriteAllText(settings, $"enabled = 1{Environment.NewLine}priority = {index}{Environment.NewLine}");
                }
                opened = new(GameDirectory(), watch: false, modsDirectory: demaster.Parent!.FullName);
                Host = opened;
                PackageIds = directories.Select(directory => Assert.Single(Host.Mods.Catalog.Packages, package => package.RootDirectory.Equals(directory, StringComparison.OrdinalIgnoreCase)).Id).ToArray();
                Assert.DoesNotContain(Host.Mods.Catalog.Packages, package => package.Status == ModEffectiveStatus.Active && !PackageIds.Contains(package.Id) && package.Format is ModFormat.Demaster or ModFormat.Junction or ModFormat.Tonberry);
                Assert.All(PackageIds, id => Assert.Equal(ModEffectiveStatus.Active, Assert.Single(Host.Mods.Catalog.Packages, package => package.Id == id).Status));
            }
            catch
            {
                opened?.Dispose();
                Restore();
                throw;
            }
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

    private static void CaptureCpu(string filename, byte[] pixels)
    {
        if (EvidenceDirectory() is { } directory)
        {
            using var image = GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 640, 448);
            image.Save(Path.Combine(directory, filename));
        }
    }

    private static string Signature(Albeoris.Games.FF8.Imaging.PixelImage image) => Signature(image.Pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray());
    private static string Signature(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
