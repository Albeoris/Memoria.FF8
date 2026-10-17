namespace Memoria.FF8.Runtime;

public sealed partial record NativeSavedWorldState
{
    public NativeSavedWorldState WithRagnarokParking(NativeRagnarokWorldRuntimeState runtime)
    {
        Validate();
        runtime.RequireSceneState();
        NativeRagnarokWorldObject ship = runtime.Objects[runtime.Allocation.RagnarokIndex];
        NativeSavedWorldPose pose = new(ship.Position.X, unchecked(-ship.Position.Z), unchecked((short)ship.Position.Y), unchecked((ushort)ship.Yaw));
        if (pose.Yaw >= 4096)
        {
            throw new InvalidDataException("Parked Ragnarok yaw is outside its normalized native object angle.");
        }
        //549E80 saves shape1 to its independent+24 pose2 before publishing
        //the current controlled foot0 pose and auxiliary return fields.
        NativeRagnarokWorldObject garden = runtime.Objects.Last(item => item.ShapeId == 64);
        NativeSavedWorldPose gardenPose = new(garden.Position.X, unchecked(-garden.Position.Z), unchecked((short)garden.Position.Y), unchecked((ushort)garden.Yaw));
        return this with { RagnarokPose2 = pose, GardenPose3 = gardenPose, RagnarokReturn = null };
    }
}
