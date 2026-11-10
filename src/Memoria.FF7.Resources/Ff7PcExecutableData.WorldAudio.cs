using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7PcExecutableData
{
    public static IReadOnlyList<byte> LoadWorldAudioSongs(Ff7Installation installation, int area)
    {
        uint address = area switch
        {
            0 or 9 => 0x96D598,
            >= 1 and <= 8 or 10 => 0x96D5A0,
            11 => 0x96D5A8,
            12 => 0x96D5B0,
            _ => throw new ArgumentOutOfRangeException(nameof(area))
        };
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        return Array.AsReadOnly(ReadVirtual(stream, pe, address, 8));
    }
}
