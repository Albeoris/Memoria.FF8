using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool NativeMovieSuppressed => services.WorldControls is { } controls && (controls.EventFlags104 & 0x100) != 0;

    private string? UnsupportedRemainingControlCode(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode == Jsm.Opcode.DEBUG)
        {
            return services.CanStoreNativeFieldDebugRequested ? null : "DEBUG requires its writable native process flag owner";
        }
        if (operation.Opcode is not (Jsm.Opcode.MOVIECUT or Jsm.Opcode.BATTLECUT))
        {
            return null;
        }
        if (services.WorldControls is null || !services.CanStoreNativeEventOwners)
        {
            return $"{operation.Opcode} requires the retained writable native control byte owners";
        }
        services.WorldControls.Validate();
        return UnsupportedWorldControlAliases();
    }

    private void ExecuteRemainingControlCode(Fiber fiber, Jsm.Opcode opcode)
    {
        NativeWorldControlState controls = services.WorldControls!;
        NativeWorldControlState next = opcode == Jsm.Opcode.MOVIECUT ? controls with { EventFlags104 = controls.EventFlags104 | 0x100u } : controls with { MusicKeep207 = 1, EventFlags104 = controls.EventFlags104 | 0x20u };
        SetNativeWorldControls(fiber, next);
    }
}
