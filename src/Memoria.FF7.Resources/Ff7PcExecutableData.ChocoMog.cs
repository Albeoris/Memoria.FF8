using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed record Ff7BattleSummonCameraResources(IReadOnlyList<short> ChocoMogFirst, IReadOnlyList<short> ChocoMogSecond, IReadOnlyList<short> FatChocoboFirst, IReadOnlyList<short> FatChocoboSecond)
{
    public IReadOnlyList<short> Script(bool fatChocobo, bool firstVariant) => fatChocobo
        ? firstVariant ? FatChocoboFirst : FatChocoboSecond
        : firstVariant ? ChocoMogFirst : ChocoMogSecond;
}

public sealed partial class Ff7PcExecutableData
{
    public static IReadOnlyList<short> LoadChocoMogCamera(Ff7Installation installation, bool firstVariant, bool fatChocobo = false)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        uint address = fatChocobo
            ? firstVariant ? 0x8981F0u : 0x898270u
            : firstVariant ? 0x898738u : 0x8987D8u;
        return ReadSummonCamera(stream, pe, address, fatChocobo ? 128 : 160);
    }

    private static IReadOnlyList<short> ReadSummonCamera(FileStream stream, PEReader pe, uint address, int length)
    {
        byte[] bytes = ReadVirtual(stream, pe, address, length);
        short[] script = new short[bytes.Length / sizeof(short)];
        for (int index = 0; index < script.Length; index++)
        {
            script[index] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * sizeof(short)));
        }
        return Array.AsReadOnly(script);
    }
}
