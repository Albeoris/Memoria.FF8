using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticMagicJunctionPreparationTests
{
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(19, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 57)]
    public void Junction_declarations_require_native_slots_and_nonzero_magic(int slot, int action)
    {
        DiagnosticBattlePreparation fixture = new([], [new(0, 0, [], []) { MagicJunctions = [new(slot, action)] }], false);
        Assert.Throws<InvalidDataException>(fixture.Validate);
    }

    [Fact]
    public void Junction_declarations_reject_duplicate_slots_magic_and_default_arrays()
    {
        DiagnosticCharacterPreparation character = new(0, 0, [], []);
        DiagnosticBattlePreparation fixture = new([], [character], false);
        Assert.Throws<InvalidDataException>(() => (fixture with { Characters = [character with { MagicJunctions = [new(1, 1), new(1, 2)] }] }).Validate());
        Assert.Throws<InvalidDataException>(() => (fixture with { Characters = [character with { MagicJunctions = [new(1, 1), new(2, 1)] }] }).Validate());
        Assert.Throws<InvalidDataException>(() => (fixture with { Characters = [character with { MagicJunctions = default }] }).Validate());
        fixture.Validate();
    }

    [Fact]
    public void Installed_strength_junction_uses_native_gf_stock_and_stat_rules_without_changing_level_or_source()
    {
        (NativeCharacterVitals original, BattlePlayerStatReader reader) = InstalledInputs();
        int row = original.CombatParty[0];
        BattleNativePlayerStatRules rules = reader.ReadRules(original, row);
        int spell = Enumerable.Range(1, rules.MagicRecords.Length - 1).OrderByDescending(id => rules.MagicRecords[id][24]).ThenBy(id => id).First();
        DiagnosticGuardianForcePreparation guardian = StrengthProvider(original);
        DiagnosticCharacterPreparation character = new(row, original.Records[row].CharacterId, [new(spell, 100)], [guardian.Id]) { MagicJunctions = [new(1, spell)] };
        DiagnosticBattlePreparation fixture = new([guardian], [character], false);
        string before = JsonSerializer.Serialize(original);
        BattlePlayerStats previous = reader.Read(original, row);
        DiagnosticPreparationResult result = GameWorld.PrepareDiagnosticCharacters(original, fixture);

        Assert.True(reader.Read(result.Vitals, row).Strength > previous.Strength);
        Assert.Equal(original.Records[row].Experience, result.Vitals.Records[row].Experience);
        Assert.Equal(original.Records[row].Weapon, result.Vitals.Records[row].Weapon);
        Assert.Equal(original.Records[row].StatBonuses, result.Vitals.Records[row].StatBonuses);
        Assert.Equal(original.GuardianForces.Select(gf => gf.Experience), result.Vitals.GuardianForces.Select(gf => gf.Experience));
        Assert.Equal(100, result.Vitals.GetMagicCount(row, spell));
        Assert.Equal(spell, result.Vitals.Records[row].Junction.MagicJunctions[1]);
        Assert.Contains(2, result.Vitals.JunctionLearnedAbilities(row));
        Assert.Equal(before, JsonSerializer.Serialize(original));
        Assert.Contains(result.Changes, change => change.Owner == "character" && change.Row == row);
        DiagnosticBattlePreparation restored = JsonSerializer.Deserialize<DiagnosticBattlePreparation>(JsonSerializer.Serialize(fixture))!;
        Assert.Equal(spell, restored.Characters[0].MagicJunctions[0].Action);

        Assert.Throws<InvalidOperationException>(() => GameWorld.PrepareDiagnosticCharacters(original, fixture with { Characters = [character with { Stocks = [] }] }));
        Assert.Throws<InvalidOperationException>(() => GameWorld.PrepareDiagnosticCharacters(original, fixture with { GuardianForces = [], Characters = [character with { GuardianForces = [] }] }));
        Assert.Throws<InvalidOperationException>(() => GameWorld.PrepareDiagnosticCharacters(original, fixture with { Characters = [character with { CharacterId = checked((byte)((character.CharacterId + 1) % 11)) }] }));
        Assert.Equal(before, JsonSerializer.Serialize(original));
    }

    private static DiagnosticGuardianForcePreparation StrengthProvider(NativeCharacterVitals source)
    {
        for (int id = 0; id < source.GuardianForces.Length; id++)
        {
            NativeCharacterVitals candidate = source.AcquireGuardianForce(id);
            if (candidate.GuardianForces[id].HasLearnedAbility(2))
            {
                return new(id, true);
            }
            NativeGuardianForceAbility? ability = candidate.GuardianForceAbilities(id).SingleOrDefault(entry => entry.Ability == 2 && !entry.Learned);
            if (ability is not null)
            {
                return new(id, true) { LearningAbility = 2, AbilityPoints = checked((ushort)Math.Max(1, ability.RequiredPoints - ability.Progress)) };
            }
        }
        throw new InvalidDataException("Installed GF definitions have no available Strength junction provider.");
    }

    private static (NativeCharacterVitals Vitals, BattlePlayerStatReader Reader) InstalledInputs()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed native character and junction definitions.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        return (NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), kernel) with { CombatParty = [0, 255, 255] }, new(kernel));
    }
}
