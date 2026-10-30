namespace Memoria.FF8.Runtime;

public sealed partial record NativeWorldSalaryState
{
    private NativeSavedWorldFootReturn? CameraToggleReturn { get; init; }
    private NativeWorldParkedCar? CameraToggleParkedCar { get; init; }
    private NativeSavedWorldCarReturn? CameraToggleCarReturn { get; init; }
    private NativeSavedWorldGardenReturn? CameraToggleGardenReturn { get; init; }
    private NativeSavedWorldRagnarokReturn? CameraToggleRagnarokReturn { get; init; }

    public NativeWorldSalaryState WithTerrainEntranceFlag(bool? entrance)
    {
        NativeSavedWorldState saved = SavedWorldEvents ?? throw new NotSupportedException("World terrain publication requires its saved-world owner.");
        return this with { SavedWorldEvents = saved.WithTerrainEntranceFlag(entrance) };
    }

    public NativeWorldSalaryState WithFootCameraToggle(WorldState world, ushort lastFieldId, ushort storyWord, short mapLocationId)
    {
        NativeSavedWorldState saved = SavedWorldEvents ?? throw new NotSupportedException("Camera mode saving requires the actual saved-world owner.");
        if (world.NativeFootRuntime is null)
        {
            if (world.NativeCarRuntime is not null)
            {
                NativeSavedWorldCarReturn car = NativeSavedWorldCarReturn.Create(world, lastFieldId, storyWord, mapLocationId) with { RetireSelectedVehicle = false };
                return this with { CameraToggleReturn = null, CameraToggleParkedCar = null, CameraToggleGardenReturn = null, CameraToggleRagnarokReturn = null, CameraToggleCarReturn = car, SavedWorldEvents = saved.WithCarReturn(car) };
            }
            if (world.NativeGardenRuntime is not null)
            {
                NativeSavedWorldGardenReturn garden = NativeSavedWorldGardenReturn.Create(world, lastFieldId, storyWord, mapLocationId);
                return this with { CameraToggleReturn = null, CameraToggleParkedCar = null, CameraToggleCarReturn = null, CameraToggleRagnarokReturn = null, CameraToggleGardenReturn = garden, SavedWorldEvents = saved.WithGardenReturn(garden) };
            }
            NativeSavedWorldRagnarokReturn ragnarok = NativeSavedWorldRagnarokReturn.Create(world, lastFieldId, storyWord, mapLocationId);
            return this with { CameraToggleReturn = null, CameraToggleParkedCar = null, CameraToggleCarReturn = null, CameraToggleGardenReturn = null, CameraToggleRagnarokReturn = ragnarok, SavedWorldEvents = saved.WithRagnarokReturn(ragnarok) };
        }
        NativeWorldParkedCar? parkedCar = world.NativeFootRuntime.ParkedCar;
        if (parkedCar is not null)
        {
            saved = saved.WithCarParking(parkedCar);
        }
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(world, lastFieldId, storyWord, mapLocationId);
        return this with { CameraToggleCarReturn = null, CameraToggleGardenReturn = null, CameraToggleRagnarokReturn = null, CameraToggleReturn = receipt, CameraToggleParkedCar = parkedCar, SavedWorldEvents = saved.WithFootReturn(receipt) };
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
        if (CameraToggleParkedCar is not null)
        {
            saved = saved.WithCarParking(CameraToggleParkedCar);
        }
        if (CameraToggleReturn is not null)
        {
            saved = saved.WithFootReturn(CameraToggleReturn);
        }
        if (CameraToggleCarReturn is not null)
        {
            saved = saved.WithCarReturn(CameraToggleCarReturn);
        }
        if (CameraToggleGardenReturn is not null)
        {
            saved = saved.WithGardenReturn(CameraToggleGardenReturn);
        }
        if (CameraToggleRagnarokReturn is not null)
        {
            saved = saved.WithRagnarokReturn(CameraToggleRagnarokReturn);
        }
        return saved with
        {
            FuelDistance112 = FuelDistance112,
            WorldEventFlags116 = SavedWorldEvents?.WorldEventFlags116,
            WorldEventVariable124 = SavedWorldEvents?.WorldEventVariable124,
            WorldEventVariable125 = SavedWorldEvents?.WorldEventVariable125,
            TerrainFlags109 = SavedWorldEvents?.TerrainFlags109
        };
    }
}
