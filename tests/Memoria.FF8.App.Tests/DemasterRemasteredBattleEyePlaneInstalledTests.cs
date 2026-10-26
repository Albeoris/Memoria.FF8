using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class DemasterRemasteredBattleEyePlaneInstalledTests
{
    [Fact]
    public void CorrespondingRemasteredBodySelectsWholeEyePlanesThroughOriginalCopiesAndBothRenderers()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIR");
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_PROFILE_TEXTURE_PACKAGES") ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_MODEL_TEXTURE_PACKAGES");
        Assert.SkipUnless(Directory.Exists(game) && File.Exists(Path.Combine(game!, "main.zzz")) && File.Exists(input), "Select corresponding Remastered resources and an optional JSON package catalog with MEMORIA_FF8_REMASTERED_GAME_DIR and MEMORIA_FF8_MODEL_PROFILE_TEXTURE_PACKAGES.");
        string[] paths = JsonSerializer.Deserialize<string[]>(File.ReadAllText(input!)) ?? throw new InvalidDataException("Expected a JSON package directory array.");
        Assert.NotEmpty(paths);
        string parent = Path.GetDirectoryName(Path.GetFullPath(input!))!;
        paths = paths.Select(path => Path.GetFullPath(path, parent)).ToArray();
        DirectoryInfo demaster = Directory.GetParent(paths[0])!;
        Assert.Equal("Demaster", demaster.Name, ignoreCase: true);
        Assert.All(paths, path => Assert.Equal(demaster.FullName, Directory.GetParent(path)!.FullName, ignoreCase: true));
        Dictionary<string, byte[]?> settings = paths.ToDictionary(path => Path.Combine(path, "mod.ini"), path => File.Exists(Path.Combine(path, "mod.ini")) ? File.ReadAllBytes(Path.Combine(path, "mod.ini")) : null);
        try
        {
            for (int index = 0; index < paths.Length; index++)
            {
                File.WriteAllText(Path.Combine(paths[index], "mod.ini"), $"enabled = 1{Environment.NewLine}priority = {index}{Environment.NewLine}");
            }
            using GameResourceHost host = new(game!, watch: false, modsDirectory: demaster.Parent!.FullName);
            Assert.Equal(GameDataEdition.Remastered, host.Resources.Source.Edition);
            string[] packages = paths.Select(path => Assert.Single(host.Mods.Catalog.Packages, package => package.RootDirectory.Equals(path, StringComparison.OrdinalIgnoreCase)).Id).ToArray();
            Regex target = new(@"\Atextures/battle\.fs/hd_new/(?<owner>d[0-9a-f]c[0-9]{3})_[01]\.(?:png|dds)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            string[] owners = host.Mods.Catalog.Files.Where(file => packages.Contains(file.PackageId) && file.Format == ModFormat.Demaster).Select(file => target.Match(file.RelativePath)).Where(match => match.Success).Select(match => match.Groups["owner"].Value.ToLowerInvariant()).Distinct().Order().ToArray();
            Assert.NotEmpty(owners);
            using BattleAssets assets = new(game!, host.Resources);
            BattleModel? selected = null;
            foreach (string owner in owners)
            {
                BattleModel candidate = assets.LoadModel(owner + ".dat");
                ResourceTextureList candidateTextures = Assert.IsType<ResourceTextureList>(candidate.Textures);
                if (Enumerable.Range(0, candidateTextures.Count).Any(index => candidateTextures.GetTexture(index).Variants?.ContainsKey("eyes-closed") == true))
                {
                    selected = candidate;
                    break;
                }
            }
            BattleModel model = selected ?? throw new InvalidDataException("The selected packages have no body with paired eye planes.");
            ResourceTextureList textures = Assert.IsType<ResourceTextureList>(model.Textures);
            string geometry = Geometry(model);
            NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(model.NativeBodyTexturePack!, 0);
            NativeBattleEffectTextureState initial = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), load).Drain505D20().State;
            NativeBattleTextureCopy openCopy = Assert.IsType<NativeBattleTextureCopy>(NativeBattleTextureMotion.Plan50C780(model.NativeTextureAnimations, load.AllocatedSlots, 0, 0));
            NativeBattleTextureCopy closedCopy = Assert.IsType<NativeBattleTextureCopy>(NativeBattleTextureMotion.Plan50C780(model.NativeTextureAnimations, load.AllocatedSlots, 0, 1));
            Assert.NotEqual(openCopy, closedCopy);
            NativeBattleEffectTextureState opened = Copy(initial, openCopy);
            NativeBattleEffectTextureState closed = Copy(opened, closedCopy);
            NativeBattleEffectTextureState restored = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(closed))!;
            NativeBattleEffectTextureState reopened = Copy(restored, openCopy);
            IReadOnlyList<NativeBattleTextureReplacement> replacements = NativeBattleTextureReplacements.Bind(model, load.Binding);
            Assert.NotEmpty(replacements);
            List<object> materials = [];
            int changedMaterials = 0;
            PixelImage[] openImages = Select(opened, true);
            PixelImage[] closedImages = Select(closed, false);
            PixelImage[] restoredImages = Select(restored, false);
            PixelImage[] reopenedImages = Select(reopened, true);
            for (int index = 0; index < textures.Count; index++)
            {
                Assert.Equal(openImages[index].Pixels, reopenedImages[index].Pixels);
                Assert.Equal(closedImages[index].Pixels, restoredImages[index].Pixels);
                TextureValue value = textures.GetTexture(index);
                if (openImages[index].Pixels.AsSpan().SequenceEqual(closedImages[index].Pixels))
                {
                    continue;
                }
                PixelImage expected = value.Variants!["eyes-closed"];
                Assert.Equal(expected.Pixels, closedImages[index].Pixels);
                Assert.Equal(value.Image.Pixels, openImages[index].Pixels);
                changedMaterials++;
                materials.Add(new { texture = index, value.Provenance, logicalWidth = value.LogicalWidth, logicalHeight = value.LogicalHeight, width = expected.Width, height = expected.Height, open = Hash(openImages[index].Pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray()), closed = Hash(expected.Pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray()) });
                SaveImage(openImages[index], $"material-{index}-open");
                SaveImage(closedImages[index], $"material-{index}-closed");
            }
            Assert.True(changedMaterials > 0, "The original closed copy must select an entire decoded HD eye plane.");
            using GpuTestDevice device = new();
            using GpuTriangleRenderer gpu = new(device.Device);
            BattleCamera camera = Camera(model);
            var openRender = Capture(openImages, "open");
            var closedRender = Capture(closedImages, "closed");
            Assert.NotEqual(openRender.Cpu, closedRender.Cpu);
            Assert.NotEqual(openRender.Gpu, closedRender.Gpu);
            Assert.Equal(closedRender, Capture(restoredImages, "json-restored-closed"));
            Assert.Equal(openRender, Capture(reopenedImages, "reopened"));
            Enable(false);
            PixelImage[] disabledImages = Enumerable.Range(0, textures.Count).Select(index => textures.GetTexture(index).Image).ToArray();
            var disabledRender = Capture(disabledImages, "packages-disabled");
            Assert.NotEqual(openRender.Cpu, disabledRender.Cpu);
            Assert.NotEqual(openRender.Gpu, disabledRender.Gpu);
            Enable(true);
            replacements = NativeBattleTextureReplacements.Bind(model, load.Binding);
            Assert.Equal(closedRender, Capture(Select(restored, false), "packages-restored-closed"));
            Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "replacement-failed" or "demaster-import-failed");
            if (EvidenceDirectory() is { } evidence)
            {
                File.WriteAllText(Path.Combine(evidence, "battle-eye-plane-rendering.json"), JsonSerializer.Serialize(new { edition = "Remastered", model = model.Name, scope = "Fresh corresponding source model at animation 0/frame 0; original TIM allocation/upload and selector 0 open/closed/open copy execution select guarded whole material images, rendered with unchanged Remastered geometry/UV through CPU/GPU consumers. Texture-state JSON and package publication restoration only; no gameplay, battle-session or automatic blink timing claim.", geometryAndUv = geometry, openCopy, closedCopy, changedMaterials, materials, open = openRender, closed = closedRender, disabled = disabledRender, jsonRestored = true, nextOpenRestored = true, packagesRestored = true }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
            }

            PixelImage[] Select(NativeBattleEffectTextureState state, bool expectOpen)
            {
                PixelImage[] images = new PixelImage[textures.Count];
                for (int index = 0; index < textures.Count; index++)
                {
                    TextureValue value = textures.GetTexture(index);
                    NativeBattleTextureReplacement[] owners = replacements.Where(replacement => ReferenceEquals(replacement.Image, value.Image)).ToArray();
                    if (owners.Length == 0)
                    {
                        images[index] = value.Image;
                        continue;
                    }
                    PixelImage[] samples = owners.Select(material =>
                    {
                        PixelImage native = NativeBattleEffectTextureReader.DecodePcRegion(state, material.Page, material.Clut, material.U, material.V, material.Width, material.Height);
                        return NativeBattleEffectTextureReader.ApplyModelReplacements(native, state, material.Page, material.Clut, material.U, material.V, null, replacements);
                    }).ToArray();
                    Assert.All(samples, sample => Assert.Equal(samples[0].Pixels, sample.Pixels));
                    images[index] = samples[0];
                    Assert.Equal(value.Image.Width, images[index].Width);
                    Assert.Equal(value.Image.Height, images[index].Height);
                    if (expectOpen)
                    {
                        Assert.Equal(value.Image.Pixels, images[index].Pixels);
                    }
                }
                return images;
            }

            (string Cpu, string Gpu) Capture(PixelImage[] images, string phase)
            {
                Assert.Equal(geometry, Geometry(model));
                using InMemoryResourceSource previewSource = new(host.Resources.Source);
                ResourceService previewResources = new(previewSource);
                AssetHandle<TextureValue>[] handles = Enumerable.Range(0, images.Length).Select(index => previewResources.GetTexture(new(new(new("battle/" + model.Name), $"texture/{index}"), previewSource.Descriptor, 1, "selected-material-preview", textures.GetTexture(index).LogicalWidth, textures.GetTexture(index).LogicalHeight, () => images[index]))).ToArray();
                BattleModel preview = model with { Textures = new ResourceTextureList(handles) };
                Assert.Equal(geometry, Geometry(preview));
                BattleRenderer cpu = new(960, 960);
                byte[] pixels = cpu.Render(new BattleStage(0, [], []), [new(preview, Vector3.Zero)], camera).ToArray();
                Assert.True(cpu.DrawnTriangles > 0);
                BattleRenderer hardware = new(960, 960) { TriangleRenderer = gpu };
                hardware.Render(new BattleStage(0, [], []), [new(preview, Vector3.Zero)], camera);
                var result = GpuTestDevice.Read(gpu.Target);
                if (EvidenceDirectory() is { } directory)
                {
                    using var bitmap = GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 960, 960);
                    bitmap.Save(Path.Combine(directory, phase + "-cpu.png"));
                    using FileStream output = File.Create(Path.Combine(directory, phase + "-gpu.png"));
                    gpu.Target.SaveAsPng(output, 960, 960);
                }
                return (Hash(pixels), Hash(result.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray()));
            }

            void Enable(bool enabled)
            {
                foreach (string package in packages)
                {
                    Assert.True(host.Mods.SetEnabled(package, enabled).Success);
                }
                Assert.True(host.Pump(force: true));
            }
        }
        finally
        {
            foreach ((string path, byte[]? bytes) in settings)
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

    private static NativeBattleEffectTextureState Copy(NativeBattleEffectTextureState state, NativeBattleTextureCopy copy)
    {
        NativeBattleEffectTextureDrain drain = state.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY).Drain505D20();
        Assert.Contains(drain.Uploads, upload => upload.Copied && upload.X == copy.DestinationX && upload.Y == copy.DestinationY && upload.Width == copy.Width && upload.Height == copy.Height);
        return drain.State;
    }

    private static BattleCamera Camera(BattleModel model)
    {
        Vector3[] points = model.Pose(0, 0).SelectMany(mesh => mesh).ToArray();
        Vector3 minimum = new(points.Min(point => point.X), points.Min(point => point.Y), points.Min(point => point.Z));
        Vector3 maximum = new(points.Max(point => point.X), points.Max(point => point.Y), points.Max(point => point.Z));
        Vector3 center = (minimum + maximum) * 0.5f;
        float radius = Vector3.Distance(minimum, maximum) * 0.5f;
        Assert.True(float.IsFinite(radius) && radius > 0);
        return new(center + new Vector3(radius * 0.5f, radius * 0.15f, -radius * 3.4f), center, 0.8f);
    }

    private static void SaveImage(PixelImage image, string name)
    {
        if (EvidenceDirectory() is { } directory)
        {
            using var bitmap = GameUi.BattlePreviewCanvas.BitmapFromRgba(image.Pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray(), image.Width, image.Height);
            bitmap.Save(Path.Combine(directory, name + ".png"));
        }
    }

    private static string Geometry(BattleModel model) => Hash(JsonSerializer.SerializeToUtf8Bytes(new { model.Bones, meshes = model.Meshes.Select(mesh => new { mesh.Vertices, mesh.Faces }).ToArray() }, new JsonSerializerOptions { IncludeFields = true }));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string? EvidenceDirectory()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }
        string directory = Path.Combine(root, "RemasteredBattleEyes");
        Directory.CreateDirectory(directory);
        return directory;
    }
}

