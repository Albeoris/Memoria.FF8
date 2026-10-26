namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect326Stack
{
    public static int? ResolveLateModelDrawEntryModulo8(NativeBattleExecutionStack? stack, byte? entryLifecycle)
    {
        if (stack is null || entryLifecycle is not (0 or 2 or 3 or 4))
        {
            return null;
        }
        NativeBattleStackFrame frame = stack.ReadCommonFrame();
        frame = frame.EnterCdecl(0x1C, 2, 0); //47CF60 to47CCB0, then tail47D490.
        frame = frame.EnterCdecl(0, 2, 0); //47D490 to500900.
        frame = frame.EnterCdecl(0, 0, 4); //50093A to508420.
        frame = frame.EnterCdecl(0, 4, 4); //Main task6326B0.
        frame = frame.EnterCdecl(0, 0, 4); //6326B5 to508420.
        frame = frame.EnterCdecl(0, 4, 4); //Late model62CC00.
        frame = frame.EnterCdecl(0, 2, 8); //62CD03 to62C820.
        return (int)(frame.EntryEsp & 7);
    }
}
