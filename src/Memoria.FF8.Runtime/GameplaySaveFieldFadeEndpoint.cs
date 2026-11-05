using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Terminal field presentation, including the black mode observed by player movement. No interpolation phase is retained.</summary>
public sealed record GameplaySaveFieldFadeEndpoint([property: JsonRequired] short Mode, [property: JsonRequired] short Red, [property: JsonRequired] short Green, [property: JsonRequired] short Blue, [property: JsonRequired] byte SyncMode)
{
    public void Validate()
    {
        if (Mode is not (0 or 3 or 4 or 7 or 8))
        {
            throw new InvalidDataException("Gameplay field fade requires a supported terminal presentation mode.");
        }
    }
}
