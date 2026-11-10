using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7PcExecutableData
{
    internal static (byte[] Definitions, uint[] Prices, byte[] MateriaClasses) LoadShopData(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] definitions = ReadVirtual(stream, pe, 0x923418, 80 * 84);
        byte[] encodedPrices = ReadVirtual(stream, pe, 0x924E58, (384 + 96) * 4);
        uint[] prices = new uint[480];
        for (int index = 0; index < prices.Length; index++)
        {
            prices[index] = BinaryPrimitives.ReadUInt32LittleEndian(encodedPrices.AsSpan(index * 4));
        }
        byte[] indices = ReadVirtual(stream, pe, 0x91ABF8, 16);
        byte[] classes = ReadVirtual(stream, pe, 0x91ABF0, indices.Max() + 1);
        return (definitions, prices, indices.Select(index => classes[index]).ToArray());
    }
}
