using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedWorldFuelMemory(JsmOperation operation)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        return NativeRuntimeProfile is { IsVerified: true } && width != 0 && operation.Parameter < 1396 && (long)operation.Parameter + width > 1392 ? "Native saved-world fuel DWORD1392..1395 requires its canonical owner projection; generic event memory cannot alias it" : null;
    }
}
