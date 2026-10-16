using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSavedWorldRagnarokReturn([property: JsonRequired] string Rule, [property: JsonRequired] long SourceWorldFrame, [property: JsonRequired] ushort LastFieldId, [property: JsonRequired] ushort StoryWord, [property: JsonRequired] NativeSavedWorldPose Pose2, [property: JsonRequired] ushort CameraYaw96, [property: JsonRequired] byte MapDisplayMode110, [property: JsonRequired] short MapLocationId)
{
    public const string CurrentRule = "ff8-en-ragnarok-field-return-549e80-pose2-v1";

    public static NativeSavedWorldRagnarokReturn Create(WorldState world, ushort lastField, ushort story, short location)
    {
        NativeRagnarokWorldRuntimeState runtime = world.NativeRagnarokRuntime ?? throw new InvalidDataException("Ragnarok return requires its executed current allocation.");
        runtime.RequireSceneState();
        if (runtime.Transfer is not null)
        {
            throw new NotSupportedException("Ragnarok field return requires stable controlled50, outside an unfinished transfer.");
        }
        var result = new NativeSavedWorldRagnarokReturn(CurrentRule, world.Frame, lastField, story, new(world.X, world.Z, unchecked((short)world.NativeHeight), checked((ushort)world.BodyYaw)), checked((ushort)world.CameraYaw), runtime.Allocation.MapDisplayMode == 3 ? (byte)0 : runtime.Allocation.MapDisplayMode, location);
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Rule != CurrentRule || SourceWorldFrame < 0 || Pose2 is null || Pose2.X is < -131072 or >= 131072 || Pose2.RawZ is < -98304 or >= 98304 || Pose2.Yaw >= 4096 || CameraYaw96 >= 4096 || MapDisplayMode110 > 2 || MapLocationId is < sbyte.MinValue or > sbyte.MaxValue)
        {
            throw new InvalidDataException("Ragnarok return lost its actual pose2, camera or ordinary saved auxiliary authority.");
        }
    }
}

public sealed partial record NativeSavedWorldState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldPose? RagnarokPose2 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldRagnarokReturn? RagnarokReturn { get; init; }

    public NativeSavedWorldState WithRagnarokReturn(NativeSavedWorldRagnarokReturn receipt)
    {
        Validate();
        receipt.Validate();
        return this with { RagnarokPose2 = receipt.Pose2, RagnarokReturn = receipt, FootReturn = null, CarReturn = null, GardenReturn = null, ScriptedReturn = null };
    }

    private void ValidateRagnarokReturn()
    {
        if (RagnarokPose2 is not null && (RagnarokPose2.X is < -131072 or >= 131072 || RagnarokPose2.RawZ is < -98304 or >= 98304 || RagnarokPose2.Yaw >= 4096))
        {
            throw new InvalidDataException("Saved Ragnarok pose2 lost native centered coordinates or yaw.");
        }
        RagnarokReturn?.Validate();
        if (RagnarokReturn is not null && (RagnarokReturn.Pose2 != RagnarokPose2 || FootReturn is not null || CarReturn is not null || GardenReturn is not null || ScriptedReturn is not null))
        {
            throw new InvalidDataException("Ragnarok field return conflicts with another saved-world auxiliary publisher.");
        }
    }
}
