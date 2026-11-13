using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedEnemyLevelTests
{
    [Theory]
    [InlineData(33, 2)]
    [InlineData(34, 8)]
    public void InstalledLevelCommandsRestoreOlderAndChangedExpandedBattles(byte command, int destinationLevel)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.KernelSection == 10 && action.Command == command);
        GameSession? sourceSession = null;
        using GameWorld source = ExpandedBattleEffectMatrixTests.StartBattle(directory, ExpandedBattleEffectMatrixTests.PrepareField(directory), 89, equipped: action, captureSession: session => sourceSession = session);
        BattleSession original = Assert.IsType<BattleSession>(source.Battle);
        BattleCombatant caster = Assert.Single(original.Combatants, actor => actor.ModelId == 10 && actor.IsPlayer);
        for (int turn = 0; turn < 24 && (!caster.IsReady || original.ActiveAction is not null); turn++)
        {
            source.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            source.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);
        Assert.Null(original.ActiveAction);

        GameState prepared = sourceSession!.PrepareSnapshot();
        BattleSnapshot snapshot = prepared.Battle!;
        Assert.Single(snapshot.Definition.ExpandedEnemyLevels!);
        GameState legacy = prepared with { Battle = snapshot with { Definition = snapshot.Definition with { ExpandedEnemyLevels = null } } };
        Assert.All(legacy.Battle.Combatants, actor => Assert.Null(actor.NativeParticipant!.ResolvedLevel));
        GameSession resumedSession = new(RoundTrip(legacy));
        using GameWorld resumed = new(resumedSession, directory);
        resumed.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(resumed.Battle);
        Assert.Single(battle.ReadDefinition().ExpandedEnemyLevels!);
        BattleCombatant target = battle.Combatants.Last(actor => !actor.IsPlayer);
        Assert.Equal(4, target.Level);
        int oldMaximum = target.MaximumHp;
        int oldHp = target.CurrentHp;
        int actionId = (command << 16) | action.ActionId;
        resumed.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id, Ability: BattleAbility.NativeCommand, ActionId: actionId));
        for (int frame = 0; frame < 100 && battle.Combatants.Single(actor => actor.Id == caster.Id).IsBusy; frame++)
        {
            resumed.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.Equal(destinationLevel, target.Level);
        int expectedMaximum = assets.ReadEnemyNativeStats(target.ModelId).Project(destinationLevel).MaximumHp;
        Assert.Equal(expectedMaximum, target.MaximumHp);
        Assert.Equal(command == 33 ? Math.Min(oldHp, expectedMaximum) : expectedMaximum - (oldMaximum - oldHp), target.CurrentHp);

        GameSession changedSession = new(RoundTrip(resumedSession.PrepareSnapshot()));
        using GameWorld changed = new(changedSession, directory);
        changed.EnsureField();
        BattleSession restored = Assert.IsType<BattleSession>(changed.Battle);
        Assert.Equal(destinationLevel, restored.Combatants.Single(actor => actor.Id == target.Id).Level);
        resumed.ApplyBattleInput(new(BattleInputKind.Tick));
        changed.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    private static GameState RoundTrip(GameState state)
    {
        return JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!;
    }
}
