using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSavedWorldUnsupportedEntry([property: JsonRequired] ImmutableArray<NativeSavedWorldPose> Poses, [property: JsonRequired] ushort CameraWord96, [property: JsonRequired] byte EntryMode104, [property: JsonRequired] byte EntityType105, [property: JsonRequired] byte Channel106, [property: JsonRequired] byte Channel107, [property: JsonRequired] byte AnimationVariant108, [property: JsonRequired] byte MapDisplayMode110)
{
    public void Validate()
    {
        if (Poses.IsDefault || Poses.Length != 8 || Poses.Any(pose => pose is null))
        {
            throw new InvalidDataException("A dormant native transport requires all original pose records and auxiliaries.");
        }
    }
}

public sealed partial record NativeSavedWorldState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldUnsupportedEntry? UnsupportedEntry { get; init; }
}
