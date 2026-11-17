using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

/// <summary>Opt-in rendering checks use two independently supplied standalone field saves without advancing gameplay.</summary>
[Collection("GPU rendering")]
public sealed class LightingFieldIntegrationTests
{
    [Fact]
    public void StandaloneFieldsPersistIndependentScenesAndFreezeNativePresentation()
    {
        string sourceA = Standalone("MEMORIA_FF8_LIGHTING_FIELD_A");
        string sourceB = Standalone("MEMORIA_FF8_LIGHTING_FIELD_B");
        string gameDirectory = TestResources.GameDirectory;
        byte[] sourceHashA = SHA256.HashData(File.ReadAllBytes(sourceA));
        byte[] sourceHashB = SHA256.HashData(File.ReadAllBytes(sourceB));
        string temporary = TemporaryDirectory();
        try
        {
            string mods = Path.Combine(temporary, "Mods");
            FieldLightingPackageStore store = FieldLightingPackageStore.Create(mods, "Independent field lighting");
            using GpuTestDevice device = new();
            using GpuTriangleRenderer gpu = new(device.Device);
            Assert.True(gpu.FieldLightingSupported, gpu.FieldLightingUnavailableReason);
            FieldLightingScene authoredA;
            FieldLightingScene authoredB;
            using (GameResourceHost host = new(gameDirectory, temporary, watch: false, modsDirectory: mods))
            {
                using (GameWorld worldA = LoadWorld(sourceA, gameDirectory, host))
                {
                    Assert.All(worldA.Field!.Models, model => Assert.Null(model.EditableOverride));
                    authoredA = LampsAroundPresentedActors(worldA, new(1, 0.35f, 0.1f));
                    FieldLightingSceneSnapshot emptyA = store.ReadScene(authoredA.FieldName);
                    Assert.Equal(FieldLightingPackageStore.AbsentRevision, emptyA.Revision);
                    store.SaveScene(authoredA, emptyA.Revision);
                    VerifyFrozenLighting(worldA, gpu, authoredA, "field-a-native");
                }
                using (GameWorld worldB = LoadWorld(sourceB, gameDirectory, host))
                {
                    Assert.NotEqual(authoredA.FieldName, worldB.Field!.Name);
                    FieldLightingSceneSnapshot absentB = store.ReadScene(worldB.Field.Name);
                    Assert.Empty(absentB.Scene.Lights);
                    Assert.Equal(FieldLightingPackageStore.AbsentRevision, absentB.Revision);
                    authoredB = LampsAroundPresentedActors(worldB, new(0.1f, 0.35f, 1));
                    store.SaveScene(authoredB, absentB.Revision);
                    VerifyFrozenLighting(worldB, gpu, authoredB, "field-b-native");
                }
                Assert.False(host.Mods.Lighting.Active);
                Assert.True(host.Mods.SetEnabled(store.Package.Id, true).Success);
                Assert.True(host.Pump(true), string.Join("; ", host.Mods.Diagnostics.Select(diagnostic => diagnostic.Message)));
                Assert.Equal(store.Package.Id, host.Mods.Lighting.PackageId);
                AssertScenesEqual(authoredA, host.Mods.Lighting.ResolveScene(authoredA.FieldName)!);
                AssertScenesEqual(authoredB, host.Mods.Lighting.ResolveScene(authoredB.FieldName)!);
                Assert.Empty(host.Mods.Lighting.ResolveScene("untouched_fixture")!.Lights);
            }

            FieldLightingPackageStore reopened = FieldLightingPackageStore.Open(store.Package);
            AssertScenesEqual(authoredA, reopened.ReadScene(authoredA.FieldName).Scene);
            AssertScenesEqual(authoredB, reopened.ReadScene(authoredB.FieldName).Scene);
            using (GameResourceHost restarted = new(gameDirectory, temporary, watch: false, modsDirectory: mods))
            {
                Assert.Equal(store.Package.Id, restarted.Mods.Lighting.PackageId);
                using GameWorld restoredA = LoadWorld(sourceA, gameDirectory, restarted);
                VerifyFrozenLighting(restoredA, gpu, restarted.Mods.Lighting.ResolveScene(authoredA.FieldName)!, "field-a-restarted");
                AssertScenesEqual(authoredB, restarted.Mods.Lighting.ResolveScene(authoredB.FieldName)!);
            }
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
        Assert.Equal(sourceHashA, SHA256.HashData(File.ReadAllBytes(sourceA)));
        Assert.Equal(sourceHashB, SHA256.HashData(File.ReadAllBytes(sourceB)));
    }

    [Theory]
    [InlineData("MEMORIA_FF8_LIGHTING_FIELD_A", "field-a-replacement")]
    [InlineData("MEMORIA_FF8_LIGHTING_FIELD_B", "field-b-replacement")]
    public void ImportedFieldModelReceivesLightingWithoutMaterialOrMeshEdits(string variable, string outputName)
    {
        string source = Standalone(variable);
        string gameDirectory = TestResources.GameDirectory;
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        string temporary = TemporaryDirectory();
        try
        {
            string mods = Path.Combine(temporary, "Mods");
            string fieldName;
            int modelId;
            FieldLightingScene scene;
            byte[] replacement;
            using (GameWorld original = LoadWorld(source, gameDirectory))
            {
                FieldAssets field = original.Field!;
                ActorVisual actor = original.Events.Actors.Select(original.CreateVisual).First(visual => visual.Visible && (uint)visual.ModelId < field.Models.Count && field.Models[visual.ModelId].Faces.Count > 0);
                modelId = actor.ModelId;
                fieldName = field.Name;
                FieldModel model = field.Models[modelId];
                Assert.Null(model.EditableOverride);
                replacement = ModelExchange.ExportGlb(ModelExchange.FromField(model));
                scene = LampsAroundPresentedActors(original, Vector3.One);
            }
            string package = Path.Combine(mods, "Memoria", "lighting-model-fixture");
            string modelPath = Path.Combine(package, "models", "field", fieldName, modelId + ".glb");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            File.WriteAllText(Path.Combine(package, "mod.ini"), "[Mod]\nEnabled=1\n");
            File.WriteAllBytes(modelPath, replacement);
            using GameResourceHost host = new(gameDirectory, temporary, watch: false, modsDirectory: mods);
            Assert.Contains(host.ModelMods.Variants, variant => variant.ModelId == $"field/{fieldName}/{modelId}");
            using GameWorld imported = LoadWorld(source, gameDirectory, host);
            Assert.NotNull(imported.Field!.Models[modelId].EditableOverride);
            using GpuTestDevice device = new();
            using GpuTriangleRenderer gpu = new(device.Device);
            Assert.True(gpu.FieldLightingSupported, gpu.FieldLightingUnavailableReason);
            VerifyFrozenLighting(imported, gpu, scene, outputName);
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
    }

    private static GameWorld LoadWorld(string source, string gameDirectory, GameResourceHost? host = null)
    {
        GameSession session = new(GameSession.Load(source));
        Assert.Equal(SceneKind.Field, session.State.Scene);
        GameWorld world = new(session, gameDirectory, resources: host?.Resources, modelOverrides: host, readSceneMods: host is null ? null : () => host.SceneMods);
        try
        {
            world.EnsureField();
            world.SetRenderSize(640, 448);
            Assert.True(world.CanPresentCurrentField, "The supplied standalone must restore a stable presentable field.");
            return world;
        }
        catch
        {
            world.Dispose();
            throw;
        }
    }

    private static FieldLightingScene LampsAroundPresentedActors(GameWorld world, Vector3 color)
    {
        FieldAssets field = world.Field!;
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        foreach (ActorVisual actor in world.Events.Actors.Select(world.CreateVisual).Where(actor => actor.Visible && (uint)actor.ModelId < field.Models.Count))
        {
            FieldModel model = field.Models[actor.ModelId];
            if (model.Faces.Count == 0)
            {
                continue;
            }
            Matrix4x4 rotation = Matrix4x4.CreateRotationZ((actor.Direction + 64) * MathF.Tau / 256);
            foreach (Vector3 point in model.Pose(actor.AnimationId, actor.AnimationFrame, actor.FirstControlRotation, actor.RootTranslationZ, actor.SkinTranslations))
            {
                Vector3 position = Vector3.Transform(point, rotation) + new Vector3(actor.X, actor.Y, actor.Z);
                minimum = Vector3.Min(minimum, position);
                maximum = Vector3.Max(maximum, position);
            }
        }
        Assert.True(float.IsFinite(minimum.X) && float.IsFinite(maximum.X), "The standalone field must contain a visible model with geometry.");
        Vector3 center = (minimum + maximum) / 2;
        float radius = Math.Max(100, Vector3.Distance(minimum, maximum));
        Vector3[] axes = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        FieldLightingScene scene = new()
        {
            FieldName = field.Name,
            Lights = axes.Select((axis, index) => new FieldSpotLight
            {
                Id = "fixture-spot-" + index,
                Name = "Fixture spotlight " + index,
                Position = center + axis * radius * 3,
                Direction = -axis,
                Color = color,
                Intensity = 2,
                Range = radius * 8,
                InnerConeDegrees = 35,
                OuterConeDegrees = 70
            }).ToArray()
        };
        FieldLightingSceneIO.Validate(scene);
        return scene;
    }

    private static void VerifyFrozenLighting(GameWorld world, GpuTriangleRenderer gpu, FieldLightingScene scene, string outputName)
    {
        world.ActiveLighting = null;
        world.Render(TimeSpan.Zero, rareCardMarkers: false, triangleRenderer: gpu);
        Color[] legacy = GpuTestDevice.Read(gpu.Target);
        JsonElement before = world.Events.Capture();
        Assert.True(world.BeginLightingEdit());
        try
        {
            Color[] Render(FieldLightingScene? lighting, string suffix)
            {
                world.ActiveLighting = lighting;
                world.Render(TimeSpan.Zero, rareCardMarkers: false, triangleRenderer: gpu, fieldInterpolation: 0.25f, smoothFieldAnimation: true);
                Assert.Null(world.LightingUnavailableReason);
                Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()), "Rendering and lighting edits must preserve the complete frozen event state.");
                Color[] pixels = GpuTestDevice.Read(gpu.Target);
                string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIGHTING_TEST_OUTPUT");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Directory.CreateDirectory(output);
                    using FileStream image = File.Create(Path.Combine(output, outputName + "-" + suffix + ".png"));
                    gpu.Target.SaveAsPng(image, gpu.Target.Width, gpu.Target.Height);
                }
                return pixels;
            }

            Color[] dark = Render(FieldLightingScene.Empty(scene.FieldName), "empty");
            Color[] lit = Render(scene, "lit");
            int changed = 0;
            int blackModelPixels = 0;
            for (int pixel = 0; pixel < dark.Length; pixel++)
            {
                if (dark[pixel] != lit[pixel])
                {
                    changed++;
                    if (dark[pixel].R == 0 && dark[pixel].G == 0 && dark[pixel].B == 0 && lit[pixel].R + lit[pixel].G + lit[pixel].B > 0)
                    {
                        blackModelPixels++;
                    }
                }
            }
            Assert.True(changed > 10, "Explicit positional lamps must change visible native or imported model pixels.");
            Assert.True(blackModelPixels > 10, "Empty active lighting must leave visible opaque model surfaces completely black.");
            Assert.True(changed < dark.Length / 2, "Lighting must preserve the painted background.");
            Assert.Equal(dark, Render(scene with { Lights = scene.Lights.Select(light => light with { Enabled = false }).ToArray() }, "disabled"));
            Assert.Equal(dark, Render(scene with { Lights = scene.Lights.Select(light => light with { Direction = -light.Direction }).ToArray() }, "aimed-away"));
            Assert.Equal(lit, Render(scene, "restored"));
            Assert.Equal(legacy, Render(null, "inactive"));
        }
        finally
        {
            world.EndLightingEdit();
        }
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
    }

    private static void AssertScenesEqual(FieldLightingScene expected, FieldLightingScene actual)
    {
        Assert.Equal(FieldLightingSceneIO.Serialize(expected), FieldLightingSceneIO.Serialize(actual));
    }

    private static string Standalone(string variable)
    {
        string? source = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(source) && File.Exists(source), $"Set {variable} to an optional standalone field save.");
        return Path.GetFullPath(source!);
    }

    private static string TemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Memoria.Lighting.Integration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
