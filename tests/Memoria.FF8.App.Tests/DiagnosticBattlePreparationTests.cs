using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticBattlePreparationTests
{
    [Fact]
    public void Direct_graphical_launch_skips_startup_without_allocating_a_fixture()
    {
        LaunchOptions options = LaunchOptions.Parse(["--battle-test", "case.json", "--control-dir", "control", "--settings-dir", "settings", "--save-state", "saves/checkpoint.json"]);
        Assert.NotNull(options.BattleTestPath);
        Assert.Equal(SceneKind.Title, options.InitialState.Scene);
        Assert.False(StartupSequence.ShouldPlay(options));
        Assert.False(options.ControlStep);
        Assert.False(options.SoftwareRenderer);
    }

    [Fact]
    public void Diagnostic_launch_requires_isolated_settings_control_and_save_paths()
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--battle-test", "case.json"]));
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--battle-test", "case.json", "--control-dir", "shared", "--settings-dir", "shared", "--save-state", "saves/checkpoint.json"]));
        GlobalSettingsPaths paths = new(isolatedDirectory: Path.GetFullPath("isolated-settings"));
        Assert.Equal(Path.GetFullPath("isolated-settings"), paths.DirectoryPath);
    }

    [Theory]
    [InlineData("--control-step")]
    [InlineData("--software-renderer")]
    [InlineData("--validate")]
    [InlineData("--debug-room")]
    public void Diagnostic_launch_rejects_incompatible_host_modes(string argument)
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--battle-test", "case.json", "--control-dir", "control", argument]));
    }

    [Fact]
    public void Diagnostic_recipe_does_not_default_required_native_inputs()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DiagnosticBattleRequest>("{}"));
    }

    [Fact]
    public void Music_prerequisites_are_explicit_and_native_keep_music_is_respected()
    {
        DiagnosticBattleRequest request = new(1, 2, 0, 0, 0);
        Assert.Throws<NotSupportedException>(request.Validate);
        (request with { Flags = 0x10 }).Validate();
        (request with { BattleMusic = 1, PrimaryMusicChannel = -1 }).Validate();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Encounter_must_fit_the_native_formation_word(int encounter)
    {
        Assert.Throws<InvalidDataException>(() => new DiagnosticBattleRequest(encounter, 0, 0x10, 0, 0).Validate());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_diagnostic_roster_retains_authored_duplicate_owners_at_their_physical_aliases_when_requested(bool authored)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for authored diagnostic roster validation.");
        using BattleAssets assets = new(directory!);
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        DiagnosticBattleRoster roster = new([new(356, 0, assets.FormationSourceHash(356))], 0)
        {
            DonorCount = 1,
            AuthoredFormationHash = authored ? assets.FormationSourceHash(147) : null,
        };
        BattleSession battle = world.StartDiagnosticBattle(new(147, 0, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([], [], true),
            Roster = roster,
        });
        try
        {
            BattleCombatant[] iguions = battle.Combatants.Where(actor => !actor.IsPlayer && actor.ModelId == 93).ToArray();
            Assert.Equal(authored ? new[] { 3, 4 } : [3], iguions.Select(actor => (int)actor.RequireNativeActorIndex()));
            Assert.Equal(11, battle.Combatants.Count(actor => actor.IsPlayer));
            Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId != 93);
        }
        finally
        {
            world.EndDiagnosticBattle();
        }
    }

    [Fact]
    public void Zero_donors_require_an_explicit_empty_list_count_and_authored_formation_hash()
    {
        string hash = new('a', 64);
        DiagnosticBattleRoster roster = new([], 1) { DonorCount = 0, AuthoredFormationHash = hash };
        roster.Validate();
        Assert.Throws<InvalidDataException>(() => (roster with { DonorCount = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (roster with { AuthoredFormationHash = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (roster with { AuthoredFormationHash = "invalid" }).Validate());
        Assert.Throws<InvalidDataException>(() => (roster with { Donors = default }).Validate());
        Assert.Throws<InvalidDataException>(() => (roster with { Donors = [new(356, 0, hash)] }).Validate());
    }

    [Fact]
    public void Installed_formation_431_has_no_added_enemy_in_an_authored_only_diagnostic()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for the original-only diagnostic roster.");
        using BattleAssets assets = new(directory!);
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        DiagnosticBattleRoster roster = new([], 1) { DonorCount = 0, AuthoredFormationHash = assets.FormationSourceHash(431) };
        BattleSession battle = world.StartDiagnosticBattle(new(431, 0, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([], [], true),
            Roster = roster,
        });
        try
        {
            BattleCombatant trauma = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive);
            Assert.Equal(109, trauma.ModelId);
            Assert.Equal(1, trauma.SceneSlot);
            Assert.Equal(3, trauma.RequireNativeActorIndex());
            Assert.Equal(11, battle.Combatants.Count(actor => actor.IsPlayer));
            Assert.Empty(battle.ReadDefinition().AddedEnemySources!);
        }
        finally
        {
            world.EndDiagnosticBattle();
        }
    }
}
