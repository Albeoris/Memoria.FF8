using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldLoadContextTests
{
    [Fact]
    public void NewGameContextHasItsExplicitOriginAndField()
    {
        NativeFieldLoadContext context = NativeFieldLoadContext.NewGame();

        context.Validate();
        Assert.Equal(1, context.Version);
        Assert.Equal(74, context.FieldId);
        Assert.Equal(NativeFieldLoadOrigin.NewGame, context.Origin);
        Assert.Equal((byte)0, context.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(74)]
    [InlineData(733)]
    [InlineData(65535)]
    public void FactoriesKeepTheExactWordFieldAndTheirDistinctProducers(int fieldId)
    {
        NativeFieldLoadContext request = NativeFieldLoadContext.FromFieldRequest(fieldId);
        NativeFieldLoadContext worldReturn = NativeFieldLoadContext.FromWorldReturn(fieldId);

        request.Validate();
        worldReturn.Validate();
        Assert.Equal(fieldId, request.FieldId);
        Assert.Equal(fieldId, worldReturn.FieldId);
        Assert.Equal(NativeFieldLoadOrigin.FieldRequest, request.Origin);
        Assert.Equal(NativeFieldLoadOrigin.WorldReturn, worldReturn.Origin);
        Assert.Equal((byte)1, request.Reason);
        Assert.Equal((byte)2, worldReturn.Reason);
        Assert.NotEqual(request, worldReturn);
    }

    [Theory]
    [InlineData(0, 733, NativeFieldLoadOrigin.FieldRequest)]
    [InlineData(2, 733, NativeFieldLoadOrigin.FieldRequest)]
    [InlineData(1, -1, NativeFieldLoadOrigin.FieldRequest)]
    [InlineData(1, 65536, NativeFieldLoadOrigin.WorldReturn)]
    [InlineData(1, 733, NativeFieldLoadOrigin.NewGame)]
    [InlineData(1, 74, (NativeFieldLoadOrigin)(-1))]
    [InlineData(1, 733, (NativeFieldLoadOrigin)255)]
    public void InvalidVersionsFieldsAndOriginsCannotCertifyAnEntry(int version, int fieldId, NativeFieldLoadOrigin origin)
    {
        NativeFieldLoadContext invalid = new(version, fieldId, origin);

        Assert.Throws<InvalidDataException>(invalid.Validate);
    }

    [Theory]
    [InlineData(NativeFieldLoadOrigin.NewGame, 74, 0)]
    [InlineData(NativeFieldLoadOrigin.FieldRequest, 733, 1)]
    [InlineData(NativeFieldLoadOrigin.WorldReturn, 733, 2)]
    public void SerializationPreservesTheProducerAndRecomputesItsReason(NativeFieldLoadOrigin origin, int fieldId, byte reason)
    {
        NativeFieldLoadContext original = new(1, fieldId, origin);
        string json = JsonSerializer.Serialize(original);
        NativeFieldLoadContext restored = JsonSerializer.Deserialize<NativeFieldLoadContext>(json)!;
        using JsonDocument document = JsonDocument.Parse(json);

        restored.Validate();
        Assert.Equal(original, restored);
        Assert.Equal(reason, restored.Reason);
        Assert.False(document.RootElement.TryGetProperty(nameof(NativeFieldLoadContext.Reason), out _));
        GameState state = new() { Scene = SceneKind.Field, FieldId = fieldId, FieldLoadContext = original };
        GameState restoredState = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(original, restoredState.FieldLoadContext);
    }

    [Fact]
    public void OnlyExecutedNewGameAndFieldRequestMethodsPublishTheirContexts()
    {
        GameSession session = GameSession.CreateColdProcess();
        Assert.Null(session.State.FieldLoadContext);

        session.StartNewGame();
        Assert.Equal(NativeFieldLoadContext.NewGame(), session.State.FieldLoadContext);
        session.RequestField(733, 0);
        Assert.Equal(NativeFieldLoadContext.FromFieldRequest(733), session.State.FieldLoadContext);
        Assert.Equal(74, session.State.PreviousFieldId);
        FieldTransition arrival = new(734, -557, 568, 24, 128, 795);
        session.RequestField(arrival);
        Assert.Equal(NativeFieldLoadContext.FromFieldRequest(734), session.State.FieldLoadContext);
        Assert.Equal(arrival, session.State.FieldArrival);
        Assert.Equal(733, session.State.PreviousFieldId);
        session.StartNewGame();
        Assert.Equal(NativeFieldLoadContext.NewGame(), session.State.FieldLoadContext);
    }

    [Theory]
    [InlineData(74)]
    [InlineData(733)]
    public void AFieldSceneOrLegacySnapshotDoesNotInventLoadProvenance(int fieldId)
    {
        GameState state = new() { Scene = SceneKind.Field, FieldId = fieldId };
        GameSession session = new(state);
        Assert.Null(session.State.FieldLoadContext);
        JsonObject legacy = JsonSerializer.SerializeToNode(state)!.AsObject();
        legacy.Remove(nameof(GameState.FieldLoadContext));

        GameState restored = JsonSerializer.Deserialize<GameState>(legacy.ToJsonString())!;

        Assert.Null(restored.FieldLoadContext);
        session.Restore(restored);
        Assert.Null(session.State.FieldLoadContext);
    }
}
