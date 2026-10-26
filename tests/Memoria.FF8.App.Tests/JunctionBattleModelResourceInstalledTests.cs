using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class JunctionBattleModelResourceInstalledTests
{
    [Fact]
    public void SelectedBodiesReachNativeFileStagingCallbacksLinkedSkeletonsAndBothRenderers()
    {
        using PackageFixture fixture = new();
        using GameResourceHost host = fixture.Open();
        using BattleAssets assets = new(TestResources.GameDirectory, host.Resources);
        Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code == "Junction.InvalidEncodedResource");
        Dictionary<string, BattleModel> models = [];
        List<object> evidence = [];
        foreach ((string name, byte[] expected) in fixture.Resources)
        {
            int character = int.Parse(name.AsSpan(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            Assert.Equal(expected, host.Resources.Read(new("battle/" + name)));
            BattleModel model = assets.LoadModel(name);
            BattleModel player = assets.LoadPlayer(character);
            Assert.Equal(name, player.Name, ignoreCase: true);
            Assert.Equal(Geometry(model), Geometry(player));
            Assert.NotEmpty(model.Meshes);
            Assert.NotEmpty(model.Animations);
            models.Add(name, model);
            int weaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == character).Id;
            NativeBattleGraphicsResource graphics = assets.ReadNativeGraphicsResource(new() { Id = "player", IsPlayer = true, ModelId = character, WeaponId = weaponId });
            NativeBattleModelDatSource body = Assert.Single(graphics.ModelLoadSources!.Value, source => !source.Linked);
            Assert.Equal(name, body.Name, ignoreCase: true);
            Assert.Equal(expected, body.Bytes.ToArray());
            Assert.Equal(expected, assets.ReadNativeBattleFile(checked((ushort)body.FileId)).Bytes.ToArray());
            Assert.Equal(expected, assets.ReadNativeModelDatSource((byte)character, false, body.Selector).Bytes.ToArray());
            Assert.Equal(JsonSerializer.Serialize(model.NativePoseSource), JsonSerializer.Serialize(graphics.PoseSource));
            Assert.Equal(model.Animations.Select(animation => (byte)animation.Frames.Count), graphics.AnimationFrames);
            BattleModel? linked = assets.ResolveWeaponModel(character, weaponId, model);
            if (linked is not null)
            {
                Assert.NotEmpty(linked.Meshes);
                Assert.Equal(JsonSerializer.Serialize(linked.NativePoseSource), JsonSerializer.Serialize(graphics.LinkedPoseSource));
                if (linked.NativeWeaponUsesBodySkeleton == true)
                {
                    Assert.Equal(model.Bones, linked.Bones);
                }
            }
            NativeBattleModelLoadStep requested = NativeBattleModelLoader.Queue507080((ushort)character, 0, 0, 0, body.Selector).Poll507070(-1, [body], NativeBattleFileStaging.Unknown());
            Assert.Equal(expected, requested.Staging.Read(NativeBattleFileStaging.Address, expected.Length));
            NativeBattleModelLoadStep prepared = requested.State.Poll507070(expected.Length, [body], requested.Staging);
            Assert.NotNull(prepared.TextureUpload);
            NativeBattleModelLoader restored = JsonSerializer.Deserialize<NativeBattleModelLoader>(JsonSerializer.Serialize(prepared.State))!;
            NativeBattleFileStaging restoredStaging = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(prepared.Staging))!;
            NativeBattleModelLoadStep complete = prepared.State.Poll507070(expected.Length, [body], prepared.Staging);
            NativeBattleModelLoadStep resumed = restored.Poll507070(expected.Length, [body], restoredStaging);
            Assert.Equal(JsonSerializer.Serialize(complete), JsonSerializer.Serialize(resumed));
            Assert.NotNull(complete.Bind);
            Assert.Equal(0, complete.Remaining);
            NativeBattleModelCpuResources coldCpu = NativeBattleModelCpuResources.FromColdProcess();
            NativeBattleModelTextureCacheLoad cache = NativeBattleModelTextureCache.FromColdProcess().Reset508260().LoadPlayer((byte)character, 0, complete.Bind!.OriginalTexturePack!);
            NativeBattleModelResourceCompletion initialized = NativeBattleModelResourceCopies.Complete(complete.Bind, coldCpu.Cpu, coldCpu.Rows, cache.ResourceIndex, null);
            Assert.NotNull(initialized.Rows[cache.ResourceIndex].Require(12));
            Assert.NotNull(initialized.Rows[cache.ResourceIndex].Require(16));
            Assert.NotNull(initialized.Rows[cache.ResourceIndex].Require(20));
            Assert.NotNull(initialized.AnimationCopy);
            Assert.Equal(JsonSerializer.Serialize(initialized), JsonSerializer.Serialize(NativeBattleModelResourceCopies.Complete(resumed.Bind!, coldCpu.Cpu, coldCpu.Rows, cache.ResourceIndex, null)));
            NativeBattleEffectTextureState uploaded = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), prepared.TextureUpload!).Drain505D20().State;
            NativeBattleTextureCopy open = Assert.IsType<NativeBattleTextureCopy>(NativeBattleTextureMotion.Plan50C780(graphics.TextureAnimations, prepared.TextureUpload!.AllocatedSlots, 0, 0));
            NativeBattleTextureCopy closed = Assert.IsType<NativeBattleTextureCopy>(NativeBattleTextureMotion.Plan50C780(graphics.TextureAnimations, prepared.TextureUpload.AllocatedSlots, 0, 1));
            NativeBattleEffectTextureState copied = Copy(Copy(uploaded, open), closed);
            NativeBattleEffectTextureState restoredTexture = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(copied))!;
            Assert.Equal(Copy(copied, open).ReadVram(), Copy(restoredTexture, open).ReadVram());
            evidence.Add(new { name, character, sha256 = Hash(expected), body.FileId, body.Selector, geometry = Geometry(model), animations = model.Animations.Count, bones = model.Bones.Count, linked = linked?.Name, open, closed, loaderCallback = prepared.State.Task!.Callback, resourceCopiesCompleted = true, snapshotNextCallback = true });
        }
        long generation = host.Resources.Generation;
        Assert.True(host.Mods.SetEnabled(fixture.PackageId, false).Success);
        Assert.False(host.Pump(true));
        Assert.Equal(generation, host.Resources.Generation);
        Assert.Contains(host.Mods.Diagnostics, item => item.Code == "resource-reload-required");
        using GameResourceHost originalHost = fixture.Open();
        using BattleAssets originalAssets = new(TestResources.GameDirectory, originalHost.Resources);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        Dictionary<string, (string Cpu, string Gpu)> originals = [];
        Dictionary<string, (string Cpu, string Gpu)> replacements = [];
        foreach ((string name, BattleModel model) in models)
        {
            BattleModel original = originalAssets.LoadModel(name);
            Assert.Equal(JsonSerializer.Serialize(original.Animations), JsonSerializer.Serialize(model.Animations));
            originals.Add(name, Capture(original, name + "-original", gpu));
            replacements.Add(name, Capture(model, name + "-enabled", gpu));
            Assert.NotEqual(originals[name].Cpu, replacements[name].Cpu);
            Assert.NotEqual(originals[name].Gpu, replacements[name].Gpu);
        }
        using GameResourceHost disabledHost = fixture.Open();
        using BattleAssets disabledAssets = new(TestResources.GameDirectory, disabledHost.Resources);
        foreach (string name in models.Keys)
        {
            Assert.Equal(originals[name], Capture(disabledAssets.LoadModel(name), name + "-fresh-host-disabled", gpu));
        }
        Assert.True(originalHost.Mods.SetEnabled(fixture.PackageId, true).Success);
        Assert.False(originalHost.Pump(true));
        using GameResourceHost restoredHost = fixture.Open();
        using BattleAssets restoredAssets = new(TestResources.GameDirectory, restoredHost.Resources);
        foreach (string name in models.Keys)
        {
            Assert.Equal(replacements[name], Capture(restoredAssets.LoadModel(name), name + "-fresh-host-restored", gpu));
        }
        Evidence("battle-model-installed.json", new { edition = host.Resources.Source.Edition.ToString(), fixture.Options, fixture.SelectedFiles, count = models.Count, models = evidence, frozenAfterRead = true, freshHostDisabled = true, freshHostRestored = true, scope = "Actual body/linked resources and native singleton file-loader/texture-copy components, including JSON and next callback; fitted CPU/GPU previews. No gameplay or full battle-session snapshot claim." });
    }

    [Fact]
    public void MalformedWinnerKeepsThePreviousWholePublicationAndItsOwningDiagnostic()
    {
        using PackageFixture fixture = new();
        using GameResourceHost host = fixture.Open();
        ModCatalogSnapshot catalog = host.Mods.Catalog;
        long generation = host.Resources.Generation;
        foreach ((string name, byte[] bytes) in fixture.Resources)
        {
            Assert.Equal(bytes, host.Resources.Read(new("battle/" + name)));
        }
        string malformed = fixture.Resources.Keys.First();
        File.WriteAllBytes(fixture.ResourcePaths[malformed], new byte[32]);
        host.Mods.RequestRefresh();
        Assert.False(host.Pump(true));
        Assert.Same(catalog, host.Mods.Catalog);
        Assert.Equal(generation, host.Resources.Generation);
        Assert.Contains(host.Mods.Diagnostics, item => item.Code == "Junction.InvalidEncodedResource" && item.PackageId == fixture.PackageId && item.RelativePath!.EndsWith(malformed, StringComparison.OrdinalIgnoreCase));
        foreach ((string name, byte[] bytes) in fixture.Resources)
        {
            Assert.Equal(bytes, host.Resources.Read(new("battle/" + name)));
        }
        Evidence("battle-model-malformed-winner.json", new { fixture.Options, fixture.SelectedFiles, malformed, unchangedGeneration = true, unchangedCatalog = true, retainedResources = fixture.Resources.Count, diagnostics = host.Mods.Diagnostics });
    }

    private static NativeBattleEffectTextureState Copy(NativeBattleEffectTextureState state, NativeBattleTextureCopy copy) => state.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY).Drain505D20().State;
    private static string Geometry(BattleModel model) => Hash(JsonSerializer.SerializeToUtf8Bytes(new { model.Bones, model.Meshes }, new JsonSerializerOptions { IncludeFields = true }));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static (string Cpu, string Gpu) Capture(BattleModel model, string phase, GpuTriangleRenderer gpu)
    {
        Vector3[] vertices = model.Pose(0, 0).SelectMany(mesh => mesh).ToArray();
        Vector3 minimum = vertices.Aggregate(Vector3.Min);
        Vector3 maximum = vertices.Aggregate(Vector3.Max);
        Vector3 center = (minimum + maximum) * 0.5f;
        float radius = Vector3.Distance(minimum, maximum) * 0.5f;
        Assert.True(float.IsFinite(radius) && radius > 0);
        BattleCamera camera = new(center + new Vector3(radius * 0.5f, radius * 0.15f, -radius * 3.4f), center, 0.8f);
        BattleRenderer cpu = new(640, 640);
        byte[] pixels = cpu.Render(new BattleStage(0, [], []), [new(model, Vector3.Zero)], camera).ToArray();
        Assert.True(cpu.DrawnTriangles > 0);
        BattleRenderer hardware = new(640, 640) { TriangleRenderer = gpu };
        hardware.Render(new BattleStage(0, [], []), [new(model, Vector3.Zero)], camera);
        byte[] hardwarePixels = GpuTestDevice.Read(gpu.Target).SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray();
        if (EvidenceDirectory() is { } directory)
        {
            using var bitmap = GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 640, 640);
            bitmap.Save(Path.Combine(directory, phase + "-cpu.png"));
            using FileStream output = File.Create(Path.Combine(directory, phase + "-gpu.png"));
            gpu.Target.SaveAsPng(output, 640, 640);
        }
        return (Hash(pixels), Hash(hardwarePixels));
    }

    private static string? EvidenceDirectory()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }
        string directory = Path.Combine(root, "BattleModelResources");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void Evidence(string filename, object value)
    {
        if (EvidenceDirectory() is { } directory)
        {
            File.WriteAllText(Path.Combine(directory, filename), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        }
    }

    private sealed class PackageFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-battle-resource-" + Guid.NewGuid().ToString("N"));
        public string PackageId { get; }
        public Dictionary<string, byte[]> Resources { get; } = [];
        public Dictionary<string, string> ResourcePaths { get; } = [];
        public Dictionary<string, int> Options { get; } = [];
        public List<object> SelectedFiles { get; } = [];

        public PackageFixture()
        {
            string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_MODEL_RESOURCE_PACKAGE");
            Assert.SkipUnless(Directory.Exists(input), "Set MEMORIA_FF8_BATTLE_MODEL_RESOURCE_PACKAGE to an optional extracted Junction package directory.");
            string? configured = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_MODEL_RESOURCE_OPTIONS");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                Options = JsonSerializer.Deserialize<Dictionary<string, int>>(File.Exists(configured) ? File.ReadAllText(configured) : configured)!;
            }
            JunctionModPackage imported = JunctionModImporter.Open(input!, Options);
            string packageName = Path.GetFileName(Path.TrimEndingDirectorySeparator(input!));
            PackageId = "Junction/" + packageName;
            string destination = Path.Combine(root, "Junction", packageName);
            Directory.CreateDirectory(destination);
            File.Copy(Path.Combine(input!, "mod.xml"), Path.Combine(destination, "mod.xml"));
            foreach (JunctionReplacement file in imported.Files)
            {
                string copy = Path.Combine(destination, file.SourcePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                using (FileStream output = File.Create(copy))
                {
                    file.CopyTo(output);
                }
                if (JunctionEncodedResources.Address(file.RelativePath) is ResourceKey key && key.Path.EndsWith(".dat", StringComparison.Ordinal))
                {
                    string name = Path.GetFileName(key.Path);
                    if (Resources.TryAdd(name, File.ReadAllBytes(copy)))
                    {
                        ResourcePaths.Add(name, copy);
                        SelectedFiles.Add(new { name, file.RelativePath, file.SourcePath, sha256 = Hash(Resources[name]) });
                    }
                }
            }
            File.WriteAllLines(Path.Combine(destination, "mod.ini"), new[] { "enabled = 1" }.Concat(Options.Select(option => $"option.{option.Key} = {option.Value}")));
            Assert.NotEmpty(Resources);
        }

        public GameResourceHost Open() => new(TestResources.GameDirectory, watch: false, modsDirectory: root);
        public void Dispose() => Directory.Delete(root, true);
    }
}
