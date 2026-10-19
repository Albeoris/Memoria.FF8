using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Authentic kernel29 byte14, consumed by4842B0's native gauge formula.</summary>
public sealed record NativeBattleClockRules([property: JsonRequired] byte AtbSpeedMultiplier)
{
    public static NativeBattleClockState Admit(NativeBattleClockState state, bool uiAllowsAtb, bool scriptWaitHeld, bool allocationPaused)
    {
        state.Validate();
        bool admitted = state.Active != 0 && uiAllowsAtb && !scriptWaitHeld && !allocationPaused && state.ActionPause == 0;
        return state with { AtbAdmitted = admitted ? (byte)1 : (byte)0 };
    }
}

/// <summary>BYTE1D280C2, BYTE1D28DEB and DWORD1D27B00; none is an elapsed-time counter.</summary>
public sealed record NativeBattleClockState([property: JsonRequired] byte Active, [property: JsonRequired] byte AtbAdmitted, [property: JsonRequired] uint ActionPause)
{
    public void Validate()
    {
        if (Active > 1 || AtbAdmitted > 1 || ActionPause > 1)
        {
            throw new InvalidDataException("Native battle clock gates exceed their original producer values.");
        }
    }
}

public sealed partial class BattleSession
{
    private NativeBattleClockState? nativeBattleClock;

    private void InitializeNativeBattleClock()
    {
        nativeBattleClock = definition.NativeClockRules is null ? null : new(0, 0, 0);
    }

    private void RestoreNativeBattleClock(NativeBattleClockState? saved)
    {
        if ((saved is null) != (definition.NativeClockRules is null))
        {
            throw new InvalidDataException("Native battle clock gates differ from their explicitly owned kernel contract.");
        }
        saved?.Validate();
        nativeBattleClock = saved;
    }

    private void SetNativeBattleClockActive(bool active)
    {
        NativeBattleClockState clock = nativeBattleClock ?? throw new NotSupportedException("Native C2 clock producer requires its original battle clock owner.");
        nativeBattleClock = clock with { Active = active ? (byte)1 : (byte)0 };
    }

    private void SetNativeBattleActionPause(bool paused)
    {
        NativeBattleClockState clock = nativeBattleClock ?? throw new NotSupportedException("Native47E080/47DDA0/47DE70 requires its battle action-pause DWORD owner.");
        nativeBattleClock = clock with { ActionPause = paused ? 1u : 0u };
    }

    private bool AdmitNativeBattleClock(bool uiAllowsAtb, bool scriptWaitHeld)
    {
        NativeBattleClockState clock = nativeBattleClock ?? throw new NotSupportedException("Native4842B0 admission requires its original C2/DEB/action-pause owners.");
        nativeBattleClock = NativeBattleClockRules.Admit(clock, uiAllowsAtb, scriptWaitHeld, NativeScriptAllocationPaused);
        return nativeBattleClock.AtbAdmitted != 0;
    }

    private void DispatchNativePeriodicSupport(bool componentClockAdmitted, bool explicitTurnAdvance = false)
    {
        //47CCB0 checks the last4842B0 DEB admission again after action/request dispatch, before482F80.
        if ((nativeBattleClock is { AtbAdmitted: 1 } clock && (clock.ActionPause == 0 || explicitTurnAdvance) || ComponentSupportClockAdmitted(componentClockAdmitted)) && !NativeScriptAllocationPaused && Outcome == BattleOutcome.Active)
        {
            AdvanceNativeSupportCallbacks();
        }
    }
}
