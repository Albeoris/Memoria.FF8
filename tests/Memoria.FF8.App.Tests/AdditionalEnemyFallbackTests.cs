using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AdditionalEnemyFallbackTests
{
    private const int Destination = 87;
    private const byte Seed = 4;
    private const int RequestedDonors = 3;

    [Fact]
    public void Quiet_field_uses_exact_destination_donors_without_spending_selection_rng()
    {
        const int quietField = 74;
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), null));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        FieldAssets field = Assert.IsType<FieldAssets>(world.Field);
        Assert.Equal(quietField, field.Id);
        Assert.False(field.RandomEncounters is { Rate: > 0, Formations.IsEmpty: false });
        SelectRoster(world, session);

        RandomEncounterSessionState? exploration = session.State.RandomEncounters;
        NativeGlobalRandomState? global = session.State.GlobalRandom;
        NativeCrtRandomState? crt = session.State.CrtRandom;
        BattleDefinition definition = world.CreateEncounterBattle(Destination, Seed);
        BattleEncounter original = world.BattleResources.LoadEncounter(Destination);
        Assert.True(definition.Combatants.Count(actor => actor.IsPlayer) > NativeBattleSlots.AllyCount || definition.AddedEnemySources is { Count: > 0 });
        Assert.True(definition.UsesOriginalEnemySlots);
        Assert.Equal(new[] { "party-0", "party-1", "party-2", "party-3" }, definition.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id));
        Assert.Equal(original.Enemies.Select(enemy => enemy.ModelId), definition.Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive && actor.SceneSlot < 8).Select(actor => actor.ModelId));
        Assert.NotNull(definition.AddedEnemySources);
        var sources = definition.AddedEnemySources;
        Assert.Equal(Enumerable.Range(ExpandedBattleFormation.OriginalEnemyPrototypeCount, RequestedDonors), sources.Keys);
        string hash = world.BattleResources.FormationSourceHash(Destination);
        var selected = new List<BattleEnemyPlacement>();
        foreach ((int slot, BattleEnemyRosterSource source) in sources)
        {
            Assert.Equal(Destination, source.Formation);
            Assert.Equal(hash, source.FormationHash);
            BattleEnemyPlacement placement = Assert.Single(original.Enemies, enemy => enemy.Slot == source.SceneSlot);
            Assert.Equal(placement.ModelId, Assert.Single(definition.Combatants, actor => !actor.IsPlayer && actor.SceneSlot == slot).ModelId);
            selected.Add(placement);
        }

        BattleDefinition repeated = world.CreateEncounterBattle(Destination, Seed);
        BattleDefinition rebuilt = world.CreateEncounterBattle(Destination, Seed, restoredDefinition: definition);
        Assert.Equal(sources.ToArray(), repeated.AddedEnemySources!.ToArray());
        Assert.Equal(sources.ToArray(), rebuilt.AddedEnemySources!.ToArray());
        Assert.Equal(exploration, session.State.RandomEncounters);
        Assert.Equal(global, session.State.GlobalRandom);
        Assert.Equal(crt, session.State.CrtRandom);
        Assert.Equal(global, definition.NativeGlobalRandom);
        Assert.Equal(crt, definition.NativeCrtRandom);
        AssertBattleEntryDraws(definition, original.Enemies.Concat(selected));

        int key = sources.Keys.First();
        BattleEnemyRosterSource admitted = sources[key];
        int other = assets.LoadField(133).RandomEncounters!.Formations.First(formation => formation != Destination);
        BattleEnemyPlacement otherPlacement = world.BattleResources.LoadEncounter(other).Enemies.First();
        AssertRejected(new(other, otherPlacement.Slot, world.BattleResources.FormationSourceHash(other)), "outside the suspended exploration encounter table");
        AssertRejected(admitted with { FormationHash = new string('0', 64) }, "installed source formation");
        int disabled = original.Slots.First(slot => !slot.Enabled).Slot;
        AssertRejected(admitted with { SceneSlot = disabled }, "eligible independent installed placement");

        void AssertRejected(BattleEnemyRosterSource source, string message)
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(() => world.CreateEncounterBattle(Destination, Seed,
                restoredDefinition: definition with { AddedEnemySources = sources.SetItem(key, source) }));
            Assert.Contains(message, failure.Message);
        }
    }

    [Fact]
    public void Eligible_area_donors_still_take_precedence_over_destination_placements()
    {
        const int caveField = 133;
        string directory = TestResources.GameDirectory;
        GameSession session = CreateFieldSession(directory, caveField);
        using GameWorld world = new(session, directory);
        world.EnsureField();
        FieldAssets field = Assert.IsType<FieldAssets>(world.Field);
        NativeFieldEncounterDefinition area = Assert.IsType<NativeFieldEncounterDefinition>(field.RandomEncounters);
        Assert.True(area.Rate > 0);
        SelectRoster(world, session);

        BattleDefinition definition = world.CreateEncounterBattle(Destination, Seed);
        Assert.NotNull(definition.AddedEnemySources);
        Assert.Equal(RequestedDonors, definition.AddedEnemySources.Count);
        Assert.All(definition.AddedEnemySources.Values, source =>
        {
            Assert.Contains(checked((ushort)source.Formation), area.Formations);
            Assert.Equal(world.BattleResources.FormationSourceHash(source.Formation), source.FormationHash);
        });
        Assert.Contains(definition.AddedEnemySources.Values, source => source.Formation != Destination);
    }

    private static GameSession CreateFieldSession(string directory, int fieldId)
    {
        GameState bootstrap = ExpandedBattleEffectMatrixTests.PrepareField(directory);
        GameSession session = new(bootstrap with { FieldId = fieldId, GlobalRandom = new(917) });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), null));
        return session;
    }

    private static void SelectRoster(GameWorld world, GameSession session)
    {
        ExpandedBattleEntryAdmissionTests.SelectFourAllies(world, session);
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { AdditionalEnemies = RequestedDonors });
    }

    private static void AssertBattleEntryDraws(BattleDefinition definition, IEnumerable<BattleEnemyPlacement> enemies)
    {
        // Entry itself rolls gauges and enemy levels. Donor selection must not add a draw to this stream.
        int draws = definition.Combatants.Count(actor => actor.IsPlayer && ((actor.NativeStatus0 | actor.NativeRetainedStatusBits) & 5) == 0 &&
            (actor.NativePlayerStatRules!.NativeStatusPassiveFlags!.Value & 0x10000) == 0);
        draws += enemies.Where(enemy => enemy.Enabled).Sum(enemy => 1 + BattleEnemyNativeStats.LevelRandomByteCount(checked((byte)enemy.Level)));
        NativeBattleRandomState expected = NativeBattleRandomState.Initialize(Seed);
        for (int draw = 0; draw < draws; draw++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleRandomState actual = Assert.IsType<NativeBattleRandomState>(definition.InitialRandom);
        Assert.Equal(expected.Lane, actual.Lane);
        Assert.Equal(expected.Indexes.ToArray(), actual.Indexes.ToArray());
    }
}
