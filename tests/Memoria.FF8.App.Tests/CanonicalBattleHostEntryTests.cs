using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CanonicalBattleHostEntryTests
{
    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 2)]
    public void Production_entry_uses_one_host_for_small_and_extended_rosters(int allies, int donors)
    {
        const ushort formation = 87;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = PrepareSession(directory, game);
        using GameWorld world = new(session, directory, sharedAssets: game);
        ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, allies), formation, allies, donors);

        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleDefinition definition = battle.ReadDefinition();
        Assert.True(definition.UsesOriginalEnemySlots);
        Assert.Equal(Enumerable.Range(0, allies).Select(row => $"party-{row}"), battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id));
        Assert.Equal(Enumerable.Range(NativeBattleSlots.FormationSlotCount, donors), definition.AddedEnemySources!.Keys);
        BattleEnemyPlacement[] authored = GameWorld.AuthoredEnemyPlacements(world.BattleResources.LoadEncounter(formation)).ToArray();
        Assert.Equal(authored.Select(placement => (placement.Slot, placement.ModelId, placement.Enabled)), definition.Combatants.Where(actor => !actor.IsPlayer && !definition.AddedEnemySources.ContainsKey(actor.SceneSlot!.Value)).Select(actor => (actor.SceneSlot!.Value, actor.ModelId, actor.InitialActive)));
        Assert.Equal(allies, process.Graphics.ActorModels.Roster.Party.Count());
        Assert.Equal(authored.Count(placement => placement.Enabled) + donors, process.Graphics.ActorModels.Roster.Enemies.Count());
        Assert.Equal(3, battle.Combatants.Count(actor => actor.IsPlayer && actor.NativeActorIndex.HasValue));
    }

    [Theory]
    [InlineData(754)]
    [InlineData(756)]
    public void Installed_dormant_only_formation_publishes_the_original_entry_and_victory_result(ushort formation)
    {
        string directory = TestResources.GameDirectory;
        var current = Run();
        if (formation == 754)
        {
            var legacy = Run(missingCameraActor: true);
            Assert.Equal(current.Result, legacy.Result);
            Assert.Equal(current.Random.Indexes.ToArray(), legacy.Random.Indexes.ToArray());
        }

        (NativeBattleRandomState Random, byte? Result) Run(bool missingCameraActor = false)
        {
            using GameAssets game = new(directory);
            GameSession session = PrepareSession(directory, game, missingCameraActor);
            using GameWorld world = new(session, directory, sharedAssets: game);
            ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, 3), formation, allies: 3, additionalEnemies: 0);
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: ExpandedBattleInstalledEntry.Seed));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            Assert.NotNull(battle.Presentation);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
            Assert.False(battle.NativeEntryReady);
            byte? expectedCameraActor = missingCameraActor ? null : (byte)0;
            Assert.Equal(expectedCameraActor, battle.ReadDefinition().NativeRetainedMemory!.VictoryCameraActorSlot);
            Assert.Equal(NativeBattleSlots.FormationSlotCount, battle.Combatants.Count(actor => !actor.IsPlayer));
            Assert.All(battle.Combatants.Where(actor => !actor.IsPlayer), actor =>
            {
                Assert.False(actor.IsActive);
                Assert.Null(actor.Slot);
            });
            Assert.Empty(battle.ReadBattlePresentationRoster().Enemies);
            NativeBattleRandomState entryRandom = battle.Capture().Random!;

            byte? graphicsResult = null;
            for (int callback = 0; callback < 1200 && !battle.CanReturnToField; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                NativeBattleGraphicsState graphics = (battle.Presentation!.Graphics)!;
                if (graphics.NativeOutcome?.ResultByte6E7 is > 0 and var published)
                {
                    graphicsResult = published;
                }
            }
            Assert.True(battle.CanReturnToField);
            Assert.Equal(BattleOutcome.Victory, battle.Outcome);
            byte? result = battle.NativeResult;
            Assert.Equal<byte?>(4, result);
            Assert.Equal(result, graphicsResult);
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
            Assert.Null(session.State.PendingBattleEncounter);
            Assert.Null(session.State.RandomEncounters?.Pending);
            Assert.Equal(SceneKind.Field, session.State.Scene);
            Assert.Equal(expectedCameraActor, session.State.NativeBattleMemory!.VictoryCameraActorSlot);
            return (entryRandom, result);
        }
    }

    private static GameSession PrepareSession(string directory, GameAssets assets, bool missingCameraActor = false)
    {
        NativeBattleRetainedMemory initial = assets.ReadColdProcessNativeBattleMemory();
        if (missingCameraActor)
        {
            initial = initial with { VictoryCameraActorSlot = null };
        }
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, initial));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        return session;
    }

    private static void PrepareParty(GameSession session, int allies)
    {
        NativeCharacterVitals vitals = session.CharacterVitals!.WithGuestBattleRecords().Rest();
        byte[] rows = Enumerable.Range(0, allies).Select(row => checked((byte)row)).ToArray();
        foreach (byte row in rows)
        {
            vitals.Records[row] = vitals.Records[row] with { MemberFlags = (ushort)(vitals.Records[row].MemberFlags | 1), Status = 0 };
        }
        session.SetCharacterVitals(vitals.WithBattleParty(rows, allies));
    }
}
