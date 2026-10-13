using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldControlStateTests
{
    [Fact]
    public void NativeNewGameEstablishesOnlyTheProvedControlFlags()
    {
        NativeWorldControlState state = NativeWorldControlState.CreateNewGame();
        state.Validate();
        Assert.Equal(1, state.SchemaVersion);
        Assert.Equal(1, state.Origin.ProofVersion);
        Assert.Equal(NativeWorldControlOriginKind.NativeNewGame, state.Origin.Kind);
        Assert.Equal(NativeWorldControlState.InitializationRule, state.Origin.InitializationRule);
        Assert.Null(state.Origin.LegacyCheckpointSha256);
        Assert.False(state.SourceDisabled);
        Assert.Equal((byte)0, state.Menu209);
    }

    [Fact]
    public void OnlyTheVerified172PreinitCheckpointHasTheOrdinaryIntroFlags()
    {
        NativeWorldControlState state = NativeWorldControlState.CreateVerifiedLegacyNewGame(NativeWorldControlState.LegacyCheckpoint172PreinitSha256.ToUpperInvariant());
        state.Validate();
        Assert.Equal(NativeWorldControlOriginKind.VerifiedLegacyNewGame, state.Origin.Kind);
        Assert.Equal(NativeWorldControlState.LegacyCheckpoint172PreinitSha256, state.Origin.LegacyCheckpointSha256);
        Assert.True(state.SourceDisabled);
        Assert.Equal((byte)0, state.Menu209);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unverified-newgame")]
    [InlineData("9d370a2f777cb1994c65e3a060b038a870879e04142e7a0d407a51476a2907d0")]
    [InlineData(NativeSavedWorldState.LegacyCheckpoint265Sha256)]
    [InlineData(NativeSavedWorldState.LegacyCheckpoint300Sha256)]
    public void UnknownLegacyStateCannotBecomeKnownFlags(string? sha256)
    {
        Assert.False(NativeWorldControlState.IsApprovedLegacyCheckpoint(sha256));
        Assert.Throws<InvalidDataException>(() => NativeWorldControlState.CreateVerifiedLegacyNewGame(sha256!));
    }

    [Fact]
    public void PartyPowerZeroSetsSourceDisabledAndClearsOnlyMenuMask2()
    {
        NativeWorldControlState original = NativeWorldControlState.CreateNewGame() with { Menu209 = 0xFF };
        NativeWorldControlState updated = original.WithPartyPower(0);
        Assert.True(updated.SourceDisabled);
        Assert.Equal((byte)0xFD, updated.Menu209);
        Assert.False(original.SourceDisabled);
        Assert.Equal((byte)0xFF, original.Menu209);
        Assert.Equal(original.Origin, updated.Origin);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void NonzeroPartyPowerClearsSourceWithoutChangingMenu(int argument)
    {
        NativeWorldControlState state = NativeWorldControlState.CreateVerifiedLegacyNewGame(NativeWorldControlState.LegacyCheckpoint172PreinitSha256) with { Menu209 = 0xA7 };
        NativeWorldControlState updated = state.WithPartyPower(argument);
        Assert.False(updated.SourceDisabled);
        Assert.Equal(state.Menu209, updated.Menu209);
    }

    [Fact]
    public void WorldEntryInvertsTheSourceAndPreservesEveryOtherMenuBit()
    {
        foreach (bool disabled in new[] { false, true })
        {
            for (int menu = 0; menu <= byte.MaxValue; menu++)
            {
                NativeWorldControlState state = NativeWorldControlState.CreateNewGame() with { SourceDisabled = disabled, Menu209 = (byte)menu };
                NativeWorldControlState updated = state.WithWorldEntryMenuFlag();
                Assert.Equal(disabled, updated.SourceDisabled);
                Assert.Equal(!disabled, (updated.Menu209 & 2) != 0);
                Assert.Equal(menu & 0xFD, updated.Menu209 & 0xFD);
                Assert.Equal(updated, updated.WithWorldEntryMenuFlag());
            }
        }
    }

    [Fact]
    public void NormalFieldLoadClearsOnlyTheLowTwoMenuBits()
    {
        foreach (bool disabled in new[] { false, true })
        {
            for (int menu = 0; menu <= byte.MaxValue; menu++)
            {
                NativeWorldControlState state = NativeWorldControlState.CreateNewGame() with { SourceDisabled = disabled, Menu209 = (byte)menu };
                NativeWorldControlState updated = state.WithNormalFieldLoad();
                Assert.Equal(disabled, updated.SourceDisabled);
                Assert.Equal((byte)(menu & 0xFC), updated.Menu209);
                Assert.Equal(state.Origin, updated.Origin);
            }
        }
    }

    [Fact]
    public void UpdatedFlagsAndProvenanceRoundTripWithoutReinitialization()
    {
        NativeWorldControlState state = NativeWorldControlState.CreateVerifiedLegacyNewGame(NativeWorldControlState.LegacyCheckpoint172PreinitSha256) with { SourceDisabled = false, Menu209 = 0xE7 };
        NativeWorldControlState restored = JsonSerializer.Deserialize<NativeWorldControlState>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(state, restored);
        Assert.Null(JsonSerializer.Deserialize<NativeWorldControlState>("null"));
    }

    [Fact]
    public void ValidationRejectsUnsupportedOrContradictoryProvenance()
    {
        NativeWorldControlState valid = NativeWorldControlState.CreateNewGame();
        NativeWorldControlState[] invalid =
        [
            valid with { SchemaVersion = 0 },
            valid with { SchemaVersion = 2 },
            valid with { Origin = null! },
            valid with { Origin = valid.Origin with { ProofVersion = 0 } },
            valid with { Origin = valid.Origin with { Kind = (NativeWorldControlOriginKind)99 } },
            valid with { Origin = valid.Origin with { InitializationRule = "unknown" } },
            valid with { Origin = valid.Origin with { LegacyCheckpointSha256 = NativeWorldControlState.LegacyCheckpoint172PreinitSha256 } },
            valid with { Origin = valid.Origin with { Kind = NativeWorldControlOriginKind.VerifiedLegacyNewGame } },
            valid with { Origin = valid.Origin with { Kind = NativeWorldControlOriginKind.VerifiedLegacyNewGame, LegacyCheckpointSha256 = NativeSavedWorldState.LegacyCheckpoint300Sha256 } }
        ];

        foreach (NativeWorldControlState state in invalid)
        {
            Assert.Throws<InvalidDataException>(() => state.Validate());
            Assert.Throws<InvalidDataException>(() => state.WithPartyPower(0));
            Assert.Throws<InvalidDataException>(() => state.WithWorldEntryMenuFlag());
            Assert.Throws<InvalidDataException>(() => state.WithNormalFieldLoad());
        }
    }

    [Fact]
    public void MissingJsonMembersCannotSupplyImplicitFlagOrOriginDefaults()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldControlState>("{}"));
        string complete = JsonSerializer.Serialize(NativeWorldControlState.CreateNewGame());

        foreach (string member in new[] { "SchemaVersion", "Origin", "SourceDisabled", "Menu209" })
        {
            Dictionary<string, JsonElement> properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(complete)!;
            properties.Remove(member);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldControlState>(JsonSerializer.Serialize(properties)));
        }

        string origin = JsonSerializer.Serialize(NativeWorldControlState.CreateNewGame().Origin);
        foreach (string member in new[] { "ProofVersion", "Kind", "InitializationRule", "LegacyCheckpointSha256" })
        {
            Dictionary<string, JsonElement> properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(origin)!;
            properties.Remove(member);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldControlOrigin>(JsonSerializer.Serialize(properties)));
        }
    }
}
