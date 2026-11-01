using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private static bool HasSavedParkedCar(NativeSavedWorldState? saved)
    {
        return saved is not null && NativeCarWorldRuntimeState.IsSupportedEntity(NativeTrainWorldObject.ClassifyShape(saved.Slot4.ShapeId));
    }

    private WorldState InitializeSavedParkedCar(WorldState world, WorldEntryVariables inputs)
    {
        NativeSavedWorldState saved = session.State.SavedWorld!;
        NativeFootWorldRuntimeState foot = world.NativeFootRuntime!;
        byte entity = checked((byte)NativeTrainWorldObject.ClassifyShape(saved.Slot4.ShapeId));
        if (saved.Slot4.ShapeId != NativeCarWorldRuntimeState.ShapeForEntity(entity))
        {
            throw new NotSupportedException("Parked car reload requires its represented original stored shape variant.");
        }
        NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(inputs.StoryWord, inputs.EventFlags265);
        if (allocation.Objects[allocation.ControlledObjectIndex].EntityType != foot.EntityType)
        {
            throw new NotSupportedException("Parked car reload differs from the actual selected original foot object.");
        }
        NativeWorldFootCameraState camera = NativeWorldFootCameraState.Create(world);
        NativeCarWorldRuntimeState car = NativeCarWorldRuntimeState.Create(saved, foot.GroundCache!, session.State.WorldProcess!, entity, entity == 33 ? null : allocation, entity == 33 ? null : camera);
        car = car with { Allocation = allocation, ProjectionCamera = camera, ObjectProcess = CreateCarObjectProcess(allocation, camera) };
        car = InitializeCarAllocation(car, saved, inputs);
        //544860 appends the saved vehicle at pose4. The loader initializes its
        //process owners afresh;549E80 retained the parked pose while foot was controlled.
        NativeSavedWorldPose pose = saved.Slot4.Pose;
        NativeWorldParkedCar parked = new(car, new(pose.X, pose.Height, -pose.RawZ), checked((short)pose.Yaw), 0) { LoadedFromSavedSlot = true };
        return world with { NativeFootRuntime = foot with { ParkedCar = parked } };
    }
}
