using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSavedWorldStateTests
{
    [Fact]
    public void NewGameInitializesOnlyTheProvedZeroSlots()
    {
        NativeSavedWorldState state = NativeSavedWorldState.CreateNewGame();
        state.Validate();
        Assert.Equal(1, state.SchemaVersion);
        Assert.Equal(NativeSavedWorldOriginKind.NativeNewGame, state.Origin.Kind);
        Assert.Equal(new NativeSavedWorldVehicleSlot(0, new NativeSavedWorldPose(0, 0, 0, 0)), state.Slot4);
        Assert.Equal(state.Slot4, state.Slot5);
    }

    [Fact]
    public void ValidationRejectsIncompleteAndUnsupportedProofs()
    {
        NativeSavedWorldState valid = NativeSavedWorldState.CreateNewGame();
        Assert.Throws<InvalidDataException>(() => (valid with { SchemaVersion = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Origin = valid.Origin with { ProofVersion = 2 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Origin = valid.Origin with { Kind = (NativeSavedWorldOriginKind)99 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Origin = valid.Origin with { InitializationRule = "unknown" } }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Origin = valid.Origin with { Kind = (NativeSavedWorldOriginKind)1 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { InitialMapDisplayMode110 = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Origin = null! }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Slot4 = null! }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Slot5 = valid.Slot5 with { Pose = null! } }).Validate());
    }

    [Fact]
    public void NonzeroSlotValuesAndRawIntegerWidthsRoundTripWithoutNormalization()
    {
        NativeSavedWorldState state = NativeSavedWorldState.CreateNewGame() with
        {
            Slot4 = new NativeSavedWorldVehicleSlot(73, new NativeSavedWorldPose(int.MinValue, int.MaxValue, short.MinValue, ushort.MaxValue)),
            Slot5 = new NativeSavedWorldVehicleSlot(255, new NativeSavedWorldPose(-24680, 13579, short.MaxValue, 4097))
        };
        NativeSavedWorldState restored = JsonSerializer.Deserialize<NativeSavedWorldState>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(state, restored);
    }

    [Fact]
    public void MissingJsonMetadataCannotBecomeAnImplicitNewGameOrigin()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSavedWorldState>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSavedWorldPose>("{\"X\":0,\"RawZ\":0,\"Height\":0}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSavedWorldOrigin>("{\"ProofVersion\":1,\"InitializationRule\":\"ff8-en-newgame-4e43fc-56d9f0-56da10-v1\"}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSavedWorldOrigin>("{\"ProofVersion\":1,\"Kind\":0,\"InitializationRule\":\"ff8-en-newgame-4e43fc-56d9f0-56da10-v1\",\"LegacyCheckpointSha256\":null}"));
    }
}
