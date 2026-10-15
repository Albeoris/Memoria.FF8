using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeFootstepOrigin([property: JsonRequired] int X, [property: JsonRequired] int Z);
/// <summary>Executed533CD0 model+48 translation, retained until the next enabled model draw.</summary>
public sealed record NativeFootstepModelOrigin([property: JsonRequired] int Version, [property: JsonRequired] long Frame, [property: JsonRequired] int ModelId, [property: JsonRequired] int CameraIndex, [property: JsonRequired] NativeFootstepOrigin? Translation, [property: JsonRequired] string? UnsupportedReason);
public sealed record NativeFootstepProjectionQuery(ActorState Actor, FieldFixedPosition? Position, int ModelId, long Frame);

public sealed partial class EventEngine
{
    public Func<NativeFootstepProjectionQuery, NativeFootstepModelOrigin>? CaptureFootstepModelOrigin { get; set; }
}
