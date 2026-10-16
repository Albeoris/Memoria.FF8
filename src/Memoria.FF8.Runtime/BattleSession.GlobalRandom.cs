namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeGlobalRandomState? nativeGlobalRandom;
    private ulong nativeGlobalRandomCalls;

    public NativeGlobalRandomChange? NativeGlobalRandomChange => definition.NativeGlobalRandom is { } before ? new(before, nativeGlobalRandom ?? throw new InvalidDataException("Battle global random owner was lost."), nativeGlobalRandomCalls) : null;

    private ushort NextNativeGlobalRandom()
    {
        nativeGlobalRandom = (nativeGlobalRandom ?? throw new NotSupportedException("Native graphics randomness requires the actual savedDWORD1CFEFB4 owner.")).Draw(out ushort result);
        nativeGlobalRandomCalls = checked(nativeGlobalRandomCalls + 1);
        return result;
    }

    private void RestoreNativeGlobalRandom(BattleSnapshot snapshot)
    {
        if ((snapshot.NativeGlobalRandom is null) != (definition.NativeGlobalRandom is null) || snapshot.NativeGlobalRandom is null && snapshot.NativeGlobalRandomCalls != 0)
        {
            throw new InvalidDataException("Battle snapshot lacks its separately owned native global RNG and call count.");
        }
        if (definition.NativeGlobalRandom is { } before)
        {
            new NativeGlobalRandomChange(before, snapshot.NativeGlobalRandom!, snapshot.NativeGlobalRandomCalls).Validate();
        }
        nativeGlobalRandom = snapshot.NativeGlobalRandom;
        nativeGlobalRandomCalls = snapshot.NativeGlobalRandomCalls;
    }
}
