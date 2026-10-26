using System.Buffers.Binary;
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
public sealed class DemasterRemasteredEyePlaneInstalledTests
{
    [Fact]
    public void OriginalRegisteredProducerSelectsWholePlanesAndRestoresAcrossCpuGpuAndPackagePublication()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_GAME_DIR");
        string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_MODEL_TEXTURE_PACKAGES");
        Assert.SkipUnless(Directory.Exists(game) && File.Exists(Path.Combine(game!, "main.zzz")) && File.Exists(catalog), "Select optional corresponding Remastered resources and a field-model package catalog.");
        string[] paths = JsonSerializer.Deserialize<string[]>(File.ReadAllText(catalog!)) ?? throw new InvalidDataException("Expected a JSON package directory array.");
        Assert.NotEmpty(paths);
        paths = paths.Select(path => Path.GetFullPath(path, Path.GetDirectoryName(Path.GetFullPath(catalog!))!)).ToArray();
        DirectoryInfo demaster = Directory.GetParent(paths[0])!;
        Assert.Equal("Demaster", demaster.Name, ignoreCase: true);
        Assert.All(paths, path => Assert.Equal(demaster.FullName, Directory.GetParent(path)!.FullName, ignoreCase: true));
        Dictionary<string, byte[]?> configurations = paths.ToDictionary(path => Path.Combine(path, "mod.ini"), path => File.Exists(Path.Combine(path, "mod.ini")) ? File.ReadAllBytes(Path.Combine(path, "mod.ini")) : null);
        try
        {
            for (int index = 0; index < paths.Length; index++)
            {
                File.WriteAllText(Path.Combine(paths[index], "mod.ini"), $"enabled = 1{Environment.NewLine}priority = {index}{Environment.NewLine}");
            }
            using GameResourceHost host = new(game!, watch: false, modsDirectory: demaster.Parent!.FullName);
            Assert.Equal(GameDataEdition.Remastered, host.Resources.Source.Edition);
            string[] packages = paths.Select(path => Assert.Single(host.Mods.Catalog.Packages, package => package.RootDirectory.Equals(path, StringComparison.OrdinalIgnoreCase)).Id).ToArray();
            using GameAssets assets = new(game!, resources: host.Resources);
            FieldAssets field = assets.LoadField(689);
            Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, field.NativeRegistration!.ExecutableHash);
            int count = field.Jsm[0] + field.Jsm[1] + field.Jsm[2] + field.Jsm[3];
            ushort group = Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(8 + index * 2))).OrderBy(value => value >> 7).ElementAt(3);
            // Explicit component initialization uses the original model and its original JSM group.
            NativeFieldRetainedActor[] actors = [new(3, 3, group >> 7, (group >> 7) + (group & 127) + 1)];
            NativeFieldRegistrationStep registration = NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration, actors, NativeBattleRetainedMemory.FromColdProcess());
            NativeBattleRetainedMemory original = registration.Memory;
            Assert.All(original.FieldTextureAnimations!.Planes!.Closed, closed => Assert.False(closed));
            NativeFieldRegisteredResource resource = Assert.Single(registration.State.Resources);
            uint bank = BinaryPrimitives.ReadUInt32LittleEndian(original.FileStaging!.Read(resource.Address + 20, 4));
            short destinationX = BinaryPrimitives.ReadInt16LittleEndian(original.FileStaging.Read(bank + 6, 2));
            short destinationY = BinaryPrimitives.ReadInt16LittleEndian(original.FileStaging.Read(bank + 8, 2));
            using GpuTestDevice device = new();
            using GpuTriangleRenderer gpu = new(device.Device);
            List<object> captures = [];
            var open = Capture(original, "open");
            NativeBattleRetainedMemory closed = NativeFieldRegistrationMachine.SetEyes532340(registration.State, original, 3, 0x200000, null, null).Memory;
            Assert.True(closed.FieldTextureAnimations!.Planes!.IsClosed(destinationX, destinationY));
            FieldModel closedModel = Bind(closed);
            ResourceTextureList closedMaterials = Assert.IsType<ResourceTextureList>(closedModel.Textures);
            Assert.Contains(Enumerable.Range(0, closedMaterials.Count), index => closedMaterials.GetTexture(index).Variants?.ContainsKey("eyes-closed") == true && packages.Any(package => closedMaterials.GetTexture(index).Provenance.StartsWith(package + "/", StringComparison.Ordinal)));
            var shut = Capture(closed, "closed");
            Assert.NotEqual(open.Cpu, shut.Cpu);
            Assert.NotEqual(open.Gpu, shut.Gpu);
            NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(closed))!;
            Assert.Equal(shut, Capture(restored, "closed-json-restored"));
            Enable(false);
            var disabled = Capture(restored, "closed-packages-disabled");
            Assert.NotEqual(shut.Cpu, disabled.Cpu);
            Assert.NotEqual(shut.Gpu, disabled.Gpu);
            Enable(true);
            Assert.Equal(shut, Capture(restored, "closed-packages-restored"));
            NativeBattleRetainedMemory opened = NativeFieldRegistrationMachine.SetEyes532340(registration.State, restored, 3, 0x100000, null, null).Memory;
            Assert.False(opened.FieldTextureAnimations!.Planes!.IsClosed(destinationX, destinationY));
            Assert.Equal(open, Capture(opened, "reopened"));
            NativeBattleFieldResourceReloadResult started = NativeFieldRegistrationMachine.SetEyes532340(registration.State, opened, 3, 0x80000, 4, NativeCrtRandomState.FromColdProcess());
            NativeBattleRetainedMemory live = started.Memory;
            NativeCrtRandomState? random = started.CrtRandomAfter;
            uint instance = Assert.Single(registration.State.Instances).Address;
            bool sawClosed = false;
            bool sawOpen = false;
            List<object> callbacks = [];
            for (int callback = 0; callback < 40; callback++)
            {
                byte countdown = live.FileStaging!.Read(instance + 102, 1)[0];
                NativeBattleRetainedMemory independent = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(live))!;
                NativeBattleFieldResourceReloadResult next = NativeFieldRegistrationMachine.AdvanceEyes530810(registration.State, live, random);
                NativeBattleFieldResourceReloadResult resumed = NativeFieldRegistrationMachine.AdvanceEyes530810(registration.State, independent, random);
                Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(resumed));
                live = next.Memory;
                random = next.CrtRandomAfter;
                if (countdown != 1)
                {
                    continue;
                }
                byte nativeFrame = live.FileStaging!.Read(instance + 103, 1)[0];
                bool isClosed = live.FieldTextureAnimations!.Planes!.IsClosed(destinationX, destinationY);
                Assert.Equal(nativeFrame == 1, isClosed);
                callbacks.Add(new { callback, nativeFrame, isClosed });
                if (isClosed && !sawClosed || !isClosed && !sawOpen)
                {
                    Assert.Equal(isClosed ? shut : open, Capture(live, isClosed ? "callback-closed" : "callback-open"));
                }
                sawClosed |= isClosed;
                sawOpen |= !isClosed;
            }
            Assert.True(sawClosed);
            Assert.True(sawOpen);
            Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "replacement-failed" or "demaster-import-failed");
            if (EvidenceDirectory() is { } directory)
            {
                File.WriteAllText(Path.Combine(directory, "remastered-eye-plane-rendering.json"), JsonSerializer.Serialize(new { scope = "Actual Remastered ONE/MCH component initialization, native explicit and automatic eye producers, registered resource publication and CPU/GPU field model rendering. No gameplay continuation or checkpoint migration.", field.Id, model = field.Models[3].TextureResourceName, field.NativeRegistration.ExecutableHash, destinationX, destinationY, callbacks, captures }, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(Path.Combine(directory, "remastered-eye-plane-component.json"), JsonSerializer.Serialize(new { registration.State, Memory = live, CrtRandom = random }));
            }

            FieldModel Bind(NativeBattleRetainedMemory memory) => assets.BindNativeRegisteredTextures(field, registration.State, memory.EffectMemory!.Textures!, memory.FieldTextureAnimations).Models[3];

            (string Cpu, string Gpu) Capture(NativeBattleRetainedMemory memory, string phase)
            {
                FieldModel model = Bind(memory);
                List<string> cpuHashes = [];
                List<string> gpuHashes = [];
                for (int direction = 0; direction < 256; direction += 64)
                {
                    Matrix4x4 rotation = Matrix4x4.CreateRotationZ((direction + 64) * MathF.Tau / 256);
                    Vector3[] points = model.Pose(0, 0).Select(point => Vector3.Transform(point, rotation)).ToArray();
                    Vector3 minimum = new(points.Min(point => point.X), points.Min(point => point.Y), points.Min(point => point.Z));
                    Vector3 maximum = new(points.Max(point => point.X), points.Max(point => point.Y), points.Max(point => point.Z));
                    Vector3 center = (minimum + maximum) * 0.5f;
                    float radius = Vector3.Distance(minimum, maximum) * 0.5f;
                    FieldCamera camera = new(Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY, new(-center.X, center.Z, radius * 4 - center.Y), Vector2.Zero, 450);
                    FieldAssets projection = new(field.Id, field.Name, [], [], [], camera, new FieldWalkmesh([]), [model]);
                    ActorVisual actor = new(3, 0, 0, 0, 0, direction, 0, 0, true, PolygonColor: new Vector3(128));
                    FieldRenderer renderer = new(projection, 768, 768);
                    byte[] pixels = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, backgroundVisible: false).ToArray();
                    renderer.TriangleRenderer = gpu;
                    renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, backgroundVisible: false);
                    cpuHashes.Add(Hash(pixels));
                    gpuHashes.Add(Hash(GpuTestDevice.Read(gpu.Target).SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray()));
                    if (EvidenceDirectory() is { } directory)
                    {
                        using var bitmap = GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 768, 768);
                        bitmap.Save(Path.Combine(directory, $"eye-plane-{phase}-{direction}-cpu.png"));
                        using FileStream output = File.Create(Path.Combine(directory, $"eye-plane-{phase}-{direction}-gpu.png"));
                        gpu.Target.SaveAsPng(output, 768, 768);
                    }
                }
                var result = (Cpu: string.Join('/', cpuHashes), Gpu: string.Join('/', gpuHashes));
                captures.Add(new { phase, cpu = cpuHashes, gpu = gpuHashes });
                return result;
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

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string? EvidenceDirectory()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }
        string directory = Path.Combine(root, "RemasteredEyePlanes");
        Directory.CreateDirectory(directory);
        return directory;
    }
}


