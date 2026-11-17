namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeGlobalRandomState? nativeGlobalRandom;
    private ulong nativeGlobalRandomCalls;

    public NativeGlobalRandomChange? NativeGlobalRandomChange => this.resources.InitialGlobalRandom is { } before ? new(before, nativeGlobalRandom ?? throw new InvalidDataException("Battle global random owner was lost."), nativeGlobalRandomCalls) : null;

    private ushort NextNativeGlobalRandom()
    {
        nativeGlobalRandom = (nativeGlobalRandom ?? throw new NotSupportedException("Native graphics randomness requires the actual savedDWORD1CFEFB4 owner.")).Draw(out ushort result);
        nativeGlobalRandomCalls = checked(nativeGlobalRandomCalls + 1);
        return result;
    }
}
