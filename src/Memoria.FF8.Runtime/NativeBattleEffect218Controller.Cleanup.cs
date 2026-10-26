using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218RestoreCommon508630(uint SourceAddress, uint CompletionAddress);

public static partial class NativeBattleEffect218Controller
{
    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceCleanup633BF0(NativeBattleEffect218Frame frame, uint address, Func<NativeBattleEffect218Frame, NativeBattleEffect218RestoreCommon508630, NativeBattleEffect218Frame> restore)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadWord(12) == 1)
        {
            frame = restore(frame, new(checked(frame.State.ReadDword(ArenaGlobal) + StreamedIoOffset), checked(address + 14)));
            task = Read(frame, address);
        }
        ushort completion = task.ReadWord(14);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), completion == 0 ? 0 : 2);
    }
}
