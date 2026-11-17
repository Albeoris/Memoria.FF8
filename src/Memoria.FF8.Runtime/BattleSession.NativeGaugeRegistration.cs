namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void PublishNativeFullGaugeRegistration(BattleCombatant actor)
    {
        if (actor.IsPlayer || actor.Slot is null || actor.NativeAtb is not { } gauge || diagnosticHoldEnemyCommands)
        {
            return;
        }
        NativeBattleGaugeRegistration registration = NativeBattleGaugeRegistrations.Evaluate4842B0(gauge, actor.NativeCombatantFlags, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor));
        if (registration.Kind == NativeBattleGaugeRegistrationKind.DeferredScript)
        {
            if (actor.NativeAi is null)
            {
                throw new NotSupportedException($"Enemy '{actor.Id}' reached a script turn without an installed AI program.");
            }
            actor.NativeCombatantFlags = registration.ActorFlags;
        }
    }

    private void PublishNativeRegisteredCallbacks4847B0()
    {
        if (diagnosticHoldEnemyCommands || ExpandedTimedLimitOwnsInput)
        {
            return;
        }
        foreach (BattleCombatant actor in enemies.Where(candidate => candidate.Slot is not null && (candidate.NativeCombatantFlags & 0x200) != 0).OrderBy(candidate => candidate.Slot!.Value.Position))
        {
            ObserveNativeUnknownStackCallback(0x484720, "registered actor callback");
            QueueExpandedAiCallback(actor, 1);
            actor.NativeCombatantFlags &= ~0x200u;
        }
    }
}
