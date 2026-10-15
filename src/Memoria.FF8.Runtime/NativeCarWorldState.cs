using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native ordinary car entity33 state from548080/557A90, independent of normal foot animation.</summary>
public sealed record NativeCarWorldRuntimeState
{
    public const string LoadRule = "ff8-en-ordinary-car-544860-548080-v4";
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required byte EntityType { get; init; }
    [JsonRequired]
    public required byte ShapeId { get; init; }
    [JsonRequired]
    public required int Speed { get; init; }
    [JsonRequired]
    public required int CameraFollowSpeed { get; init; }
    [JsonRequired]
    public required int TerrainTiltBlend { get; init; }
    [JsonRequired]
    public required int ModelTilt { get; init; }
    [JsonRequired]
    public required byte MapDisplayMode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldGroundCacheState? GroundCache { get; init; }
    public int? GroundYaw { get; init; }
    [JsonRequired]
    public int? MovementMagnitude20409F4 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeTrainWorldAllocation? Allocation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldFootCameraState? ProjectionCamera { get; init; }

    public static NativeCarWorldRuntimeState Create(NativeSavedWorldState saved, NativeWorldGroundCacheState cache, NativeWorldProcess process, byte entityType = 33, NativeTrainWorldAllocation? allocation = null, NativeWorldFootCameraState? projectionCamera = null)
    {
        saved.Validate();
        cache.Validate();
        process.Validate();
        byte shape = entityType switch { 32 => 74, 33 => 73, _ => throw new NotSupportedException("Native ordinary car initialization requires its actual supported entity type.") };
        var result = new NativeCarWorldRuntimeState { Version = 4, Rule = LoadRule, EntityType = entityType, ShapeId = shape, Speed = 0, CameraFollowSpeed = process.CarCameraFollowSpeed, TerrainTiltBlend = 0, ModelTilt = 0, MapDisplayMode = saved.ReadMapDisplayModeForFreshFootLoader(), GroundCache = cache, GroundYaw = 0, MovementMagnitude20409F4 = 0, Allocation = allocation, ProjectionCamera = projectionCamera };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Version != 4 || Rule != LoadRule || EntityType is not (32 or 33) || ShapeId != (EntityType == 32 ? 74 : 73) || Speed is < -64 or > 64 || CameraFollowSpeed is < 0 or > 128 || TerrainTiltBlend is < 0 or > 64 || ModelTilt is < -128 or > 192 || MapDisplayMode != 0 || GroundCache is null || GroundYaw is null or < -1120 or > 1120 || MovementMagnitude20409F4 is null or < 0 or > 64)
        {
            throw new InvalidDataException("Unsupported native ordinary car runtime state.");
        }
        GroundCache.Validate();
        if (EntityType == 32 && (Allocation is null || ProjectionCamera is null) || EntityType == 33 && (Allocation is not null || ProjectionCamera is not null))
        {
            throw new InvalidDataException("Native car object allocation differs from its executed loader contract.");
        }
        Allocation?.Validate();
        ProjectionCamera?.Validate();
    }
}

/// <summary>Executed549E80(mode1) car save and vehicle shape retirement; historical foot bytes are not reused.</summary>
public sealed record NativeSavedWorldCarReturn
{
    public const string SaveRule = "ff8-en-car-field-return-549e80-mode1-v2";
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required long SourceWorldFrame { get; init; }
    [JsonRequired]
    public required ushort LastFieldId { get; init; }
    [JsonRequired]
    public required ushort StoryWord { get; init; }
    [JsonRequired]
    public required NativeSavedWorldPose Pose { get; init; }
    [JsonRequired]
    public required ushort CameraYaw96 { get; init; }
    [JsonRequired]
    public required byte EntityType105 { get; init; }
    [JsonRequired]
    public required byte Channel106 { get; init; }
    [JsonRequired]
    public required byte Channel107 { get; init; }
    [JsonRequired]
    public required byte AnimationVariant108 { get; init; }
    [JsonRequired]
    public required byte MapDisplayMode110 { get; init; }
    [JsonRequired]
    public required short MapLocationId { get; init; }

    public NativeSemanticWorldTravel? SemanticTravel { get; init; }

    public static NativeSavedWorldCarReturn Create(WorldState world, ushort lastField, ushort story, short location)
    {
        world.NativeCarRuntime?.Validate();
        if (world.NativeCarRuntime is null)
        {
            throw new NotSupportedException("A car save requires executed native car runtime provenance.");
        }
        var result = new NativeSavedWorldCarReturn { Version = 2, Rule = SaveRule, SourceWorldFrame = world.Frame, LastFieldId = lastField, StoryWord = story, Pose = new NativeSavedWorldPose(world.X, world.Z, checked((short)world.NativeHeight), checked((ushort)world.BodyYaw)), CameraYaw96 = checked((ushort)world.CameraYaw), EntityType105 = world.NativeCarRuntime.EntityType, Channel106 = 255, Channel107 = 255, AnimationVariant108 = 0, MapDisplayMode110 = world.NativeCarRuntime.MapDisplayMode, MapLocationId = location };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Version != 2 || (SemanticTravel is null ? Rule != SaveRule : Rule != NativeSemanticWorldTravel.ReturnRule) || SourceWorldFrame < 0 || Pose is null || Pose.X is < -131072 or >= 131072 || Pose.RawZ is < -98304 or >= 98304 || Pose.Yaw >= 4096 || CameraYaw96 >= 4096 || EntityType105 is not (32 or 33) || Channel106 != 255 || Channel107 != 255 || AnimationVariant108 != 0 || MapDisplayMode110 != 0 || MapLocationId is < sbyte.MinValue or > sbyte.MaxValue)
        {
            throw new InvalidDataException("Invalid native car-to-field save receipt.");
        }
        SemanticTravel?.ValidateReturn(StoryWord, EntityType105, Pose);
    }
}
