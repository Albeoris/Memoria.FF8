using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeModelPartSnapshotTests
{
    [Fact]
    public void InactiveSnapshotContainsOnlyEmptyDynamicStateAndAcceptsDifferentResourceTables()
    {
        NativeModelPartsDefinition original = Definition('A', 100, 0, 1, 2, 0, 100);
        NativeModelPartResourceState state = NativeModelPartResourceState.Create(original);
        string json = JsonSerializer.Serialize(state);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(new[] { "Translations" }, document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("Translations").ValueKind);
        Assert.Empty(state.Translations);
        NativeModelPartsDefinition changed = Definition('B', 9000, 1, 3, 5, 20, -1000);
        NativeModelPartResourceState restored = JsonSerializer.Deserialize<NativeModelPartResourceState>(json)!;
        restored.Validate(changed);
        Assert.Empty(restored.Step(changed).SkinTranslations);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void ActiveSnapshotKeepsItsDynamicSamplesButUsesCurrentResourceSkinBindings()
    {
        NativeModelPartsDefinition original = Definition('A', 100, 0, 1, 2, 0, 100);
        NativeModelPartResourceState state = NativeModelPartResourceState.Create(original).StartTranslation(original, 0, 20, 0, -10, 10);
        state = state.Step(original).State.Step(original).State;
        Assert.Equal((short)3, state.Translations[0].Counter);
        string json = JsonSerializer.Serialize(state);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement translation = document.RootElement.GetProperty("Translations").GetProperty("0");
        Assert.Equal(new[] { "Counter", "Duration", "Mode", "StartX", "StartY", "StartZ", "TargetX", "TargetY", "TargetZ" }, translation.EnumerateObject().Select(property => property.Name).Order());
        Assert.DoesNotContain("SourceHash", json);
        Assert.DoesNotContain("SourceOffset", json);
        Assert.DoesNotContain("SkinFlags", json);
        Assert.DoesNotContain("Parts", json);
        NativeModelPartsDefinition changed = Definition('B', 9000, 1, 2, 4, 20, -1000);
        NativeModelPartResourceState restored = JsonSerializer.Deserialize<NativeModelPartResourceState>(json)!;
        restored.Validate(changed);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeModelPartStep next = restored.Step(changed);
        Assert.Equal(new[] { 1, 2 }, next.SkinTranslations.Keys.Order());
        Assert.Equal(new Vector3(106, 200, 297), next.SkinTranslations[1]);
        Assert.Equal(new Vector3(108, 200, 296), next.SkinTranslations[2]);
        Assert.Equal((short)5, next.State.Translations[0].Counter);
        Assert.Equal((short)100, next.State.Translations[0].StartX);
        Assert.Equal((short)120, next.State.Translations[0].TargetX);
        Assert.Equal((short)-1000, changed.Parts[0].TargetX);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void CompletedTranslationRestoresAgainstNewGeometryAndRestartsFromSavedTarget()
    {
        NativeModelPartsDefinition original = Definition('A', 100, 0, 1, 2, 0, 100);
        NativeModelPartResourceState state = NativeModelPartResourceState.Create(original).StartTranslation(original, 0, 20, 0, -10, 1).Step(original).State;
        Assert.Equal((short)4, state.Translations[0].Mode);
        NativeModelPartsDefinition changed = Definition('B', 9000, 1, 2, 4, 20, -1000);
        NativeModelPartResourceState restored = JsonSerializer.Deserialize<NativeModelPartResourceState>(JsonSerializer.Serialize(state))!;
        restored.Validate(changed);
        NativeModelPartStep retained = restored.Step(changed);
        Assert.Equal(new Vector3(120, 200, 290), retained.SkinTranslations[1]);
        Assert.Equal(new Vector3(120, 200, 290), retained.SkinTranslations[2]);
        NativeModelPartResourceState restarted = restored.StartTranslation(changed, 0, -20, 0, 10, 10);
        Assert.Equal((short)120, restarted.Translations[0].StartX);
        Assert.Equal((short)100, restarted.Translations[0].TargetX);
        Assert.Equal(new Vector3(118, 200, 291), restarted.Step(changed).SkinTranslations[1]);
    }

    [Fact]
    public void ActivePartAbsentFromCurrentDefinitionFailsInsteadOfGuessingAnotherBinding()
    {
        NativeModelPartsDefinition original = Definition('A', 100, 0, 1, 2, 0, 100);
        NativeModelPartResourceState state = NativeModelPartResourceState.Create(original).StartTranslation(original, 0, 20, 0, 0, 10);
        NativeModelPartsDefinition changed = original with { SourceHash = new string('B', 64), SourceOffset = 9000, Parts = [] };
        Assert.Throws<InvalidDataException>(() => state.Validate(changed));
        Assert.Throws<InvalidDataException>(() => state.Step(changed));
    }

    [Fact]
    public void LegacyCopiedResourceRowsWithoutDynamicStateRequireExplicitMigration()
    {
        const string old = "{\"SourceHash\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\",\"SourceOffset\":100,\"Parts\":[],\"SkinFlags\":[]}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeModelPartResourceState>(old));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeModelPartResourceState>("{}"));
    }

    [Theory]
    [InlineData("Counter")]
    [InlineData("StartX")]
    [InlineData("StartY")]
    [InlineData("StartZ")]
    [InlineData("TargetX")]
    [InlineData("TargetY")]
    [InlineData("TargetZ")]
    [InlineData("Duration")]
    [InlineData("Mode")]
    public void MissingDynamicSampleWordsCannotBeReconstructedFromCurrentResource(string missing)
    {
        NativeModelPartsDefinition definition = Definition('A', 100, 0, 1, 2, 0, 100);
        NativeModelPartResourceState state = NativeModelPartResourceState.Create(definition).StartTranslation(definition, 0, 20, 0, 0, 10);
        JsonNode document = JsonNode.Parse(JsonSerializer.Serialize(state))!;
        Assert.True(document["Translations"]!["0"]!.AsObject().Remove(missing));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeModelPartResourceState>(document.ToJsonString()));
    }

    private static NativeModelPartsDefinition Definition(char hash, int offset, short firstSkin, short skinCount, int skins, short firstVertex, short targetX)
    {
        short[] row = [firstSkin, skinCount, -7, 0, 11, 22, 33, 0x1234, 9, 8, 7, 6, targetX, 200, 300, 90];
        NativeModelSkinBinding[] bindings = Enumerable.Range(0, skins).Select(index => new NativeModelSkinBinding(checked((short)(firstVertex + index * 4)), 4, checked((short)(index % 2)), checked((ushort)(0x1000 + index)))).ToArray();
        return new(new string(hash, 64), offset, [new(row)], bindings);
    }
}
