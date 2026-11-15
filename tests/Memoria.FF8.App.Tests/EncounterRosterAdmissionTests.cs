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
    public void Party_and_enemy_restrictions_are_independent_even_for_the_native_exception()
    {
        var unsupported = new BattleEncounterRosterRestrictionReason(BattleEncounterRosterRestriction.UnsupportedEnemyLifecycle, ModelId: 24, LevelBand: 1, Callback: 1, InstructionOffset: 12);
        BattleEncounterRosterAdmission ordinary = BattleEncounterRosterAdmission.ForFormation(87, []);
        BattleEncounterRosterAdmission final = BattleEncounterRosterAdmission.ForFormation(511, []);
        BattleEncounterRosterAdmission enemyOnly = BattleEncounterRosterAdmission.ForFormation(87, [unsupported]);
        BattleEncounterRosterAdmission both = BattleEncounterRosterAdmission.ForFormation(511, [unsupported]);

        Assert.False(ordinary.UsesPrimaryPartyOnly);
        Assert.True(ordinary.AllowsAdditionalEnemies);
        Assert.True(final.UsesPrimaryPartyOnly);
        Assert.True(final.AllowsAdditionalEnemies);
        Assert.False(enemyOnly.UsesPrimaryPartyOnly);
        Assert.False(enemyOnly.AllowsAdditionalEnemies);
        Assert.Equal(unsupported, Assert.Single(enemyOnly.Reasons));
        Assert.True(both.UsesPrimaryPartyOnly);
        Assert.False(both.AllowsAdditionalEnemies);
    }

    [Fact]
    public void Reachable_reserves_and_every_ability_band_contribute_party_departure_facts()
    {
        BattleEncounter encounter = Encounter(87, Enemy(0, 10), Enemy(2, 11, false), Enemy(3, 12, false));
        Dictionary<int, NativeBattleAiDefinition> programs = new()
        {
            [10] = Program(0, [31, 2, 0]),
            [11] = Program(1, [2, 14, 0, 0, 2, 0, 2, 0, 52, 3, 0]),
            [12] = Program(4, [4, 203, 12, 1, 0], (2, 1, Eject))
        };

        (HashSet<int> slots, BattleEncounterRosterAdmission admission) = GameWorld.InspectEncounterRoster(encounter, model => programs[model], [Eject]);

        Assert.Equal(new[] { 2, 3 }, slots.Order());
        Assert.True(admission.UsesPrimaryPartyOnly);
        Assert.True(admission.AllowsAdditionalEnemies);
        Assert.Equal(new BattleEncounterRosterRestrictionReason(BattleEncounterRosterRestriction.ScriptedPartyMembership, 12, 2, 4, 2, 12), Assert.Single(admission.Reasons));
    }

    [Fact]
    public void Only_dispatched_Eject_that_can_target_party_changes_admission()
    {
        BattleEncounter encounter = Encounter(87, Enemy(0, 10));
        BattleNativeAction cure = Eject with { Command = 2, ActionId = 27, KernelSection = 1, KernelIndex = 27, AttackType = 3, Statuses1 = 0x100520D };
        BattleNativeAction ordinaryDamage = Eject with { ActionId = 1, Statuses1 = 0, Statuses0 = 1 };
        BattleNativeAction directlyPreparedEject = Eject with { ActionId = 7 };

        Assert.True(Inspect(Program(1, [4, 2, 12, 1, 0], (0, 1, Eject))).UsesPrimaryPartyOnly);
        Assert.True(Inspect(Program(1, [4, 206, 11, 253, 1, 253, 0], (0, 1, Eject))).UsesPrimaryPartyOnly);
        Assert.True(Inspect(Program(1, [43, 2, 7, 7, 6, 0])).UsesPrimaryPartyOnly);
        Assert.Empty(Inspect(Program(1, [43, 3, 7, 7, 6, 0])).Reasons);
        Assert.Empty(Inspect(Program(1, [43, 2, 7, 7, 7, 1, 6, 0])).Reasons);
        Assert.Empty(Inspect(Program(1, [4, 205, 12, 1, 0], (0, 1, Eject))).Reasons);
        Assert.Empty(Inspect(Program(1, [4, 2, 12, 1, 0], (0, 1, cure))).Reasons);
        Assert.Empty(Inspect(Program(1, [4, 2, 12, 1, 0], (0, 1, ordinaryDamage))).Reasons);
        Assert.Empty(Inspect(Program(1, [0, 4, 2, 12, 1, 0], (0, 1, Eject))).Reasons);
        Assert.Empty(Inspect(Program(1, [4, 2, 0], (0, 1, Eject))).Reasons);
        Assert.Empty(Inspect(Program(1, [47, 48, 53, 1, 58, 2, 0])).Reasons);

        BattleEncounterRosterAdmission Inspect(NativeBattleAiDefinition program) => GameWorld.InspectEncounterRoster(encounter, _ => program, [Eject, cure, ordinaryDamage, directlyPreparedEject]).Admission;
    }

    [Fact]
    public void Referenced_character_prototype_denies_enemy_additions_without_capping_the_party()
    {
        BattleEnemyPlacement characterPrototype = Enemy(2, -16, false);
        BattleEncounter encounter = Encounter(87, Enemy(0, 10), characterPrototype);
        (HashSet<int> slots, BattleEncounterRosterAdmission admission) = GameWorld.InspectEncounterRoster(encounter, model =>
        {
            Assert.Equal(10, model);
            return Program(0, [2, 14, 0, 0, 2, 0, 2, 0, 31, 2, 0]);
        }, []);

        Assert.Equal(2, Assert.Single(slots));
        Assert.False(admission.UsesPrimaryPartyOnly);
        Assert.False(admission.AllowsAdditionalEnemies);
        Assert.Equal(new BattleEncounterRosterRestrictionReason(BattleEncounterRosterRestriction.UnsupportedEnemyLifecycle, 10, SceneSlot: 2), Assert.Single(admission.Reasons));
        BattleEnemyPlacement nativeEntry = Assert.Single(GameWorld.AuthoredEnemyPlacements(encounter));
        Assert.Equal(0, nativeEntry.Slot);
        GameWorld.RequireEnemyStatisticsContract(nativeEntry);
        NotSupportedException unavailable = Assert.Throws<NotSupportedException>(() => GameWorld.RequireEnemyStatisticsContract(characterPrototype));
        Assert.Contains("native character participant statistics contract", unavailable.Message);
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
    public void Installed_supported_enemy_lifecycle_retains_additional_enemy_permission(int formation)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(formation);

        (HashSet<int> slots, BattleEncounterRosterAdmission admission) = GameWorld.InspectEncounterRoster(encounter, model => assets.ReadEnemyAi(model, 0), assets.NativeActions);

        Assert.True(admission.AllowsAdditionalEnemies);
        Assert.False(admission.UsesPrimaryPartyOnly);
        if (formation == 164)
        {
            Assert.Contains(3, slots);
        }
    }

    [Fact]
    public void Installed_final_battle_uses_saved_primary_rows_and_preserves_additional_selection()
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

        Assert.True(native.EncounterRosterAdmission.UsesPrimaryPartyOnly);
        Assert.Contains(native.EncounterRosterAdmission.Reasons, reason => reason.Restriction == BattleEncounterRosterRestriction.NativePartyReplacement);
        Assert.True(native.EncounterRosterAdmission.AllowsAdditionalEnemies);
        Assert.False(native.UsesExpandedRoster);
        Assert.Equal(new[] { "party-2", "party-0", "party-1" }, native.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id));

        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { AdditionalEnemies = 1 });
        BattleDefinition withDonors = world.CreateEncounterBattle(511, 4);

        Assert.Equal(new[] { "party-2", "party-0", "party-1" }, withDonors.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id));
        Assert.Equal(native.EncounterRosterAdmission.Reasons.ToArray(), withDonors.EncounterRosterAdmission.Reasons.ToArray());
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
