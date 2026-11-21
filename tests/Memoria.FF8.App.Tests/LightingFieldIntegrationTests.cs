using System.Numerics;
using System.Diagnostics;
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
public sealed class LightingFieldIntegrationTests(ITestOutputHelper output)
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
            using (GameResourceHost originalResources = new(gameDirectory, temporary, watch: false, modsDirectory: mods))
            using (GameWorld original = LoadWorld(source, gameDirectory, originalResources))
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

    private static GameWorld LoadWorld(string source, string gameDirectory, GameResourceHost host)
    {
        GameSession session = new(PlayerStateLoader.Load(source, gameDirectory, resources: host.Resources, modelOverrides: host, readGameplayPolicy: () => host.GameplayPolicy, prepareCharacterDefinitions: initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, host)));
        Assert.Equal(SceneKind.Field, session.State.Scene);
        GameWorld world = new(session, gameDirectory, resources: host.Resources, modelOverrides: host, readSceneMods: () => host.SceneMods, readGameplayPolicy: () => host.GameplayPolicy);
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
            Lights = axes.Select((axis, index) => new FieldLight
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

    private void VerifyFrozenLighting(GameWorld world, GpuTriangleRenderer gpu, FieldLightingScene scene, string outputName)
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
                if (!string.IsNullOrWhiteSpace(output) && suffix is "empty" or "lit" or "primary-shadow" or "primary-disabled" or "primary-moved-shadow" or "inactive" or "primary-moved-unshadowed")
                {
                    Directory.CreateDirectory(output);
                    using FileStream image = File.Create(Path.Combine(output, outputName + "-" + suffix + ".png"));
                    gpu.Target.SaveAsPng(image, gpu.Target.Width, gpu.Target.Height);
                }
                return pixels;
            }

            Assert.Equal(legacy, Render(FieldLightingScene.Empty(scene.FieldName), "empty"));
            Color[] dark = Render(scene with { Lights = scene.Lights.Select(light => light with { Enabled = false }).ToArray() }, "disabled");
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
            Assert.True(blackModelPixels > 10, "An authored scene with all sources disabled must leave visible opaque model surfaces completely black.");
            Assert.True(changed < dark.Length / 2, "Lighting must preserve the painted background.");
            Assert.Equal(dark, Render(scene with { Lights = scene.Lights.Select(light => light with { Direction = -light.Direction }).ToArray() }, "aimed-away"));
            Assert.Equal(lit, Render(scene, "restored"));
            Assert.Equal(legacy, Render(scene with { Lights = [], PrimaryLightId = null }, "removed-all"));
            Assert.Equal(legacy, Render(scene with { FieldName = "another_field" }, "another-field"));
            Assert.Equal(legacy, Render(null, "inactive"));

            if (outputName == "field-a-native")
            {
                FieldCamera camera = world.Field!.GetCamera(world.Events.Scene.CameraIndex);
                Vector3 pivot = world.Field.Walkmesh.Triangles[0].A;
                Matrix4x4 yaw = Matrix4x4.CreateRotationZ(0.08f);
                Vector3 x = Vector3.TransformNormal(camera.AxisX, yaw);
                Vector3 y = Vector3.TransformNormal(camera.AxisY, yaw);
                Vector3 z = Vector3.TransformNormal(camera.AxisZ, yaw);
                FieldCamera inspection = camera with
                {
                    AxisX = x,
                    AxisY = y,
                    AxisZ = z,
                    Translation = camera.Translation + new Vector3(Vector3.Dot(camera.AxisX - x, pivot), Vector3.Dot(camera.AxisY - y, pivot), Vector3.Dot(camera.AxisZ - z, pivot))
                };
                world.Render(TimeSpan.Zero, rareCardMarkers: false, triangleRenderer: gpu, inspectionCamera: inspection);
                Color[] geometryOnly = GpuTestDevice.Read(gpu.Target);
                world.Render(TimeSpan.Zero, rareCardMarkers: false, triangleRenderer: gpu, inspectionCamera: inspection, previewInspectionBackdrop: true);
                Color[] withBackdrop = GpuTestDevice.Read(gpu.Target);
                Assert.True(geometryOnly.Zip(withBackdrop).Count(pair => pair.First != pair.Second) > 100);
                Assert.True(legacy.Zip(withBackdrop).Count(pair => pair.First != pair.Second) > 100);
                Assert.Equal(legacy, Render(null, "inspection-reset"));
            }

            FieldLight top = scene.Lights[4];
            FieldLightingScene primaryScene = scene with { PrimaryLightId = top.Id };
            Color[] primary = Render(primaryScene, "primary-shadow");
            Dictionary<int, string> primaryStates = world.LightingShadowStates.ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
            string? primaryDiagnostic = world.LightingShadowDiagnostic;
            bool[] footprint = GroundShadowMask(dark, lit, primary);
            int groundPixels = footprint.Count(value => value);

            FieldLightingScene disabledPrimary = primaryScene with { Lights = scene.Lights.Select(light => light.Id == top.Id ? light with { Enabled = false } : light).ToArray() };
            Color[] disabledShadow = Render(disabledPrimary, "primary-disabled");
            Assert.All(world.LightingShadowStates.Values, state => Assert.Equal(FieldActorShadowState.IntentionalNone, state));
            Assert.Equal(disabledShadow, Render(disabledPrimary with { PrimaryLightId = null }, "primary-disabled-unselected"));

            Vector3 center = (scene.Lights[4].Position + scene.Lights[5].Position) / 2;
            Vector3 movedPosition = top.Position + new Vector3(top.Range / 8, top.Range / 16, 0);
            FieldLight moved = top with { Position = movedPosition, Direction = Vector3.Normalize(center - movedPosition) };
            FieldLightingScene movedScene = primaryScene with { Lights = scene.Lights.Select(light => light.Id == top.Id ? moved : light).ToArray() };
            Color[] movedUnshadowed = Render(movedScene with { PrimaryLightId = null }, "primary-moved-unshadowed");
            Color[] movedShadow = Render(movedScene, "primary-moved-shadow");
            Dictionary<int, string> movedStates = world.LightingShadowStates.ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
            string? movedDiagnostic = world.LightingShadowDiagnostic;
            bool[] movedFootprint = GroundShadowMask(dark, movedUnshadowed, movedShadow);
            int movedGroundPixels = movedFootprint.Count(value => value);
            int footprintChanges = footprint.Zip(movedFootprint).Count(pair => pair.First != pair.Second);
            if (primaryStates.Values.Contains(nameof(FieldActorShadowState.Silhouette)) && groundPixels > 0)
            {
                Assert.True(footprintChanges > 0, "Moving an actual contributing Primary must move its visible ground-shadow footprint.");
            }
            IReadOnlyList<CpuSubmissionTiming> cpuSubmissionTimings = outputName is "field-a-native" or "field-b-native" ? MeasureCpuSubmission(world, gpu, scene) : [];
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
            string summary = JsonSerializer.Serialize(new
            {
                Fixture = outputName,
                Field = scene.FieldName,
                world.Events.Frame,
                Adapter = Microsoft.Xna.Framework.Graphics.GraphicsAdapter.DefaultAdapter.Description,
                Backend = "WindowsDX",
                EnabledLights = scene.Lights.Count,
                ChangedModelPixels = changed,
                BlackModelPixels = blackModelPixels,
                PrimaryStates = primaryStates,
                PrimaryDiagnostic = primaryDiagnostic,
                GroundShadowPixels = groundPixels,
                MovedPrimaryStates = movedStates,
                MovedPrimaryDiagnostic = movedDiagnostic,
                MovedGroundShadowPixels = movedGroundPixels,
                GroundFootprintChanges = footprintChanges,
                CpuSubmissionMeasurement = "Stopwatch wall time around GameWorld.Render, including CPU work and graphics submission/driver waits. No per-frame readback. This is not GPU execution time.",
                CpuSubmissionTimings = cpuSubmissionTimings,
                GpuExecutionMilliseconds = (double?)null
            }, new JsonSerializerOptions { WriteIndented = true });
            output.WriteLine(summary);
            string? outputDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIGHTING_TEST_OUTPUT");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                File.WriteAllText(Path.Combine(outputDirectory, outputName + "-summary.json"), summary);
            }
        }
        finally
        {
            world.EndLightingEdit();
        }
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
    }

    private sealed record CpuSubmissionTiming(string Scenario, int Width, int Height, int WarmupFrames, int MeasuredFrames, double AverageMilliseconds, double P95Milliseconds);

    private static IReadOnlyList<CpuSubmissionTiming> MeasureCpuSubmission(GameWorld world, GpuTriangleRenderer gpu, FieldLightingScene scene)
    {
        const int warmupFrames = 10;
        const int measuredFrames = 60;
        FieldLight top = scene.Lights[4];
        Vector3 center = (scene.Lights[4].Position + scene.Lights[5].Position) / 2;
        Vector3 diagonal = Vector3.Normalize(Vector3.One);
        float distance = Vector3.Distance(top.Position, center);
        FieldLight seventh = top with { Id = "perf-spot-6", Name = "Performance spotlight 6", Position = center + diagonal * distance, Direction = -diagonal };
        FieldLight eighth = top with { Id = "perf-spot-7", Name = "Performance spotlight 7", Position = center - diagonal * distance, Direction = diagonal };
        FieldLightingScene one = scene with { Lights = [top], PrimaryLightId = top.Id };
        FieldLightingScene eight = scene with { Lights = [.. scene.Lights, seventh, eighth], PrimaryLightId = top.Id };
        FieldLightingSceneIO.Validate(one);
        FieldLightingSceneIO.Validate(eight);
        List<CpuSubmissionTiming> results = [];
        FieldLightingScene? previous = world.ActiveLighting;
        int previousWidth = world.RenderWidth;
        int previousHeight = world.RenderHeight;
        try
        {
            world.SetRenderSize(1920, 1080);
            foreach ((string scenario, FieldLightingScene? lighting) in new (string, FieldLightingScene?)[] { ("Inactive", null), ("One spotlight with Primary", one), ("Eight spotlights with Primary", eight) })
            {
                world.ActiveLighting = lighting;
                for (int frame = 0; frame < warmupFrames; frame++)
                {
                    world.Render(TimeSpan.Zero, rareCardMarkers: false, triangleRenderer: gpu);
                }
                double[] milliseconds = new double[measuredFrames];
                for (int frame = 0; frame < measuredFrames; frame++)
                {
                    long started = Stopwatch.GetTimestamp();
                    world.Render(TimeSpan.Zero, rareCardMarkers: false, triangleRenderer: gpu);
                    milliseconds[frame] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                }
                Assert.Null(world.LightingUnavailableReason);
                Array.Sort(milliseconds);
                results.Add(new(scenario, 1920, 1080, warmupFrames, measuredFrames, milliseconds.Average(), milliseconds[(int)Math.Ceiling(measuredFrames * 0.95) - 1]));
            }
        }
        finally
        {
            world.ActiveLighting = previous;
            world.SetRenderSize(previousWidth, previousHeight);
        }
        return results;
    }

    private static bool[] GroundShadowMask(Color[] dark, Color[] unshadowed, Color[] shadowed)
    {
        return Enumerable.Range(0, dark.Length).Select(pixel => dark[pixel] == unshadowed[pixel] && shadowed[pixel].R + shadowed[pixel].G + shadowed[pixel].B < unshadowed[pixel].R + unshadowed[pixel].G + unshadowed[pixel].B).ToArray();
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
