using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Initial selected section9 single-foot/coordinate0 allocation, validated against the complete original clause.</summary>
public sealed record NativeScriptedWorldFootRecord([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] string ProgramHash, [property: JsonRequired] string CoordinateHash, [property: JsonRequired] byte ShapeId, [property: JsonRequired] ushort MinimumStory, [property: JsonRequired] ushort MaximumStory, [property: JsonRequired] NativeSavedWorldPose Pose)
{
    public const string AllocationRule = "ff8-en-world-section9-selected-static-foot-coordinate0-v1";

    public void Validate()
    {
        if (Version != 1 || Rule != AllocationRule || ProgramHash is not { Length: 64 } || !ProgramHash.All(Uri.IsHexDigit) || CoordinateHash is not { Length: 64 } || !CoordinateHash.All(Uri.IsHexDigit) || ShapeId is not (0 or 4) || MinimumStory >= MaximumStory || Pose is null || Pose != new NativeSavedWorldPose(0, 0, 0, 0))
        {
            throw new InvalidDataException("Scripted world's static native foot record lost its selected original allocation authority.");
        }
    }
}

/// <summary>Actual scripted loader auxiliaries, retained body global and independent original static foot/vehicle records.</summary>
public sealed record NativeScriptedWorldLoadReceipt([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] long SourceWorldFrame, [property: JsonRequired] ushort RetainedBodyYaw, [property: JsonRequired] NativeScriptedWorldFootRecord FootRecord0, [property: JsonRequired] NativeSavedWorldVehicleSlot Slot4, [property: JsonRequired] NativeSavedWorldVehicleSlot Slot5, [property: JsonRequired] byte AnimationVariant, [property: JsonRequired] byte MapDisplayMode, [property: JsonRequired] int RetainedCameraVelocity)
{
    public const string LoadRule = "ff8-en-scripted-mode1-2-load-542b90-548080-static-foot-record0-v1";

    public static NativeScriptedWorldLoadReceipt Create(NativeSavedWorldState saved, WorldState previousWorld, NativeScriptedWorldFootRecord footRecord)
    {
        footRecord.Validate();
        (long sourceFrame, ushort bodyYaw, byte mapDisplay) = ResolveRetained(saved, previousWorld);
        // 548080 mode1/2 replaces position from the selected rail and retains body yaw;
        // it reads current saved BYTE110. Executed 542B90 resets animation variant0.
        var receipt = new NativeScriptedWorldLoadReceipt(1, LoadRule, sourceFrame, bodyYaw, footRecord, saved.Slot4, saved.Slot5, 0, mapDisplay, previousWorld.CameraVelocity);
        receipt.Validate();
        return receipt;
    }

    public static (long SourceFrame, ushort BodyYaw, byte MapDisplay) ResolveRetained(NativeSavedWorldState saved, WorldState previousWorld)
    {
        saved.Validate();
        long sourceFrame;
        ushort bodyYaw;
        byte mapDisplay;
        if (saved.ScriptedReturn is NativeSavedWorldScriptedReturn priorReturn)
        {
            NativeScriptedWorldRuntimeState prior = previousWorld.NativeScriptedRuntime ?? throw new NotSupportedException("Scripted reload has no executed preceding scripted runtime.");
            prior.Validate();
            if (!prior.Path.FieldTransitionReady || prior.Return != priorReturn || previousWorld.Frame != prior.Path.Callback || previousWorld != prior.ToWorldState(previousWorld) || saved.Slot4 != prior.Load.Slot4 || saved.Slot5 != prior.Load.Slot5)
            {
                throw new NotSupportedException("Scripted reload lost its completed preceding native callback or saved object-record authority.");
            }
            sourceFrame = prior.Path.Callback;
            bodyYaw = prior.Load.RetainedBodyYaw;
            mapDisplay = priorReturn.MapDisplayMode110;
        }
        else if (saved.CarReturn is NativeSavedWorldCarReturn car)
        {
            previousWorld.NativeCarRuntime?.Validate();
            if (previousWorld.NativeCarRuntime is null || previousWorld.Frame != car.SourceWorldFrame || CurrentPose(previousWorld) != car.Pose)
            {
                throw new NotSupportedException("Retained native body has no matching preceding car callback authority.");
            }
            sourceFrame = car.SourceWorldFrame;
            bodyYaw = car.Pose.Yaw;
            mapDisplay = car.MapDisplayMode110;
        }
        else if (saved.FootReturn is NativeSavedWorldFootReturn foot)
        {
            foot.RequireCompleteFootReload();
            previousWorld.NativeFootRuntime?.Validate();
            if (previousWorld.NativeFootRuntime is not NativeFootWorldRuntimeState runtime || previousWorld.Frame != foot.SourceWorldFrame || CurrentPose(previousWorld) != foot.Pose0 || previousWorld.CameraYaw != foot.CameraYaw96 || runtime.EntityType != foot.EntityType105 || runtime.AnimationVariant != foot.AnimationVariant108 || (runtime.MapDisplayMode == 3 ? 0 : runtime.MapDisplayMode) != foot.MapDisplayMode110)
            {
                throw new NotSupportedException("Retained native body has no matching preceding foot callback authority.");
            }
            sourceFrame = foot.SourceWorldFrame;
            bodyYaw = foot.Pose0.Yaw;
            mapDisplay = foot.MapDisplayMode110!.Value;
        }
        else
        {
            throw new NotSupportedException("Scripted allocation requires an executed preceding native world return.");
        }
        return (sourceFrame, bodyYaw, mapDisplay);
    }

