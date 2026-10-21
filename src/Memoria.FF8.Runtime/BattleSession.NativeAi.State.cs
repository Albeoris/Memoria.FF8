namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleScriptState nativeScriptState = new();
    public NativeBattleScriptState NativeScriptState => nativeScriptState.DeepCopy();

    private void QueueNativeWait(int callbacks)
    {
        StopTurnAdvance();
        nativeScriptState = nativeScriptState with { Tasks = nativeScriptState.Tasks.AppendWait(checked((ushort)callbacks)) };
    }

    private bool DispatchNativeWaits()
    {
        return NativeScriptAllocationPaused;
    }

    private void AcquireNativeGuardianForce(byte id)
    {
        if (id >= 16)
        {
            throw new InvalidDataException("Native GF acquisition has no original saved GF record.");
        }
        nativeScriptState = nativeScriptState with { AcquiredGuardianForces = [.. nativeScriptState.AcquiredGuardianForces, id] };
        AcquireGuardianForceInBattle(id);
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
