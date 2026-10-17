namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void PublishNativeFullGaugeRegistration(BattleCombatant actor)
    {
        if (NativeGraphics?.UiServices is not { } ui || actor.NativeIndex is < 0 or >= 7 || actor.NativeAtb is not { } gauge)
        {
            return;
        }
        NativeBattleGaugeRegistration registration = NativeBattleGaugeRegistrations.Evaluate4842B0(gauge, actor.NativeCombatantFlags, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor));
        switch (registration.Kind)
        {
            case NativeBattleGaugeRegistrationKind.None:
                return;
            case NativeBattleGaugeRegistrationKind.Menu:
                NativeGraphics = NativeGraphics with { UiServices = ui with { Commands = ui.Commands.Enqueue(checked((byte)actor.NativeIndex), 0x11) } };
                break;
            case NativeBattleGaugeRegistrationKind.ForcedCommand:
                //483EB0 invokes485030 before486E00 and484D20. The current485030 owner requires a physical player slot.
                NativeGraphics = NativeGraphics with { UiServices = ui with { Commands = ui.Commands.Enqueue(checked((byte)actor.NativeIndex), 0x12) } };
                BattleCombatant target = ChooseNativeRandomActionTarget(false) ?? combatants.SingleOrDefault(candidate => candidate.NativeIndex == 3) ?? throw new NotSupportedException("Native486E00's no-living-enemy mask8 requires its actual physical slot3 command binding.");
                EnqueueNativeInputCommand(new(actor.Id, target.Id) { NativeQueuedCommand = 1, NativeTargetMask = checked((ushort)(1 << target.NativeIndex)) });
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
        foreach (BattleCombatant actor in combatants.Where(candidate => candidate.NativeIndex is >= 3 and < 7 && (candidate.NativeCombatantFlags & 0x200) != 0).OrderBy(candidate => candidate.NativeIndex))
        {
            //484720(actor,1,2): any nonzero third argument selects physical bank1, not a packet flags BYTE.
            QueueNativeCallback(actor, 1, 1);
            actor.NativeCombatantFlags &= ~0x200u;
        }
    }
}
