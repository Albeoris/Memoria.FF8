using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Mods.Tests;

public sealed class FieldLightingTests : IDisposable
{
    private const string Valid = """
        {"schemaVersion":1,"fieldName":"fixture_a","lights":[{"id":"ceiling","name":"Ceiling","enabled":true,"type":"spot","position":[1,2,300],"direction":[0,0,-1],"color":[1,0.5,0.25],"intensity":2,"range":600,"innerConeDegrees":30,"outerConeDegrees":60}],"primaryLightId":"ceiling","groundShadow":{"opacity":0.5,"softness":1}}
        """;
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FieldLighting.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void RoundTripRetainsPositionDirectionColorAndDisabledPrimary()
    {
        FieldLightingScene scene = FieldLightingSceneIO.Deserialize(Encoding.UTF8.GetBytes(Valid), "fixture_a");
        FieldLight light = Assert.Single(scene.Lights);
        Assert.Equal(new Vector3(1, 2, 300), light.Position);
        Assert.Equal(-Vector3.UnitZ, light.Direction);
        Assert.Equal(new Vector3(1, 0.5f, 0.25f), light.Color);
        scene = scene with { Lights = [light with { Enabled = false }] };
        FieldLightingScene roundTrip = FieldLightingSceneIO.Deserialize(FieldLightingSceneIO.Serialize(scene));
        Assert.False(Assert.Single(roundTrip.Lights).Enabled);
        Assert.Equal("ceiling", roundTrip.PrimaryLightId);
        Assert.Equal(FieldLightingSceneIO.Serialize(scene), FieldLightingSceneIO.Serialize(roundTrip));
    }

    [Fact]
    public void MixedLightsRoundTripWithoutPositionalSunlightProperties()
    {
        FieldLightingScene original = FieldLightingSceneIO.Deserialize(Encoding.UTF8.GetBytes(Valid));
        FieldLight sunlight = new() { Id = "sun", Name = "Sun", Type = FieldLight.SunlightType, Color = new(1, 0.8f, 0.6f), Intensity = 0.6f };
        FieldLightingScene scene = original with { Lights = [original.Lights[0], sunlight], PrimaryLightId = sunlight.Id };
        byte[] bytes = FieldLightingSceneIO.Serialize(scene);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement light = document.RootElement.GetProperty("lights")[1];
        Assert.Equal(7, light.EnumerateObject().Count());
        Assert.Equal("sunlight", light.GetProperty("type").GetString());
        FieldLightingScene restored = FieldLightingSceneIO.Deserialize(bytes);
        Assert.Equal(scene.Lights, restored.Lights);
        Assert.Equal("sun", restored.PrimaryLightId);

        string invalid = Encoding.UTF8.GetString(bytes).Replace("\"type\": \"sunlight\",", "\"type\": \"sunlight\", \"range\": 2,", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => FieldLightingSceneIO.Deserialize(Encoding.UTF8.GetBytes(invalid)));
    }

    [Fact]
    public void Old_documents_default_to_no_receivers_and_authored_patches_round_trip()
    {
        FieldLightingScene scene = FieldLightingSceneIO.Deserialize(Encoding.UTF8.GetBytes(Valid));
        Assert.Empty(scene.ShadowReceivers);
        FieldShadowReceiver floor = new() { Id = "floor-01", Kind = "floor", Center = new(15, -5, 7), Width = 50, Extent = 30 };
        FieldShadowReceiver wall = new() { Id = "wall-01", Kind = "wall", Enabled = false, Center = new(4, 30, 40), Width = 60, Extent = 25, HeadingDegrees = 66 };
        FieldLightingScene updated = scene with { ShadowReceivers = [floor, wall] };
        Assert.Equal(updated.ShadowReceivers, FieldLightingSceneIO.Deserialize(FieldLightingSceneIO.Serialize(updated)).ShadowReceivers);
        Assert.Throws<InvalidDataException>(() => FieldLightingSceneIO.Validate(updated with { ShadowReceivers = [floor, wall with { Id = floor.Id }] }));
        Assert.Throws<InvalidDataException>(() => FieldLightingSceneIO.Validate(updated with { ShadowReceivers = [floor with { Width = 0 }] }));
    }

