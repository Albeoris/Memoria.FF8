using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState InitializeParkedGarden(WorldState world, WorldTransition arrival, WorldEntryVariables inputs, short selectedGroundHeight)
    {
        NativeSavedWorldState saved = session.State.SavedWorld!;
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory ?? throw new NotSupportedException("Parked Garden allocation requires the current executed world-entry bytes.");
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Parked Garden allocation requires its actual saved GF13 flags.");
        NativeGardenWorldAllocation allocation = assets.ReadParkedGardenAllocation(arrival, entry, session.State.EventMemory, saved, vitals);
        NativeFootWorldRuntimeState foot = world.NativeFootRuntime ?? throw new InvalidDataException("Parked Garden loader requires its controlled normal-foot owner.");
        foot.RequireNativeGroundState();
        NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Parked Garden entry requires its retained native process owner.");
        NativeGardenWorldRuntimeState garden = NativeGardenWorldRuntimeState.CreateFresh(saved, allocation, foot.GroundCache!, process, assets.ReadGardenCameraDefaults(), NativeWorldFootCameraState.Create(world), selectedGroundHeight, ReadInitialGardenTerrainOcclusion());
        WorldState result = world with { NativeGardenRuntime = garden };
        ValidateParkedGarden(result, arrival, inputs);
        return result;
    }

    private void ValidateParkedGarden(WorldState world, WorldTransition arrival, WorldEntryVariables inputs)
    {
        NativeSavedWorldState saved = session.State.SavedWorld!;
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory ?? throw new NotSupportedException("Parked Garden restoration requires its actual world-entry bytes.");
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Parked Garden allocation requires its actual saved GF13 flags.");
        NativeGardenWorldAllocation allocation = assets.ReadParkedGardenAllocation(arrival, entry, session.State.EventMemory, saved, vitals);
        NativeGardenWorldRuntimeState garden = world.NativeGardenRuntime ?? throw new InvalidDataException("Parked Garden restoration lost its executed allocation owner.");
        NativeFootWorldRuntimeState foot = world.NativeFootRuntime ?? throw new InvalidDataException("Parked Garden restoration lost its controlled foot owner.");
        garden.Validate();
        foot.RequireNativeGroundState();
        assets.ValidateNativeGroundCache(garden.GroundCache, replacements);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(allocation), JsonSerializer.SerializeToElement(garden.Allocation)))
        {
            throw new InvalidDataException("Restored parked Garden allocation differs from its authenticated original loader inputs.");
        }
        if (foot.EntityType != 0 || inputs.StoryWord != allocation.Story)
        {
            throw new InvalidDataException("Parked Garden allocation differs from its controlled entity or executed story owner.");
        }
    }

    private bool? ReadInitialGardenTerrainOcclusion()
    {
        WorldState prior = session.State.DepartingWorldState ?? throw new NotSupportedException("Garden loader requires the last executed terrain-render owner for its retained camera-occlusion latch.");
        if (prior.NativeGardenRuntime is NativeGardenWorldRuntimeState garden)
        {
            garden.Validate();
            return garden.PriorTerrainOcclusion;
        }
        NativeCarWorldRuntimeState car = prior.NativeCarRuntime ?? throw new NotSupportedException("Garden loader has no represented preceding terrain-render owner.");
        car.Validate();
        NativeWorldFootCameraState camera = car.ProjectionCamera ?? throw new NotSupportedException("Garden loader's preceding car has no retained pre-query projection camera.");
        IReadOnlyDictionary<int, int> priorReplacements = WorldTerrainSelection.Replacements(prior.ReplacementMask);
        short? height = null;
        foreach (NativeWorldGroundAlternative alternative in car.GroundCache!.Alternatives)
        {
            WorldGroundSelection selected = assets.LookupNativeGround(camera.X, camera.RawZ, priorReplacements, [alternative.PreviousGround]) ?? throw new NotSupportedException("Garden loader's preceding camera has no proved pre-query terrain height.");
            if (selected.Identity != alternative.PreviousGround)
            {
                throw new NotSupportedException("Garden loader's preceding terrain identity does not contain its actual pre-query camera origin.");
            }
            short candidate = unchecked((short)-(int)selected.Ground.Height);
            if (height is short observed && observed != candidate)
            {
                throw new NotSupportedException("Garden loader's preceding ground alternatives disagree on the retained camera height.");
            }
            height = candidate;
        }
        return assets.CertifyNativeTerrainCameraOcclusion(camera, height!.Value, priorReplacements, car.EntityType).Occluded;
    }
}
