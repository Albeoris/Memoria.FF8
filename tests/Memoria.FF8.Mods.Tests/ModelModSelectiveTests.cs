using System.Numerics;
using Memoria.FF8.Assets;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModelModSelectiveTests
{
    [Fact]
    public void OneClipUpdatesGameplayTracksAndLeavesOtherSlotsAndGeometry()
    {
        BattleModel original = Battle();
        EditableModel editable = ModelExchange.FromBattle(original);
        EditableAnimation changed = ShiftRoot(editable.Animations[1], 5);
        ModelModInstallPlan plan = ModelModInstaller.Plan([new("battle/test.dat", editable, false, [new("embedded", 1, changed)])]);
        ModelMods mods = Prepare(plan, "first");
        BattleModel result = mods.Apply("battle/test.dat", original);
        Assert.Equal(2, result.Animations.Count);
        Assert.Same(original.Animations[0], result.Animations[0]);
        Assert.Same(original.Bones, result.Bones);
        Assert.Equal(editable.Meshes.Count, result.EditableOverride!.Meshes.Count);
        Assert.NotEqual(original.Animations[1].Frames[0].Translation, result.Animations[1].Frames[0].Translation);
        Assert.Equal(changed.Channels[0].Values[0], result.EditableOverride.Animations[1].Channels[0].Values[0]);
        Assert.Single(plan.Files);
        Assert.Empty(ModelExchange.ImportGlb(plan.Files[0].Content, allowBoneCountChange: true).Model.Meshes);
    }

    [Fact]
    public void GeometryOnlyPreservesAllClipsAndNativeRig()
    {
        BattleModel original = Battle();
        EditableModel editable = ModelExchange.FromBattle(original);
        EditableMesh changed = editable.Meshes[0] with { Vertices = editable.Meshes[0].Vertices.Select(vertex => vertex with { Position = vertex.Position * 2 }).ToArray() };
        ModelMods mods = Prepare(ModelModInstaller.Plan([new("battle/test.dat", editable with { Meshes = [changed] }, true, [])]), "first");
        BattleModel result = mods.Apply("battle/test.dat", original);
        Assert.Same(original.Bones, result.Bones);
        Assert.Same(original.Animations[0], result.Animations[0]);
        Assert.Same(original.Animations[1], result.Animations[1]);
        Assert.Equal(changed.Vertices[1].Position, result.EditableOverride!.Meshes[0].Vertices[1].Position);
    }

    [Fact]
    public void PrecedenceResolvesEachClipSeparatelyAndSpecificProviderIsInspectable()
    {
        EditableModel editable = ModelExchange.FromBattle(Battle());
        ModelModInstallPlan high = ModelModInstaller.Plan([new("battle/test.dat", editable, false, [new("embedded", 1, ShiftRoot(editable.Animations[1], 7))])]);
        ModelModInstallPlan low = ModelModInstaller.Plan([new("battle/test.dat", editable, false, [new("embedded", 0, ShiftRoot(editable.Animations[0], 3)), new("embedded", 1, ShiftRoot(editable.Animations[1], 4))])]);
        ModFile[] files = Files(high, "high").Concat(Files(low, "low")).ToArray();
        Assert.True(ModelMods.TryPrepare(new(1, [], files), out ModelMods mods, out _));
        EditableModel resolved = mods.Resolve("battle/test.dat", editable);
        Assert.Equal(3, resolved.Animations[0].Channels[0].Values[0].X);
        Assert.Equal(7, resolved.Animations[1].Channels[0].Values[0].X);
        Assert.Equal(4, mods.Resolve("battle/test.dat", editable, "low").Animations[1].Channels[0].Values[0].X);
    }

    [Fact]
    public void DuplicateTargetsAndRigChangesAreRejected()
    {
        EditableModel model = ModelExchange.FromBattle(Battle());
        ModelModReplacement replacement = new("battle/test.dat", model, true, []);
        Assert.Throws<InvalidDataException>(() => ModelModInstaller.Plan([replacement, replacement]));
        ModelMods mods = Prepare(ModelModInstaller.Plan([replacement with { Model = model with { Bones = [model.Bones[0] with { Translation = Vector3.One }] } }]), "first");
        Assert.Throws<NotSupportedException>(() => mods.Resolve("battle/test.dat", model));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstallPreservesSettingsAndUnselectedFilesAndRollsBackOnFailure(bool externalDestination)
    {
        string temporary = Path.Combine(Path.GetTempPath(), "Memoria.ModelInstall", Guid.NewGuid().ToString("N"));
        string allowed = Path.Combine(temporary, "Mods", "Memoria");
        string destination = externalDestination ? Path.Combine(temporary, "Exports", "Nested", "Sample") : Path.Combine(allowed, "Sample");
        try
        {
            Directory.CreateDirectory(destination);
            string settings = Path.Combine(destination, "mod.ini");
            File.WriteAllText(settings, "enabled=0\npriority=3\n");
            string unrelated = Path.Combine(destination, "unrelated.txt");
            File.WriteAllText(unrelated, "retained");
            EditableModel model = ModelExchange.FromBattle(Battle());
            ModelModInstallPlan plan = ModelModInstaller.Plan([new("battle/test.dat", model, true, [new("embedded", 1, model.Animations[1])])]);
            Assert.Empty(plan.GetConflicts(allowed, destination));
            ModelModInstaller.Install(allowed, destination, plan);
            Assert.Equal(plan.Files.Count, plan.GetConflicts(allowed, destination).Count);
            if (externalDestination)
            {
                Assert.False(Directory.Exists(allowed));
            }
            Assert.Empty(Directory.EnumerateDirectories(destination, ".model-stage-*"));
            byte[] old = File.ReadAllBytes(Path.Combine(destination, plan.Files[0].RelativePath));
            string failure = Path.Combine(destination, plan.Files[1].RelativePath);
            File.Delete(failure);
            Directory.CreateDirectory(failure);
            Exception? failureException = Record.Exception(() => ModelModInstaller.Install(allowed, destination, plan));
            Assert.True(failureException is IOException or UnauthorizedAccessException);
            Assert.Equal(old, File.ReadAllBytes(Path.Combine(destination, plan.Files[0].RelativePath)));
            Assert.Equal("enabled=0\npriority=3\n", File.ReadAllText(settings));
            Assert.Equal("retained", File.ReadAllText(unrelated));
            Assert.Empty(Directory.EnumerateDirectories(destination, ".model-stage-*"));
            ModelModInstallPlan escaping = new([new("models/../../escaped.glb", plan.Files[0].Content)]);
            Assert.Throws<InvalidDataException>(() => ModelModInstaller.Install(allowed, destination, escaping));
            Assert.False(File.Exists(Path.Combine(temporary, "escaped.glb")));
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, true);
            }
        }
    }

    private static ModelMods Prepare(ModelModInstallPlan plan, string package)
    {
        Assert.True(ModelMods.TryPrepare(new(1, [], Files(plan, package)), out ModelMods mods, out var diagnostics), string.Join("; ", diagnostics.Select(item => item.Message)));
        return mods;
    }

    private static IEnumerable<ModFile> Files(ModelModInstallPlan plan, string package) => plan.Files.Select(output => new ModFile(package, ModFormat.Memoria, output.RelativePath, 1, () => output.Content));
    private static EditableAnimation ShiftRoot(EditableAnimation animation, float x) => animation with { Channels = animation.Channels.Select(channel => channel.Path == "translation" ? channel with { Values = channel.Values.Select(value => value with { X = x }).ToArray() } : channel).ToArray() };

    private static BattleModel Battle()
    {
        BattlePose pose = new(Vector3.Zero, [Vector3.Zero], [Vector3.One]);
        BattleMesh mesh = new([new(Vector3.Zero, 0), new(Vector3.UnitX, 0), new(Vector3.UnitY, 0)], [new([0, 1, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, new(255, 255, 255, 255)) { NativeMaterial = new(128, 0, [Vector2.Zero, Vector2.Zero, Vector2.Zero]), NativePolygon = System.Collections.Immutable.ImmutableArray.CreateRange(new byte[16]) }]);
        return new("test.dat", [new(-1, 0)], [mesh], [new PixelImage(1, 1)], [new([pose]), new([pose, pose])], 1);
    }
}
