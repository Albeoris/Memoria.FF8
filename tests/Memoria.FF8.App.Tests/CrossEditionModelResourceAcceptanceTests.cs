using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossEditionModelResourceAcceptanceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SelectedBundlesKeepPrimaryRuntimeAndRenderMatchingGeometryAcrossPublicationAndJsonContinuation(int fixtureIndex)
    {
        string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_RESOURCE_ACCEPTANCE");
        Assert.SkipUnless(File.Exists(catalog), "Select an optional external model-resource acceptance catalog.");
        Fixture[] fixtures = JsonSerializer.Deserialize<Fixture[]>(File.ReadAllText(catalog!), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.NotEmpty(fixtures);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        Assert.True((uint)fixtureIndex < fixtures.Length, "The acceptance catalog requires three independently scoped model packages.");
        Fixture fixture = fixtures[fixtureIndex];
        {
            using GameResourceHost host = new(fixture.PrimaryDirectory, watch: false, modsDirectory: fixture.ModsDirectory, modelSourceDirectory: fixture.ModelDirectory);
            using GameResourceHost primary = new(fixture.PrimaryDirectory, watch: false, modsDirectory: fixture.EmptyModsDirectory);
            using GameResourceHost donor = new(fixture.ModelDirectory, watch: false, modsDirectory: fixture.EmptyModsDirectory);
            Assert.Equal(GameDataEdition.Classic, host.Resources.Source.Edition);
            Assert.Equal(primary.Resources.Source.Id, host.Resources.Source.Id);
            Assert.Equal(primary.Resources.Source.Layouts.ToArray(), host.Resources.Source.Layouts.ToArray());
            Assert.DoesNotContain("native-field-model-sheet", host.Resources.Source.Layouts);
            SelectedModelResourceSource selected = Assert.IsType<SelectedModelResourceSource>(host.Resources.GetSource<IFieldModelSource>());
            Assert.Equal(GameDataEdition.Remastered, selected.ModelSourceDescriptor.Edition);
            Assert.True(host.Mods.Catalog.Packages.Count > 0, string.Join(Environment.NewLine, host.Mods.Diagnostics.Select(value => value.Code + ": " + value.Message)));
            string package = Assert.Single(host.Mods.Catalog.Packages, value => value.RootDirectory.Equals(fixture.PackageDirectory, StringComparison.OrdinalIgnoreCase)).Id;
            Assert.DoesNotContain(host.Mods.Diagnostics, value => value.Code is "replacement-failed" or "demaster-import-failed");
            List<object> reports = [];
            object coverage = VerifyCoverage(fixture, host, package);
            if (fixture.Field)
            {
                using GameAssets selectedAssets = new(fixture.PrimaryDirectory, resources: host.Resources);
                using GameAssets primaryAssets = new(fixture.PrimaryDirectory, resources: primary.Resources);
                using GameAssets donorAssets = new(fixture.ModelDirectory, resources: donor.Resources);
                FieldAssets field = selectedAssets.LoadField(689);
                FieldAssets nativeField = primaryAssets.LoadField(689);
                FieldAssets donorField = donorAssets.LoadField(689);
                Assert.Equal(nativeField.NativeRegistration!.ExecutableHash, field.NativeRegistration!.ExecutableHash);
                Assert.NotEqual(donorField.NativeRegistration!.ExecutableHash, field.NativeRegistration.ExecutableHash);
                Assert.Equal(nativeField.Jsm, field.Jsm);
                FieldModel model = field.Models[3];
                Assert.Equal(nativeField.Models[3].NativeParts!.SourceHash, model.NativeRegistrationParts!.SourceHash);
                Assert.Equal(nativeField.Models[3].NativeParts!.SourceOffset, model.NativeRegistrationParts.SourceOffset);
                Assert.Equal(donorField.Models[3].NativeParts!.SourceHash, model.NativeParts!.SourceHash);
                Assert.Equal(donorField.Models[3].NativeParts!.SourceOffset, model.NativeParts.SourceOffset);
                string geometry = FieldGeometry(model);
                Assert.Equal(FieldGeometry(donorField.Models[3]), geometry);
                Assert.NotEqual(FieldGeometry(nativeField.Models[3]), geometry);
                Assert.Equal(donorField.Models[3].Pose(0, 0), model.Pose(0, 0));
                Assert.Equal(JsonSerializer.Serialize(nativeField.Models[3].Animations), JsonSerializer.Serialize(model.Animations));
                ResourceTextureList materials = Assert.IsType<ResourceTextureList>(model.Textures);
                Verify("field", geometry, materials, () => FieldGeometry(model), phase => CaptureField(model, field, phase));
                VerifyPrimaryContinuation(host, selectedAssets, field, nativeField, package, (bound, phase) => CaptureField(bound, field, phase));
            }
            if (fixture.Battle)
            {
                using BattleAssets selectedAssets = new(fixture.PrimaryDirectory, host.Resources);
                using BattleAssets primaryAssets = new(fixture.PrimaryDirectory, primary.Resources);
                using BattleAssets donorAssets = new(fixture.ModelDirectory, donor.Resources);
                BattleModel selectedBody = selectedAssets.LoadPlayer(0);
                BattleModel nativeBody = primaryAssets.LoadPlayer(0);
                BattleModel donorBody = donorAssets.LoadPlayer(0);
                VerifyBattle("body", selectedBody, nativeBody, donorBody, requireDifference: true);
                VerifyBattle("weapon", selectedAssets.LoadWeapon(0, 0, selectedBody), primaryAssets.LoadWeapon(0, 0, nativeBody), donorAssets.LoadWeapon(0, 0, donorBody), requireDifference: false);
                VerifyBattleEyes(host, selectedBody, package, (bound, phase) => CaptureBattle("body", bound, phase));
            }
            Assert.DoesNotContain(host.Mods.Diagnostics, value => value.Code is "replacement-failed" or "demaster-import-failed");
            if (Evidence(fixture.Id) is { } evidence)
            {
                File.WriteAllText(Path.Combine(evidence, "acceptance.json"), JsonSerializer.Serialize(new { primaryEdition = host.Resources.Source.Edition.ToString(), modelEdition = selected.ModelSourceDescriptor.Edition.ToString(), scope = "Selected complete visual bundles, original primary runtime registration and independent native component JSON continuation. No gameplay or automatic donor eye-state claim.", coverage, models = reports }, new JsonSerializerOptions { WriteIndented = true }));
            }

            void VerifyBattle(string name, BattleModel model, BattleModel original, BattleModel corresponding, bool requireDifference)
            {
                string geometry = BattleGeometry(model);
                Assert.Equal(BattleGeometry(corresponding), geometry);
                if (requireDifference)
                {
                    Assert.NotEqual(BattleGeometry(original), geometry);
                }
                Assert.Equal(JsonSerializer.Serialize(original.Animations), JsonSerializer.Serialize(model.Animations));
                Verify(name, geometry, Assert.IsType<ResourceTextureList>(model.Textures), () => BattleGeometry(model), phase => CaptureBattle(name, model, phase));
            }

            void Verify(string name, string geometry, ResourceTextureList materials, Func<string> geometryNow, Func<string, (string Cpu, string Gpu)> capture)
            {
                Assert.Contains(Enumerable.Range(0, materials.Count), index => materials.GetTexture(index).Provenance.StartsWith(package + "/", StringComparison.Ordinal));
                var enabled = capture("enabled");
                Assert.True(host.Mods.SetEnabled(package, false).Success);
                Assert.True(host.Pump(force: true));
                Assert.All(Enumerable.Range(0, materials.Count), index => Assert.False(materials.GetTexture(index).Provenance.StartsWith(package + "/", StringComparison.Ordinal)));
                var disabled = capture("disabled");
                Assert.NotEqual(enabled.Cpu, disabled.Cpu);
                Assert.NotEqual(enabled.Gpu, disabled.Gpu);
                Assert.Equal(geometry, geometryNow());
                Assert.True(host.Mods.SetEnabled(package, true).Success);
                Assert.True(host.Pump(force: true));
                Assert.Equal(enabled, capture("restored"));
                Assert.Equal(geometry, geometryNow());
                reports.Add(new { name, geometryAndUv = geometry, enabled = new { enabled.Cpu, enabled.Gpu }, disabled = new { disabled.Cpu, disabled.Gpu } });
            }

            (string Cpu, string Gpu) CaptureBattle(string name, BattleModel model, string phase)
            {
                Vector3[] points = model.Pose(0, 0).SelectMany(mesh => mesh).ToArray();
                (Vector3 center, float radius) = Bounds(points);
                BattleCamera camera = new(center + new Vector3(radius * 0.5f, radius * 0.15f, -radius * 3.4f), center, 0.8f);
                BattleRenderer renderer = new(640, 640);
                byte[] cpu = renderer.Render(new BattleStage(0, [], []), [new(model, Vector3.Zero)], camera).ToArray();
                Assert.True(renderer.DrawnTriangles > 0);
                renderer.TriangleRenderer = gpu;
                renderer.Render(new BattleStage(0, [], []), [new(model, Vector3.Zero)], camera);
                return Capture(name + "-" + phase, cpu);
            }

            (string Cpu, string Gpu) CaptureField(FieldModel model, FieldAssets field, string phase)
            {
                List<string> cpuHashes = [];
                List<string> gpuHashes = [];
                for (int direction = 0; direction < 256; direction += 64)
                {
                    Matrix4x4 rotation = Matrix4x4.CreateRotationZ((direction + 64) * MathF.Tau / 256);
                    (Vector3 center, float radius) = Bounds(model.Pose(0, 0).Select(point => Vector3.Transform(point, rotation)).ToArray());
                    FieldCamera camera = new(Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY, new(-center.X, center.Z, radius * 4 - center.Y), Vector2.Zero, 450);
                    FieldAssets projection = new(field.Id, field.Name, [], [], [], camera, new FieldWalkmesh([]), [model]);
                    ActorVisual actor = new(0, 0, 0, 0, 0, direction, 0, 0, true, PolygonColor: new Vector3(128));
                    FieldRenderer renderer = new(projection, 640, 640);
                    byte[] cpu = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, backgroundVisible: false).ToArray();
                    renderer.TriangleRenderer = gpu;
                    renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, backgroundVisible: false);
                    var result = Capture($"field-{phase}-{direction}", cpu);
                    cpuHashes.Add(result.Cpu);
                    gpuHashes.Add(result.Gpu);
                }
                return (string.Join('/', cpuHashes), string.Join('/', gpuHashes));
            }

            (string Cpu, string Gpu) Capture(string name, byte[] cpu)
            {
                byte[] hardware = GpuTestDevice.Read(gpu.Target).SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray();
                if (Evidence(fixture.Id) is { } evidence)
                {
                    using var bitmap = GameUi.BattlePreviewCanvas.BitmapFromRgba(cpu, 640, 640);
                    bitmap.Save(Path.Combine(evidence, name + "-cpu.png"));
                    using FileStream output = File.Create(Path.Combine(evidence, name + "-gpu.png"));
                    gpu.Target.SaveAsPng(output, 640, 640);
                }
                return (Hash(cpu), Hash(hardware));
            }
        }
    }

    private static object VerifyCoverage(Fixture fixture, GameResourceHost host, string package)
    {
        Regex fieldTarget = new(@"\Atextures/field\.fs/(?:field_hd_new|field_hd)/(?<owner>[dnop][0-9]{3})_[0-9]+(?:_alpha)?\.(?:png|dds)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Regex battleTarget = new(@"\Atextures/battle\.fs/hd_new/(?<owner>c0m[0-9]{3}|d[0-9a-f][cw][0-9]{3})_[0-9]+\.(?:png|dds)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        string[] files = host.Mods.Catalog.Files.Where(file => file.PackageId == package).Select(file => file.RelativePath).ToArray();
        HashSet<string> fieldOwners = files.Select(file => fieldTarget.Match(file)).Where(match => match.Success).Select(match => match.Groups["owner"].Value.ToLowerInvariant()).ToHashSet();
        string[] battleOwners = files.Select(file => battleTarget.Match(file)).Where(match => match.Success).Select(match => match.Groups["owner"].Value.ToLowerInvariant()).Distinct().Order().ToArray();
        HashSet<string> consumed = [];
        int fieldPlacements = 0;
        if (fieldOwners.Count > 0)
        {
            ModelCatalogEntry[] entries;
            using (GameAssets inventory = new(fixture.PrimaryDirectory, resources: host.Resources))
            {
                entries = inventory.EnumerateFieldModelSources().Where(entry => entry.LoadError is null && entry.Name.Length >= 4 && fieldOwners.Contains(ExternalOwner(entry.Name[..4]))).ToArray();
            }
            Assert.NotEmpty(entries);
            foreach (string owner in fieldOwners.Where(owner => owner[0] == 'd').Order())
            {
                using GameAssets library = new(fixture.PrimaryDirectory, resources: host.Resources);
                ResourceTextureList textures = Assert.IsType<ResourceTextureList>(library.LoadFieldMainCharacterTextures(owner));
                Assert.Contains(Enumerable.Range(0, textures.Count), index => textures.GetTexture(index).Provenance.StartsWith(package + "/", StringComparison.Ordinal));
                consumed.Add(owner);
            }
            foreach (IGrouping<int?, ModelCatalogEntry> field in entries.GroupBy(entry => entry.FieldId))
            {
                using GameAssets assets = new(fixture.PrimaryDirectory, resources: host.Resources);
                foreach (ModelCatalogEntry entry in field)
                {
                    FieldModel model = assets.LoadFieldModelSource(entry);
                    Assert.Equal(GameDataEdition.Remastered, model.TextureSourceDescriptor!.Edition);
                    ResourceTextureList textures = Assert.IsType<ResourceTextureList>(model.Textures);
                    Assert.Contains(Enumerable.Range(0, textures.Count), index => textures.GetTexture(index).Provenance.StartsWith(package + "/", StringComparison.Ordinal));
                    consumed.Add(ExternalOwner(entry.Name[..4]));
                    fieldPlacements++;
                }
            }
            Assert.Equal(fieldOwners.Order(), consumed.Order());
        }
        foreach (string owner in battleOwners)
        {
            using BattleAssets battle = new(fixture.PrimaryDirectory, host.Resources);
            BattleModel model = owner[0] == 'd' && owner[2] == 'w' ? battle.LoadWeapon(int.Parse(owner.AsSpan(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture), int.Parse(owner.AsSpan(3, 3), CultureInfo.InvariantCulture), battle.LoadPlayer(int.Parse(owner.AsSpan(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture))) : battle.LoadModel(owner + ".dat");
            Assert.Equal(GameDataEdition.Remastered, model.TextureSourceDescriptor!.Edition);
            ResourceTextureList textures = Assert.IsType<ResourceTextureList>(model.Textures);
            Assert.Contains(Enumerable.Range(0, textures.Count), index => textures.GetTexture(index).Provenance.StartsWith(package + "/", StringComparison.Ordinal));
        }
        Console.WriteLine($"Selected bundle catalog {fixture.Id}: {fieldPlacements} field placements / {fieldOwners.Count} field owners; {battleOwners.Length} battle owners.");
        return new { fieldPlacements, fieldOwners = consumed.Order().ToArray(), battleOwners };
    }

    private static string ExternalOwner(string name) => (char.ToLowerInvariant(name[0]) == 'c' ? "n" : name[..1].ToLowerInvariant()) + name[1..].ToLowerInvariant();

    private static void VerifyBattleEyes(GameResourceHost host, BattleModel model, string package, Func<BattleModel, string, (string Cpu, string Gpu)> capture)
    {
        ResourceTextureList textures = Assert.IsType<ResourceTextureList>(model.Textures);
        NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(model.NativeBodyTexturePack!, 0);
        NativeBattleEffectTextureState initial = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), load).Drain505D20().State;
        NativeBattleTextureCopy openCopy = Assert.IsType<NativeBattleTextureCopy>(NativeBattleTextureMotion.Plan50C780(model.NativeTextureAnimations, load.AllocatedSlots, 0, 0));
        NativeBattleTextureCopy closedCopy = Assert.IsType<NativeBattleTextureCopy>(NativeBattleTextureMotion.Plan50C780(model.NativeTextureAnimations, load.AllocatedSlots, 0, 1));
        NativeBattleEffectTextureState open = Copy(initial, openCopy);
        NativeBattleEffectTextureState closed = Copy(open, closedCopy);
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(DetachedJson(closed))!);
        IReadOnlyList<NativeBattleTextureReplacement> replacements = NativeBattleTextureReplacements.Bind(model, load.Binding);
        Assert.NotEmpty(replacements);
        var openCapture = Capture(open, "native-open");
        var closedCapture = Capture(closed, "native-closed");
        Assert.NotEqual(openCapture.Cpu, closedCapture.Cpu);
        Assert.NotEqual(openCapture.Gpu, closedCapture.Gpu);
        Assert.Equal(closedCapture, Capture(restored, "native-closed-json-restored"));
        Assert.Equal(openCapture, Capture(Copy(restored, openCopy), "native-reopened"));
        Assert.True(host.Mods.SetEnabled(package, false).Success);
        Assert.True(host.Pump(force: true));
        var disabled = capture(model, "native-disabled");
        Assert.NotEqual(closedCapture.Cpu, disabled.Cpu);
        Assert.NotEqual(closedCapture.Gpu, disabled.Gpu);
        Assert.True(host.Mods.SetEnabled(package, true).Success);
        Assert.True(host.Pump(force: true));
        replacements = NativeBattleTextureReplacements.Bind(model, load.Binding);
        Assert.Equal(closedCapture, Capture(restored, "native-closed-packages-restored"));

        (string Cpu, string Gpu) Capture(NativeBattleEffectTextureState state, string phase)
        {
            PixelImage[] images = Enumerable.Range(0, textures.Count).Select(index =>
            {
                TextureValue value = textures.GetTexture(index);
                NativeBattleTextureReplacement[] owners = replacements.Where(replacement => ReferenceEquals(replacement.Image, value.Image)).ToArray();
                if (owners.Length == 0)
                {
                    return value.Image;
                }
                PixelImage[] samples = owners.Select(material => NativeBattleEffectTextureReader.ApplyModelReplacements(NativeBattleEffectTextureReader.DecodePcRegion(state, material.Page, material.Clut, material.U, material.V, material.Width, material.Height), state, material.Page, material.Clut, material.U, material.V, null, replacements)).ToArray();
                Assert.All(samples, sample => Assert.Equal(samples[0].Pixels, sample.Pixels));
                return samples[0];
            }).ToArray();
            using InMemoryResourceSource source = new(host.Resources.Source);
            ResourceService resources = new(source);
            AssetHandle<TextureValue>[] handles = Enumerable.Range(0, images.Length).Select(index => resources.GetTexture(new(new(new("battle/" + model.Name), $"texture/{index}"), source.Descriptor, 1, "selected-material-preview", textures.GetTexture(index).LogicalWidth, textures.GetTexture(index).LogicalHeight, () => images[index]))).ToArray();
            return capture(model with { Textures = new ResourceTextureList(handles) }, phase);
        }
    }

    private static NativeBattleEffectTextureState Copy(NativeBattleEffectTextureState state, NativeBattleTextureCopy copy) => state.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY).Drain505D20().State;

    private static void VerifyPrimaryContinuation(GameResourceHost host, GameAssets assets, FieldAssets field, FieldAssets primary, string package, Func<FieldModel, string, (string Cpu, string Gpu)> capture)
    {
        int count = field.Jsm[0] + field.Jsm[1] + field.Jsm[2] + field.Jsm[3];
        ushort group = Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(8 + index * 2))).OrderBy(value => value >> 7).ElementAt(3);
        NativeFieldRetainedActor[] actors = [new(3, 3, group >> 7, (group >> 7) + (group & 127) + 1)];
        NativeFieldRegistrationStep selected = NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration!, actors, NativeBattleRetainedMemory.FromColdProcess());
        NativeFieldRegistrationStep native = NativeFieldRegistrationMachine.ReloadKind3(primary.NativeRegistration!, actors, NativeBattleRetainedMemory.FromColdProcess());
        Assert.Equal(JsonSerializer.Serialize(native.State), JsonSerializer.Serialize(selected.State));
        Assert.Equal(DetachedJson(native.Memory), DetachedJson(selected.Memory));
        NativeFieldTextureBinding open = Bind(selected.Memory);
        var openCapture = capture(open.Field.Models[3], "native-open");
        NativeBattleRetainedMemory closed = NativeFieldRegistrationMachine.SetEyes532340(selected.State, selected.Memory, 3, 0x200000, null, null).Memory;
        NativeFieldTextureBinding shut = Bind(closed);
        var closedCapture = capture(shut.Field.Models[3], "native-closed");
        Assert.NotEqual(openCapture.Cpu, closedCapture.Cpu);
        Assert.NotEqual(openCapture.Gpu, closedCapture.Gpu);
        NativeBattleRetainedMemory closedRestored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleRetainedMemory>(DetachedJson(closed))!);
        Assert.Equal(closedCapture, capture(Bind(closedRestored).Field.Models[3], "native-closed-json-restored"));
        Assert.True(host.Mods.SetEnabled(package, false).Success);
        Assert.True(host.Pump(force: true));
        var disabled = capture(Bind(closedRestored).Field.Models[3], "native-closed-disabled");
        Assert.NotEqual(closedCapture.Cpu, disabled.Cpu);
        Assert.NotEqual(closedCapture.Gpu, disabled.Gpu);
        Assert.True(host.Mods.SetEnabled(package, true).Success);
        Assert.True(host.Pump(force: true));
        Assert.Equal(closedCapture, capture(Bind(closedRestored).Field.Models[3], "native-closed-packages-restored"));
        NativeBattleRetainedMemory opened = NativeFieldRegistrationMachine.SetEyes532340(selected.State, closedRestored, 3, 0x100000, null, null).Memory;
        Assert.Equal(openCapture, capture(Bind(opened).Field.Models[3], "native-reopened"));
        NativeBattleFieldResourceReloadResult started = NativeFieldRegistrationMachine.SetEyes532340(selected.State, selected.Memory, 3, 0x80000, 4, NativeCrtRandomState.FromColdProcess());
        NativeBattleRetainedMemory memory = started.Memory;
        NativeCrtRandomState? random = started.CrtRandomAfter;
        for (int callback = 0; callback < 8; callback++)
        {
            NativeBattleRetainedMemory independent = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleRetainedMemory>(DetachedJson(memory))!);
            NativeBattleFieldResourceReloadResult next = NativeFieldRegistrationMachine.AdvanceEyes530810(selected.State, memory, random);
            NativeBattleFieldResourceReloadResult resumed = NativeFieldRegistrationMachine.AdvanceEyes530810(selected.State, independent, random);
            Assert.Equal(DetachedJson(next.Memory), DetachedJson(resumed.Memory));
            Assert.Equal(next.CrtRandomAfter, resumed.CrtRandomAfter);
            NativeFieldTextureBinding liveBinding = assets.BindNativeRegisteredTextureState(field, selected.State, next.Memory.EffectMemory!.Textures!, next.Memory.FieldTextureAnimations, 3);
            NativeFieldTextureBinding resumedBinding = assets.BindNativeRegisteredTextureState(field, selected.State, resumed.Memory.EffectMemory!.Textures!, resumed.Memory.FieldTextureAnimations, 3);
            Assert.Equal(FieldGeometry(field.Models[3]), FieldGeometry(liveBinding.Field.Models[3]));
            Assert.Equal(FieldGeometry(liveBinding.Field.Models[3]), FieldGeometry(resumedBinding.Field.Models[3]));
            ResourceTextureList liveMaterials = Assert.IsType<ResourceTextureList>(liveBinding.Field.Models[3].Textures);
            ResourceTextureList resumedMaterials = Assert.IsType<ResourceTextureList>(resumedBinding.Field.Models[3].Textures);
            Assert.Contains(Enumerable.Range(0, liveMaterials.Count), index => liveMaterials.GetTexture(index).Provenance.StartsWith(package + "/", StringComparison.Ordinal));
            Assert.Equal(Enumerable.Range(0, liveMaterials.Count).Select(index => Hash(liveMaterials.GetTexture(index).Image.Pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray())), Enumerable.Range(0, resumedMaterials.Count).Select(index => Hash(resumedMaterials.GetTexture(index).Image.Pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray())));
            memory = next.Memory;
            random = next.CrtRandomAfter;
        }

        NativeFieldTextureBinding Bind(NativeBattleRetainedMemory retained) => assets.BindNativeRegisteredTextureState(field, selected.State, retained.EffectMemory!.Textures!, retained.FieldTextureAnimations, 3);
    }

    private static (Vector3 Center, float Radius) Bounds(Vector3[] points)
    {
        Vector3 minimum = new(points.Min(point => point.X), points.Min(point => point.Y), points.Min(point => point.Z));
        Vector3 maximum = new(points.Max(point => point.X), points.Max(point => point.Y), points.Max(point => point.Z));
        float radius = Vector3.Distance(minimum, maximum) * 0.5f;
        Assert.True(float.IsFinite(radius) && radius > 0);
        return ((minimum + maximum) * 0.5f, radius);
    }

    private static string FieldGeometry(FieldModel model) => Hash(JsonSerializer.SerializeToUtf8Bytes(new { model.Bones, vertices = model.Vertices.Select(vertex => new { vertex.Position, vertex.Bone }).ToArray(), model.Faces }, new JsonSerializerOptions { IncludeFields = true }));
    private static string BattleGeometry(BattleModel model) => Hash(JsonSerializer.SerializeToUtf8Bytes(new { model.Bones, meshes = model.Meshes.Select(mesh => new { mesh.Vertices, mesh.Faces }).ToArray() }, new JsonSerializerOptions { IncludeFields = true }));
    private static string DetachedJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string? Evidence(string id)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }
        string directory = Path.Combine(root, "SelectedModelResources", id);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed record Fixture(string Id, string PrimaryDirectory, string ModelDirectory, string ModsDirectory, string EmptyModsDirectory, string PackageDirectory, bool Field, bool Battle);
}
