namespace Memoria.FF8.Runtime;

public static class NativeBattleXatmDeath276Stack
{
    public static uint ResolveDirectionResidue(NativeBattleExecutionStack stack, byte entryLifecycle)
    {
        if (entryLifecycle is not (0 or 2 or 3 or 4))
        {
            throw new NotSupportedException("Native X-ATM092 arc stack requires its proved outer battle callback phase.");
        }
        NativeBattleStackFrame frame = stack.ReadCommonFrame();
        frame = frame.EnterCdecl(0x1C, 2, 0);
        frame = frame.EnterCdecl(0, 2, 0);
        frame = frame.EnterCdecl(0, 0, 4);
        frame = frame.EnterCdecl(0, 4, 4);
        frame = frame.EnterCdecl(0, 2, 16);
        frame = frame.EnterCdecl(0, 4, 4);
        return frame.AddressAtOffset(-0x38);
    }
}
