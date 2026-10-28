using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class DemasterWholeAtlasAcceptanceTests
{
    [Fact]
    public void SuppliedWholeAtlasesMatchOriginalModelUvAndSurvivePackageChanges()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_WHOLE_ATLAS_FIXTURES");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(input) && File.Exists(input), "Select an optional original model atlas fixture catalog with MEMORIA_FF8_WHOLE_ATLAS_FIXTURES.");
        Fixture fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(input!), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Expected a model atlas fixture catalog.");
        string basis = Path.GetDirectoryName(Path.GetFullPath(input!))!;
        string game = Path.GetFullPath(fixture.GameDirectory, basis);
        string packageDirectory = Path.GetFullPath(fixture.PackageDirectory, basis);
        string evidence = Path.GetFullPath(fixture.EvidenceDirectory, basis);
        Directory.CreateDirectory(evidence);
        using IResourceSource source = GameDataSourceFactory.Open(game);
        Assert.Equal(GameDataEdition.Remastered, source.Descriptor.Edition);
        ResourceService resources = new(source);
        List<TextureBuildContext> contexts = [];
        List<Exception> failures = [];
        resources.ReplacementFailed += (_, exception) => failures.Add(exception);
        using ModRuntime mods = new(applicationBaseDirectory: basis, modsDirectory: Path.GetFullPath(fixture.ModsDirectory, basis), watch: false, prepare: catalog =>
        {
            ResourceService.PreparedResources prepared = resources.Prepare(new RecordingResolver(new DemasterTextureResolver(catalog), contexts));
            return new(true, Commit: () => prepared.Commit(notify: false), AfterCommit: prepared.Notify);
        });
        Pump();
        string package = Assert.Single(mods.Catalog.Packages, item => item.RootDirectory.Equals(packageDirectory, StringComparison.OrdinalIgnoreCase)).Id;
        string configuration = Path.Combine(packageDirectory, "mod.ini");
        byte[]? priorConfiguration = File.Exists(configuration) ? File.ReadAllBytes(configuration) : null;
        string declaration = Path.Combine(packageDirectory, "texture-layouts.json");
        byte[] priorDeclaration = File.ReadAllBytes(declaration);
        DateTime declarationTime = File.GetLastWriteTimeUtc(declaration);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        List<object> report = [];
        HashSet<string> observedTargets = new(StringComparer.OrdinalIgnoreCase);
        int metadataRefresh = 0;
        try
        {
            using BattleAssets assets = new(game, resources);
            foreach (string owner in fixture.Owners)
            {
                Enable(false);
                BattleModel model = assets.LoadModel(owner + ".dat");
                ResourceTextureList materials = Assert.IsType<ResourceTextureList>(model.Textures);
                Assert.NotEmpty(model.Meshes);
                Assert.NotEmpty(model.Animations);
                string geometry = Geometry(model);
                string[] originalPixels = Enumerable.Range(0, materials.Count).Select(index => Signature(materials[index])).ToArray();
                var originalRender = Render("original");
                Enable(true);
                List<object> materialEvidence = [];
                for (int index = 0; index < materials.Count; index++)
                {
                    TextureValue value = materials.GetTexture(index);
                    TextureBuildContext context = contexts.Last(candidate => candidate.Key.Owner.Path == $"battle/{owner}.dat" && candidate.Key.Subresource == $"texture/{index}");
                    TextureSourceAddress address = Assert.IsType<TextureSourceAddress>(context.NativeSheet);
                    Assert.Equal((128, 128), (address.CanvasWidth, address.CanvasHeight));
                    int row = int.Parse(address.Subresource.AsSpan(4), CultureInfo.InvariantCulture);
                    string target = $"textures/battle.fs/hd_new/{owner}_{row & ~1}.dds";
                    Assert.Contains(target, fixture.Targets);
                    ModFile file = Assert.Single(mods.Catalog.FindAll(ModFormat.Demaster, target), candidate => candidate.PackageId == package);
                    PixelImage atlas = ModImageDecoder.Decode(file).Image;
                    Assert.Equal((1536, 1536), (atlas.Width, atlas.Height));
                    Assert.Equal((768, 768), (value.Image.Width, value.Image.Height));
                    Assert.Equal((context.LogicalWidth, context.LogicalHeight), (value.LogicalWidth, value.LogicalHeight));
                    Assert.Contains(target, value.Provenance, StringComparison.OrdinalIgnoreCase);
                    Assert.Equal(PixelCropSignature(atlas, address.X * 6, ((row & 1) * 128 + address.Y) * 6, address.Width * 6, address.Height * 6), Signature(value.Image));
                    int sampledCorners = VerifyNativeUv(model, index, atlas, value.Image);
                    observedTargets.Add(target);
                    materialEvidence.Add(new { index, row, target, value.Provenance, width = value.Image.Width, height = value.Image.Height, sha256 = Signature(value.Image), sampledNativeCorners = sampledCorners });
                }
                var enabledRender = Render("enabled");
                Assert.NotEqual(originalRender.Cpu, enabledRender.Cpu);
                Assert.NotEqual(originalRender.Gpu, enabledRender.Gpu);
                string[] enabledPixels = Enumerable.Range(0, materials.Count).Select(index => Signature(materials[index])).ToArray();
                Enable(false);
                Assert.Equal(originalPixels, Enumerable.Range(0, materials.Count).Select(index => Signature(materials[index])).ToArray());
                Assert.Equal(originalRender, Render("disabled"));
                Enable(true);
                Assert.Equal(enabledPixels, Enumerable.Range(0, materials.Count).Select(index => Signature(materials[index])).ToArray());
                Assert.Equal(enabledRender, Render("restored"));
                long priorGeneration = resources.Generation;
                File.WriteAllBytes(declaration, priorDeclaration.Concat(Enumerable.Repeat((byte)'\n', ++metadataRefresh)).ToArray());
                mods.Refresh();
                Pump();
                Assert.True(resources.Generation > priorGeneration);
                Assert.Equal(enabledPixels, Enumerable.Range(0, materials.Count).Select(index => Signature(materials[index])).ToArray());
                Assert.Equal(geometry, Geometry(model));
                report.Add(new { owner, originalResourceSha256 = Hash(resources.Read(new ResourceKey($"battle/{owner}.dat"))), materials = materialEvidence, originalRender, enabledRender, geometryAndUv = geometry, toggleRestoredOriginal = true, reloadPreservedAtlasPixels = true });

                (string Cpu, string Gpu) Render(string phase)
                {
                    BattleCamera camera = Camera(model);
                    BattleRenderer cpu = new(480, 480);
                    byte[] pixels = cpu.Render(new BattleStage(0, [], []), [new(model, Vector3.Zero)], camera).ToArray();
                    Assert.True(cpu.DrawnTriangles > 0);
                    BattleRenderer hardware = new(480, 480) { TriangleRenderer = gpu };
                    hardware.Render(new BattleStage(0, [], []), [new(model, Vector3.Zero)], camera);
                    var rendered = GpuTestDevice.Read(gpu.Target);
                    using (var bitmap = GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 480, 480))
                    {
                        bitmap.Save(Path.Combine(evidence, $"{owner}-{phase}-cpu.png"));
                    }
                    using (FileStream output = File.Create(Path.Combine(evidence, $"{owner}-{phase}-gpu.png")))
                    {
                        gpu.Target.SaveAsPng(output, 480, 480);
                    }
                    return (Hash(pixels), Hash(rendered.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray()));
                }
            }
            Assert.Equal(fixture.Targets.Order(StringComparer.OrdinalIgnoreCase), observedTargets.Order(StringComparer.OrdinalIgnoreCase));
            Assert.Empty(failures);
            File.WriteAllText(Path.Combine(evidence, "result.json"), JsonSerializer.Serialize(new { sourceEdition = source.Descriptor.Edition.ToString(), declaredTargets = observedTargets.Count, scope = "Original model geometry and original native UV metadata; exact whole-atlas DDS crops; CPU/GPU component previews, package off/on, and metadata refresh. No gameplay or original-driver pixel equivalence claim.", models = report }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        }
        finally
        {
            File.WriteAllBytes(declaration, priorDeclaration);
            File.SetLastWriteTimeUtc(declaration, declarationTime);
            if (priorConfiguration is null)
            {
                File.Delete(configuration);
            }
            else
            {
                File.WriteAllBytes(configuration, priorConfiguration);
            }
        }

        void Pump()
        {
            Assert.True(mods.PreparePending(force: true), JsonSerializer.Serialize(mods.Diagnostics) + Environment.NewLine + string.Join(Environment.NewLine, failures.Select(failure => failure.ToString())));
            Assert.True(mods.CommitPending());
        }

        void Enable(bool enabled)
        {
            Assert.True(mods.SetEnabled(package, enabled).Success);
            Pump();
        }
    }

    private static int VerifyNativeUv(BattleModel model, int texture, PixelImage atlas, PixelImage material)
    {
        TextureSourceRegion region = model.TextureRegions[texture];
        int count = 0;
        foreach (BattleFace face in model.Meshes.SelectMany(mesh => mesh.Faces).Where(face => face.Texture == texture))
        {
            NativeBattleFaceMaterial native = Assert.IsType<NativeBattleFaceMaterial>(face.NativeMaterial);
            int pixelsPerWord = 4 >> ((native.TexturePage >> 7) & 3);
            int originX = ((native.TexturePage & 15) * 64 - region.X) * pixelsPerWord;
            int originY = (native.TexturePage & 16) * 16 - region.Y;
            for (int corner = 0; corner < face.Uv.Length; corner++)
            {
                Vector2 local = native.Uv[corner] + new Vector2(originX, originY);
                Assert.Equal(local, face.Uv[corner]);
                int localX = (int)local.X * 6;
                int localY = (int)local.Y * 6;
                if (localX < 0 || localY < 0 || localX >= material.Width || localY >= material.Height)
                {
                    continue;
                }
                // The original native page UV projects into the full 256-square allocation.
                int atlasX = ((int)native.Uv[corner].X + originX) * 6;
                int atlasY = ((int)native.Uv[corner].Y + originY + (region.Y & 128)) * 6;
                Assert.Equal(atlas.Pixels[atlasY * atlas.Width + atlasX], material.Pixels[localY * material.Width + localX]);
                count++;
            }
        }
        return count;
    }

    private static string PixelCropSignature(PixelImage source, int x, int y, int width, int height)
    {
        PixelImage expected = new(width, height);
        for (int row = 0; row < height; row++)
        {
            source.Pixels.AsSpan((y + row) * source.Width + x, width).CopyTo(expected.Pixels.AsSpan(row * width, width));
        }
        return Signature(expected);
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

    private static string Signature(PixelImage image) => Hash(MemoryMarshal.AsBytes(image.Pixels.AsSpan()).ToArray());
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Geometry(BattleModel model) => Hash(JsonSerializer.SerializeToUtf8Bytes(new { model.Bones, meshes = model.Meshes.Select(mesh => new { mesh.Vertices, mesh.Faces }).ToArray() }, new JsonSerializerOptions { IncludeFields = true }));
    private sealed record Fixture(string GameDirectory, string ModsDirectory, string PackageDirectory, string[] Owners, string[] Targets, string EvidenceDirectory);
    private sealed class RecordingResolver(ITextureReplacementResolver inner, List<TextureBuildContext> contexts) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            contexts.Add(context);
            return inner.Resolve(context);
        }
    }
}
