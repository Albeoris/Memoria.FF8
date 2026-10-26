using Memoria.FF8.Assets;
using Memoria.FF8.Audio;

namespace Memoria.FF8;

internal static class PortableWaveDecoder
{
    public static byte[] Decode(GlobalAudioWaveFormat format, byte[] compressed)
    {
        return WaveDecoder.Decode(format.Tag, format.Channels, format.BitsPerSample, format.BlockAlign, format.ExtraData.ToArray(), compressed);
    }
}