    private static NativeSavedWorldPose CurrentPose(WorldState world)
    {
        return new NativeSavedWorldPose(world.X, world.Z, checked((short)world.NativeHeight), checked((ushort)world.BodyYaw));
    }

    public void Validate()
    {
        FootRecord0?.Validate();
        if (Version != 1 || Rule != LoadRule || SourceWorldFrame < 0 || RetainedBodyYaw >= 4096 || FootRecord0 is null || Slot4?.Pose is null || Slot5?.Pose is null || AnimationVariant != 0)
        {
            throw new InvalidDataException("Scripted loader lost its retained world/object-record or auxiliary authority.");
        }
    }
}

/// <summary>Executed549E80(mode1/2) entity globals and the actual dense foot record saved before late548300 publication.</summary>
public sealed record NativeSavedWorldScriptedReturn([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] long SourceWorldFrame, [property: JsonRequired] ushort LastFieldId, [property: JsonRequired] ushort StoryWord, [property: JsonRequired] WorldTransition Packet, [property: JsonRequired] FieldTransition FieldEntry, [property: JsonRequired] NativeScriptedWorldLoadReceipt Load, [property: JsonRequired] NativeWorldPathCamera Camera, [property: JsonRequired] ushort CameraYaw96, [property: JsonRequired] byte EntityType105, [property: JsonRequired] byte Channel106, [property: JsonRequired] byte Channel107, [property: JsonRequired] byte AnimationVariant108, [property: JsonRequired] byte MapDisplayMode110, [property: JsonRequired] short MapLocationId, [property: JsonRequired] NativeSavedWorldPose Pose0)
{
    public const string SaveRule = "ff8-en-scripted-field-return-549e80-mode1-2-v1";
    public static NativeSavedWorldScriptedReturn Create(NativeScriptedWorldRuntimeState scripted, ushort lastField, ushort story, short location, FieldTransition fieldEntry)
    {
        scripted.Load.Validate();
        if (!scripted.Path.EndpointRequested || scripted.Path.Callback != scripted.Path.EndpointCallback || scripted.Path.Fade != 0)
        {
            throw new InvalidOperationException("Native scripted save belongs to the actual rail endpoint before departure fade.");
        }
        var receipt = new NativeSavedWorldScriptedReturn(1, SaveRule, scripted.Path.Callback, lastField, story, scripted.Path.Packet, fieldEntry, scripted.Load, scripted.Path.Camera, CameraYaw(scripted.Path.Camera), scripted.Path.EntityType, 255, 255, scripted.Load.AnimationVariant, scripted.Load.MapDisplayMode == 3 ? (byte)0 : scripted.Load.MapDisplayMode, location, ExpectedPose0(scripted.Path, scripted.Load));
        receipt.Validate();
        return receipt;
    }

    internal static NativeSavedWorldPose ExpectedPose0(NativeWorldScriptedPathState path, NativeScriptedWorldLoadReceipt load)
    {
        if (path.Mode == 1)
        {
            return load.FootRecord0.Pose;
        }
        NativeWorldPathPosition prior = path.EndpointPriorEntityPosition ?? throw new InvalidDataException("Native mode2 save requires the previous callback's committed dense foot pose.");
        return NativeScriptedWorldRuntimeState.FootPoseFromBody(prior, load.RetainedBodyYaw);
    }

    public static ushort CameraYaw(NativeWorldPathCamera camera)
    {
        // 552EB0 uses signed WORD target-minus-eye; HIWORD203ED00 is atan2(dx,dz).
        NativeWorldPathPosition eye = camera.Eye ?? throw new InvalidDataException("Native scripted saved camera has no executed eye.");
        int dx = unchecked((short)(camera.Target.X - eye.X));
        int dz = unchecked((short)(camera.Target.Z - eye.Z));
        return unchecked((ushort)NativeWorldScriptedPathMath.NativeAngle(dx, dz));
    }

