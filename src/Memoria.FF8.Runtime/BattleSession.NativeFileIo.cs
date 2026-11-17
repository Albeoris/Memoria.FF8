using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattleFileIoState? NativeFileIo { get; private set; }
    public NativeVibrationProcessState? NativeVibrationProcess { get; private set; }
}
