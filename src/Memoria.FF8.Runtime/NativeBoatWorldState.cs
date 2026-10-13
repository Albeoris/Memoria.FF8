using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Executed selected section9 shape4/coordinate0 allocation and original section10 coordinate record.</summary>
public sealed record NativeBoatWorldFootRecord([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] string ProgramHash, [property: JsonRequired] string CoordinateHash, [property: JsonRequired] ushort MinimumStory, [property: JsonRequired] ushort MaximumStory, [property: JsonRequired] NativeSavedWorldPose Pose)
{
    public const string AllocationRule = "ff8-en-world-section9-clause1-shape4-coordinate0-v1";

    public void Validate()
    {
        if (Version != 1 || Rule != AllocationRule || ProgramHash is not { Length: 64 } || !ProgramHash.All(Uri.IsHexDigit) || CoordinateHash is not { Length: 64 } || !CoordinateHash.All(Uri.IsHexDigit) || MinimumStory != 34 || MaximumStory != 205 || Pose is null || Pose != new NativeSavedWorldPose(0, 0, 0, 0))
        {
            throw new InvalidDataException("Boat's static native foot record lost its selected original allocation authority.");
        }
    }
}

/// <summary>Actual boat loader auxiliaries, retained body global and independent original static foot/vehicle records.</summary>
public sealed record NativeBoatWorldLoadReceipt([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] long CarSourceFrame, [property: JsonRequired] ushort RetainedBodyYaw, [property: JsonRequired] NativeBoatWorldFootRecord FootRecord0, [property: JsonRequired] NativeSavedWorldVehicleSlot Slot4, [property: JsonRequired] NativeSavedWorldVehicleSlot Slot5, [property: JsonRequired] byte AnimationVariant, [property: JsonRequired] byte MapDisplayMode)
{
    public const string LoadRule = "ff8-en-boat66-load-542b90-548080-static-foot-record0-v1";

    public static NativeBoatWorldLoadReceipt Create(NativeSavedWorldState saved, WorldState previousWorld, NativeBoatWorldFootRecord footRecord)
    {
        saved.Validate();
        previousWorld.NativeCarRuntime?.Validate();
        NativeSavedWorldCarReturn car = saved.CarReturn ?? throw new NotSupportedException("Scripted boat allocation requires the executed preceding native car return.");
        if (previousWorld.NativeCarRuntime is null || previousWorld.Frame != car.SourceWorldFrame || new NativeSavedWorldPose(previousWorld.X, previousWorld.Z, checked((short)previousWorld.NativeHeight), checked((ushort)previousWorld.BodyYaw)) != car.Pose)
        {
            throw new NotSupportedException("Retained native object record0 has no matching preceding car callback authority.");
        }
        footRecord.Validate();
        var receipt = new NativeBoatWorldLoadReceipt(1, LoadRule, car.SourceWorldFrame, car.Pose.Yaw, footRecord, saved.Slot4, saved.Slot5, 0, car.MapDisplayMode110);
        receipt.Validate();
        return receipt;
    }

    public void Validate()
    {
        FootRecord0?.Validate();
        if (Version != 1 || Rule != LoadRule || CarSourceFrame < 0 || RetainedBodyYaw >= 4096 || FootRecord0 is null || Slot4?.Pose is null || Slot5?.Pose is null || Slot4.ShapeId != 255 || Slot5.ShapeId != 0 || AnimationVariant != 0 || MapDisplayMode != 0)
        {
            throw new InvalidDataException("Scripted boat loader lost its retained car/object-record or auxiliary authority.");
        }
    }
}

/// <summary>Executed549E80(mode1) entity66 globals and the independently allocated static foot-record pose0 write.</summary>
public sealed record NativeSavedWorldBoatReturn([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] long SourceWorldFrame, [property: JsonRequired] ushort LastFieldId, [property: JsonRequired] ushort StoryWord, [property: JsonRequired] WorldTransition Packet, [property: JsonRequired] FieldTransition FieldEntry, [property: JsonRequired] NativeBoatWorldLoadReceipt Load, [property: JsonRequired] NativeWorldPathCamera Camera, [property: JsonRequired] ushort CameraYaw96, [property: JsonRequired] byte EntityType105, [property: JsonRequired] byte Channel106, [property: JsonRequired] byte Channel107, [property: JsonRequired] byte AnimationVariant108, [property: JsonRequired] byte MapDisplayMode110, [property: JsonRequired] short MapLocationId)
{
    public const string SaveRule = "ff8-en-boat66-field-return-549e80-mode1-v1";
    [JsonIgnore]
    public NativeSavedWorldPose Pose0 => Load.FootRecord0.Pose;

