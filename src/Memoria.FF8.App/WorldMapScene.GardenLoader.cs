using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState InitializeParkedGarden(WorldState world, WorldTransition arrival, WorldEntryVariables inputs, short selectedGroundHeight, NativeSavedWorldFootReturn? battleReload = null)
    {
        NativeSavedWorldState saved = session.State.SavedWorld!;
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory ?? throw new NotSupportedException("Parked Garden allocation requires the current executed world-entry bytes.");
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Parked Garden allocation requires its actual saved GF13 flags.");
        NativeGardenWorldAllocation allocation = assets.ReadParkedGardenAllocation(arrival, entry, session.State.EventMemory, saved, vitals, battleReload: battleReload);
        NativeWorldAuxiliaryRelocationReceipt? auxiliaryEntry = assets.ReadGardenAuxiliaryRelocations(arrival, session.State.EventMemory, allocation.LoadPose);
        NativeFootWorldRuntimeState foot = world.NativeFootRuntime ?? throw new InvalidDataException("Parked Garden loader requires its controlled normal-foot owner.");
        foot.RequireNativeGroundState();
        NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Parked Garden entry requires its retained native process owner.");
        // The parked allocation shares the already initialized controlled foot camera and ground owners.
        NativeGardenWorldRuntimeState garden = NativeGardenWorldRuntimeState.CreateFresh(saved, allocation, foot.GroundCache!, process, assets.ReadGardenCameraDefaults(), NativeWorldFootCameraState.Create(world), selectedGroundHeight, ReadInitialTerrainOcclusion()) with
        {
            AuxiliaryEntry = auxiliaryEntry,
            AnimationVariant = foot.AnimationVariant,
            MapDisplayMode = foot.MapDisplayMode,
            GroundYaw = checked((short)foot.GroundYaw!.Value),
            Forest = foot.Forest!,
            CameraPitch = foot.Forest!.CameraPitch
        };
        garden = garden with { BackgroundRails = CreateBackgroundRails(allocation.Flags265, garden.ProjectionCamera!) };
        WorldState result = world with { NativeGardenRuntime = garden };
        if (auxiliaryEntry is not null)
        {
            session.SetEventMemory(auxiliaryEntry.ApplyTo(session.State.EventMemory));
        }
        ValidateParkedGarden(result, arrival, inputs, battleReload);
        return result;
    }

    private void ValidateParkedGarden(WorldState world, WorldTransition arrival, WorldEntryVariables inputs, NativeSavedWorldFootReturn? battleReload = null)
    {
        NativeSavedWorldState saved = session.State.SavedWorld!;
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory ?? throw new NotSupportedException("Parked Garden restoration requires its actual world-entry bytes.");
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Parked Garden allocation requires its actual saved GF13 flags.");
        NativeGardenWorldRuntimeState garden = world.NativeGardenRuntime ?? throw new InvalidDataException("Parked Garden restoration lost its executed allocation owner.");
        if (garden.CarBodyPose is not null)
        {
            ValidateRetainedGardenAllocation(garden, arrival, inputs);
            world.NativeFootRuntime!.RequireNativeGroundState();
            return;
        }
        NativeGardenWorldAllocation allocation = assets.ReadParkedGardenAllocation(arrival, entry, session.State.EventMemory, saved, vitals, garden.AuxiliaryEntry, battleReload);
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
}
