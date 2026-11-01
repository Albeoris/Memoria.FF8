using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private NativeCarWorldRuntimeState InitializeCarAllocation(NativeCarWorldRuntimeState car, NativeSavedWorldState saved, WorldEntryVariables inputs)
    {
        if (saved.Slot4.ShapeId != car.ShapeId)
        {
            throw new InvalidDataException("Native car allocation requires its selected original saved slot4.");
        }
        byte guardianFlags = (session.State.CharacterVitals ?? throw new NotSupportedException("Native car allocation requires its actual GF13 initialization flags.")).GuardianForces[13].Flags;
        ImmutableArray<NativeTrainWorldObject> appended = assets.ReadCarAppendedAllocation(saved.Slot4, inputs.ReplacementFlags, guardianFlags);
        return car with { AppendedObjects = appended, AllocationReplacementFlags264 = inputs.ReplacementFlags, AllocationGuardianForce13Flags = guardianFlags };
    }

    private void ValidateCarAppendedAllocation(NativeCarWorldRuntimeState car, WorldEntryVariables inputs)
    {
        if (car.AllocationReplacementFlags264 is not { } replacements264 || car.AllocationGuardianForce13Flags is not { } guardianFlags || car.AppendedObjects.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native car allocation requires its retained appended object owners; migrate the historical snapshot explicitly.");
        }
        if ((replacements264 & 7) != (inputs.ReplacementFlags & 7))
        {
            throw new InvalidDataException("Native car retained auxiliary objects differ from their actual264 allocation flags.");
        }
        NativeTrainWorldObject initialCar = car.AppendedObjects[0];
        NativeSavedWorldVehicleSlot selected = new(car.ShapeId, initialCar.Pose);
        ImmutableArray<NativeTrainWorldObject> actual = assets.ReadCarAppendedAllocation(selected, replacements264, guardianFlags);
        if (actual.Length != car.AppendedObjects.Length || actual.Where((item, index) => item.RawOperand != car.AppendedObjects[index].RawOperand || !item.CoordinateBytes.SequenceEqual(car.AppendedObjects[index].CoordinateBytes)).Any())
        {
            throw new InvalidDataException("Native car appended objects differ from their original selected saved pose, GF flag or section10 coordinates.");
        }
    }

    private static int CarSelectedAllocationIndex(NativeCarWorldRuntimeState car)
    {
        return car.Allocation!.Objects.Length;
    }

    private static ImmutableArray<NativeTrainWorldObject> CarContactAppendedObjects(WorldState world, NativeCarWorldRuntimeState car, NativeWorldParkedCar? parked = null)
    {
        if (car.AppendedObjects.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native car contacts require the executed appended allocation.");
        }
        NativeWorldParkedCar? actualParked = parked ?? world.NativeFootRuntime?.ParkedCar;
        NativeWorldPathPosition position = actualParked?.Position ?? new(world.X, world.NativeHeight, -world.Z);
        short yaw = actualParked?.BodyYaw ?? checked((short)world.BodyYaw);
        return car.AppendedObjects.SetItem(0, NativeCarWorldAllocationReader.FromPose(car.ShapeId, position, unchecked((ushort)yaw)));
    }
}
