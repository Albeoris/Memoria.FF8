namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleScriptState nativeScriptState = new();
    public NativeBattleScriptState NativeScriptState => nativeScriptState.DeepCopy();

    private void QueueNativeWait(int callbacks)
    {
        nativeScriptState = nativeScriptState with { WaitCounters = [.. nativeScriptState.WaitCounters, checked((ushort)callbacks)] };
    }

    private bool DispatchNativeWaits()
    {
        if (nativeScriptState.WaitCounters.Length == 0)
        {
            return false;
        }
        //48ACD0 decrements each event WORD independently; zero releases the dispatch gate on its next callback.
        bool held = nativeScriptState.WaitCounters.Any(counter => counter != 0);
        nativeScriptState = nativeScriptState with { WaitCounters = nativeScriptState.WaitCounters.Where(counter => counter != 0).Select(counter => (ushort)(counter - 1)).ToArray() };
        return held;
    }

    private void AcquireNativeGuardianForce(byte id)
    {
        if (id >= 16)
        {
            throw new InvalidDataException("Native GF acquisition has no original saved GF record.");
        }
        nativeScriptState = nativeScriptState with { AcquiredGuardianForces = [.. nativeScriptState.AcquiredGuardianForces, id] };
    }

    private void AwardNativeCard(byte id)
    {
        if (id >= NativeCardInventory.CardCount)
        {
            throw new InvalidDataException("Native battle card acquisition has no original card record.");
        }
        nativeScriptState = nativeScriptState with { Cards = [.. nativeScriptState.Cards, id] };
    }

    private void UnlockNativeTutorial(int id)
    {
        nativeScriptState = nativeScriptState with { UnlockedTutorials = nativeScriptState.UnlockedTutorials.Append(id).Distinct().Order().ToArray() };
    }
}
