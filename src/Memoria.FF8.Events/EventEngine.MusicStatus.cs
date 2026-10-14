using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeMusicStatus(JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.MUSICSTATUS)
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return "Native MUSICSTATUS requires its authenticated original query contract";
        }
        try
        {
            NativeGlobalSoundProcess backend = services.GlobalSoundProcess ?? throw new NotSupportedException("Native MUSICSTATUS requires the explicit current audio backend owner.");
            backend.Validate();
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private void ExecuteNativeMusicStatus(Fiber fiber)
    {
        // Native51FC20 has no operands and writes only R0, returning2 immediately.
        fiber.Results[0] = services.QueryNativeMusicStatus();
    }
}