    public void Validate()
    {
        Load?.Validate();
        if (Version != 1 || Rule != SaveRule || SourceWorldFrame < 30 || Load is null || StoryWord < Load.FootRecord0.MinimumStory || StoryWord >= Load.FootRecord0.MaximumStory || Packet is null || unchecked((byte)Packet.Mode) != EntityType105 || unchecked((byte)Packet.EntryIndex) >= 14 || unchecked((sbyte)Packet.Extra) < 0 || FieldEntry is null || FieldEntry.FieldId is < 0 or > ushort.MaxValue || Camera is null || CameraYaw96 != CameraYaw(Camera) || EntityType105 is not (>= 16 and <= 22 or 65 or 66) || Channel106 != 255 || Channel107 != 255 || AnimationVariant108 != Load.AnimationVariant || MapDisplayMode110 != (Load.MapDisplayMode == 3 ? 0 : Load.MapDisplayMode) || MapLocationId is < sbyte.MinValue or > sbyte.MaxValue || Pose0 is null || EntityType105 is >= 16 and <= 22 && Pose0.Yaw != Load.RetainedBodyYaw || EntityType105 is 65 or 66 && Pose0 != Load.FootRecord0.Pose)
        {
            throw new InvalidDataException("Invalid native scripted save receipt.");
        }
    }
}

public sealed record NativeScriptedWorldRuntimeState([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] NativeWorldScriptedPathState Path, [property: JsonRequired] NativeScriptedWorldLoadReceipt Load, [property: JsonRequired] NativeSavedWorldScriptedReturn? Return)
{
    public const string RuntimeRule = "ff8-en-scripted-mode1-2-world-runtime-v1";

    [JsonIgnore]
    public NativeSavedWorldPose CurrentFootPose0 => Path.Mode == 1 ? Load.FootRecord0.Pose : FootPoseFromBody(Path.EntityPosition, Load.RetainedBodyYaw);

    internal static NativeSavedWorldPose FootPoseFromBody(NativeWorldPathPosition body, ushort physicalYaw) => new(body.X, unchecked(-body.Z), unchecked((short)body.Y), physicalYaw);

    public void Validate()
    {
        Load?.Validate();
        Return?.Validate();
        if (Version != 1 || Rule != RuntimeRule || Path is null || Path.Version != 1 || Path.Rule != NativeWorldScriptedPathState.LoadRule || Path.EntityType is not (>= 16 and <= 22 or 65 or 66) || Path.Mode is not (1 or 2) || Path.Nodes.IsDefault || Path.Nodes.Length != Path.NodeCount || Path.Nodes.Any(node => node is null) || Path.Presentation.IsDefault || Path.Presentation.Length != Path.NodeCount || Path.Presentation.Any(pose => pose is null) || Path.Packet is null || Path.Callback < 0 || Path.Fade is < 0 or > 256 || Path.Fade % 16 != 0 || Path.EndpointRequested && (Path.EndpointCallback is null or < 30 || Path.Callback != Path.EndpointCallback + Path.Fade / 16 || Path.CameraCounter != 0) || !Path.EndpointRequested && (Path.EndpointCallback is not null || Path.Fade != 0 || Path.CameraCounter != Path.Callback + 1) || Load is null || Path.EndpointRequested != (Return is not null) || Return is not null && (Return.SourceWorldFrame != Path.EndpointCallback || Return.Packet != Path.Packet || Return.Load != Load || Return.Camera != Path.Camera))
        {
            throw new InvalidDataException("Scripted runtime and actual endpoint save authority disagree.");
        }
        if (Path.EndpointRequested != (Path.EndpointPriorEntityPosition is not null) || Return is not null && Return.Pose0 != NativeSavedWorldScriptedReturn.ExpectedPose0(Path, Load))
        {
            throw new InvalidDataException("Native scripted saved foot differs from its pre-publication endpoint body witness.");
        }
    }

    public WorldState ToWorldState(WorldState? previous = null)
    {
        Validate();
        return (previous ?? new WorldState()) with { Frame = Path.Callback, X = Path.EntityPosition.X, Z = unchecked(-Path.EntityPosition.Z), NativeHeight = Path.EntityPosition.Y, BodyYaw = Load.RetainedBodyYaw, CameraYaw = NativeSavedWorldScriptedReturn.CameraYaw(Path.Camera), CameraVelocity = Load.RetainedCameraVelocity, NativeFootRuntime = null, NativeCarRuntime = null, NativeScriptedRuntime = this, GroundType = null };
    }
}
