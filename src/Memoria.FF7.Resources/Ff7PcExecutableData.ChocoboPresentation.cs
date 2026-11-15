using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Memoria.FF7.Resources;

public sealed record Ff7ChocoboPresentationModel(string Hrc, IReadOnlyList<string> Animations, bool Transparent, bool PatchYaw)
{
    public int StoryCharacter { get; init; } = -1;
}
public sealed record Ff7ChocoboSceneryDescriptor(short X, short Y, short Z, ushort Flags, byte Model, byte RotationX, byte RotationY, byte RotationZ, short Frame, short Increment);
public sealed record Ff7ChocoboPresentationData(IReadOnlyList<int> SpriteMap, IReadOnlyList<string> SpriteTextures, IReadOnlyList<Ff7ChocoboPresentationModel> Models, IReadOnlyList<Ff7ChocoboSceneryDescriptor> Scenery);

public sealed partial class Ff7PcExecutableData
{
    public static Ff7ChocoboPresentationData LoadChocoboPresentationData(Ff7Installation installation, bool shortCourse)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        int[] spriteMap = ReadIntegers(shortCourse ? 0x970350u : 0x96FF50u, shortCourse ? 90 : 255);
        int[] texturePointers = ReadIntegers(shortCourse ? 0x96FCF0u : 0x96FEC0u, shortCourse ? 7 : 36);
        string[] textures = texturePointers.Select(pointer => Path.ChangeExtension(ReadName(unchecked((uint)pointer)), ".tex")).ToArray();
        int[] modelMap = ReadIntegers(shortCourse ? 0x96FB78u : 0x96FA80u, (shortCourse ? 35 : 31) * 2);
        Ff7ChocoboPresentationModel[] models = new Ff7ChocoboPresentationModel[modelMap.Length / 2];
        for (int index = 0; index < models.Length; index++)
        {
            int[] descriptor = ReadIntegers(checked(0x96F330u + (uint)modelMap[index * 2] * 48), 12);
            if (descriptor[0] < 1 || descriptor[0] > 10)
            {
                throw new InvalidDataException("FFVII Chocobo model animation count exceeds its original descriptor layout.");
            }
            string hrc = ReadName(unchecked((uint)descriptor[1]));
            string[] clips = descriptor.Skip(2).Take(descriptor[0]).Select(pointer => Path.ChangeExtension(ReadName(unchecked((uint)pointer)), ".a")).ToArray();
            models[index] = new(hrc, Array.AsReadOnly(clips), modelMap[index * 2 + 1] == 1, shortCourse && index is 0x11 or 0x13 or 0x16 or 0x1F) { StoryCharacter = modelMap[index * 2] switch { 2 => 0, 37 => 8, 38 => 2, _ => -1 } };
        }
        int sceneryCount = shortCourse ? 19 : 15;
        byte[] sceneryBytes = ReadVirtual(stream, pe, shortCourse ? 0x972B68u : 0x972A78u, sceneryCount * 16);
        Ff7ChocoboSceneryDescriptor[] scenery = new Ff7ChocoboSceneryDescriptor[sceneryCount];
        for (int index = 0; index < scenery.Length; index++)
        {
            ReadOnlySpan<byte> data = sceneryBytes.AsSpan(index * 16, 16);
            scenery[index] = new(BinaryPrimitives.ReadInt16LittleEndian(data), BinaryPrimitives.ReadInt16LittleEndian(data[2..]), BinaryPrimitives.ReadInt16LittleEndian(data[4..]), BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), data[15], data[12], data[13], data[14], BinaryPrimitives.ReadInt16LittleEndian(data[10..]), BinaryPrimitives.ReadInt16LittleEndian(data[8..]));
        }
        return new(Array.AsReadOnly(spriteMap), Array.AsReadOnly(textures), Array.AsReadOnly(models), Array.AsReadOnly(scenery));

        int[] ReadIntegers(uint address, int count)
        {
            byte[] bytes = ReadVirtual(stream, pe, address, checked(count * 4));
            int[] values = new int[count];
            for (int index = 0; index < count; index++)
            {
                values[index] = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(index * 4));
            }
            return values;
        }

        string ReadName(uint address)
        {
            byte[] bytes = ReadVirtual(stream, pe, address, 256);
            int terminator = Array.IndexOf(bytes, (byte)0);
            if (terminator < 1)
            {
                throw new InvalidDataException("FFVII Chocobo resource name lacks its native terminator.");
            }
            return Encoding.ASCII.GetString(bytes, 0, terminator);
        }
    }
}
