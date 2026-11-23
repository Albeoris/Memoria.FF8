using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods.Tests;

public sealed class FieldScene3DTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.Scene3D.Tests", Guid.NewGuid().ToString("N"));
    private const string Field = "fixture_a";
    private const string Asset = "painting";

    [Fact]
    public void DisabledPackagePublishesAssetAndOnlyItsFieldAfterRevisionCheck()
    {
        FieldScene3DPackageStore store = FieldScene3DPackageStore.Create(root, "Field scene fixture");
        Assert.False(store.Package.RequestedEnabled);
        FieldSceneDraft empty = store.Open(Field);
        string fieldPath = Path.Combine(store.Package.RootDirectory, FieldSceneDocuments.ScenePath(Field));
        Assert.False(File.Exists(fieldPath));
        byte[] glb = Triangle();
        string hash = FieldSceneDocuments.Digest(glb);
        FieldSceneDocument document = Document(hash);
        FieldSceneDraft saved = store.Save(document, empty.Revision, new Dictionary<string, byte[]> { [hash] = glb });
        Assert.True(File.Exists(Path.Combine(store.Package.RootDirectory, FieldSceneDocuments.AssetPath(hash))));
        Assert.Equal(saved.Revision, store.Open(Field).Revision);
        Assert.Throws<IOException>(() => store.Save(document, empty.Revision, new Dictionary<string, byte[]>()));
        Assert.Equal(saved.Revision, store.Open(Field).Revision);
        Assert.Empty(store.Open("fixture_b").Prepared.Document.Views);
        Assert.Contains("enabled=0", File.ReadAllText(Path.Combine(store.Package.RootDirectory, "mod.ini")));
    }

    [Fact]
    public void HigherPriorityReplacementClaimsItsScopeBeforeDependenciesAreDecoded()
    {
        byte[] glb = Triangle();
        string hash = FieldSceneDocuments.Digest(glb);
        FieldSceneDocument document = Document(hash);
        ModCatalogSnapshot snapshot = new(1, [], [Marker("higher"), Binding("higher", document), Payload("higher", hash, glb), Marker("lower"), Binding("lower", document)]);
        Assert.True(ModelMods.TryPrepare(snapshot, out ModelMods modelMods, out _));
        Assert.Empty(modelMods.Variants);
        Assert.True(FieldScene3DMods.TryPrepare(snapshot, out FieldScene3DMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Equal("higher", Assert.Single(mods.Resolve(Field, 0, new Dictionary<int, int>())).Scene.PackageId);
        Assert.Empty(mods.Resolve(Field, 1, new Dictionary<int, int>()));
        Assert.Empty(mods.Resolve(Field, 0, new Dictionary<int, int> { [2] = 1 }));
        Assert.Equal("Memoria.FieldSceneViewConflict", Assert.Single(diagnostics).Code);

        snapshot = new(2, [], [Marker("higher"), Binding("higher", document), Marker("lower"), Binding("lower", document), Payload("lower", hash, glb)]);
        Assert.False(FieldScene3DMods.TryPrepare(snapshot, out mods, out diagnostics));
        Assert.Empty(mods.Resolve(Field, 0, new Dictionary<int, int>()));
        Assert.Equal("higher", diagnostics.First().PackageId);
    }

    [Fact]
    public void RuntimeEnablesPresentationPackagesAndRetainsTheLastGoodGeneration()
    {
        FieldScene3DPackageStore store = FieldScene3DPackageStore.Create(root, "Remastered scene fixture");
        byte[] glb = Triangle();
        string hash = FieldSceneDocuments.Digest(glb);
        store.Save(Document(hash), store.Open(Field).Revision, new Dictionary<string, byte[]> { [hash] = glb });
        using ModRuntime runtime = new(applicationBaseDirectory: root, modsDirectory: root, watch: false, isCompatible: _ => false, prepare: _ => new(true, UsablePackageIds: []));
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        ModPackage package = Assert.Single(runtime.InstalledPackages);
        Assert.Equal(ModEffectiveStatus.Disabled, package.Status);
        Assert.True(package.FieldScenePresentationOnly);
        Assert.True(runtime.SetEnabled(package.Id, true).Success);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.InstalledPackages).Status);
        FieldScene3DMods committed = runtime.FieldScenes3D;
        Assert.Single(committed.Resolve(Field, 0, new Dictionary<int, int>()));
        File.WriteAllText(Path.Combine(store.Package.RootDirectory, FieldSceneDocuments.ScenePath(Field)), "{}");
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.False(runtime.CommitPending());
        Assert.Same(committed, runtime.FieldScenes3D);
    }

    [Fact]
    public void DocumentsRejectDuplicatePropertiesAndCameraMotionWithoutCapture()
    {
        Assert.Throws<JsonException>(() => FieldSceneDocuments.ValidateComponent("""{"schemaVersion":1,"schemaVersion":1}"""u8));
        FieldSceneDocument document = Document(new string('a', 64));
        FieldSceneView view = document.Views[0] with { CameraMotion = FieldSceneCameraMotion.CaptureRelative };
        Assert.Throws<InvalidDataException>(() => FieldSceneDocuments.Write(document with { Views = [view] }));
        Assert.Throws<InvalidDataException>(() => FieldSceneDocuments.AssetPath("../scene.glb"));
    }

    [Fact]
    public void CalibrationPreservesUniformScaleAndReportsResiduals()
    {
        Matrix4x4 expected = Matrix4x4.CreateRotationX(0.7f) * Matrix4x4.CreateScale(128) * Matrix4x4.CreateTranslation(48, 90, -160);
        FieldSceneLandmark[] points = new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, Vector3.One }.Select(point => new FieldSceneLandmark(point, Vector3.Transform(point, expected))).ToArray();
        FieldSceneCalibrationResult result = FieldSceneCalibration.Fit(points);
        Assert.Equal(128, result.Scale, 3);
        Assert.InRange(result.MaximumFieldUnits, 0, 0.0001f);
        Assert.Throws<InvalidDataException>(() => FieldSceneCalibration.Fit([points[0], points[1], points[1]]));
    }

    private static FieldSceneDocument Document(string hash) => new(1, Field, [new(Asset, hash)], [new("view-0", 0, FieldSceneBackgroundMode.Replace, [new("wall", Asset, FieldSceneDocuments.Values(Matrix4x4.Identity))], NativeStates: new() { [2] = -1 })]);
    private static ModFile Marker(string package) => new(package, ModFormat.Memoria, FieldSceneDocuments.ComponentPath, 1, () => FieldSceneDocuments.Component.ToArray());
    private static ModFile Binding(string package, FieldSceneDocument document) => new(package, ModFormat.Memoria, FieldSceneDocuments.ScenePath(Field), 1, () => FieldSceneDocuments.Write(document));
    private static ModFile Payload(string package, string hash, byte[] glb) => new(package, ModFormat.Memoria, FieldSceneDocuments.AssetPath(hash), 1, () => glb);

    private static byte[] Triangle()
    {
        EditableVertex[] vertices = new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY }.Select(point => new EditableVertex(point, Vector3.UnitZ, Vector2.Zero, Vector4.Zero, Vector4.Zero, Vector4.One)).ToArray();
        EditableMesh mesh = new("wall", vertices, [0, 1, 2], -1, Matrix4x4.Identity);
        return ModelExchange.ExportGlb(new("scene fixture", "fixture", [], [mesh], [], [], default));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
