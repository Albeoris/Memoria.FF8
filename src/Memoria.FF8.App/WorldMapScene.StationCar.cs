using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState? PrepareStationCarReboard(WorldState state, NativeWorldSalaryState salary)
    {
        if (state.NativeFootRuntime is not { StationTrains: { } source, StationTrainProcess: { } station } foot || source.Load.SavedVehicleObjects.IsEmpty || salary.InputHistory is not { PreviousButtons: { } previous } input || (input.CurrentButtons & ~previous & 0x40) == 0 || salary.EventUi?.ScriptMode != 0 || foot.CameraTransition is { Flags: not 0 })
        {
            return null;
        }
        NativeTrainWorldObject savedCar = source.Load.SavedVehicleObjects[0];
        byte entity = checked((byte)savedCar.EntityType);
        if (savedCar.ShapeId != NativeCarWorldRuntimeState.ShapeForEntity(entity))
        {
            throw new NotSupportedException("Station car boarding requires its represented original stored shape variant.");
        }
        NativeWorldFootCameraState camera = station.ProjectionCamera ?? throw new InvalidDataException("Station car boarding requires its executed current pre-terrain camera.");
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        NativeWorldPathWords player = context.RelativeWords(new(state.X, state.NativeHeight, -state.Z));
        NativeTrainRailBank? primary = station.Primary.TrackIndex == 0 ? station.Primary : station.Alternate;
        NativeTrainRailBank? alternate = station.Primary.TrackIndex == 1 ? station.Primary : station.Alternate;
        NativeNormalFootObjectProcess objects = new(1, source.Load.Allocation, station.Callback, primary, alternate, camera, primary is null ? null : NormalTrainDistance(primary, player, context), alternate is null ? null : NormalTrainDistance(alternate, player, context))
        {
            RidingTrainSource = source,
            CarDistancePosition = new(state.X, state.NativeHeight, -state.Z)
        };
        NativeCarWorldRuntimeState car = NativeCarWorldRuntimeState.Create(session.State.SavedWorld!, foot.GroundCache!, session.State.WorldProcess!, entity, entity == 33 ? null : source.Load.Allocation, entity == 33 ? null : camera) with
        {
            Allocation = source.Load.Allocation,
            ProjectionCamera = camera,
            ObjectProcess = objects,
            Forest = foot.Forest,
            GroundYaw = foot.GroundYaw,
            MapDisplayMode = foot.MapDisplayMode,
            PriorTerrainOcclusion = foot.PriorTerrainOcclusion,
            MovementMagnitude20409F4 = foot.MovementMagnitude20409F4
        };
        car = InitializeCarAllocation(car, session.State.SavedWorld!, WorldEntryVariables.Read(session.State));
        car.Validate();
        NativeWorldParkedCar parked = new(car, savedCar.Position, checked((short)savedCar.Pose.Yaw), 0) { LoadedFromSavedSlot = true };
        //548AF0 retains the existing allocation. Only54B460's successful selection
        //publishes the car; unsuccessful boarding leaves the station foot owner intact.
        return PrepareCarReboard(state with { NativeFootRuntime = foot with { ParkedCar = parked } }, salary);
    }
}
