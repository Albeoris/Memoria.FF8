using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF7.Resources;

public sealed class Ff7PcExecutableData
{
    public const string SupportedSha256 = "26998200234F7A49A700BDC1AE5D0A43FD001CC909871A366CF82BDFFAA351C9";

    private Ff7PcExecutableData(short[] sinCos, ushort[] angles, string[] movieFiles, string[] musicNames, byte[] fieldRandomTable, byte fieldRandomStep, int[] soundPan, int[] soundVolume, byte[] materiaClasses, short[] materiaStatModifiers)
    {
        NativeSinCos = Array.AsReadOnly(sinCos);
        NativeArcAngleWords = Array.AsReadOnly(angles);
        NativeArcAngles = Array.AsReadOnly(angles.Select(angle => checked((byte)angle)).ToArray());
        MovieFiles = Array.AsReadOnly(movieFiles);
        MusicNames = Array.AsReadOnly(musicNames);
        NativeFieldRandomTable = Array.AsReadOnly(fieldRandomTable);
        FieldRandomStep = fieldRandomStep;
        NativeSoundPan = Array.AsReadOnly(soundPan);
        NativeSoundVolume = Array.AsReadOnly(soundVolume);
        NativeMateriaClasses = Array.AsReadOnly(materiaClasses);
        NativeMateriaStatModifiers = Array.AsReadOnly(materiaStatModifiers);
    }

    public IReadOnlyList<short> NativeSinCos { get; }
    public IReadOnlyList<ushort> NativeArcAngleWords { get; }
    public IReadOnlyList<byte> NativeArcAngles { get; }
    public IReadOnlyList<string> MovieFiles { get; }
    public IReadOnlyList<string> MusicNames { get; }
    public IReadOnlyList<byte> NativeFieldRandomTable { get; }
    public byte FieldRandomStep { get; }
    public IReadOnlyList<int> NativeSoundPan { get; }
    public IReadOnlyList<int> NativeSoundVolume { get; }
    public IReadOnlyList<byte> NativeMateriaClasses { get; }
    public IReadOnlyList<short> NativeMateriaStatModifiers { get; }
    public float FieldNearPlane => 50;
    public float FieldFarPlane => 32000;
    public ushort InitialField => 116;
    public short InitialX => 0;
    public short InitialY => 0;
    public ushort InitialTriangle => 0;
    public byte InitialDirection => 0;
    public byte InitialPersistentBank3Byte0 => 1;
    public IReadOnlyList<byte> InitialParty => Array.AsReadOnly(new byte[] { 0, 255, 255 });
    public ushort InitialAvailableCharacters => 1;

