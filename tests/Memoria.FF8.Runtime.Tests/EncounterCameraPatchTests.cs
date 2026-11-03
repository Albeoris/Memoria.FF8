using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterCameraPatchTests
{
    [Theory]
    [InlineData(-45)]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(45)]
    public void CameraElevationCompilesAndSurvivesSnapshotSerialization(double degrees)
    {
        EncounterPatchComposition composition = Compose(Layer("camera", Edit("raise", "0", degrees.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        Assert.Equal(degrees, composition.CameraProperties["elevationDegrees"]);
        EncounterPatchOrigin origin = Assert.Single(composition.Origins);
        Assert.Equal("camera", origin.Target);
        Assert.Equal("elevationDegrees", origin.Property);
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, new() { Combatants = [new() { Id = "original", Name = "Original" }] }, new("fixture.encounter", "original", 11, 11, [], true));
        Assert.Equal(degrees, compiled.Camera!.ElevationDegrees);
        Assert.Equal(degrees, JsonSerializer.Deserialize<EncounterCompiledDefinition>(JsonSerializer.Serialize(compiled))!.Camera!.ElevationDegrees);
    }

    [Fact]
    public void MissingCameraPropertyPreservesDefaultForOlderSnapshots()
    {
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new("fixture.encounter", "Classic", ["original"]), []);
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, new() { Combatants = [new() { Id = "original", Name = "Original" }] }, new("fixture.encounter", "original", 11, 11, [], true));
        Assert.Empty(composition.CameraProperties);
        Assert.Null(compiled.Camera);
        Assert.Null(JsonSerializer.Deserialize<EncounterCompiledDefinition>(JsonSerializer.Serialize(compiled))!.Camera);
    }

    [Fact]
    public void GuardedCameraRefinementRequiresActualPreviousWriter()
    {
        EncounterPatchLayer first = Layer("first", Edit("raise", "0", "12"));
        EncounterPatchLayer second = Layer("second", Edit("refine", "12", "18", "first", "raise"));
        EncounterPatchLayer third = Layer("third", Edit("final", "18", "15", "second", "refine"));
        Assert.Equal(15, Compose(third, second, first).CameraProperties["elevationDegrees"]);
        EncounterPatchLayer stale = Layer("stale", Edit("stale", "18", "15", "first", "raise"));
        Assert.Contains("Conflicting write", Assert.Throws<InvalidDataException>(() => Compose(first, second, stale)).Message);
    }

    [Fact]
    public void CameraConflictsAndWrongGuardsRejectWholeCandidate()
    {
        EncounterPatchLayer first = Layer("first", Edit("raise", "0", "12"));
        EncounterPatchComposition accepted = Compose(first);
        InvalidDataException conflict = Assert.Throws<InvalidDataException>(() => Compose(first, Layer("other", Edit("raise", "12", "18"))));
        Assert.Contains("Conflicting write", conflict.Message);
        Assert.Contains("first", conflict.Message);
        Assert.Contains("atomically", conflict.Message);
        Assert.Contains("Guard failed", Assert.Throws<InvalidDataException>(() => Compose(Layer("wrong", Edit("raise", "1", "12")))).Message);
        Assert.Equal(12, accepted.CameraProperties["elevationDegrees"]);
    }

    [Theory]
    [InlineData("-46")]
    [InlineData("46")]
    [InlineData("1e309")]
    [InlineData("null")]
    [InlineData("\"12\"")]
    public void CameraRejectsUnboundedOrUntypedValues(string value)
    {
        Assert.Throws<InvalidDataException>(() => Layer("invalid", Edit("raise", "0", value)));
        Assert.Throws<InvalidDataException>(() => Layer("invalid", Edit("raise", value, "0")));
    }

    [Fact]
    public void CameraRejectsUnknownPropertiesAndActorFields()
    {
        Assert.Throws<InvalidDataException>(() => Layer("invalid", Edit("raise", "0", "12").Replace("elevationDegrees", "eyeY", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => Layer("invalid", Edit("raise", "0", "12").Replace("\"property\"", "\"actor\":\"original\",\"property\"", StringComparison.Ordinal)));
    }

    private static EncounterPatchComposition Compose(params EncounterPatchLayer[] layers)
    {
        return EncounterPatchComposer.Compose(new("fixture.encounter", "Classic", ["original"]), layers);
    }

    private static EncounterPatchLayer Layer(string document, string operation)
    {
        return new(document, "gameplay/encounters/camera.json", EncounterPatchDocument.Parse("{\"schemaVersion\":1,\"documentId\":\"" + document + "\",\"encounter\":\"fixture.encounter\",\"atomic\":true,\"operations\":[" + operation + "]}"));
    }

    private static string Edit(string id, string expected, string value, string? producerDocument = null, string? producerOperation = null)
    {
        string producer = producerDocument is null ? "" : ",\"producer\":{\"documentId\":\"" + producerDocument + "\",\"operationId\":\"" + producerOperation + "\"}";
        return "{\"id\":\"" + id + "\",\"kind\":\"SetCameraProperty\",\"property\":\"elevationDegrees\",\"expectedValue\":" + expected + ",\"value\":" + value + producer + "}";
    }
}
