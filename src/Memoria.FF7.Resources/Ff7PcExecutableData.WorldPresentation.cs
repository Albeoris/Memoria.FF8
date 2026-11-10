using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7WorldCameraProfile(int Pitch, int Distance);
public sealed record Ff7WorldPresentationData(IReadOnlyList<Ff7WorldCameraProfile> CameraProfiles, IReadOnlyList<short> ArcAngles, float DegreesPerAngleUnit, double RadiansPerDegree);
public sealed record Ff7WorldCharacterDescriptor(string HrcName, int ScaleWord, IReadOnlyList<string> Animations)
{
    public float Scale => Math.Max(1, ScaleWord / 512f) * 15.2f;

    public Ff7FieldModelResources Load(Ff7WorldResources resources)
    {
        return Ff7FieldModelResources.Load(resources.Archive, new PcFieldModelDescriptor(HrcName, 0, HrcName, ScaleWord.ToString(System.Globalization.CultureInfo.InvariantCulture), Array.Empty<PcFieldLight>(), 0, Animations));
    }
}

public sealed partial class Ff7PcExecutableData
{
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

    public static Ff7WorldCharacterDescriptor LoadWorldCharacterDescriptor(Ff7Installation installation, int character)
    {
        uint address = character switch
        {
            0 => 0x96B3D0,
            1 => 0x96B3E8,
            2 => 0x96B400,
            _ => throw new NotSupportedException("FFVII world character resources require an original supported leader.")
        };
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] row = ReadVirtual(stream, pe, address, 20);
        string hrc = ReadWorldPresentationName(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(4))).TrimStart('/');
        int count = row[8];
        byte[] pointers = ReadVirtual(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(16)), checked(count * 4));
        string[] animations = new string[count];
        for (int index = 0; index < count; index++)
        {
            animations[index] = ReadWorldPresentationName(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(pointers.AsSpan(index * 4)));
        }
        return new(hrc, BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(12)), Array.AsReadOnly(animations));
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