    [Theory]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":2")]
    [InlineData("\"schemaVersion\":1,", "")]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1")]
    [InlineData("\"fieldName\":\"fixture_a\"", "\"fieldName\":\"../fixture_a\"")]
    [InlineData("\"type\":\"spot\"", "\"type\":\"directional\"")]
    [InlineData("\"type\":\"spot\"", "\"type\":42")]
    [InlineData("\"direction\":[0,0,-1]", "\"direction\":[0,0,0]")]
    [InlineData("\"direction\":[0,0,-1]", "\"direction\":[0,0,-2]")]
    [InlineData("\"position\":[1,2,300]", "\"position\":[1,2,1e100]")]
    [InlineData("\"position\":[1,2,300]", "\"position\":[1,2]")]
    [InlineData("\"position\":[1,2,300]", "\"position\":[1,2,3,4]")]
    [InlineData("\"position\":[1,2,300]", "\"position\":[1,2,\"NaN\"]")]
    [InlineData("\"color\":[1,0.5,0.25]", "\"color\":[1,-0.1,0]")]
    [InlineData("\"intensity\":2", "\"intensity\":-1")]
    [InlineData("\"range\":600", "\"range\":0")]
    [InlineData("\"innerConeDegrees\":30", "\"innerConeDegrees\":60")]
    [InlineData("\"outerConeDegrees\":60", "\"outerConeDegrees\":86")]
    [InlineData("\"primaryLightId\":\"ceiling\"", "\"primaryLightId\":\"missing\"")]
    [InlineData("\"opacity\":0.5", "\"opacity\":1.01")]
    [InlineData("\"softness\":1", "\"softness\":3.01")]
    [InlineData("\"softness\":1", "\"softness\":1,\"ambient\":0.1")]
    [InlineData("\"enabled\":true,", "")]
    public void InvalidOrAmbiguousDocumentsAreRejectedWithoutPartialPublication(string before, string after)
    {
        string invalid = Valid.Replace(before, after, StringComparison.Ordinal);
        ModCatalogSnapshot catalog = new(1, [], [Marker("Memoria/lights"), Payload("Memoria/lights", "fixture_a", invalid)]);
        Assert.False(FieldLightingMods.TryPrepare(catalog, out FieldLightingMods result, out IReadOnlyList<ModDiagnostic> errors));
        Assert.False(result.Active);
        Assert.Equal("Memoria.InvalidFieldLighting", Assert.Single(errors).Code);
    }

    [Fact]
    public void EightEnabledLightsAreAllowedAndAdditionalDisabledLightsArePreserved()
    {
        FieldLightingScene scene = new() { FieldName = "fixture_a", Lights = Enumerable.Range(0, 12).Select(index => new FieldLight { Id = "lamp-" + index, Name = "Lamp " + index, Enabled = index < 8 }).ToArray() };
        FieldLightingSceneIO.Validate(scene);
        Assert.Equal(12, FieldLightingSceneIO.Deserialize(FieldLightingSceneIO.Serialize(scene)).Lights.Count);
        Assert.Throws<InvalidDataException>(() => FieldLightingSceneIO.Validate(scene with { Lights = scene.Lights.Select(light => light with { Enabled = true }).ToArray() }));
        Assert.Throws<InvalidDataException>(() => FieldLightingSceneIO.Validate(scene with { Lights = [scene.Lights[0], scene.Lights[0]] }));
    }

    [Fact]
    public void SelectedComponentOwnsAbsentScenesAndNeverMergesLowerPriorityLights()
    {
        string other = Valid.Replace("fixture_a", "fixture_b", StringComparison.Ordinal);
        ModCatalogSnapshot catalog = new(1, [], [Marker("Memoria/high"), Payload("Memoria/high", "fixture_a", Valid), Marker("Memoria/low"), Payload("Memoria/low", "fixture_b", other)]);
        Assert.True(FieldLightingMods.TryPrepare(catalog, out FieldLightingMods result, out _));
        Assert.Equal("Memoria/high", result.PackageId);
        Assert.Single(result.ResolveScene("fixture_a")!.Lights);
        Assert.Empty(result.ResolveScene("fixture_b")!.Lights);
        Assert.Null(FieldLightingMods.Empty.ResolveScene("fixture_a"));
        Assert.True(FieldLightingMods.TryPrepare(new(2, [], [Marker("Memoria/empty")]), out FieldLightingMods empty, out _));
        Assert.True(empty.Active);
        Assert.Empty(empty.ResolveScene("fixture_a")!.Lights);
    }

    [Theory]
    [InlineData("lighting/default.json", true)]
    [InlineData("lighting/fields/fixture_b.json", true)]
    [InlineData("lighting/fields/fixture_a.json", false)]
    public void GlobalWrongSelectorAndUnmarkedPayloadsAreRejected(string path, bool marker)
    {
        List<ModFile> files = [new("Memoria/lights", ModFormat.Memoria, path, 1, () => Encoding.UTF8.GetBytes(Valid))];
        if (marker)
        {
            files.Add(Marker("Memoria/lights"));
        }
        Assert.False(FieldLightingMods.TryPrepare(new(1, [], files), out _, out _));
    }

    [Fact]
    public void CreationIsDisabledAndReadingAnUntouchedSceneDoesNotWriteIt()
    {
        FieldLightingPackageStore store = FieldLightingPackageStore.Create(directory, "My lighting");
        Assert.False(store.Package.RequestedEnabled);
        Assert.False(store.Package.ExplicitEnabled);
        Assert.True(store.Package.HasLighting);
        FieldLightingSceneSnapshot empty = store.ReadScene("fixture_a");
        Assert.Empty(empty.Scene.Lights);
        Assert.Null(empty.Scene.PrimaryLightId);
        Assert.Equal(FieldLightingPackageStore.AbsentRevision, empty.Revision);
        Assert.False(Directory.Exists(Path.Combine(store.Package.RootDirectory, "lighting", "fields")));
        Assert.Equal("My lighting", NativeModDescriptor.Read(Path.Combine(store.Package.RootDirectory, "mod.json")).Name);
    }

    [Fact]
    public void SavesAreIsolatedAndExplicitEmptyScenesSurviveReload()
    {
        FieldLightingPackageStore store = FieldLightingPackageStore.Create(directory, "Two fields");
        string unrelated = Path.Combine(store.Package.RootDirectory, "unrelated.txt");
        File.WriteAllText(unrelated, "retained");
        byte[] manifest = File.ReadAllBytes(Path.Combine(store.Package.RootDirectory, "mod.json"));
        FieldLightingScene first = FieldLightingSceneIO.Deserialize(Encoding.UTF8.GetBytes(Valid));
        FieldLightingSceneSnapshot savedA = store.SaveScene(first, FieldLightingPackageStore.AbsentRevision);
        FieldLightingScene second = first with { FieldName = "fixture_b", Lights = [first.Lights[0] with { Position = new(10, 20, -30), Color = new(0, 0, 1) }] };
        store.SaveScene(second, FieldLightingPackageStore.AbsentRevision);
        FieldLightingPackageStore reopened = FieldLightingPackageStore.Open(store.Package);
        Assert.Equal(FieldLightingSceneIO.Serialize(first), FieldLightingSceneIO.Serialize(reopened.ReadScene("fixture_a").Scene));
        Assert.Equal(FieldLightingSceneIO.Serialize(second), FieldLightingSceneIO.Serialize(reopened.ReadScene("fixture_b").Scene));
        reopened.SaveScene(FieldLightingScene.Empty("fixture_a"), savedA.Revision);
        Assert.Empty(reopened.ReadScene("fixture_a").Scene.Lights);
        Assert.Null(reopened.ReadScene("fixture_a").Scene.PrimaryLightId);
        Assert.True(File.Exists(Path.Combine(store.Package.RootDirectory, FieldLightingSceneIO.ScenePath("fixture_a"))));
        Assert.Single(reopened.ReadScene("fixture_b").Scene.Lights);
        Assert.Equal("retained", File.ReadAllText(unrelated));
        Assert.Equal(manifest, File.ReadAllBytes(Path.Combine(store.Package.RootDirectory, "mod.json")));
        Assert.Empty(Directory.EnumerateFiles(store.Package.RootDirectory, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void ConcurrentSaveAndExternalChangesCannotSilentlyOverwriteTheLoadedRevision()
    {
        FieldLightingPackageStore store = FieldLightingPackageStore.Create(directory, "Conflicts");
        FieldLightingScene scene = FieldLightingSceneIO.Deserialize(Encoding.UTF8.GetBytes(Valid));
        FieldLightingSceneSnapshot saved = store.SaveScene(scene, FieldLightingPackageStore.AbsentRevision);
        Assert.Throws<IOException>(() => store.SaveScene(FieldLightingScene.Empty("fixture_a"), FieldLightingPackageStore.AbsentRevision));
        string path = Path.Combine(store.Package.RootDirectory, FieldLightingSceneIO.ScenePath("fixture_a"));
        byte[] external = FieldLightingSceneIO.Serialize(scene with { GroundShadow = new() { Opacity = 0.75f } });
        File.WriteAllBytes(path, external);
        Assert.Throws<IOException>(() => store.SaveScene(FieldLightingScene.Empty("fixture_a"), saved.Revision));
        Assert.Equal(external, File.ReadAllBytes(path));
        Assert.Equal("ceiling", saved.Scene.PrimaryLightId);
        File.Delete(path);
        Assert.Throws<IOException>(() => store.SaveScene(scene, saved.Revision));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ManagedPackageCopyPreservesSourceAndUnrelatedContentUnderANewIdentity()
    {
        FieldLightingPackageStore source = FieldLightingPackageStore.Create(directory, "Original");
        source.SaveScene(FieldLightingSceneIO.Deserialize(Encoding.UTF8.GetBytes(Valid)), FieldLightingPackageStore.AbsentRevision);
        File.WriteAllText(Path.Combine(source.Package.RootDirectory, "unrelated.txt"), "keep me");
        ModPackage managed = source.Package with { IsBundled = true };
        Assert.False(FieldLightingPackageStore.CanEdit(managed, out string? reason));
        Assert.Contains("custom copy", reason);
        Assert.Throws<IOException>(() => FieldLightingPackageStore.Open(managed));
        Assert.Single(FieldLightingPackageStore.OpenReadOnly(managed).ReadScene("fixture_a").Scene.Lights);
        FieldLightingPackageStore copy = FieldLightingPackageStore.CreateWritableCopy(directory, managed, "My editable copy");
        Assert.NotEqual(source.Package.Id, copy.Package.Id);
        Assert.False(copy.Package.RequestedEnabled);
        Assert.True(copy.IsWritable);
        Assert.Equal("keep me", File.ReadAllText(Path.Combine(copy.Package.RootDirectory, "unrelated.txt")));
        FieldLightingSceneSnapshot copied = copy.ReadScene("fixture_a");
        copy.SaveScene(FieldLightingScene.Empty("fixture_a"), copied.Revision);
        Assert.Single(source.ReadScene("fixture_a").Scene.Lights);
    }

    [Fact]
    public void RuntimePublishesValidatedLightingTogetherAndKeepsLastGoodGeneration()
    {
        FieldLightingPackageStore store = FieldLightingPackageStore.Create(directory, "Runtime");
        using ModRuntime runtime = new(applicationBaseDirectory: directory, modsDirectory: directory, watch: false, prepare: _ => new(true, UsablePackageIds: []));
        Assert.True(runtime.PreparePending(force: true), string.Join("; ", runtime.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.True(runtime.CommitPending());
        ModPackage package = Assert.Single(runtime.InstalledPackages);
        Assert.True(package.HasLighting);
        Assert.False(runtime.Lighting.Active);
        Assert.True(runtime.SetEnabled(package.Id, true).Success);
        Assert.True(runtime.PreparePending(force: true));
        Assert.False(runtime.Lighting.Active);
        Assert.True(runtime.CommitPending());
        Assert.True(runtime.Lighting.Active);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.InstalledPackages).Status);
        Assert.Empty(runtime.Lighting.ResolveScene("fixture_a")!.Lights);
        store.SaveScene(FieldLightingSceneIO.Deserialize(Encoding.UTF8.GetBytes(Valid)), FieldLightingPackageStore.AbsentRevision);
        runtime.RequestRefresh();
        Assert.True(runtime.PreparePending(force: true));
        Assert.Empty(runtime.Lighting.ResolveScene("fixture_a")!.Lights);
        Assert.True(runtime.CommitPending());
        FieldLightingMods committed = runtime.Lighting;
        Assert.Single(committed.ResolveScene("fixture_a")!.Lights);
        File.WriteAllText(Path.Combine(store.Package.RootDirectory, FieldLightingSceneIO.ScenePath("fixture_a")), "{}");
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.False(runtime.CommitPending());
        Assert.Same(committed, runtime.Lighting);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidFieldLighting");
    }

    private static ModFile Marker(string package)
    {
        return new(package, ModFormat.Memoria, FieldLightingSceneIO.ComponentPath, 1, () => "{\"schemaVersion\":1}"u8.ToArray());
    }

    private static ModFile Payload(string package, string field, string json)
    {
        return new(package, ModFormat.Memoria, FieldLightingSceneIO.ScenePath(field), 1, () => Encoding.UTF8.GetBytes(json));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
