using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class DemasterModelProfileRenderingTests
{
    [Fact]
    public void SelectedRemasteredBattleModelsKeepTheirGeometryAndUvAcrossPackageRenderingGenerations()
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
            Regex target = new(@"\Atextures/battle\.fs/hd_new/(?<owner>c0m[0-9]{3}|d[0-9a-f][cw][0-9]{3})_[0-5]\.(?:png|dds)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            string[] owners = host.Mods.Catalog.Files.Where(file => packages.Contains(file.PackageId) && file.Format == ModFormat.Demaster).Select(file => target.Match(file.RelativePath)).Where(match => match.Success).Select(match => match.Groups["owner"].Value.ToLowerInvariant()).Distinct().Order().ToArray();
            string[] bodies = owners.Where(name => name[0] == 'd' && name[2] == 'c').Take(1).ToArray();
            string[] weapons = owners.Where(name => name[0] == 'd' && name[2] == 'w').Take(1).ToArray();
            string[] enemies = owners.Where(name => name.StartsWith("c0m", StringComparison.Ordinal)).Take(2).ToArray();
            Assert.Single(bodies);
            Assert.Single(weapons);
            Assert.NotEmpty(enemies);
            string[] selected = bodies.Concat(weapons).Concat(enemies).ToArray();
            using BattleAssets assets = new(game!, host.Resources);
            using GpuTestDevice device = new();
            using GpuTriangleRenderer gpu = new(device.Device);
            List<object> report = [];
            foreach (string name in selected)
            {
                Enable(false);
                BattleModel model;
                if (name[0] == 'd' && name[2] == 'w')
                {
                    int character = int.Parse(name.AsSpan(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    int weapon = int.Parse(name.AsSpan(3, 3), CultureInfo.InvariantCulture);
                    model = assets.LoadWeapon(character, weapon, assets.LoadPlayer(character));
                }
                else
                {
                    model = assets.LoadModel(name + ".dat");
                }
                Assert.NotEmpty(model.Meshes);
                Assert.NotEmpty(model.Animations);
                ResourceTextureList materials = Assert.IsType<ResourceTextureList>(model.Textures);
                string geometry = Geometry(model);
                BattleCamera camera = Camera(model);
                var original = Capture("original");
                Enable(true);
                TextureValue[] enabledMaterials = Enumerable.Range(0, materials.Count).Select(materials.GetTexture).ToArray();
                Assert.Contains(enabledMaterials, value => packages.Any(package => value.Provenance.StartsWith(package + "/", StringComparison.Ordinal)));
                var enabled = Capture("enabled");
                Assert.NotEqual(original.Cpu, enabled.Cpu);
                Assert.NotEqual(original.Gpu, enabled.Gpu);
                Enable(false);
                var disabled = Capture("disabled");
                Assert.Equal(original, disabled);
                Assert.All(Enumerable.Range(0, materials.Count), index => Assert.DoesNotContain(packages, package => materials.GetTexture(index).Provenance.StartsWith(package + "/", StringComparison.Ordinal)));
                Enable(true);
                var restored = Capture("restored");
                Assert.Equal(enabled, restored);
                report.Add(new { model = name, sourceEdition = host.Resources.Source.Edition.ToString(), vertices = model.Meshes.Sum(mesh => mesh.Vertices.Count), faces = model.Meshes.Sum(mesh => mesh.Faces.Count), bones = model.Bones.Count, geometryAndUv = geometry, original = new { cpu = original.Cpu, gpu = original.Gpu }, enabled = new { cpu = enabled.Cpu, gpu = enabled.Gpu }, disabled = new { cpu = disabled.Cpu, gpu = disabled.Gpu }, restored = new { cpu = restored.Cpu, gpu = restored.Gpu }, materials = enabledMaterials.Select(value => new { value.Provenance, value.LogicalWidth, value.LogicalHeight, physicalWidth = value.Image.Width, physicalHeight = value.Image.Height }).ToArray() });

                (string Cpu, string Gpu) Capture(string phase)
                {
                    Assert.Equal(geometry, Geometry(model));
                    BattleRenderer cpu = new(640, 640);
                    byte[] pixels = cpu.Render(new BattleStage(0, [], []), [new(model, Vector3.Zero)], camera).ToArray();
                    Assert.True(cpu.DrawnTriangles > 0, $"The original model {name} has no visible fitted CPU triangles.");
                    BattleRenderer hardwareRenderer = new(640, 640) { TriangleRenderer = gpu };
                    hardwareRenderer.Render(new BattleStage(0, [], []), [new(model, Vector3.Zero)], camera);
                    var hardware = GpuTestDevice.Read(gpu.Target);
                    if (EvidenceDirectory() is { } directory)
                    {
                        using var bitmap = GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 640, 640);
                        bitmap.Save(Path.Combine(directory, $"{name}-{phase}-cpu.png"));
                        using FileStream output = File.Create(Path.Combine(directory, $"{name}-{phase}-gpu.png"));
                        gpu.Target.SaveAsPng(output, 640, 640);
                    }
                    return (Hash(pixels), Hash(hardware.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray()));
                }
            }
            Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "replacement-failed" or "demaster-import-failed");
            if (EvidenceDirectory() is { } evidence)
            {
                File.WriteAllText(Path.Combine(evidence, "model-profile-rendering.json"), JsonSerializer.Serialize(new { sourceEdition = "Remastered", scope = "Original source geometry and UV component previews through the CPU and GPU BattleRenderer consumers, animation 0/frame 0. Restored means package publication re-enabled; no gameplay, native battle execution or standalone restore claim. Native c0m source owners are rendered as their actual decoded models; their filename family does not imply creature geometry.", models = report }, new JsonSerializerOptions { WriteIndented = true }));
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

    private static string Geometry(BattleModel model) => Hash(JsonSerializer.SerializeToUtf8Bytes(new { model.Bones, meshes = model.Meshes.Select(mesh => new { mesh.Vertices, mesh.Faces }).ToArray() }, new JsonSerializerOptions { IncludeFields = true }));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string? EvidenceDirectory()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }
        string directory = Path.Combine(root, "RemasteredModels");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
