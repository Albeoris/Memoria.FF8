using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7WorldCameraProfile(int Pitch, int Distance);
public sealed record Ff7WorldPresentationData(IReadOnlyList<Ff7WorldCameraProfile> CameraProfiles, IReadOnlyList<short> ArcAngles, float DegreesPerAngleUnit, double RadiansPerDegree);
public sealed record Ff7WorldCharacterDescriptor(string HrcName, int ScaleWord, IReadOnlyList<string> Animations)
{
    public (short Red, short Green, short Blue)? ColorOffset { get; init; }
    public bool Transparent { get; init; }

    public float Scale => Math.Max(1, ScaleWord / 512f) * 15.2f;

    public Ff7FieldModelResources Load(Ff7WorldResources resources)
    {
        return Ff7FieldModelResources.Load(resources.Archive, new PcFieldModelDescriptor(HrcName, 0, HrcName, ScaleWord.ToString(System.Globalization.CultureInfo.InvariantCulture), Array.Empty<PcFieldLight>(), 0, Animations));
    }
}

public sealed partial class Ff7PcExecutableData
{
    internal static int[] LoadWorldTextureAnimationIds(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] source = ReadVirtual(stream, pe, 0x96B448, 44);
        return Enumerable.Range(0, 22).Select(index => (int)BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(index * 2))).ToArray();
    }

    public static Ff7WorldPresentationData LoadWorldPresentation(Ff7Installation installation, Ff7WorldResources resources)
    {
        byte[] source = resources.ReadRequired("atn.tbl");
        if (source.Length != 1024)
        {
            throw new InvalidDataException("FFVII world camera requires its complete inverse-angle table.");
        }
        short[] angles = new short[512];
        for (int index = 0; index < angles.Length; index++)
        {
            angles[index] = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(index * 2));
        }
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        float degrees = BitConverter.ToSingle(ReadVirtual(stream, pe, 0x7B7A60, 4));
        double radians = BitConverter.ToDouble(ReadVirtual(stream, pe, 0x7B7AD8, 8));
        return new(LoadWorldCameraProfiles(installation), Array.AsReadOnly(angles), degrees, radians);
    }

    public static IReadOnlyList<Ff7WorldCameraProfile> LoadWorldCameraProfiles(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] pitches = ReadVirtual(stream, pe, 0x969A58, 16);
        byte[] distances = ReadVirtual(stream, pe, 0x969A68, 16);
        return Array.AsReadOnly(Enumerable.Range(0, 4).Select(index => new Ff7WorldCameraProfile(BinaryPrimitives.ReadInt32LittleEndian(pitches.AsSpan(index * 4)), BinaryPrimitives.ReadInt32LittleEndian(distances.AsSpan(index * 4)))).ToArray());
    }

    public static Ff7WorldCharacterDescriptor LoadWorldCharacterDescriptor(Ff7Installation installation, int character, int world = 0, int? tint = null, int? resourceArea = null)
    {
        return TryLoadWorldCharacterDescriptor(installation, character, out Ff7WorldCharacterDescriptor? descriptor, world, tint, resourceArea) ? descriptor! : throw new NotSupportedException($"FFVII world {world} model {character} has no original descriptor in the selected resource area.");
    }

    public static bool TryLoadWorldCharacterDescriptor(Ff7Installation installation, int character, out Ff7WorldCharacterDescriptor? descriptor, int world = 0, int? tint = null, int? resourceArea = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(character);
        if (character >= 32)
        {
            descriptor = null;
            return false;
        }
        int area = resourceArea ?? (world == 0 ? 0 : world + 9);
        if (area is < 0 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(resourceArea));
        }
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        uint address;
        if (character < 3)
        {
            address = character switch { 0 => 0x96B3D0, 1 => 0x96B3E8, _ => 0x96B400 };
        }
        else
        {
            int slot = unchecked((sbyte)ReadVirtual(stream, pe, 0x96DF10u + (uint)(area * 32 + character), 1)[0]);
            if (slot < 1)
            {
                descriptor = null;
                return false;
            }
            (uint table, int tableCount) = area switch
            {
                0 => (0x96A910u, 10),
                1 => (0x96AAF0u, 13),
                2 => (0x96ABF8u, 13),
                3 => (0x96AD00u, 13),
                4 => (0x96AE08u, 13),
                5 => (0x96AF10u, 13),
                6 => (0x96B018u, 13),
                7 => (0x96B120u, 12),
                8 => (0x96B210u, 12),
                9 or 10 => (0x96AAC8u, 2),
                11 => (0x96B300u, 7),
                _ => (0x96B390u, 3)
            };
            if (slot > tableCount)
            {
                throw new InvalidDataException("FFVII world model mapping exceeds its original resource table.");
            }
            address = table + (uint)((slot - 1) * 20);
        }
        byte[] row = ReadVirtual(stream, pe, address, 20);
        string hrc = ReadWorldPresentationName(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(4))).TrimStart('/');
        int count = row[8];
        byte[] pointers = ReadVirtual(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(16)), checked(count * 4));
        string[] animations = new string[count];
        for (int index = 0; index < count; index++)
        {
            animations[index] = ReadWorldPresentationName(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(pointers.AsSpan(index * 4)));
        }
        string name = ReadWorldPresentationName(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(row));
        descriptor = new(hrc, BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(12)), Array.AsReadOnly(animations)) { Transparent = name.Equals("barrier", StringComparison.Ordinal) };
        int? colorRow = (world, character, tint) switch
        {
            (0, 4, _) => 0,
            (0, 19, >= 0 and <= 5) => tint,
            (2, 13, 5 or 6) => tint,
            _ => null
        };
        if (colorRow is int paletteRow)
        {
            byte[] color = ReadVirtual(stream, pe, 0x96DD78u + (uint)paletteRow * 8, 6);
            descriptor = descriptor with { ColorOffset = (BinaryPrimitives.ReadInt16LittleEndian(color), BinaryPrimitives.ReadInt16LittleEndian(color.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(color.AsSpan(4))) };
        }
        return true;
    }

    private static string ReadWorldPresentationName(Stream stream, PEReader pe, uint address)
    {
        byte[] bytes = ReadVirtual(stream, pe, address, 64);
        int length = Array.IndexOf(bytes, (byte)0);
        if (length < 1 || bytes.AsSpan(0, length).ContainsAnyInRange((byte)128, byte.MaxValue))
        {
            throw new InvalidDataException("FFVII world presentation resource identity is invalid.");
        }
        string name = Encoding.ASCII.GetString(bytes, 0, length);
        if (Path.GetFileName(name.TrimStart('/')) != name.TrimStart('/'))
        {
            throw new InvalidDataException("FFVII world presentation resource identity contains a directory.");
        }
        return name;
    }
}
