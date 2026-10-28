using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticOrdinaryAttackPreludeTests
{
    [Theory]
    [InlineData("valid", false)]
    [InlineData("pending", false)]
    [InlineData("flags", false)]
    [InlineData("physical", true)]
    [InlineData("model", true)]
    [InlineData("scene", true)]
    [InlineData("stage", true)]
    [InlineData("level-band", true)]
    [InlineData("initialization", true)]
    public void Completed_native_allocation_refuses_immutable_identity_mismatch_without_using_flags_as_a_completion_shortcut(string change, bool rejected)
    {
        string hash = new('a', 64);
        DiagnosticTargetExpectation expected = Request().Target! with { Stage = 142, Model = 122, PhysicalIndex = 5 };
        BattleDiagnosticTarget observed = new("native-allocation-1", 142, 122, 2, 5, 1, 27760, true, true, hash, 600);
        int band = 0;
        switch (change)
        {
            case "pending":
                observed = observed with { StatisticsReady = false, PhysicalIndex = 3 };
                break;
            case "flags":
                observed = observed with { Flags = 0xC1, Alive = false };
                break;
            case "physical":
                observed = observed with { PhysicalIndex = 3 };
                break;
            case "model":
                observed = observed with { Model = 123 };
                break;
            case "scene":
                observed = observed with { SceneSlot = 3 };
                break;
            case "stage":
                observed = observed with { Stage = 143 };
                break;
            case "level-band":
                band = 1;
                break;
            case "initialization":
                observed = observed with { InitializationScriptSha256 = new string('b', 64) };
                break;
        }
        if (rejected)
        {
            Assert.Throws<InvalidDataException>(() => GameWorld.VerifyInitializedPreludeAllocationIdentity(expected, observed, 0, band, hash));
        }
        else
        {
            GameWorld.VerifyInitializedPreludeAllocationIdentity(expected, observed, 0, band, hash);
        }
    }

    [Fact]
    public void Declared_ordinary_prelude_enum_strings_roundtrip_and_nonordinary_commands_are_refused()
    {
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, Converters = { new JsonStringEnumConverter() } };
        JsonObject value = JsonSerializer.SerializeToNode(Request(), options)!.AsObject();
        Assert.Equal("Action", value["prelude"]!["input"]!["kind"]!.GetValue<string>());
        DiagnosticBattleRequest restored = JsonSerializer.Deserialize<DiagnosticBattleRequest>(value.ToJsonString(), options)!;
        restored.Validate();
        Assert.Equal(BattleAbility.Attack, restored.Prelude!.Input.Ability);
        value["prelude"]!["input"]!["kind"] = "InstantVictory";
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<DiagnosticBattleRequest>(value.ToJsonString(), options)!.Validate());
    }

    [Fact]
    public void Prelude_requires_declared_ordinary_attack_and_original_enemy_identity_in_both_supported_modes()
    {
        DiagnosticBattleRequest request = Request();
        request.Validate();
        (request with { AtbMode = BattleAtbMode.TurnBased }).Validate();
        Assert.Throws<InvalidDataException>(() => (request with { AtbMode = (BattleAtbMode)999 }).Validate());
        Assert.Throws<InvalidDataException>(() => (request with { PrepareEntryBeforePresentation = false }).Validate());
        Assert.Throws<InvalidDataException>(() => (request with { Prelude = request.Prelude! with { Input = request.Prelude.Input with { Ability = BattleAbility.Magic } } }).Validate());
        Assert.Throws<InvalidDataException>(() => (request with { Prelude = request.Prelude! with { MaximumCallbacks = 18001 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (request with { Prelude = request.Prelude! with { MaximumAttacks = 0 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (request with { Target = request.Target! with { Model = 99 } }).Validate());
    }

    [Theory]
    [InlineData(120, 100, 87, false, 5)]
    [InlineData(511, 122, 121, true, 5)]
    public void Installed_original_phase_entry_serializes_and_continues_its_first_native_callback_without_owner_fabrication(int encounter, int model, int predecessor, bool allocation, int physical)
    {
        string directory = InstalledGame();
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        using GameAssets assets = new(directory);
        DiagnosticBattleRequest request = Request() with
        {
            Encounter = encounter,
            AtbMode = BattleAtbMode.TurnBased,
            Target = Request().Target! with { Stage = world.BattleResources.LoadEncounter(encounter).StageId, Model = model, PhysicalIndex = physical },
            Driver = Request().Driver! with { EnemyAbility = Request().Driver!.EnemyAbility! with { Model = model } },
            Prelude = Request().Prelude! with { DefeatedModel = predecessor, RequireNativeAllocation = allocation, DeathScriptSha256 = Convert.ToHexStringLower(SHA256.HashData(world.BattleResources.ReadEnemyAi(predecessor, 0).Scripts[3])) }
        };
        BattleSession original = world.StartDiagnosticBattle(request);
        try
        {
            BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(original.Capture()))!;
            BattleSession clone = BattleSession.Restore(snapshot);
            clone.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            clone.ConfigureDiagnosticScheduling(true, request.InstantGuardianForceCharge, request.Prelude!.Input.ActorId);
            Assert.Equal(BattleAtbMode.TurnBased, original.Mode);
            original.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(0, original.DiagnosticRestoreCount);
            Assert.Equal(0, original.DiagnosticResetCount);
            Assert.Equal(predecessor, original.Combatants.Single(actor => actor.Id == "enemy-0").ModelId);
            Assert.DoesNotContain(original.Events, value => value.Kind == "NativeSpawn");
        }
        finally
        {
            world.AbandonFaultedDiagnosticBattle();
            world.EndDiagnosticBattle();
        }
    }

    [Fact]
    public void Installed_prelude_rejects_a_mismatched_original_death_callback_before_advancing_time()
    {
        string directory = InstalledGame();
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        DiagnosticBattleRequest request = Request() with { Target = Request().Target! with { Stage = world.BattleResources.LoadEncounter(120).StageId } };
        BattleSession active = world.StartDiagnosticBattle(request);
        int hp = active.Combatants.Single(actor => actor.Id == "enemy-0").CurrentHp;
        try
        {
            Assert.Throws<InvalidDataException>(() => world.PrepareDiagnosticEntryBeforePresentation(request));
            Assert.Equal(0, active.NativeHostCallbacks);
            Assert.Equal(hp, active.Combatants.Single(actor => actor.Id == "enemy-0").CurrentHp);
            Assert.Equal(0, active.DiagnosticRestoreCount);
            Assert.Equal(0, active.DiagnosticResetCount);
        }
        finally
        {
            world.AbandonFaultedDiagnosticBattle();
            world.EndDiagnosticBattle();
        }
    }

    private static DiagnosticBattleRequest Request()
    {
        return new(120, 0, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            AtbMode = BattleAtbMode.Normal,
            PrepareEntryBeforePresentation = true,
            Fixture = new([], [], true),
            Target = new("enemy-2", 0, 100, 2, 5, 0xC3, 1, null, null),
            Driver = new(new(BattleInputKind.Action, ActorId: "enemy-2", TargetId: "party-0", Ability: BattleAbility.EnemyAbility, ActionId: 13), 104) { EnemyAbility = new(100, 2, 0, 6, 2, 13, 1, 201) },
            Prelude = new(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-0", Ability: BattleAbility.Attack), 87, 0, new string('0', 64), false)
        };
    }

    private static string InstalledGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original formation120/511 phase initialization.");
        return directory!;
    }
}
