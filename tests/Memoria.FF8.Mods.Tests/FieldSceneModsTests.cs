using System.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class FieldSceneModsTests
{
    private const string Valid = """
        {"schemaVersion":1,"fieldId":175,"fieldName":"bghall_6","backgrounds":[{"layer":0,"moduleId":5,"state":0,"visible":false}],"actors":[{"objectId":8,"model":{"fieldId":175,"modelId":9},"animationId":1,"when":{"moduleId":5,"state":0}}]}
        """;

    [Fact]
    public void ReadsIndependentBackgroundAndExistingObjectBindingsInCatalogOrder()
    {
        ModCatalogSnapshot catalog = Catalog(File("Memoria/first", Valid), File("Memoria/second", Valid));
        Assert.True(FieldSceneMods.TryPrepare(catalog, out FieldSceneMods result, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(new[] { "Memoria/first", "Memoria/second" }, result.Patches.Select(patch => patch.PackageId));
        FieldScenePatch patch = result.Patches[0].Patch;
        Assert.Null(patch.Movement);
        Assert.False(Assert.Single(patch.Backgrounds).Visible);
        FieldActorPatch actor = Assert.Single(patch.Actors);
        Assert.Equal(8, actor.ObjectId);
        Assert.Equal(9, actor.Model.ModelId);
        Assert.Equal(1, actor.AnimationId);
        Assert.Equal(5, actor.When!.ModuleId);
    }

    [Theory]
    [InlineData(-180f)]
    [InlineData(-12.5f)]
    [InlineData(0f)]
    [InlineData(180f)]
    public void ReadsMovementOnlyPatchesIncludingInclusiveBounds(float rotationDegrees)
    {
        string angle = rotationDegrees.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string json = $"{{\"schemaVersion\":1,\"fieldId\":175,\"fieldName\":\"bghall_6\",\"movement\":{{\"rotationDegrees\":{angle}}}}}";
        Assert.True(FieldSceneMods.TryPrepare(Catalog(File("Memoria/example", json)), out FieldSceneMods result, out var diagnostics));
        Assert.Empty(diagnostics);
        FieldScenePatch patch = Assert.Single(result.Patches).Patch;
        Assert.Empty(patch.Backgrounds);
        Assert.Empty(patch.Actors);
        Assert.Equal(rotationDegrees, Assert.IsType<FieldMovementPatch>(patch.Movement).RotationDegrees);
    }

    [Theory]
    [InlineData("{\"rotationDegrees\":45}")]
    [InlineData("null")]
    public void MovementCanAccompanyExistingBackgroundAndActorChanges(string movement)
    {
        string json = Valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"movement\":" + movement, StringComparison.Ordinal);
        Assert.True(FieldSceneMods.TryPrepare(Catalog(File("Memoria/example", json)), out FieldSceneMods result, out var diagnostics));
        Assert.Empty(diagnostics);
        FieldScenePatch patch = Assert.Single(result.Patches).Patch;
        Assert.Single(patch.Backgrounds);
        Assert.Single(patch.Actors);
        if (movement == "null")
        {
            Assert.Null(patch.Movement);
        }
        else
        {
            Assert.Equal(45f, Assert.IsType<FieldMovementPatch>(patch.Movement).RotationDegrees);
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"rotationDegrees\":null}")]
    [InlineData("{\"rotationDegrees\":-180.01}")]
    [InlineData("{\"rotationDegrees\":180.01}")]
    [InlineData("{\"rotationDegrees\":1e100}")]
    [InlineData("{\"rotationDegrees\":-1e100}")]
    [InlineData("{\"rotationDegrees\":\"NaN\"}")]
    [InlineData("{\"rotationDegrees\":\"Infinity\"}")]
    [InlineData("{\"rotationDegrees\":true}")]
    [InlineData("{\"rotationDegrees\":0,\"unknown\":1}")]
    public void InvalidOrMissingMovementAnglesRejectEntirePublication(string movement)
    {
        string json = Valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"movement\":" + movement, StringComparison.Ordinal);
        Assert.False(FieldSceneMods.TryPrepare(Catalog(File("Memoria/good", Valid), File("Memoria/bad", json)), out FieldSceneMods result, out var diagnostics));
        Assert.Empty(result.Patches);
        ModDiagnostic error = Assert.Single(diagnostics);
        Assert.Equal("Memoria.InvalidFieldPatch", error.Code);
        Assert.Equal("Memoria/bad", error.PackageId);
    }

    [Theory]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":2")]
    [InlineData("\"schemaVersion\":1,", "")]
    [InlineData("\"fieldId\":175", "\"fieldId\":-1")]
    [InlineData("\"fieldName\":\"bghall_6\"", "\"fieldName\":\"../bghall_6\"")]
    [InlineData("\"fieldName\":\"bghall_6\"", "\"fieldName\":null")]
    [InlineData("\"layer\":0", "\"layer\":4")]
    [InlineData("\"moduleId\":5", "\"moduleId\":256")]
    [InlineData("\"state\":0", "\"state\":-1")]
    [InlineData("\"visible\":false", "\"visibility\":false")]
    [InlineData("\"objectId\":8", "\"objectId\":-1")]
    [InlineData("\"modelId\":9", "\"modelId\":-1")]
    [InlineData("\"animationId\":1", "\"animationId\":-1")]
    [InlineData("\"animationId\":1", "\"animationId\":1,\"direction\":256")]
    public void InvalidVersionAddressesAndUnknownMembersRejectEntirePublication(string before, string after)
    {
        ModCatalogSnapshot catalog = Catalog(File("Memoria/good", Valid), File("Memoria/bad", Valid.Replace(before, after, StringComparison.Ordinal)));
        Assert.False(FieldSceneMods.TryPrepare(catalog, out FieldSceneMods result, out var diagnostics));
        Assert.Empty(result.Patches);
        ModDiagnostic error = Assert.Single(diagnostics);
        Assert.Equal("Memoria/bad", error.PackageId);
        Assert.Equal("fields/bghall_6.json", error.RelativePath);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"fieldId\":175,\"fieldName\":\"bghall_6\"}")]
    [InlineData("{\"schemaVersion\":1,\"fieldId\":175,\"fieldName\":\"bghall_6\",\"movement\":null}")]
    [InlineData("{\"schemaVersion\":1,\"fieldId\":175,\"fieldName\":\"bghall_6\",\"backgrounds\":[],\"actors\":[],\"movement\":null}")]
    [InlineData("{\"schemaVersion\":1,\"fieldId\":175,\"fieldName\":\"bghall_6\",\"backgrounds\":[null]}")]
    [InlineData("{\"schemaVersion\":1,\"fieldId\":175,\"fieldName\":\"bghall_6\",\"actors\":[null]}")]
    [InlineData("{\"schemaVersion\":1,\"fieldId\":175,\"fieldName\":\"bghall_6\",\"actors\":null}")]
    [InlineData("null")]
    public void EmptyOrNullPatchesAreDiagnosed(string json)
    {
        Assert.False(FieldSceneMods.TryPrepare(Catalog(File("Memoria/example", json)), out _, out var diagnostics));
        Assert.Equal("Memoria.InvalidFieldPatch", Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void DuplicateActorAndLayerAddressesAreRejected()
    {
        string actor = "{\"objectId\":8,\"model\":{\"fieldId\":175,\"modelId\":9}}";
        string background = "{\"layer\":0,\"moduleId\":5,\"state\":0,\"visible\":false}";
        foreach (string json in new[]
        {
            $"{{\"schemaVersion\":1,\"fieldId\":175,\"fieldName\":\"bghall_6\",\"actors\":[{actor},{actor}]}}",
            $"{{\"schemaVersion\":1,\"fieldId\":175,\"fieldName\":\"bghall_6\",\"backgrounds\":[{background},{background}]}}"
        })
        {
            Assert.False(FieldSceneMods.TryPrepare(Catalog(File("Memoria/example", json)), out _, out _));
        }
    }

    [Fact]
    public void PathMustMatchFieldAndOtherFormatsOrDocumentationAreNotPatches()
    {
        ModFile wrongPath = new("Memoria/example", ModFormat.Memoria, "fields/other.json", 1, () => Encoding.UTF8.GetBytes(Valid));
        Assert.False(FieldSceneMods.TryPrepare(Catalog(wrongPath), out _, out _));
        ModFile readme = new("Memoria/example", ModFormat.Memoria, "README.md", 1, () => []);
        ModFile texture = new("Demaster/example", ModFormat.Demaster, "fields/bghall_6.json", 1, () => []);
        Assert.True(FieldSceneMods.TryPrepare(Catalog(readme, texture), out FieldSceneMods result, out _));
        Assert.Empty(result.Patches);
    }

    [Theory]
    [InlineData("{\"x\":-64,\"y\":48,\"width\":0,\"height\":64}")]
    [InlineData("{\"x\":-32769,\"y\":48,\"width\":48,\"height\":64}")]
    [InlineData("{\"x\":-64,\"y\":48,\"width\":48,\"height\":65537}")]
    [InlineData("{\"x\":-64,\"y\":48,\"width\":48}")]
    public void InvalidTileRegionsRejectPublication(string region)
    {
        string json = Valid.Replace("\"visible\":false", "\"visible\":false,\"region\":" + region, StringComparison.Ordinal);
        Assert.False(FieldSceneMods.TryPrepare(Catalog(File("Memoria/example", json)), out _, out var diagnostics));
        Assert.Equal("Memoria.InvalidFieldPatch", Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void RegionUsesOriginalTileOriginsAndExclusiveRightAndBottomEdges()
    {
        const string region = "{\"x\":-64,\"y\":48,\"width\":48,\"height\":64}";
        string json = Valid.Replace("\"visible\":false", "\"visible\":false,\"region\":" + region, StringComparison.Ordinal);
        Assert.True(FieldSceneMods.TryPrepare(Catalog(File("Memoria/example", json)), out var result, out _));
        FieldBackgroundRegion selection = Assert.IsType<FieldBackgroundRegion>(Assert.Single(Assert.Single(result.Patches).Patch.Backgrounds).Region);
        Assert.True(selection.Contains(-64, 48));
        Assert.True(selection.Contains(-17, 111));
        Assert.False(selection.Contains(-65, 48));
        Assert.False(selection.Contains(-16, 48));
        Assert.False(selection.Contains(-64, 112));
    }

    private static ModFile File(string package, string json) => new(package, ModFormat.Memoria, "fields/bghall_6.json", 1, () => Encoding.UTF8.GetBytes(json));

    private static ModCatalogSnapshot Catalog(params ModFile[] files) => new(1, [], files);
}
