using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedEnemyAiAdmissionTests
{
    private const ushort EntranceFormation = 29;
    private const ushort OrdinaryFormation = 87;

    [Fact]
    public void UnsupportedInstalledEntranceRejectsCreationWithoutChangingTheSelectedPartyOrWorld()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = PrepareSession(directory);
        using GameWorld world = new(session, directory);
        PreparePendingEncounter(world, session, EntranceFormation);
        BattleEnemyPlacement entrance = Assert.Single(world.BattleResources.LoadEncounter(EntranceFormation).Slots, slot => slot.ModelId == 74);
        NativeBattleAiDefinition program = world.BattleResources.ReadEnemyAi(entrance.ModelId, 1);
        for (int band = 0; band < 3; band++)
        {
            List<string> blockers = [];
            Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(program with { LevelBand = band }, world.BattleResources.NativeActions, EntranceFormation, blockers, originalEnemySlots: true));
            Assert.Contains("opcode:27", blockers);
        }

        string state = JsonSerializer.Serialize(session.PrepareSnapshot());
        NotSupportedException rejected = Assert.Throws<NotSupportedException>(() => world.CreateEncounterBattle(EntranceFormation, nativeSeed: 4));
        Assert.Contains($"formation {EntranceFormation}, scene slot {entrance.Slot}, model {entrance.ModelId}, level band ", rejected.Message);
        Assert.Contains("opcode:27", rejected.Message);
        Assert.Equal(state, JsonSerializer.Serialize(session.PrepareSnapshot()));
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, session.State.CharacterVitals!.GetBattlePartyRows(4));
        Assert.Null(world.Battle);
    }

    [Fact]
    public void OrdinaryInstalledEnemiesKeepTheirAiAndTheSelectedFourthAlly()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = PrepareSession(directory);
        using GameWorld world = new(session, directory);
        PreparePendingEncounter(world, session, OrdinaryFormation);

        BattleDefinition definition = world.CreateEncounterBattle(OrdinaryFormation, nativeSeed: 4);
        Assert.True(definition.Combatants.Count(actor => actor.IsPlayer) > NativeBattleSlots.AllyCount || definition.AddedEnemySources is { Count: > 0 });
        Assert.Equal(new int?[] { 0, 1, 2, 3 }, definition.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.MagicRecordId));
        Assert.All(definition.Combatants.Where(actor => !actor.IsPlayer), actor =>
        {
            NativeBattleAiDefinition actual = Assert.IsType<NativeBattleAiDefinition>(actor.NativeAi);
            bool original = definition.UsesOriginalEnemySlots && definition.AddedEnemySources?.ContainsKey(actor.SceneSlot!.Value) != true;
            NativeBattleAiDefinition installed = world.BattleResources.ReadEnemyAi(actor.ModelId, actor.Level, includeDeathLifecycle: original);
            Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(actual));
            Assert.Equal(0, actor.PhysicalPower);
            Assert.Equal(0, actor.DrainPercent);
        });
    }

    [Fact]
    public void ReconstructingAnInstalledPrimitiveSnapshotRequiresExplicitMigration()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = PrepareSession(directory);
        using GameWorld world = new(session, directory);
        PreparePendingEncounter(world, session, OrdinaryFormation);
        BattleDefinition frozen = world.CreateEncounterBattle(OrdinaryFormation, nativeSeed: 4);
        Assert.True(frozen.Combatants.Count(actor => actor.IsPlayer) > NativeBattleSlots.AllyCount || frozen.AddedEnemySources is { Count: > 0 });
        BattleCombatantDefinition enemy = frozen.Combatants.First(actor => !actor.IsPlayer);
        BattleDefinition legacy = frozen with { Combatants = frozen.Combatants.Select(actor => actor == enemy ? actor with { NativeAi = null } : actor).ToArray() };

        NotSupportedException rejected = Assert.Throws<NotSupportedException>(() => world.CreateEncounterBattle(OrdinaryFormation, nativeSeed: 4, restoredDefinition: legacy));
        Assert.Contains($"formation {OrdinaryFormation}, scene slot {enemy.SceneSlot}, model {enemy.ModelId}", rejected.Message);
        Assert.Contains("retired primitive enemy execution contract", rejected.Message);
        Assert.Contains("Migrate this battle snapshot explicitly", rejected.Message);
    }

    private static GameSession PrepareSession(string directory)
    {
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        return session;
    }

    private static void PreparePendingEncounter(GameWorld world, GameSession session, ushort formation)
    {
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 4));
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 2] }).WithGuestBattleRecords().Rest();
        vitals = vitals with { Records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray() };
        session.SetCharacterVitals(vitals.WithBattleParty([0, 1, 2, 3], 4));
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 4 });
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        // Inject only the pending component request. No encounter runs or field travel occurs.
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, formation, source)));
    }
}