    public static NativeSavedWorldBoatReturn Create(NativeBoatWorldRuntimeState boat, ushort lastField, ushort story, short location, FieldTransition fieldEntry)
    {
        boat.Load.Validate();
        if (!boat.Path.EndpointRequested || boat.Path.Callback != boat.Path.EndpointCallback || boat.Path.Fade != 0)
        {
            throw new InvalidOperationException("Native boat save belongs to the actual rail endpoint before departure fade.");
        }
        var receipt = new NativeSavedWorldBoatReturn(1, SaveRule, boat.Path.Callback, lastField, story, boat.Path.Packet, fieldEntry, boat.Load, boat.Path.Camera, CameraYaw(boat.Path.Camera), 66, 255, 255, boat.Load.AnimationVariant, boat.Load.MapDisplayMode == 3 ? (byte)0 : boat.Load.MapDisplayMode, location);
        receipt.Validate();
        return receipt;
    }

    public static ushort CameraYaw(NativeWorldPathCamera camera)
    {
        // 552EB0 uses signed WORD target-minus-eye; HIWORD203ED00 is atan2(dx,dz).
        int dx = unchecked((short)(camera.Target.X - camera.Eye.X));
        int dz = unchecked((short)(camera.Target.Z - camera.Eye.Z));
        return unchecked((ushort)NativeWorldScriptedPathMath.NativeAngle(dx, dz));
    }

    public void Validate()
    {
        Load?.Validate();
        if (Version != 1 || Rule != SaveRule || SourceWorldFrame < 30 || StoryWord is < 34 or >= 205 || Packet is null || unchecked((byte)Packet.Mode) != 66 || unchecked((byte)Packet.EntryIndex) >= 14 || unchecked((sbyte)Packet.Extra) < 0 || FieldEntry is null || FieldEntry.FieldId is < 0 or > ushort.MaxValue || Load is null || Camera is null || CameraYaw96 != CameraYaw(Camera) || EntityType105 != 66 || Channel106 != 255 || Channel107 != 255 || AnimationVariant108 != Load.AnimationVariant || MapDisplayMode110 != Load.MapDisplayMode || MapLocationId is < sbyte.MinValue or > sbyte.MaxValue)
        {
            throw new InvalidDataException("Invalid native scripted boat save receipt.");
        }
    }
}

public sealed record NativeBoatWorldRuntimeState([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] NativeWorldScriptedPathState Path, [property: JsonRequired] NativeBoatWorldLoadReceipt Load, [property: JsonRequired] NativeSavedWorldBoatReturn? Return)
{
    public const string RuntimeRule = "ff8-en-boat66-scripted-world-runtime-v1";

    public void Validate()
    {
        Load?.Validate();
        Return?.Validate();
        if (Version != 1 || Rule != RuntimeRule || Path is null || Path.Version != 1 || Path.Rule != NativeWorldScriptedPathState.LoadRule || Path.EntityType != 66 || Path.ShapeId != 78 || Path.Packet is null || Path.Callback < 0 || Path.Fade is < 0 or > 256 || Path.Fade % 16 != 0 || Path.EndpointRequested && (Path.EndpointCallback is null or < 30 || Path.Callback != Path.EndpointCallback + Path.Fade / 16 || Path.CameraCounter != 0) || !Path.EndpointRequested && (Path.EndpointCallback is not null || Path.Fade != 0 || Path.CameraCounter != Path.Callback + 1) || Load is null || Path.EndpointRequested != (Return is not null) || Return is not null && (Return.SourceWorldFrame != Path.EndpointCallback || Return.Packet != Path.Packet || Return.Load != Load || Return.Camera != Path.Camera))
        {
            throw new InvalidDataException("Scripted boat runtime and actual endpoint save authority disagree.");
        }
    }

    public WorldState ToWorldState(WorldState? previous = null)
    {
        Validate();
        return (previous ?? new WorldState()) with { Frame = Path.Callback, X = Path.EntityPosition.X, Z = unchecked(-Path.EntityPosition.Z), NativeHeight = Path.EntityPosition.Y, BodyYaw = Load.RetainedBodyYaw, CameraYaw = NativeSavedWorldBoatReturn.CameraYaw(Path.Camera), NativeFootRuntime = null, NativeCarRuntime = null, NativeBoatRuntime = this, GroundType = null };
    }
}
