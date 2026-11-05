using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Ambient sound selection and terminal controls. Playback buffers, cursors and ramp phase are transient.</summary>
public sealed record GameplaySaveFieldLoopSound([property: JsonRequired] int Voice, [property: JsonRequired] uint EffectId, [property: JsonRequired] byte Volume, [property: JsonRequired] byte Pan)
{
    public void Validate()
    {
        if (Voice is < 0 or >= 30 || Volume > 127 || Pan > 127)
        {
            throw new InvalidDataException("Gameplay ambient sound requires a bounded voice and terminal volume/pan.");
        }
    }
}
