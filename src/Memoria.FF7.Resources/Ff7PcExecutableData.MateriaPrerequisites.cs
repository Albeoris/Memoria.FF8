using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7PcExecutableData
{
    public IReadOnlyList<IReadOnlyList<byte>> NativeMateriaRequirementGroups { get; private init; } = [];

    private static IReadOnlyList<IReadOnlyList<byte>> ReadMateriaRequirementGroups(Stream stream, PEReader pe)
    {
        return Array.AsReadOnly(new IReadOnlyList<byte>[]
        {
            Array.AsReadOnly(ReadVirtual(stream, pe, 0x91F078, 21)),
            Array.AsReadOnly(ReadVirtual(stream, pe, 0x91F090, 16)),
            Array.AsReadOnly(ReadVirtual(stream, pe, 0x91F0A0, 7)),
            Array.AsReadOnly(ReadVirtual(stream, pe, 0x91F0A8, 2))
        });
    }
}
