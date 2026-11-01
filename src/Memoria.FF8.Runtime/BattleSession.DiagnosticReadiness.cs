namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    /// <summary>Prepares only the selected diagnostic player's native gauge, retaining normal full-gauge registration.</summary>
    public void PrepareDiagnosticPlayerReadiness(string actorId)
    {
        RequireNativeEntryReady();
        BattleCombatant actor = GetActor(actorId);
        NativeBattleAtbState gauge = actor.NativeAtb ?? throw new NotSupportedException("Diagnostic readiness requires the actual native ATB owner.");
        if (Outcome != BattleOutcome.Active || !actor.IsPlayer || actor.NativeIndex is < 0 or >= 3 || actor.IsBusy || !CanNativeBattleActorAct(actor) || NativeGraphics?.UiServices is null || ObserveDiagnosticDrainBlockers().Count != 0)
        {
            throw new InvalidOperationException("Diagnostic readiness requires an idle, fully entered battle and an actionable physical player.");
        }
        gauge.Validate();
        NativeBattleAtbState full = gauge with { Current = gauge.Maximum };
        NativeBattleGaugeRegistration registration = NativeBattleGaugeRegistrations.Evaluate4842B0(full, actor.NativeCombatantFlags, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor));
        if (registration.Kind is NativeBattleGaugeRegistrationKind.ForcedCommand or NativeBattleGaugeRegistrationKind.DeferredScript)
        {
            throw new NotSupportedException("Diagnostic readiness cannot replace compelled or script-owned player commands.");
        }
        actor.NativeAtb = full;
        PublishNativeFullGaugeRegistration(actor);
    }
}
