using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed record Ff7ChocoboClass(short TopSpeed, short TopSpeedRange, short CruiseSpeed, short CruiseSpeedRange);
public sealed record Ff7ChocoboNativeData(IReadOnlyList<Ff7ChocoboClass> Classes, IReadOnlyList<short> Angles, IReadOnlyList<byte[]> Names, byte[] SpecialName, IReadOnlyList<int> AnimationLengths, Ff7BattleCameraMath Trigonometry);

public sealed partial class Ff7PcExecutableData
{
    public static Ff7ChocoboNativeData LoadChocoboData(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] classData = ReadVirtual(stream, pe, 0x97A3E0, 32);
        Ff7ChocoboClass[] classes = new Ff7ChocoboClass[4];
        for (int index = 0; index < classes.Length; index++)
        {
            ReadOnlySpan<byte> row = classData.AsSpan(index * 8, 8);
            classes[index] = new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]), BinaryPrimitives.ReadInt16LittleEndian(row[6..]));
        }
        byte[] angleData = ReadVirtual(stream, pe, 0x97CDA0, 8193 * 2);
        short[] angles = new short[8193];
        for (int index = 0; index < angles.Length; index++)
        {
            angles[index] = BinaryPrimitives.ReadInt16LittleEndian(angleData.AsSpan(index * 2));
        }
        byte[] nameData = ReadVirtual(stream, pe, 0x97CC58, 45 * 7);
        byte[][] names = new byte[45][];
        for (int index = 0; index < names.Length; index++)
        {
            names[index] = nameData.AsSpan(index * 7, 7).ToArray();
        }
        byte[] animationData = ReadVirtual(stream, pe, 0x96EBC0, 6 * 4);
        int[] animationLengths = new int[6];
        for (int index = 0; index < animationLengths.Length; index++)
        {
            animationLengths[index] = BinaryPrimitives.ReadInt32LittleEndian(animationData.AsSpan(index * 4));
        }
        return new(Array.AsReadOnly(classes), Array.AsReadOnly(angles), Array.AsReadOnly(names), ReadVirtual(stream, pe, 0x97C8A8, 8), Array.AsReadOnly(animationLengths), LoadBattleCameraMath(installation));
    }
}
