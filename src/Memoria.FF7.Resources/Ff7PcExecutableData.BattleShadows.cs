using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7PcExecutableData
{
    public static float LoadBattleShadowGroundHeight(Ff7Installation installation, int locationId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(locationId);
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] initialization = ReadVirtual(stream, pe, 0x5E352E, 14 * 7);
        uint selected = checked(0xC3F680u + (uint)locationId);
        bool elevated = false;
        for (int offset = 0; offset < initialization.Length; offset += 7)
        {
            if (initialization[offset] != 0xC6 || initialization[offset + 1] != 5 || initialization[offset + 6] != 1)
            {
                throw new InvalidDataException("FFVII battle shadow ground initialization differs from its supported native profile.");
            }
            elevated |= BinaryPrimitives.ReadUInt32LittleEndian(initialization.AsSpan(offset + 2)) == selected;
        }
        return elevated ? -10 : -4;
    }
}
