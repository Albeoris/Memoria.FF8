using System.Text;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods.Tests;

public sealed class FieldSpringBoneModsTests
{
    private const string ExactPath = "models/field/sample/0.springs.json";
    private const string SharedPath = "models/field/main_chr/d024.springs.json";
    private const string Payload = """{"version":1,"expectedBoneCount":3,"expectedParents":[-1,0,1],"joints":[{"bone":2,"localTail":[0,0,20],"stiffness":12,"damping":3,"gravity":[0,0,-11]}]}""";

    [Theory]
    [InlineData(ExactPath, true)]
    [InlineData(SharedPath, true)]
    [InlineData("MODELS/FIELD/MAIN_CHR/D024.SPRINGS.JSON", true)]
    [InlineData("models/field/sample/-1.springs.json", false)]
    [InlineData("models/field/sample/00.springs.json", false)]
    [InlineData("models/field/main_chr/o024.springs.json", false)]
    [InlineData("models/world/0.springs.json", false)]
    public void IdentifiesFieldSpringDocuments(string path, bool valid)
    {
        Assert.Equal(valid, FieldSpringBoneMods.IsSpringDocumentPath(path));
    }

    [Fact]
    public void UsesPackageOrderAndPrefersAnExactTargetWithinTheSamePackage()
    {
        FieldSpringBoneMods mods = Prepare(File("top", SharedPath), File("top", ExactPath, WithStiffness(20)), File("next", "models/field/sample/1.springs.json", WithStiffness(30)), File("next", SharedPath, WithStiffness(40)));
        FieldModel original = Model("d024");
        Assert.Equal(20f, Assert.Single(mods.Apply("field/sample/0", original, original).SpringBones).Stiffness);
        Assert.Equal(12f, Assert.Single(mods.Apply("field/sample/1", original, original).SpringBones).Stiffness);
        Assert.Equal(new[] { "top", "next" }, mods.PackageIds);
    }

    [Fact]
    public void TargetsBorrowedGeometryOnlyThroughItsExactFieldEntry()
    {
        FieldSpringBoneMods mods = Prepare(File("one", SharedPath));
        FieldModel textureAlias = Model() with { MainCharacterTextures = true, TextureResourceName = "d024" };
        FieldModel original = Model("d024");
        Assert.Same(textureAlias, mods.Apply("field/sample/0", textureAlias, textureAlias));
        Assert.Same(original, mods.Apply("world/0", original, original));
        FieldModel applied = mods.Apply("field/sample/0", original, original);
        Assert.NotSame(original, applied);
        Assert.Same(applied, mods.Apply("field/sample/0", original, original));
        Assert.Empty(original.SpringBones);
    }

    [Theory]
    [InlineData("\"version\":1", "\"version\":2")]
    [InlineData("\"version\":1", "\"version\":1,\"version\":1")]
    [InlineData("\"bone\":2", "\"bone\":0")]
    [InlineData("\"localTail\":[0,0,20]", "\"localTail\":[0,0,0]")]
    [InlineData("\"stiffness\":12", "\"stiffness\":-1")]
    [InlineData("\"damping\":3", "\"damping\":-1")]
    [InlineData("\"gravity\":[0,0,-11]", "\"gravity\":[0,0,5e100]")]
    [InlineData("\"expectedParents\":[-1,0,1],", "")]
    public void RejectsInvalidSharedDefinitionsWithOwnerDiagnostic(string before, string after)
    {
        ModFile file = File("bad", SharedPath, Payload.Replace(before, after, StringComparison.Ordinal));
        Assert.False(FieldSpringBoneMods.TryPrepare(new(1, [], [file]), out FieldSpringBoneMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Empty(mods.Definitions);
        ModDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Memoria.InvalidFieldSpringBones", diagnostic.Code);
        Assert.Equal(file.PackageId, diagnostic.PackageId);
        Assert.Equal(file.RelativePath, diagnostic.RelativePath);
    }

    [Fact]
    public void RejectsIncompatibleTargetEvenWhenTheBoneCountMatches()
    {
        FieldSpringBoneMods mods = Prepare(File("one", SharedPath));
        FieldModel original = Model("d024");
        FieldModel replaced = original with { Bones = [new(-1, 0), new(0, 20), new(0, 20)] };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => mods.Apply("field/sample/0", original, replaced));
        Assert.Contains("parent indices", error.Message);
        Assert.Empty(replaced.SpringBones);
    }

    private static FieldSpringBoneMods Prepare(params ModFile[] files)
    {
        Assert.True(FieldSpringBoneMods.TryPrepare(new(1, [], files), out FieldSpringBoneMods mods, out IReadOnlyList<ModDiagnostic> diagnostics), string.Join("\n", diagnostics.Select(diagnostic => diagnostic.Message)));
        return mods;
    }

    private static string WithStiffness(int stiffness) => Payload.Replace("\"stiffness\":12", FormattableString.Invariant($"\"stiffness\":{stiffness}"), StringComparison.Ordinal);

    private static ModFile File(string packageId, string path, string json = Payload) => new(packageId, ModFormat.Memoria, path, 1, () => Encoding.UTF8.GetBytes(json));

    private static FieldModel Model(string? mainCharacter = null) => new("sample", [new(-1, 0), new(0, 20), new(1, 20)], [], [], [], []) { MainCharacterSourceId = mainCharacter };
}
