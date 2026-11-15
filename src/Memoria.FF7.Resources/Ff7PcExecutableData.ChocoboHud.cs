using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Memoria.FF7.Resources;

public sealed record Ff7ChocoboHudSprite(int Slot, int X, int Y, int Width, int Height, float U, float V, float TextureWidth, float TextureHeight, int Palette, float Depth);
public sealed record Ff7ChocoboHudData(IReadOnlyList<string> Textures, IReadOnlyList<Ff7ChocoboHudSprite> Crowns, IReadOnlyList<Ff7ChocoboHudSprite> Controls, Ff7ChocoboHudSprite Fade, IReadOnlyList<int> FaceSelectors, IReadOnlyList<uint> MarkerColors, IReadOnlyList<uint> PlayerMarkerColors);

public sealed partial class Ff7PcExecutableData
{
    public static Ff7ChocoboHudData LoadChocoboHudData(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] names = ReadVirtual(stream, pe, 0x971F88, 11 * 4);
        string[] textures = new string[11];
        for (int index = 0; index < textures.Length; index++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(names.AsSpan(index * 4));
            byte[] text = ReadVirtual(stream, pe, pointer, 256);
            int terminator = Array.IndexOf(text, (byte)0);
            if (terminator < 1)
            {
                throw new InvalidDataException("FFVII Chocobo HUD resource name lacks its native terminator.");
            }
            textures[index] = Path.ChangeExtension(Encoding.ASCII.GetString(text, 0, terminator), ".tex");
        }
        byte[] selectorBytes = ReadVirtual(stream, pe, 0x96E8A8, 12 * 4);
        int[] selectors = Enumerable.Range(0, 12).Select(index => BinaryPrimitives.ReadInt32LittleEndian(selectorBytes.AsSpan(index * 4))).ToArray();
        return new(Array.AsReadOnly(textures), Array.AsReadOnly(new[] { Sprite(0x96E8D8), Sprite(0x96E920) }), Array.AsReadOnly(new[] { Sprite(0x96E968), Sprite(0x96E9B0) }), Sprite(0x97A498), Array.AsReadOnly(selectors), Colors(0x96E878, 6), Colors(0x96E890, 5));

        Ff7ChocoboHudSprite Sprite(uint address)
        {
            byte[] bytes = ReadVirtual(stream, pe, address, 72);
            ReadOnlySpan<byte> data = bytes;
            return new(BinaryPrimitives.ReadInt32LittleEndian(data), BinaryPrimitives.ReadUInt16LittleEndian(data[20..]), BinaryPrimitives.ReadUInt16LittleEndian(data[22..]), BinaryPrimitives.ReadInt32LittleEndian(data[40..]), BinaryPrimitives.ReadInt32LittleEndian(data[44..]), BinaryPrimitives.ReadSingleLittleEndian(data[12..]), BinaryPrimitives.ReadSingleLittleEndian(data[16..]), BinaryPrimitives.ReadSingleLittleEndian(data[52..]), BinaryPrimitives.ReadSingleLittleEndian(data[56..]), BinaryPrimitives.ReadInt32LittleEndian(data[48..]), BinaryPrimitives.ReadSingleLittleEndian(data[36..]));
        }

        IReadOnlyList<uint> Colors(uint address, int count)
        {
            byte[] bytes = ReadVirtual(stream, pe, address, count * 4);
            return Array.AsReadOnly(Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToArray());
        }
    }
}
