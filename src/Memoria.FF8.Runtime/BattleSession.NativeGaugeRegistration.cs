namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void PublishNativeFullGaugeRegistration(BattleCombatant actor)
    {
        if (NativeGraphics?.UiServices is not { } ui || actor.NativeActorIndex is null || actor.NativeAtb is not { } gauge)
        {
            return;
        }
        NativeBattleGaugeRegistration registration = NativeBattleGaugeRegistrations.Evaluate4842B0(gauge, actor.NativeCombatantFlags, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor));
        if (diagnosticHoldEnemyCommands && !actor.IsPlayer && registration.Kind == NativeBattleGaugeRegistrationKind.DeferredScript)
        {
            return;
        }
        switch (registration.Kind)
        {
            case NativeBattleGaugeRegistrationKind.None:
                return;
            case NativeBattleGaugeRegistrationKind.Menu:
                NativeGraphics = NativeGraphics with { UiServices = ui with { Commands = ui.Commands.Enqueue(checked((byte)actor.RequireNativeActorIndex()), 0x11) } };
                break;
            case NativeBattleGaugeRegistrationKind.ForcedCommand:
                ObserveNativeUnknownStackCallback(0x483EB0, "forced full-gauge command");
                //483EB0's non-script branch invokes485030 before486E00 and484D20.
                NativeGraphics = NativeGraphics with { UiServices = ui with { Commands = ui.Commands.Enqueue(checked((byte)actor.RequireNativeActorIndex()), 0x12) } };
                BattleTargetSelection selection = ChooseOriginalRandomActionTarget(BattleSide.Enemies);
                if (selection is not NativeBattleTargetRequestAdapter.DeferredOperands original ||
                    ReadOriginalSelectedActor(original, activeOnly: false) is not { } target)
                {
                    throw new NotSupportedException("Native486E00 requires its actual allocated target command binding.");
                }
                BattleTargetRequest forced = new(selection, BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.UseCurrentBattlePolicy);
                EnqueueNativeInputCommand(new(actor.Id, target.Id, forced) { NativeQueuedCommand = 1 });
                break;
            case NativeBattleGaugeRegistrationKind.DeferredScript:
                break;
            default:
                throw new InvalidDataException("Native4842B0 registration selected an invalid branch.");
        }
        actor.NativeCombatantFlags = registration.ActorFlags;
    }

    private void PublishNativeRegisteredCallbacks4847B0()
    {
        foreach (BattleCombatant actor in combatants.Where(candidate => candidate.NativeActorIndex is >= 3 and < 7 && (candidate.NativeCombatantFlags & 0x200) != 0).OrderBy(candidate => candidate.NativeActorIndex))
        {
            if (diagnosticHoldEnemyCommands)
            {
                continue;
            }
            ObserveNativeUnknownStackCallback(0x484720, "registered actor callback");
            //484720(actor,1,2): a nonzero third argument selects the ordinary physical bank2.
            QueueNativeCallback(actor, 1, 2);
            actor.NativeCombatantFlags &= ~0x200u;
        }
    }
}
