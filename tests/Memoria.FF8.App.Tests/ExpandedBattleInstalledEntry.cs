using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class ExpandedBattleInstalledEntry
{
    public const ushort Formation = 87;
    public const byte Seed = 4;

    public static void Prepare(GameWorld world, GameSession session, Action<GameSession> prepareParty, ushort formation = Formation, int allies = 4, int additionalEnemies = 4)
    {
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 4)).Accepted);
        prepareParty(session);
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = allies, AdditionalEnemies = additionalEnemies });
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        NativeRandomEncounterState random = session.State.RandomEncounters?.Native ?? NativeRandomEncounterState.FromColdProcess();
        session.SetRandomEncounters(new(true, random, new(SceneKind.Field, 133, world.Events.Frame, formation, source)));
    }

    public static BattleSession Start(GameWorld world)
    {
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        WaitForEntry(world, battle);
        return battle;
    }

    public static void WaitForEntry(GameWorld world, BattleSession battle)
    {
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        for (int callback = 0; callback < 1200 && !process.EntryReady; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(process.EntryReady);
    }

    public static void AdmitDebugVictory(GameWorld world)
    {
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        for (int callback = 0; callback < 1200 && !battle.CanAdmitDebugOutcome; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(battle.CanAdmitDebugOutcome);
        world.ApplyBattleInput(new(BattleInputKind.InstantVictory));
    }

    public static void CompleteDebugVictory(GameWorld world)
    {
        AdmitDebugVictory(world);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        for (int callback = 0; callback < 1200 && !battle.CanReturnToField; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
    }
}
