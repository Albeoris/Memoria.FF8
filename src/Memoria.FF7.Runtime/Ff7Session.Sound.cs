using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private readonly Dictionary<int, Ff7NativeSoundDescription> nativeSoundDescriptions = [];

    private Ff7NativeSoundDescription ReadNativeSoundDescription(int soundId)
    {
        if (!nativeSoundDescriptions.TryGetValue(soundId, out Ff7NativeSoundDescription? description))
        {
            Ff7SoundSample sample = Ff7SoundResources.Read(installation, checked(soundId - 1));
            description = new(sample.NativeSampleRate, !sample.Loop && sample.CompressedByteLength > 0x20000, sample.Loop);
            nativeSoundDescriptions.Add(soundId, description);
        }
        return description;
    }
}
