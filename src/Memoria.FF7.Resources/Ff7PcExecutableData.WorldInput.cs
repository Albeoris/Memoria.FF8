using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7PcExecutableData
{
    public static IReadOnlyList<byte> LoadWorldCollisionMasks(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        return Array.AsReadOnly(ReadVirtual(stream, pe, 0x96DDB0, 26 * 8));
    }
}
