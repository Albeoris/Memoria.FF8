namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void AdvanceNativeSupportCallbacks()
    {
        if (nativeSupport is null)
        {
            return;
        }
        if (!nativeSupport.EntryInitialized)
        {
            throw new InvalidOperationException("Native482F80 was admitted before47CCB0 case3 initialized its support owners.");
        }
        ObserveMegaSpark279SupportClockStack();
        if (nativeSupport.Cooldown != 0)
        {
            nativeSupport = nativeSupport with { Cooldown = (ushort)(nativeSupport.Cooldown - 1) };
            return;
        }
        byte flags = NativeSupportFlags((byte)8);
        if ((flags & 8) != 0 && nativeSupport.GilgameshUsed == 0 && (HasAcquiredGuardianForce) && NextRandomByte() <= 12)
        {
            QueueNativeAutomaticGuardian(checked((byte)(NextRandomByte() / 64 + 7)));
            nativeSupport = nativeSupport with { GilgameshUsed = 1, Cooldown = definition.NativeSupportRules!.Cooldown };
            return;
        }
            RunExpandedPeriodicAngelo();
        nativeSupport = nativeSupport with { Cooldown = definition.NativeSupportRules!.Cooldown };
    }

}
