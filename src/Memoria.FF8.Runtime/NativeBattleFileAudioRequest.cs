using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFileAudioRequest([property: JsonRequired] int Channel, [property: JsonRequired] string ResourceName, [property: JsonRequired] int Gain, [property: JsonRequired] int Result, [property: JsonRequired] NativeGlobalSoundBackend Backend)
{
    public static NativeBattleFileAudioRequest Create46CBB0(NativeGlobalSoundProcess process, int channel, string resourceName, int gain)
    {
        ArgumentNullException.ThrowIfNull(process);
        process.Validate();
        // TODO(TD-003): the explicit bufferless backend records the CD audio request without creating original file handles or streaming buffers.
        NativeBattleFileAudioRequest request = new(channel, resourceName, gain, 0, process.Backend);
        request.Validate();
        return request;
    }

    public void Validate()
    {
        if (Channel != 0 || string.IsNullOrWhiteSpace(ResourceName) || ResourceName.IndexOfAny(['/', '\\', ':', '\0']) >= 0 || Result != 0 || Backend != NativeGlobalSoundBackend.Bufferless)
        {
            throw new InvalidDataException("Native46CBB0 requires its selected channel0, relative CD resource name and explicit bufferless failure result.");
        }
    }
}
