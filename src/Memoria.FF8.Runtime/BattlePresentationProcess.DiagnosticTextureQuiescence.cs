using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    public BattleDiagnosticTerminalTextureAttempt? LastDiagnosticTerminalTextureAttempt { get; private set; }
}
