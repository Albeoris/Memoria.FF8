using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private void ObserveDiagnosticMountedReserveDispatch(BattleDiagnosticEffectDispatch dispatch)
    {
        if (diagnosticDriver?.Stage != DiagnosticDriverStage.AwaitDispatch || diagnosticDriverRecipe is not { } recipe ||
            world?.DiagnosticReceipt?.Preparation.Roster?.MountedReserveSceneSlot is not int slot ||
            dispatch.ActorId != (recipe.EnemyTrigger?.ActorId ?? recipe.Input.ActorId) || dispatch.RequestedEffect != recipe.ExpectedEffect ||
            dispatch.ExecutedEffect != recipe.ExpectedEffect || dispatch.ActionId != (recipe.ExpectedNativeAction ?? recipe.Input.ActionId))
        {
            return;
        }
        BattleSession battle = diagnosticDriverBattle!;
        diagnosticMountedReserve = new(dispatch.Serial, dispatch.Callback, GameWorld.ObserveDiagnosticRosterMembers(battle, includeMountedReserve: false), battle.ObserveDiagnosticTarget($"enemy-{slot}"));
    }

    private void ObserveDiagnosticMountedReserveActivation(BattleEvent value)
    {
        if (diagnosticMountedReserve is not { ActivationCallback: null } receipt || value.Kind != "Activated" || value.ActorId != receipt.ReserveAtDispatch.ActorId)
        {
            return;
        }
        BattleSession battle = diagnosticDriverBattle!;
        diagnosticMountedReserve = receipt with
        {
            ActivationCallback = battle.NativeHostCallbacks ?? throw new InvalidOperationException("The mounted reserve activation lost its original battle clock."),
            MembersAfterActivation = GameWorld.ObserveDiagnosticRosterMembers(battle, includeMountedReserve: false)
        };
    }
}
