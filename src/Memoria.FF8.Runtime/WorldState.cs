using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The three original kind7 transition packet words, before native byte truncation.</summary>
public sealed record WorldTransition(short EntryIndex, short Mode, short Extra);

/// <summary>Native world coordinates and temporal state; independent from the departing field VM.</summary>
public sealed record WorldState
{
    public long Frame { get; init; }
    public int X { get; init; }
    public int Z { get; init; }
    public int NativeHeight { get; init; }
    public int BodyYaw { get; init; }
    public int CameraYaw { get; init; }
    public int CameraVelocity { get; init; }
    public int CameraTargetX { get; init; }
    public int CameraTargetZ { get; init; }
    public int CameraTargetNativeHeight { get; init; }
    public int AnimationId { get; init; }
    public int AnimationCounter { get; init; }
    public byte? GroundType { get; init; }
    public ushort ReplacementMask { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFootWorldRuntimeState? NativeFootRuntime { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeCarWorldRuntimeState? NativeCarRuntime { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeGardenWorldRuntimeState? NativeGardenRuntime { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeScriptedWorldRuntimeState? NativeScriptedRuntime { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeTrainWorldRuntimeState? NativeTrainRuntime { get; init; }
}
