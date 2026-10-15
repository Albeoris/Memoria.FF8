using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleSnapshotSceneTests
{
    [Theory]
    [InlineData(SceneKind.Field)]
    [InlineData(SceneKind.WorldMap)]
    public void RuntimeSnapshotAcceptsCombatScenesIndependentlyOfFutureBattlePolicy(SceneKind scene)
    {
        BattleSession battle = new(new BattleDefinition
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        battle.InstantVictory();
        GameSession session = new(new GameState { Scene = scene, WorldArrival = scene == SceneKind.WorldMap ? new WorldTransition(0, 0, 0) : null, FullBattlePlaythrough = null, Battle = battle.Capture() });
        Assert.Equal(BattleOutcome.Victory, session.State.Battle!.Outcome);
        Assert.Throws<InvalidDataException>(() => new GameSession(session.State with { Scene = SceneKind.Movie }));
    }
}
