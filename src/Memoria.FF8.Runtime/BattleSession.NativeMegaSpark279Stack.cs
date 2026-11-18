using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool ObservesMegaSpark279ClockStack => Presentation?.ObservesNativeMegaSpark279ClockStack == true;

    private void ObserveMegaSpark279AtbClockStack()
    {
        bool preserved = ObservesMegaSpark279ClockStack && NativeBattleMegaSpark279ClockStack.AtbContextHasNoHelper(guardianForceCasts.Count, nativeEscape.Held, nativeEscape.PresentationHeld);
        NativeBattleMegaSpark279ClockPath path = preserved ? NativeBattleMegaSpark279ClockPath.AtbWithoutHelper : NativeBattleMegaSpark279ClockPath.Unknown;
        if (preserved)
        {
            foreach (BattleCombatant actor in combatants)
            {
                if (actor.NativeAtb is not { } gauge || !NativeBattleAtbState.CanAdvance(actor.NativeCombatantFlags, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor)))
                {
                    continue;
                }
                NativeBattleMegaSpark279ClockPath participant = NativeBattleMegaSpark279ClockStack.AtbParticipantPath(gauge, actor.NativeAtbSpeed, definition.NativeClockRules!.AtbSpeedMultiplier, actor.NativeCombatantFlags, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor));
                path = NativeBattleMegaSpark279ClockStack.CombineAtbPaths(path, participant);
                if (path == NativeBattleMegaSpark279ClockPath.Unknown)
                {
                    break;
                }
            }
        }
        ObserveMegaSpark279ClockStack(0x4842B0, "admitted ATB update", path);
    }

    private void ObserveMegaSpark279StatusClockStack()
    {
        bool preserved = ObservesMegaSpark279ClockStack;
        if (preserved)
        {
            foreach (BattleCombatant actor in combatants)
            {
                if (actor.IsActive && !NativeBattleMegaSpark279ClockStack.StatusHasNoTimer(NativeSavedStatusBits(actor), actor.NativeStatusTimers))
                {
                    preserved = false;
                    break;
                }
            }
        }
        ObserveMegaSpark279ClockStack(0x483470, "admitted status clock", preserved ? NativeBattleMegaSpark279ClockPath.StatusWithoutTimer : NativeBattleMegaSpark279ClockPath.Unknown);
    }

    private void ObserveMegaSpark279SupportClockStack()
    {
        bool preserved = ObservesMegaSpark279ClockStack && NativeBattleMegaSpark279ClockStack.SupportHasCooldown(nativeSupport);
        ObserveMegaSpark279ClockStack(0x482F80, "admitted support clock", preserved ? NativeBattleMegaSpark279ClockPath.SupportCooldown : NativeBattleMegaSpark279ClockPath.Unknown);
    }

    private void ObserveMegaSpark279ClockStack(uint callback, string phase, NativeBattleMegaSpark279ClockPath path)
    {
        if (path == NativeBattleMegaSpark279ClockPath.Unknown)
        {
            ObserveNativeUnknownStackCallback(callback, phase);
            return;
        }
        Presentation?.ObserveLogicalMegaSpark279ClockCall(callback, phase, path);
    }
}


public static class NativeBattleMegaSpark279Stack
{
    public static NativeBattleStackBytes ObserveVitals(NativeBattleStackBytes stack, NativeBattlePartyPresentation presentation, IReadOnlyList<bool> guardianActive, NativeBattleExecutionStack? execution = null)
    {
        stack.Validate();
        presentation.Validate();
        if (stack.FirstOffset != -0xAC || stack.Bytes.Length != 8 || guardianActive.Count != 3)
        {
            throw new InvalidDataException("Mega Spark UI stack observation requires its actual eight-byte common-frame interval and three party flags.");
        }
        uint edi = 0;
        byte ediKnown = 12;
        for (int index = 0; index < 3; index++)
        {
            if (presentation.Rows[index] is not { } row)
            {
                if (guardianActive[index])
                {
                    throw new InvalidDataException("Mega Spark UI observation selected a GF panel without its current party owner.");
                }
                continue;
            }
            string phase = $"Vitals party row {index}";
            stack = stack.StoreRegister32(-0xA8, edi, ediKnown, 0x4B0833, phase);
            stack = stack.StoreUInt32(-0xAC, 0x60, 0x4B0853, phase);
            uint ebp = checked(0x1D76971u + (uint)(index * 0x6C));
            Track(row.DisplayedHitPointsQ12, row.HitPoints, ebp - 0x23, phase);
            if (guardianActive[index])
            {
                if (row.DisplayedGuardianForceHitPointsQ12 is not int displayed || row.GuardianForceHitPoints is not ushort hitPoints)
                {
                    stack = stack.Invalidate(-0xAC, 8, 0x4B0500, "Missing active GF UI cache");
                    ediKnown = 0;
                    continue;
                }
                Track(displayed, hitPoints, ebp - 0xF, phase);
            }
            edi = guardianActive[index] ? ebp + 5 : ebp - 0xF;
            ediKnown = 15;
        }
        return stack;

        void Track(int displayed, ushort hitPoints, uint record, string phase)
        {
            int target = unchecked((short)hitPoints) << 12;
            if (displayed < target)
            {
                stack = stack.StoreRegister32(-0xA8, record, 15, 0x4B884A, phase);
            }
            else if (displayed > target)
            {
                stack = stack.StoreUInt32(-0xA8, 3, 0x4B07E8, phase);
                if (execution is not null)
                {
                    stack = stack.StoreRegister32(-0xAC, execution.ReadCommonFrame().AddressAtOffset(-0x8F), 15, 0x4B07EA, phase);
                }
                else
                {
                    stack = stack.StoreRegister32(-0xAC, 0, 0, 0x4B07EA, phase);
                }
            }
        }
    }
}
