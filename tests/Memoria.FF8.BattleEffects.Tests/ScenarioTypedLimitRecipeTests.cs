using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioTypedLimitRecipeTests
{
    private static readonly JsonSerializerOptions InputJson = new(RecordJson.Options) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Installed_Slots_recipes_keep_Selphie_and_real_rolls_in_independently_pinned_entries()
    {
        using InstalledFixture fixture = InstalledFixture.Open();
        Dictionary<string, DiagnosticFreshEntryDeclaration> pins = new(StringComparer.OrdinalIgnoreCase);
        foreach (byte actionId in new byte[] { 51, 52, 53, 56 })
        {
            BattleNativeAction action = Assert.Single(fixture.Assets.NativeActions, action => action.Command == 16 && action.ActionId == actionId);
            Assert.True(ScenarioDiscovery.TryPrepareTypedLimitRecipe(fixture.Assets, fixture.Initial, fixture.Experience, action, fixture.Template, fixture.Directory, out JsonObject? recipe, out string? blocker, pins), blocker);
            JsonObject character = recipe!["fixture"]!["characters"]![0]!.AsObject();
            int row = character["row"]!.GetValue<int>();
            Assert.Equal(5, fixture.Initial.Records[row].CharacterId);
            Assert.Equal(5, character["character_id"]!.GetValue<int>());
            Assert.Null(character["magic_command"]);
            Assert.Empty(character["commands"]!.AsArray());
            Assert.Equal(new[] { row, 255, 255 }, recipe["fixture"]!["combat_party"]!.AsArray().Select(value => value!.GetValue<int>()));
            Assert.Equal(1 << (actionId - 51), recipe["fixture"]!["limits"]!["learn_special_magic"]!.GetValue<int>());
            JsonObject driver = recipe["driver"]!.AsObject();
            DiagnosticSlotsDeclaration declaration = driver["slots_admission"]!.Deserialize<DiagnosticSlotsDeclaration>(RecordJson.Options)!;
            Assert.Equal(DiagnosticSlotsDeclaration.ReadySelfBatch2, declaration.Reselection);
            BattleInput input = driver["input"]!.Deserialize<BattleInput>(InputJson)!;
            declaration.Validate(input, action.MagicId, actionId, 16);
            Assert.Equal($"party-{row}", input.ActorId);
            Assert.Equal(4, driver["limit_selection"]!["minimum_crisis_level"]!.GetValue<int>());
            Assert.Equal(DiagnosticFreshEntryDeclaration.SingleUse, driver["fresh_entry"]!["kind"]!.GetValue<string>());
            Assert.Empty(driver["repairs"]!.AsArray());
            Assert.Null(driver["terminal_hold"]);
            Assert.Null(driver["shot_cycle"]);
            Assert.False(recipe["fixture"]!["limits"]!["borrow_blue_magic_for_squall"]!.GetValue<bool>());
            if (actionId is 53 or 56)
            {
                BattleEncounter formation = fixture.Assets.LoadEncounter(recipe["encounter"]!.GetValue<int>());
                BattleEnemyPlacement target = Assert.Single(formation.Enemies, candidate => candidate.Enabled);
                Assert.False(target.Hidden);
                Assert.False(target.InitiallySuspended);
                Assert.False(target.Untargetable);
                Assert.Equal(target.ModelId, recipe["target"]!["model"]!.GetValue<int>());
                Assert.Equal(target.Slot, recipe["target"]!["scene_slot"]!.GetValue<int>());
                Assert.Equal(formation.StageId, recipe["target"]!["stage"]!.GetValue<int>());
                Assert.Equal($"enemy-{target.Slot}", input.TargetId);
                Assert.Equal(3, recipe["target"]!["physical_index"]!.GetValue<int>());
                Assert.Null(recipe["target"]!["opening_action"]);
                Assert.Null(recipe["target"]!["opening_effect"]);
                NativeBattleAiDefinition ai = fixture.Assets.ReadEnemyAi(target.ModelId, 1);
                Assert.NotEmpty(ai.Scripts[0]);
                Assert.All(ai.Scripts[0], value => Assert.Equal((byte)0, value));
                Assert.NotEmpty(ai.Scripts[3]);
                Assert.All(ai.Scripts[3], value => Assert.Equal((byte)0, value));
                Assert.Equal(0x10u, fixture.Assets.ReadEnemyNativeCombatantFlags(target.ModelId));
                NativeBattleStatusAllocation status = fixture.Assets.ReadEnemyNativeStatusData(target.ModelId);
                Assert.Equal((ushort)0, status.Saved);
                Assert.Equal(0u, status.Battle);
                if (actionId == 53)
                {
                    Assert.Equal(0x10000u, action.Statuses1);
                    Assert.True(status.Resistances[24] < 200);
                    foreach (int level in Enumerable.Range(1, 100))
                    {
                        Assert.True(action.StatusAttack - fixture.Assets.ReadEnemyNativeStats(target.ModelId).Project(level).Spirit / 4 - status.Resistances[24] > 0);
                    }
                }
                else
                {
                    Assert.Equal((byte)255, action.StatusAttack);
                    Assert.Equal((ushort)1, action.Statuses0);
                }
            }
        }
        Assert.Single(pins);
    }

    [Fact]
    public void Installed_Shot_recipes_declare_original_ammo_and_the_whole_native_chain()
    {
        using InstalledFixture fixture = InstalledFixture.Open();
        foreach (byte ammo in Enumerable.Range(0, 8).Select(value => (byte)value))
        {
            BattleNativeAction action = Assert.Single(fixture.Assets.NativeActions, action => action.Command == 237 && action.ActionId == ammo);
            Assert.True(ScenarioDiscovery.TryPrepareTypedLimitRecipe(fixture.Assets, fixture.Initial, fixture.Experience, action, fixture.Template, fixture.Directory, out JsonObject? recipe, out string? blocker), blocker);
            JsonObject character = recipe!["fixture"]!["characters"]![0]!.AsObject();
            int row = character["row"]!.GetValue<int>();
            Assert.Equal(2, fixture.Initial.Records[row].CharacterId);
            JsonObject driver = recipe["driver"]!.AsObject();
            BattleInput input = driver["input"]!.Deserialize<BattleInput>(InputJson)!;
            DiagnosticShotCycleDeclaration declaration = driver["shot_cycle"]!.Deserialize<DiagnosticShotCycleDeclaration>(RecordJson.Options)!;
            declaration.Validate(input, action.MagicId, ammo, 237);
            Assert.Equal(Assert.Single(fixture.Assets.NativeActions, candidate => candidate.Command == 14 && candidate.ActionId == ammo).MagicId, declaration.OpeningEffect);
            Assert.Equal(Assert.Single(fixture.Assets.NativeActions, candidate => candidate.Command == 238 && candidate.ActionId == ammo).MagicId, declaration.EndingEffect);
            Assert.Equal($"party-{row}", input.ActorId);
            Assert.Equal("enemy-6", input.TargetId);
            JsonNode stock = Assert.Single(recipe["fixture"]!["items"]!.AsArray())!;
            Assert.Equal(101 + ammo, stock["id"]!.GetValue<int>());
            Assert.Equal(3, stock["count"]!.GetValue<int>());
            Assert.Empty(driver["repairs"]!.AsArray());
            Assert.Null(driver["terminal_hold"]);
            Assert.Null(driver["fresh_entry"]);
            Assert.Null(driver["enemy_ability"]);
        }
    }

    [Theory]
    [InlineData(14, 0, 21)]
    [InlineData(238, 0, 21)]
    [InlineData(237, 8, 21)]
    [InlineData(16, 1, 1)]
    [InlineData(15, 51, 1)]
    [InlineData(241, 0, 22)]
    public void Unproved_aliases_and_other_Limit_processes_do_not_become_selectable(byte command, ushort action, int section)
    {
        BattleNativeAction native = new(command, action, section, action)
        {
            MagicId = 1,
            AttackType = 1,
            Power = 0,
            HitCount = 1,
            TargetFlags = 0x40,
            AttackFlags = 0,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
        Assert.False(ScenarioDiscovery.IsTypedLimitScenario(native));
    }

    [Fact]
    public void Slots_level_search_cannot_lower_an_owner_or_invent_a_spell_absent_from_native_tables()
    {
        ImmutableArray<byte> selections = new byte[128].ToImmutableArray();
        BattleNativeLimitDefinition rules = new(5, 16, 0, 0, new byte[32].ToImmutableArray()) { SlotsSelectionBytes = selections, SlotsSpellBytes = Enumerable.Range(0, 8).SelectMany(_ => new byte[] { 51, 1 }).ToImmutableArray() };
        Assert.Equal(19, ScenarioDiscovery.ResolveSlotsPreparationLevel(rules, 51, 19));
        Assert.Equal(0, ScenarioDiscovery.ResolveSlotsPreparationLevel(rules, 56, 19));
    }

    private sealed class InstalledFixture : IDisposable
    {
        private readonly IResourceSource source;
        public string Directory { get; }
        public string Template { get; }
        public NativeCharacterVitals Initial { get; }
        public NativeBattleExperienceDefinition Experience { get; }
        public BattleAssets Assets { get; }

        private InstalledFixture(string directory)
        {
            Directory = directory;
            source = GameDataSourceFactory.Open(directory);
            ResourceService resources = new(source);
            byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
            Initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), kernel);
            Experience = NativeBattleExperienceDefinitionReader.Read(kernel);
            Assets = new(directory, resources);
            Template = Path.Combine(Path.GetTempPath(), "typed-limit-recipe-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(Template, """{"target":{"actor_id":"enemy-6","physical_index":3},"fixture":{"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[],"magic_command":{"ability":20,"slot":0}}]},"driver":{}}""");
        }

        public static InstalledFixture Open()
        {
            string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
            Assert.SkipUnless(directory is not null && System.IO.Directory.Exists(directory), "Select installed resources for original typed Limit recipe validation.");
            return new(directory!);
        }

        public void Dispose()
        {
            Assets.Dispose();
            source.Dispose();
            File.Delete(Template);
        }
    }
}
