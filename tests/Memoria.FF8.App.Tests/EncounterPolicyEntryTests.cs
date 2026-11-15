using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class EncounterPolicyEntryTests
{
    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 0)]
    [InlineData(3, 1)]
    public void Installed_battle_entry_applies_published_actor_modifiers_in_both_roster_modes(int allies, int additionalEnemies)
    {
        bool expanded = allies > 3 || additionalEnemies > 0;
        string directory = TestResources.GameDirectory;
        using GameResourceHost resources = OpenHost(directory, ActorModifiers);
        Assert.Single(resources.EncounterMods.Layers);
        GameSession session = CreateSession(directory, allies, additionalEnemies);
        using GameWorld world = new(session, directory, resources: resources.Resources, applyEncounterMods: resources.ApplyEncounterMods);
        world.EnsureField();

        world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 4));
        BattleSession battle = world.PrepareBattleEntry(136, 0, 4, BattleAtbMode.TurnBased).Battle;
        BattleDefinition definition = battle.ReadDefinition();
        Assert.Equal(expanded, definition.UsesExpandedRoster);
        Assert.Equal(allies, definition.Combatants.Count(actor => actor.IsPlayer));
        Assert.Equal(session.State.GameplayPreferences.GetEffectiveAdditionalEnemies(allies), definition.AddedEnemySources?.Count ?? 0);
        Assert.Equal(expanded, definition.NativeGraphics is null);
        EncounterCompiledDefinition mod = Assert.IsType<EncounterCompiledDefinition>(definition.EncounterMod);
        Assert.Equal(EncounterBindingResolver.Disc1Edea, mod.Encounter);
        Assert.NotNull(mod.NativeEvents);
        Assert.NotEmpty(mod.NativeEvents.DeathTerminal);
        BattleCombatantDefinition source = Assert.Single(definition.Combatants, actor => actor.Id == "enemy-0");
        BattleEnemyNativeProjection baseline = world.BattleResources.ReadEnemyNativeStats(source.ModelId).Project(source.Level);
        Assert.Equal(1.25, source.MaximumHpMultiplier);
        Assert.Equal(EncounterActorProperties.ScaleMaximumHp(baseline.MaximumHp, 1.25), source.MaximumHp);
        Assert.Equal(source.MaximumHp, source.CurrentHp);
        Assert.Equal((byte)0, source.NativeStatusResistances[15]);
        Assert.NotNull(source.NativeAi);
        BattleCombatant live = Assert.Single(battle.Combatants, actor => actor.Id == source.Id);
        Assert.Equal(source.MaximumHp, live.CurrentHp);
        Assert.Equal((byte)0, live.NativeStatusResistances[15]);

        BattleDefinition rebuilt = world.CreateEncounterBattle(136, 4, restoredDefinition: definition);
        Assert.Same(mod, rebuilt.EncounterMod);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(rebuilt.Combatants.Single(actor => actor.Id == source.Id)));
    }

    [Fact]
    public void Enabled_package_without_a_valid_publication_is_rejected_during_expanded_entry()
    {
        string directory = TestResources.GameDirectory;
        using GameResourceHost resources = OpenHost(directory, ActorModifiers.Replace("ff8.disc1.edea", "unknown.encounter", StringComparison.Ordinal));
        Assert.Empty(resources.EncounterMods.Layers);
        Assert.Contains(resources.Mods.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidEncounterComposition");

        GameSession session = CreateSession(directory, allies: 4);
        using GameWorld world = new(session, directory, resources: resources.Resources, applyEncounterMods: resources.ApplyEncounterMods);
        world.EnsureField();

        InvalidDataException failure = Assert.Throws<InvalidDataException>(() => world.CreateEncounterBattle(136, 4));
        Assert.Contains("enabled encounter package", failure.Message);
        Assert.Contains("unknown.encounter", failure.Message);
        Assert.Null(world.Battle);
    }

    [Fact]
    public void Expanded_entry_retains_published_camera_elevation_for_original_camera_services()
    {
        const string surround = """
                { "id": "camera", "kind": "SetCameraProperty", "property": "elevationDegrees", "expectedValue": 0, "value": 12 },
            """;
        string directory = TestResources.GameDirectory;
        using GameResourceHost resources = OpenHost(directory, ActorModifiers.Replace("\"operations\": [", "\"operations\": [\n" + surround, StringComparison.Ordinal));
        Assert.Single(resources.EncounterMods.Layers);
        GameSession session = CreateSession(directory, allies: 4);
        using GameWorld world = new(session, directory, resources: resources.Resources, applyEncounterMods: resources.ApplyEncounterMods);
        world.EnsureField();

        BattleDefinition definition = world.CreateEncounterBattle(136, 4);
        Assert.True(definition.UsesExpandedRoster);
        Assert.Equal(12, definition.EncounterMod!.Camera!.ElevationDegrees);
        Assert.Null(world.Battle);
    }

    private static GameResourceHost OpenHost(string directory, string document)
    {
        string application = TestResources.ArtifactFile(Guid.NewGuid().ToString("N"));
        string package = Path.Combine(application, "Mods", "Memoria", "Encounter");
        string definitions = Path.Combine(package, "gameplay", "encounters");
        Directory.CreateDirectory(definitions);
        File.WriteAllText(Path.Combine(package, "mod.ini"), "enabled=1\n");
        File.WriteAllText(Path.Combine(definitions, "encounter.json"), document);
        GameResourceHost resources = new(directory, applicationBaseDirectory: application, watch: false);
        resources.ConnectEncounterMods();
        return resources;
    }

    private static GameSession CreateSession(string directory, int allies, int additionalEnemies = 0)
    {
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        NativeCharacterVitals vitals = session.CharacterVitals!.WithGuestBattleRecords();
        vitals = vitals with { Records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray() };
        session.SetCharacterVitals(vitals.Rest().WithBattleParty(Enumerable.Range(0, allies).Select(row => checked((byte)row)).ToArray(), allies));
        session.Restore(session.State with { GameplayPreferences = session.State.GameplayPreferences with { BattleAllyCount = allies, AdditionalEnemies = additionalEnemies } });
        return session;
    }

    private const string ActorModifiers = """
        {
          "schemaVersion": 1,
          "documentId": "fixture.encounter-edit",
          "encounter": "ff8.disc1.edea",
          "atomic": true,
          "operations": [
            { "id": "hp", "kind": "SetActorProperty", "actor": "original", "property": "stats.maximumHpMultiplier", "expectedValue": 1.0, "value": 1.25 },
            { "id": "reflect", "kind": "SetActorProperty", "actor": "original", "property": "statusResistances.reflectMultiplier", "expectedValue": 1.0, "value": 0 }
          ]
        }
        """;
}
