using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Executed549E80 Garden pose3/+36 and auxiliary save, independently of car slots4/5.</summary>
public sealed record NativeSavedWorldGardenReturn([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] long SourceWorldFrame, [property: JsonRequired] ushort LastFieldId, [property: JsonRequired] ushort StoryWord, [property: JsonRequired] NativeSavedWorldPose Pose3, [property: JsonRequired] ushort CameraYaw96, [property: JsonRequired] byte EntityType105, [property: JsonRequired] byte Channel106, [property: JsonRequired] byte Channel107, [property: JsonRequired] byte AnimationVariant108, [property: JsonRequired] byte MapDisplayMode110, [property: JsonRequired] short MapLocationId)
{
    public const string SaveRule = "ff8-en-garden-field-return-549e80-pose3-v1";
    public const string OriginalSaveRule = "ff8-original-save-garden-position-v1";

    public static NativeSavedWorldGardenReturn Create(WorldState world, ushort lastField, ushort story, short location)
    {
        NativeGardenWorldRuntimeState garden = world.NativeGardenRuntime ?? throw new InvalidDataException("Native Garden save requires its executed current allocation and motor.");
        garden.Validate();
        if (world.NativeFootRuntime is not null || garden.Transfer is not null)
        {
            throw new NotSupportedException("Active Garden field return requires ordinary controlled48, outside a pending transfer or foot departure.");
        }
        var result = new NativeSavedWorldGardenReturn(1, SaveRule, world.Frame, lastField, story, new NativeSavedWorldPose(world.X, world.Z, unchecked((short)world.NativeHeight), checked((ushort)world.BodyYaw)), checked((ushort)world.CameraYaw), 48, 255, 255, garden.AnimationVariant, garden.MapDisplayMode == 3 ? (byte)0 : garden.MapDisplayMode, location);
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Version != 1 || Rule is not (SaveRule or OriginalSaveRule) || SourceWorldFrame < 0 || Pose3 is null || Pose3.X is < -131072 or >= 131072 || Pose3.RawZ is < -98304 or >= 98304 || Pose3.Yaw >= 4096 || CameraYaw96 >= 4096 || EntityType105 != 48 || Channel106 != 255 || Channel107 != 255 || AnimationVariant108 != 0 || MapDisplayMode110 > 2 || MapLocationId is < sbyte.MinValue or > sbyte.MaxValue)
        {
            throw new InvalidDataException("Native Garden return lost its executed pose3 and actual saved auxiliary bytes.");
        }
    }
}
