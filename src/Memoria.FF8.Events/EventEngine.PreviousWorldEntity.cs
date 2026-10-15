using Albeoris.Games.FF8.Jsm;
using System.Text.Json;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ValidatePreviousWorldEntityLoad(int fieldId, byte[] jsm, JsonElement? allocationContext)
    {
        if (services.WorldConfiguration is not { } configuration)
        {
            return;
        }
        configuration.Validate();
        if (EventMemory.Cells.Any(pair => pair.Key <= 86 && (long)pair.Key + pair.Value.Width > 86))
        {
            throw new NotSupportedException("Native world entity allocation overlaps retained uncertain memory whose byte projection is unimplemented.");
        }
        if (allocationContext is JsonElement context)
        {
            bool authentic = services is Memoria.FF8.Runtime.GameSession session && (session.State.EventState is JsonElement current && JsonElement.DeepEquals(current, context) || session.State.DepartingFieldState is JsonElement departing && JsonElement.DeepEquals(departing, context));
            Checkpoint saved = context.Deserialize<Checkpoint>(CheckpointOptions) ?? throw new InvalidDataException("Empty native field allocation restore context.");
            if (!authentic || saved.Version != 1 || saved.FieldId != fieldId || saved.ProgramHash != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(jsm)))
            {
                throw new InvalidDataException("Native field allocation restore context does not match its saved original VM and program.");
            }
            ValidatePreviousWorldEntityCheckpoint(saved);
        }
    }

    private void InitializePreviousWorldEntity()
    {
        if (services.WorldConfiguration is { } configuration)
        {
            // Native52BC00 copies BYTE1(2036B4C) on every allocation, independently of field identity.
            Scene.PreviousWorldEntityByte86 = configuration.EntityByte;
        }
    }

    private string? UnsupportedPreviousWorldEntityAccess(JsmOperation operation)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        if (width == 0 || operation.Parameter > 86 || (long)operation.Parameter + width - 1 < 86)
        {
            return null;
        }
        if (operation.Parameter != 86 || operation.Opcode is not (Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B))
        {
            return "Native world entity BYTE86 requires its exact byte read; overlapping access or an unimplemented writer preserves PC and operands";
        }
        if (Scene.PreviousWorldEntityByte86 is null)
        {
            return "Native world entity BYTE86 requires executed process configuration and field allocation authority";
        }
        if (EventMemory.Cells.Any(pair => pair.Key <= 86 && (long)pair.Key + pair.Value.Width > 86))
        {
            return "Native world entity BYTE86 disagrees with its exact allocated memory authority";
        }
        return null;
    }

    private void ValidatePreviousWorldEntityCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.PreviousWorldEntityByte86 is byte known)
        {
            if (saved.EventMemory.Cells.Any(pair => pair.Key <= 86 && (long)pair.Key + pair.Value.Width > 86) || services.ReplayScene != Memoria.FF8.Runtime.SceneKind.WorldMap && services.WorldConfiguration?.EntityByte != known)
            {
                throw new InvalidDataException("Saved world entity BYTE86 disagrees with its allocated process configuration.");
            }
        }
        else if (services.ReplayScene != Memoria.FF8.Runtime.SceneKind.WorldMap && services.WorldConfiguration is not null)
        {
            throw new InvalidDataException("Saved field lacks its native world entity allocation authority.");
        }
    }
}