    /// <summary>Converts the original script volume to hundredths of a decibel, before shared host output preferences.</summary>
    public int NativeSoundGain(int volume)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(volume);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, 127);
        return NativeSoundVolume[volume];
    }

    public byte[] CreateInitialPersistentVariables()
    {
        byte[] data = new byte[1280];
        data[28] = 255;
        data[29] = 255;
        data[256 + 9] = 0;
        data[256 + 10] = 255;
        data[256 + 11] = 255;
        data[768] = InitialPersistentBank3Byte0;
        data[768 + 104] = 255;
        return data;
    }

    public static Ff7PcExecutableData Load(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        string path = Path.Combine(installation.RootDirectory, "ff7", "resources", "ff7_1.02", "ff7_en");
        if (!File.Exists(path))
        {
            throw new InvalidDataException("The selected FF7 installation has no supported native execution-data profile.");
        }
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > 64 * 1024 * 1024 || !Convert.ToHexString(SHA256.HashData(stream)).Equals(SupportedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The installed FF7 native execution-data content hash is unsupported.");
        }
        stream.Position = 0;
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        PEHeader header = pe.PEHeaders.PEHeader ?? throw new InvalidDataException("FF7 native execution data has no PE header.");
        if (header.Magic != PEMagic.PE32)
        {
            throw new InvalidDataException("FF7 native execution data is not the supported 32-bit profile.");
        }
        byte[] directions = ReadVirtual(stream, pe, 0x908E30, 1024);
        byte[] arc = ReadVirtual(stream, pe, 0x9067C0, 258);
        short[] sinCos = new short[512];
        ushort[] angles = new ushort[129];
        for (int i = 0; i < sinCos.Length; i++)
        {
            sinCos[i] = BinaryPrimitives.ReadInt16LittleEndian(directions.AsSpan(i * 2));
            if (sinCos[i] < -4096 || sinCos[i] > 4096)
            {
                throw new InvalidDataException("FF7 native direction table exceeds its fixed-point range.");
            }
        }
        for (int i = 0; i < angles.Length; i++)
        {
            angles[i] = BinaryPrimitives.ReadUInt16LittleEndian(arc.AsSpan(i * 2));
            if (angles[i] > 32)
            {
                throw new InvalidDataException("FF7 native angle table exceeds its octant range.");
            }
        }
        byte[] moviePointers = ReadVirtual(stream, pe, 0x7BAE80, 54 * 4);
        string[] movieFiles = new string[54];
        for (int index = 0; index < movieFiles.Length; index++)
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(moviePointers.AsSpan(index * 4));
            byte[] name = ReadVirtual(stream, pe, address, 64);
            int terminator = Array.IndexOf(name, (byte)0);
            if (terminator < 1 || name.AsSpan(0, terminator).ContainsAnyInRange((byte)128, byte.MaxValue))
            {
                throw new InvalidDataException("FF7 movie resource name is not bounded ASCII.");
            }
            string movie = Encoding.ASCII.GetString(name, 0, terminator);
            if (Path.GetFileName(movie) != movie || !movie.EndsWith(".avi", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("FF7 movie resource identity is invalid.");
            }
            movieFiles[index] = movie;
        }
        byte[] musicPointers = ReadVirtual(stream, pe, 0x9684C8, 99 * 4);
        string[] musicNames = new string[99];
        for (int index = 0; index < musicNames.Length; index++)
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(musicPointers.AsSpan(index * 4));
            byte[] name = ReadVirtual(stream, pe, address, 32);
            int terminator = Array.IndexOf(name, (byte)0);
            if (terminator < 1 || name.AsSpan(0, terminator).ContainsAnyInRange((byte)128, byte.MaxValue))
            {
                throw new InvalidDataException("FF7 music resource name is not bounded ASCII.");
            }
            string music = Encoding.ASCII.GetString(name, 0, terminator);
            if (music.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            {
                throw new InvalidDataException("FF7 music resource identity is invalid.");
            }
            musicNames[index] = music;
        }
        byte[] nativePan = ReadVirtual(stream, pe, 0x967860, 128 * 4);
        int[] soundPan = new int[128];
        for (int index = 0; index < soundPan.Length; index++)
        {
            soundPan[index] = BinaryPrimitives.ReadInt32LittleEndian(nativePan.AsSpan(index * 4));
            if (soundPan[index] is < -10000 or > 10000)
            {
                throw new InvalidDataException("FF7 native sound panning exceeds the device range.");
            }
        }
        byte[] nativeVolume = ReadVirtual(stream, pe, 0x967660, 128 * 4);
        int[] soundVolume = new int[128];
        for (int index = 0; index < soundVolume.Length; index++)
        {
            soundVolume[index] = BinaryPrimitives.ReadInt32LittleEndian(nativeVolume.AsSpan(index * 4));
            if (soundVolume[index] is < -10000 or > 0)
            {
                throw new InvalidDataException("FF7 native sound volume exceeds the device attenuation range.");
            }
        }
        if (soundVolume[0] != -10000 || soundVolume[^1] != 0)
        {
            throw new InvalidDataException("FF7 native sound volume endpoints are invalid.");
        }
        byte[] materiaIndices = ReadVirtual(stream, pe, 0x91ABF8, 16);
        byte[] materiaClasses = ReadVirtual(stream, pe, 0x91ABF0, materiaIndices.Max() + 1);
        byte[] resolvedMateriaClasses = materiaIndices.Select(index => materiaClasses[index]).ToArray();
        byte[] nativeStatModifiers = ReadVirtual(stream, pe, 0x8FEEC8, 17 * 16);
        short[] materiaStatModifiers = new short[17 * 8];
        for (int index = 0; index < materiaStatModifiers.Length; index++)
        {
            materiaStatModifiers[index] = BinaryPrimitives.ReadInt16LittleEndian(nativeStatModifiers.AsSpan(index * 2));
        }
        return new Ff7PcExecutableData(sinCos, angles, movieFiles, musicNames, ReadVirtual(stream, pe, 0x9059A0, 256), ReadVirtual(stream, pe, 0x905E50, 1)[0], soundPan, soundVolume, resolvedMateriaClasses, materiaStatModifiers);
    }

    private static byte[] ReadVirtual(Stream stream, PEReader pe, uint address, int count)
    {
        PEHeader header = pe.PEHeaders.PEHeader!;
        long rva = address - (long)header.ImageBase;
        foreach (SectionHeader section in pe.PEHeaders.SectionHeaders)
        {
            long relative = rva - section.VirtualAddress;
            if (relative < 0 || relative + count > section.SizeOfRawData)
            {
                continue;
            }
            long offset = section.PointerToRawData + relative;
            if (offset < 0 || offset + count > stream.Length)
            {
                break;
            }
            stream.Position = offset;
            byte[] data = new byte[count];
            stream.ReadExactly(data);
            return data;
        }
        throw new InvalidDataException("FF7 native execution table is outside its PE section data.");
    }
}
