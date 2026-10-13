using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private GlobalSoundResource? ResolveGlobalSound(int sampleIndex)
    {
        GlobalAudioSampleDescriptor sample;
        try
        {
            sample = GlobalAudioFormat.Read(assets.GameDirectory, sampleIndex);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return null;
        }
        return CreateMutedGlobalSoundResource(sample);
    }

    internal static GlobalSoundResource? CreateMutedGlobalSoundResource(GlobalAudioSampleDescriptor sample)
    {
        GlobalAudioWaveFormat wave = sample.WaveFormat;
        if (wave.Tag is not (1 or 2) || wave.Channels == 0 || wave.SampleRate == 0)
        {
            return null;
        }
        int decodedBits = wave.Tag == 2 ? 16 : wave.BitsPerSample;
        int? delay = null;
        if (sample.LoopStartPcmByte != 0)
        {
            int pcmBlockAlign = 2 * wave.Channels;
            if (decodedBits != 16 || pcmBlockAlign > ushort.MaxValue || sample.LoopEndPcmByte <= sample.LoopStartPcmByte || sample.LoopStartPcmByte % pcmBlockAlign != 0 || sample.LoopEndPcmByte % pcmBlockAlign != 0)
            {
                return null;
            }
            // Native 46AFF2..46B008 uses unsigned shifts, independent of channels and pitch.
            uint denominator = unchecked(wave.SampleRate << 4) / 1000;
            uint numerator = unchecked(sample.LoopStartPcmByte << 3);
            if (denominator == 0 || numerator / denominator > int.MaxValue)
            {
                return null;
            }
            delay = (int)(numerator / denominator);
        }
        var policy = new GlobalSoundLoopPolicy(sample.PlaybackFlags, sample.LoopStartPcmByte, sample.LoopEndPcmByte, wave.SampleRate, decodedBits, wave.Channels, delay);
        return new GlobalSoundResource(sample.SampleIndex, GlobalSoundBackendStatus.Muted, policy);
    }
}
