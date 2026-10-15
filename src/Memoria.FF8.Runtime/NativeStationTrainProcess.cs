using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Exact input words for one552AD0 normal-foot camera, captured before the terrain movement query.</summary>
public sealed record NativeWorldFootCameraState([property: JsonRequired] int X, [property: JsonRequired] int RawZ, [property: JsonRequired] int NativeHeight, [property: JsonRequired] int TargetX, [property: JsonRequired] int TargetRawZ, [property: JsonRequired] int TargetNativeHeight, [property: JsonRequired] int Yaw, [property: JsonRequired] short Pitch, [property: JsonRequired] short OffsetY, [property: JsonRequired] short OffsetZ)
{
    public static NativeWorldFootCameraState Create(WorldState world)
    {
        NativeFootWorldRuntimeState foot = world.NativeFootRuntime ?? throw new InvalidDataException("Native foot camera capture requires the executed foot process.");
        NativeWorldForestState forest = foot.Forest ?? throw new InvalidDataException("Native foot camera capture has no current selected pitch.");
        var state = new NativeWorldFootCameraState(world.X, world.Z, world.NativeHeight, world.CameraTargetX, world.CameraTargetZ, world.CameraTargetNativeHeight, world.CameraYaw, forest.CameraPitch, foot.CameraOffsetY, foot.CameraOffsetZ);
        state.Validate();
        return state;
    }

    public NativeWorldPathPosition Origin => new(X, NativeHeight, -RawZ);
    public NativeWorldPathPosition Target => new(TargetX, TargetNativeHeight, -TargetRawZ);

    public void Validate()
    {
        if (X is < -131072 or >= 131072 || RawZ is < -98304 or >= 98304 || Yaw is < 0 or >= 4096 || Pitch is < -512 or > 0 || OffsetZ >= 0)
        {
            throw new InvalidDataException("Native normal-foot camera lost its physical origin, selected angles or retained camera offsets.");
        }
    }
}

/// <summary>The existing545480 train banks after548AF0 releases the riding camera and returns control to foot movement.</summary>
public sealed record NativeStationTrainProcess([property: JsonRequired] int Version, [property: JsonRequired] long Callback, [property: JsonRequired] NativeTrainRailBank Primary, [property: JsonRequired] NativeTrainRailBank? Alternate, [property: JsonRequired] NativeWorldFootCameraState? ProjectionCamera)
{
    public static NativeStationTrainProcess Create(NativeTrainWorldRuntimeState source)
    {
        source.Validate();
        //5484B0 writes WORD28=0 after the choice, including choice0/548AF0.
        return new NativeStationTrainProcess(1, source.Callback, source.Primary with { Pause = 0 }, source.Alternate, null);
    }

    public void Validate(NativeTrainWorldRuntimeState source)
    {
        ProjectionCamera?.Validate();
        if (Version != 1 || Callback < source.Callback || Primary is null || Primary.TrackIndex != source.Primary.TrackIndex || Alternate is null != (source.Alternate is null) || Alternate is not null && Alternate.TrackIndex != source.Alternate!.TrackIndex || Callback == source.Callback && ProjectionCamera is not null || Callback > source.Callback && ProjectionCamera is null)
        {
            throw new InvalidDataException("Native station train process lost its original banks or actual foot callback camera.");
        }
        if (Callback == source.Callback && (!SameBank(Primary, source.Primary with { Pause = 0 }) || !SameBank(Alternate, source.Alternate)))
        {
            throw new InvalidDataException("Native station train transition differs from5484B0's actual stop release.");
        }
    }

    private static bool SameBank(NativeTrainRailBank? first, NativeTrainRailBank? second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }
        return first.TrackIndex == second.TrackIndex && first.ChunkHash == second.ChunkHash && first.Direction == second.Direction && first.Pause == second.Pause && first.Speed == second.Speed && first.Status == second.Status && first.StopCounter == second.StopCounter && first.Nodes.SequenceEqual(second.Nodes) && first.Presentation.SequenceEqual(second.Presentation);
    }
}
