using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeMusicPlaybackDecision([property: JsonRequired] int Channel, [property: JsonRequired] int? MusicId, [property: JsonRequired] bool PlayRequested, [property: JsonRequired] bool StopRequested, [property: JsonRequired] bool ConcertRequested, [property: JsonRequired] int? PlayGain, [property: JsonRequired] int? FinalGain)
{
    public void Validate()
    {
        if ((uint)Channel > 1 || PlayRequested && (MusicId is null || PlayGain is null || !StopRequested || ConcertRequested) || ConcertRequested && (Channel != 0 || MusicId != 43 || !StopRequested || PlayGain is not null || FinalGain is not null))
        {
            throw new InvalidDataException("Native music decision lost its physical channel, decoded ID or actual stop/play/concert branch.");
        }
    }
}
