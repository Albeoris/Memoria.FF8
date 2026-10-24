using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSoundGainRequest([property: JsonRequired] int RawDuration, [property: JsonRequired] int RawGain, [property: JsonRequired] int DurationMilliseconds, [property: JsonRequired] byte Gain, [property: JsonRequired] byte FirstVoice, [property: JsonRequired] byte VoiceCount, [property: JsonRequired] NativeGlobalSoundBackend Backend)
{
    public static NativeBattleSoundGainRequest Create46BEC0(NativeGlobalSoundProcess process, int duration, int gain)
    {
        ArgumentNullException.ThrowIfNull(process);
        process.Validate();
        int milliseconds = unchecked((byte)duration) * 10;
        // TODO(TD-003): the explicit bufferless backend records requests without inventing native voice buffers or gain interpolation.
        return new(duration, gain, milliseconds < 50 ? 0 : milliseconds, unchecked((byte)(gain & 127)), 1, 29, process.Backend);
    }

    public void Validate()
    {
        int milliseconds = unchecked((byte)RawDuration) * 10;
        if (DurationMilliseconds != (milliseconds < 50 ? 0 : milliseconds) || Gain != (RawGain & 127) || FirstVoice != 1 || VoiceCount != 29 || Backend != NativeGlobalSoundBackend.Bufferless)
        {
            throw new InvalidDataException("Native46BEC0 requires its original duration byte, seven-bit gain, voices1 through29 and explicit sound backend.");
        }
    }
}
