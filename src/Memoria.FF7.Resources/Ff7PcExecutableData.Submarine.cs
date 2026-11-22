using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7PcExecutableData
{
    public static Ff7SubmarineResources LoadSubmarineResources(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] terrain = ReadVirtual(stream, pe, 0x996F10, 64 * 64);
        byte[] routes = ReadVirtual(stream, pe, 0x998718, 128 * 32);
        byte[] routeDefaults = ReadVirtual(stream, pe, 0x988CF8, 128 * 32);
        byte[] fleet = ReadVirtual(stream, pe, 0x997F6C, 12 * 48);
        byte[] player = ReadVirtual(stream, pe, 0x988524, 40);
        byte[] speeds = ReadVirtual(stream, pe, 0x9870B0, 19 * 4);
        byte[] types = ReadVirtual(stream, pe, 0x987220, 5 * 0x38);
        byte[] motion0 = ReadVirtual(stream, pe, 0x987120 - 32, 33 * 2);
        byte[] motion1 = ReadVirtual(stream, pe, 0x987168 - 32, 33 * 2);
        byte[] motion2 = ReadVirtual(stream, pe, 0x9871B0 - 32, 33 * 2);
        byte[] motion3 = ReadVirtual(stream, pe, 0x9871F8 - 32, 33 * 2);
        byte[] roots = ReadVirtual(stream, pe, 0x989CD8, 256 * 2);
        byte[] reciprocals = ReadVirtual(stream, pe, 0x989E58, 256 * 2);
        Dictionary<int, int> initialGlobals = new();
        foreach ((uint start, int length) in new[] { (0x987000u, 0xB0), (0x9884F8u, 0x2C), (0x9873D8u, 4) })
        {
            byte[] values = ReadVirtual(stream, pe, start, length);
            for (int offset = 0; offset < values.Length; offset += 4)
            {
                initialGlobals.Add(checked((int)start + offset), BinaryPrimitives.ReadInt32LittleEndian(values.AsSpan(offset)));
            }
        }
        return Ff7SubmarineResources.Read(terrain, routes, fleet, player, speeds, types, motion0, motion1, motion2, motion3, roots, reciprocals, initialGlobals, routeDefaults);
    }
}
