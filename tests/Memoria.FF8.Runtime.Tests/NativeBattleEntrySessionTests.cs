using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEntrySessionTests
{
    [Theory]
    [InlineData(0xE010, 0x20, 0xE030)]
    [InlineData(0x00E0, 0x80, 0x0080)]
    [InlineData(0x0020, 0x10, 0x0020)]
    [InlineData(0x0000, 0x0F, 0x000F)]
    public void FormationOverrideClearsOnlyNativeAdvantageBits(ushort requested, byte formation, ushort expected)
    {
        Assert.Equal(expected, NativeBattleEntryRules.MergeFlags(requested, formation));
    }

    [Fact]
    public void SideAdvantageUsesPhysicalSlotsAndPersistsWithoutAnotherDraw()
    {
        BattleSession battle = new(Definition(1), BattleAtbMode.TurnBased);
        BattleSnapshot snapshot = battle.Capture();

        Assert.Equal((byte)4, battle.NativeEntryAdvantage);
        Assert.Equal(BattleSession.AtbMaximum, battle.Combatants[0].Atb);
        Assert.Equal(0, battle.Combatants[1].Atb);
        Assert.Equal(0x800000u, snapshot.Combatants[1].NativeStatus!.Battle & 0x800000u);
        Assert.Equal((byte)3, snapshot.Random!.Indexes[0]);
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(snapshot)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored.Capture())));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativeEntryAdvantage = null }));
    }

    [Fact]
    public void FormationForcedNormalSkipsUnknownDatAndPreservesRandom()
    {
        BattleDefinition definition = Definition(1) with { NativeEntry = new(0x80, 0) };
        definition = definition with { Combatants = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { NativeTemplate = actor.NativeTemplate! with { Information254 = null } }).ToArray() };
        BattleSession battle = new(definition, BattleAtbMode.Normal);

        Assert.Equal((byte)0, battle.NativeEntryAdvantage);
        Assert.Equal((ushort)0x80, battle.ResolvedNativeFlags);
        Assert.Equal(definition.InitialRandom, battle.Capture().Random);
    }

    [Fact]
    public void UnforcedEntryRejectsAnUnownedNonKoDatByte()
    {
        BattleDefinition definition = Definition(0);
        definition = definition with { Combatants = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { NativeTemplate = actor.NativeTemplate! with { Information254 = null } }).ToArray() };

        Assert.Contains("informationBYTE254", Assert.Throws<NotSupportedException>(() => new BattleSession(definition, BattleAtbMode.Normal)).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void NativeAuxiliaryParticipantsPreserveTheirOriginalBlankDisplayNames(string name)
    {
        BattleDefinition definition = Definition(0);
        definition = definition with { Combatants = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { Name = name }).ToArray() };

        BattleSession battle = new(definition, BattleAtbMode.Normal);

        Assert.Equal(name, battle.Combatants.Single(actor => !actor.IsPlayer).Name);
        Assert.Equal(name, battle.Capture().Definition.Combatants.Single(actor => !actor.IsPlayer).Name);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    public void NullNativeNamesAndBlankSyntheticNamesRemainInvalid(string? name, bool native)
    {
        BattleDefinition definition = Definition(0);
        definition = definition with { Combatants = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { Name = name!, NativeTemplate = native ? actor.NativeTemplate : null }).ToArray() };

        Assert.Throws<ArgumentException>(() => new BattleSession(definition, BattleAtbMode.Normal));
    }

    private static BattleDefinition Definition(byte randomIndex)
    {
        NativeBattleEnemyLevel level = new(100, 20, 5, 20, 5, 100, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        return new()
        {
            NativeEntry = new(0, 0),
            InitialRandom = new(Enumerable.Repeat(randomIndex, 8).ToImmutableArray(), 0),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, NativeTemplate = new(1, Enumerable.Repeat(level, 101).ToArray()) { Information254 = 0 } }
            ]
        };
    }
}
