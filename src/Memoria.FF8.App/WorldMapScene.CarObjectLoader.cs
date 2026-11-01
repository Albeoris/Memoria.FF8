using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private NativeCarWorldRuntimeState CreateCarObjectLoader(WorldTransition packet, WorldEntryVariables inputs, NativeSavedWorldState departure, NativeWorldGroundCacheState cache, NativeWorldProcess process, NativeSavedWorldPose carPose, NativeWorldFootCameraState camera)
    {
        byte entity = checked((byte)packet.Mode);
        if (!NativeCarWorldRuntimeState.IsSupportedEntity(entity) || unchecked((sbyte)packet.Extra) != -1)
        {
            throw new NotSupportedException("Native car allocation requires its actual ordinary controlled-car packet.");
        }
        NativeWorldAllocationClause clause = assets.ReadWorldAllocationClause(inputs.StoryWord);
        byte mapDisplay = departure.ReadMapDisplayModeForFreshFootLoader();
        if (clause.ContainsShape(1))
        {
            NativeRagnarokWorldRuntimeState owner = CreateCarRagnarokOwner(packet, carPose, camera, cache, process, mapDisplay);
            if (owner.Allocation.AuxiliaryEntry is { } auxiliary)
            {
                session.SetEventMemory(auxiliary.ApplyTo(session.State.EventMemory));
            }
            return NativeCarWorldRuntimeState.Create(departure, cache, process, entity, projectionCamera: camera, ragnarokObjects: owner);
        }
        if (clause.ContainsShape(64) || clause.ContainsShape(65))
        {
            NativeGardenWorldRuntimeState owner = CreateCarGardenOwner(packet, carPose, camera, cache, process, mapDisplay);
            if (owner.AuxiliaryEntry is { } auxiliary)
            {
                session.SetEventMemory(auxiliary.ApplyTo(session.State.EventMemory));
            }
            return NativeCarWorldRuntimeState.Create(departure, cache, process, entity, projectionCamera: camera, gardenObjects: owner);
        }
        NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(inputs.StoryWord, inputs.EventFlags265);
        NativeCarWorldRuntimeState car = NativeCarWorldRuntimeState.Create(departure, cache, process, entity, entity == 33 ? null : allocation, entity == 33 ? null : camera);
        car = car with { Allocation = allocation, ProjectionCamera = camera, ObjectProcess = CreateCarObjectProcess(allocation, camera) };
        return InitializeCarAllocation(car, session.State.SavedWorld!, inputs);
    }
}
