namespace Memoria.FF8.Runtime;

public sealed partial record NativeWorldSalaryState
{
    private NativeSavedWorldFootReturn? CameraToggleReturn { get; init; }

    public NativeWorldSalaryState WithFootCameraToggle(WorldState world, ushort lastFieldId, ushort storyWord, short mapLocationId)
    {
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(world, lastFieldId, storyWord, mapLocationId);
        NativeSavedWorldState saved = SavedWorldEvents ?? throw new NotSupportedException("Camera mode saving requires the actual saved-world owner.");
        return this with { CameraToggleReturn = receipt, SavedWorldEvents = saved.WithFootReturn(receipt) };
    }

    public NativeWorldSalaryState WithWorldEventCommand(ushort opcode, ushort operand, NativeWorldMessage? decodedMessage = null)
    {
        if (opcode == 0xFF26)
        {
            return WithWorldScriptMode(unchecked((byte)operand));
        }
        if (opcode == 0xFF36)
        {
            return this with { EventUi = RequireWorldEventUi().SuppressClose() };
        }
        if (opcode == 0xFF1F)
        {
            if (decodedMessage is null || decodedMessage.WindowId != unchecked((sbyte)operand) || decodedMessage.MessageId != operand >> 8)
            {
                throw new NotSupportedException("World message command requires its original signed window selector and unsigned message resource.");
            }
            return this with { EventUi = RequireWorldEventUi().Show(decodedMessage) };
        }
        if (opcode == 0xFF24)
        {
            return this with { EventUi = RequireWorldEventUi().Close(operand) };
        }
        NativeSavedWorldState saved = SavedWorldEvents ?? throw new NotSupportedException("Native world event commands require their actual saved-world owner.");
        NativeSavedWorldState next = opcode switch
        {
            0xFF28 => saved with { WorldEventFlags116 = (saved.WorldEventFlags116 ?? new NativeWorldEventFlags(0, 0)).WithOperand(operand) },
            0xFF2E => saved.WithWorldEventVariable(unchecked((byte)operand), unchecked((byte)(operand >> 8))),
            _ => throw new NotSupportedException($"Unsupported saved-world event command{opcode:X4}.")
        };
        return this with { SavedWorldEvents = next };
    }

    private NativeSavedWorldState? ApplySavedWorldOwners(NativeSavedWorldState? saved)
    {
        if (saved is null)
        {
            return null;
        }
        if (CameraToggleReturn is not null)
        {
            saved = saved.WithFootReturn(CameraToggleReturn);
        }
        return saved with
        {
            FuelDistance112 = FuelDistance112,
            WorldEventFlags116 = SavedWorldEvents?.WorldEventFlags116,
            WorldEventVariable124 = SavedWorldEvents?.WorldEventVariable124,
            WorldEventVariable125 = SavedWorldEvents?.WorldEventVariable125
        };
    }
}
