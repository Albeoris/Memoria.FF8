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
        Assert.Null(state.Origin.LegacyCheckpointSha256);
        Assert.Equal(new NativeSavedWorldVehicleSlot(0, new NativeSavedWorldPose(0, 0, 0, 0)), state.Slot4);
        Assert.Equal(state.Slot4, state.Slot5);
    }

    [Theory]
    [InlineData(NativeSavedWorldState.LegacyCheckpoint265Sha256)]
    [InlineData(NativeSavedWorldState.LegacyCheckpoint300Sha256)]
    public void VerifiedLegacyFactoryRetainsTheApprovedSourceIdentity(string sha256)
    {
        NativeSavedWorldState state = NativeSavedWorldState.CreateVerifiedLegacyNewGame(sha256.ToUpperInvariant());
        state.Validate();
        Assert.Equal(NativeSavedWorldOriginKind.VerifiedLegacyNewGame, state.Origin.Kind);
        Assert.Equal(sha256, state.Origin.LegacyCheckpointSha256);
        Assert.Equal(NativeSavedWorldState.CreateNewGame().Slot4, state.Slot4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("3b78a5a2af71c9af981d71b713f504778ce06183f0ab6f7a8e3fc5aba1da0a90")]
    [InlineData("unverified-newgame")]
    public void UnapprovedLegacyIdentityCannotCreateDefaults(string sha256)
    {
        Assert.False(NativeSavedWorldState.IsApprovedLegacyCheckpoint(sha256));
        Assert.Throws<InvalidDataException>(() => NativeSavedWorldState.CreateVerifiedLegacyNewGame(sha256));
    }

    [Fact]
    public void ValidationRejectsIncompleteAndUnsupportedProofs()
    {
        NativeSavedWorldState valid = NativeSavedWorldState.CreateNewGame();
        Assert.Throws<InvalidDataException>(() => (valid with { SchemaVersion = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Origin = valid.Origin with { ProofVersion = 2 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Origin = valid.Origin with { Kind = (NativeSavedWorldOriginKind)99 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Origin = valid.Origin with { InitializationRule = "unknown" } }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Origin = valid.Origin with { LegacyCheckpointSha256 = NativeSavedWorldState.LegacyCheckpoint265Sha256 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Origin = valid.Origin with { Kind = NativeSavedWorldOriginKind.VerifiedLegacyNewGame } }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Origin = null! }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Slot4 = null! }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Slot5 = valid.Slot5 with { Pose = null! } }).Validate());
    }

    [Fact]
    public void NonzeroSlotValuesAndRawIntegerWidthsRoundTripWithoutNormalization()
    {
        NativeSavedWorldState state = NativeSavedWorldState.CreateVerifiedLegacyNewGame(NativeSavedWorldState.LegacyCheckpoint300Sha256) with
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
    }
}
