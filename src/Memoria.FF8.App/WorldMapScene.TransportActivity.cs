using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private readonly record struct TransportContactPublication(long Frame, int BroadContact);
    private TransportContactPublication? transportContactPublication;

    private void RecordTransportContactPublication(long callbackFrame, int? broadContact)
    {
        transportContactPublication = broadContact is int contact ? new(callbackFrame, contact) : null;
    }

    internal bool? CompleteNativeTransportActivity(WorldState state, NativeWorldSalaryState salary, bool controlPathCompleted, bool transferAdmitted)
    {
        if (transferAdmitted)
        {
            return true;
        }
        if (!controlPathCompleted)
        {
            return null;
        }
        bool carController = state.NativeCarRuntime is not null;
        bool footController = state.NativeFootRuntime is { } foot && (foot.ParkedCar is not null || foot.StationTrains is not null || state.NativeGardenRuntime is not null || state.NativeRagnarokRuntime is not null);
        if (!carController && !footController)
        {
            return null;
        }
        NativeWorldInputHistory input = salary.InputHistory ?? throw new InvalidDataException("Completed transport controls require their actual current/prior input publication.");
        if ((input.CurrentButtons & 0x40) == 0 || input.PreviousButtons is ushort previous && (previous & 0x40) != 0 || salary.EventUi?.ScriptMode is > 0)
        {
            return false;
        }
        if (input.PreviousButtons is null)
        {
            return null;
        }
        //559240 reset activity.54A7F0's view path does not set it; only an
        //admitted transfer writes1.54E9B0's separate draw-point writer is foot-only.
        if (state.NativeCarRuntime is { } car)
        {
            if (car.CameraTransition is { Flags: not 0 } || (WorldEntryVariables.Read(session.State).EventFlags265 & 128) != 0)
            {
                return false;
            }
            //A refused foot placement can fall back to a recognized parked
            //vehicle. This complete simple allocation has none of those targets.
            if (car.Allocation is not null && car.GardenObjects is null && car.RagnarokObjects is null && !car.AllocatedObjects.Any(item => item.ShapeId is 1 or 2 or 3 or 64 or 65))
            {
                return false;
            }
            return null;
        }
        //54E9B0 cannot start a draw point when the actual548CD0 broad result
        //is nonnegative. Unknown or absent contacts retain that separate boundary.
        return transportContactPublication is { } publication && publication.Frame == state.Frame && publication.BroadContact >= 0 ? false : null;
    }
}
