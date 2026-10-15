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
        Assert.False(state.SourceDisabled);
        Assert.Equal((byte)0, state.EventFlags103);
        Assert.Equal((byte)0, state.Menu209);
    }

    [Fact]
    public void KnownControlBytesUseOneLittleEndianBankAndRejectUnknownNeighbors()
    {
        NativeWorldControlState original = NativeWorldControlState.CreateNewGame() with { EventFlags103 = 0xA5, EventFlags104 = 0xC3D2E1F0 };
        byte[] bytes = [0xA5, 0xF0, 0xE1, 0xD2, 0xC3];
        foreach (int width in new[] { 1, 2, 4 })
        {
            for (int address = 103; address + width <= 108; address++)
            {
                int expected = width == 1 ? bytes[address - 103] : width == 2 ? BitConverter.ToUInt16(bytes, address - 103) : BitConverter.ToInt32(bytes, address - 103);
                Assert.Equal(expected, original.ReadEventBytes(address, width));
                foreach (int value in new[] { 0, -1, int.MinValue, 0x76543210 })
                {
                    byte[] updatedBytes = bytes.ToArray();
                    BitConverter.GetBytes(value).AsSpan(0, width).CopyTo(updatedBytes.AsSpan(address - 103, width));
                    NativeWorldControlState updated = original.WriteEventBytes(address, width, value);
                    Assert.Equal(updatedBytes[0], updated.EventFlags103);
                    Assert.Equal(BitConverter.ToUInt32(updatedBytes, 1), updated.EventFlags104);
                    Assert.Equal(original.Menu209, updated.Menu209);
                    Assert.Equal(original.MusicKeep207, updated.MusicKeep207);
                    Assert.Equal(original.Origin, updated.Origin);
                    Assert.Equal(updated, JsonSerializer.Deserialize<NativeWorldControlState>(JsonSerializer.Serialize(updated)));
                }
            }
        }
        foreach ((int address, int width) in new[] { (102, 1), (102, 2), (102, 4), (105, 4), (107, 2), (103, 0), (103, 3), (int.MaxValue, 4) })
        {
            Assert.Throws<NotSupportedException>(() => original.ReadEventBytes(address, width));
            Assert.Throws<NotSupportedException>(() => original.WriteEventBytes(address, width, -1));
        }
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldControlState>(JsonSerializer.Serialize(original).Replace("\"EventFlags103\":165,", "")));
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
        NativeWorldControlState state = NativeWorldControlState.CreateNewGame().WithPartyPower(0) with { Menu209 = 0xA7 };
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
                NativeWorldControlState state = NativeWorldControlState.CreateNewGame().WithPartyPower(disabled ? 0 : 1) with { Menu209 = (byte)menu };
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
                NativeWorldControlState state = NativeWorldControlState.CreateNewGame().WithPartyPower(disabled ? 0 : 1) with { Menu209 = (byte)menu };
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
        NativeWorldControlState state = NativeWorldControlState.CreateNewGame() with { Menu209 = 0xE7 };
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
            valid with { Origin = valid.Origin with { Kind = (NativeWorldControlOriginKind)1 } }
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

        foreach (string member in new[] { "SchemaVersion", "Origin", "EventFlags104", "Menu209", "MusicKeep207" })
        {
            Dictionary<string, JsonElement> properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(complete)!;
            properties.Remove(member);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldControlState>(JsonSerializer.Serialize(properties)));
        }

        string origin = JsonSerializer.Serialize(NativeWorldControlState.CreateNewGame().Origin);
        foreach (string member in new[] { "ProofVersion", "Kind", "InitializationRule" })
        {
            Dictionary<string, JsonElement> properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(origin)!;
            properties.Remove(member);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldControlOrigin>(JsonSerializer.Serialize(properties)));
        }
    }
}
