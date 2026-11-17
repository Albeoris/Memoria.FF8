using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class EncounterRosterAdmissionTests
{
    private static readonly BattleNativeAction Eject = new(8, 336, 3, 336)
    {
        MagicId = 83, AttackType = 2, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 0x80, DamagePresentation = 0,
        Element = 0, ElementPercent = null, StatusAttack = 255, Statuses0 = 0, Statuses1 = 0x10000,
        Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
    };

    [Fact]
    public void Reachable_reserves_are_followed_across_referenced_enemy_programs()
    {
        BattleEncounter encounter = Encounter(87, Enemy(0, 10), Enemy(2, 11, false), Enemy(3, 12, false));
        Dictionary<int, NativeBattleAiDefinition> programs = new()
        {
            [10] = Program(0, [31, 2, 0]),
            [11] = Program(1, [2, 14, 0, 0, 2, 0, 2, 0, 52, 3, 0]),
            [12] = Program(4, [4, 203, 12, 1, 0], (2, 1, Eject))
        };

        HashSet<int> slots = GameWorld.InspectEncounterRoster(encounter, model => programs[model], [Eject]);

        Assert.Equal(new[] { 2, 3 }, slots.Order());
    }

    [Fact]
    public void Referenced_character_prototype_requires_an_explicit_statistics_contract()
    {
        BattleEnemyPlacement characterPrototype = Enemy(2, -16, false);
        BattleEncounter encounter = Encounter(87, Enemy(0, 10), characterPrototype);
        NotSupportedException unsupported = Assert.Throws<NotSupportedException>(() => GameWorld.InspectEncounterRoster(encounter, model =>
        {
            Assert.Equal(10, model);
            return Program(0, [2, 14, 0, 0, 2, 0, 2, 0, 31, 2, 0]);
        }, []));

        BattleEnemyPlacement nativeEntry = Assert.Single(GameWorld.AuthoredEnemyPlacements(encounter));
        Assert.Equal(0, nativeEntry.Slot);
        GameWorld.RequireEnemyStatisticsContract(nativeEntry);
        NotSupportedException unavailable = Assert.Throws<NotSupportedException>(() => GameWorld.RequireEnemyStatisticsContract(characterPrototype));
        Assert.Contains("native character participant statistics contract", unavailable.Message);
        Assert.Equal(unavailable.Message, unsupported.Message);
    }

    [Theory]
    [InlineData(new byte[] { 31, 2 })]
    [InlineData(new byte[] { 35, 40, 0 })]
    [InlineData(new byte[] { 59, 2, 1, 0 })]
    [InlineData(new byte[] { 31, 2, 0 })]
    public void Malformed_AI_or_missing_reserve_is_not_a_roster_fallback(byte[] script)
    {
        BattleEncounter encounter = Encounter(87, Enemy(0, 10));

        Assert.Throws<InvalidDataException>(() => GameWorld.InspectEncounterRoster(encounter, _ => Program(0, script), []));
    }

    [Fact]
    public void Referenced_empty_prototype_is_invalid_data()
    {
        BattleEncounter encounter = Encounter(87, Enemy(0, 10), Enemy(2, 239, false));

        Assert.Throws<InvalidDataException>(() => GameWorld.InspectEncounterRoster(encounter, _ => Program(0, [31, 2, 0]), []));
    }

    [Theory]
    [InlineData(29)]
    [InlineData(164)]
    public void Installed_supported_enemy_lifecycle_reads_actual_reserve_slots(int formation)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(formation);

        HashSet<int> slots = GameWorld.InspectEncounterRoster(encounter, model => assets.ReadEnemyAi(model, 0), assets.NativeActions);

        if (formation == 164)
        {
            Assert.Contains(3, slots);
        }
        else
        {
            Assert.All(slots, slot => Assert.Contains(encounter.Slots, enemy => enemy.Slot == slot));
        }
    }

    [Fact]
    public void Installed_final_battle_keeps_four_selected_rows_and_additional_enemy_preference()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        ExpandedBattleEntryAdmissionTests.SelectFourAllies(world, session);
        session.SetCharacterVitals(session.State.CharacterVitals!.WithBattleParty([2, 0, 1, 3], 4));
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { AdditionalEnemies = 0 });

        BattleDefinition native = world.CreateEncounterBattle(511, 4);

        Assert.True(native.UsesOriginalEnemySlots);
        Assert.Equal(new[] { "party-2", "party-0", "party-1", "party-3" }, native.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id));

        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { AdditionalEnemies = 1 });
        BattleDefinition withDonors = world.CreateEncounterBattle(511, 4);

        Assert.Equal(new[] { "party-2", "party-0", "party-1", "party-3" }, withDonors.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id));
        Assert.Equal(new byte[] { 2, 0, 1, 3 }, session.State.CharacterVitals!.GetBattlePartyRows(4));
        Assert.Equal(4, session.State.GameplayPreferences.BattleAllyCount);
        Assert.Equal(1, session.State.GameplayPreferences.AdditionalEnemies);
    }

    private static BattleEncounter Encounter(int id, params BattleEnemyPlacement[] placements)
    {
        return new(id, 0, 0, 0, 0, placements.Where(placement => placement.Enabled).ToArray()) { Slots = placements };
    }

    private static BattleEnemyPlacement Enemy(int slot, int model, bool enabled = true)
    {
        return new(slot, model, default, 1, false, false) { Enabled = enabled };
    }

    private static NativeBattleAiDefinition Program(int callback, byte[] script, params (int Band, int Slot, BattleNativeAction Action)[] abilities)
    {
        byte[][] scripts = [[0], [0], [0], [0], [0]];
        scripts[callback] = script;
        var catalog = new NativeBattleAiAbility[48];
        Array.Fill(catalog, new NativeBattleAiAbility(0, 0, 0));
        foreach ((int band, int slot, BattleNativeAction action) in abilities)
        {
            catalog[band * 16 + slot] = new(action.Command, 0, action.ActionId);
        }
        return new(scripts, catalog, 0, []);
    }
}
