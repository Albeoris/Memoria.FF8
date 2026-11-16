using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private void ObserveDiagnosticAllocatedFocus(BattleDiagnosticEffectDispatch dispatch)
    {
        if (diagnosticDriver is not { Stage: DiagnosticDriverStage.AwaitDispatch } driver || diagnosticDriverRecipe is not { EnemyTrigger: { AllocatedFocus: { } focus } trigger } recipe ||
            dispatch.RequestedEffect != recipe.ExpectedEffect && dispatch.ExecutedEffect != recipe.ExpectedEffect)
        {
            return;
        }
        BattleSession battle = diagnosticDriverBattle!;
        if (dispatch.ActorId is not { } actor || dispatch.NativeActorIndex != focus.PhysicalIndex ||
            dispatch.NativeAiCallbackStage != 0 || !focus.ScriptSha256.Equals(dispatch.NativeAiScriptSha256, StringComparison.OrdinalIgnoreCase))
        {
            driver.Fail("ALLOCATED_FOCUS_NATIVE_IDENTITY_MISMATCH");
            return;
        }
        BattleEvent[] allocations = battle.Events.Where(value => value.Kind == "NativeSpawn" && value.ActorId == trigger.ActorId && value.TargetId == actor && value.Amount == focus.SceneSlot).ToArray();
        DiagnosticBattleRosterMember[] members = GameWorld.ObserveDiagnosticRosterMembers(battle, includeMountedReserve: false);
        BattleDiagnosticTarget owner = battle.ObserveDiagnosticTarget(actor);
        if (allocations.Length != 1 || owner.Model != focus.Model || owner.SceneSlot != focus.SceneSlot || !owner.Alive || !owner.StatisticsReady ||
            members.Any(member => member.Actor == trigger.ActorId) || !members.Any(member => member.Actor == actor))
        {
            driver.Fail("ALLOCATED_FOCUS_ORIGINAL_SPAWN_MISMATCH");
            return;
        }
        diagnosticAllocatedFocus = new(dispatch.Serial, dispatch.Callback, allocations[0], owner, members);
        driver.BindAllocatedFocalActor(actor);
    }
}
