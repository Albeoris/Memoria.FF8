using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Original46C850 filename table and original DirectSound/DirectMusic gain tables.</summary>
public sealed class NativeMusicCatalog
{
    private readonly byte[] image;
    private readonly PEHeaders headers;

    public NativeMusicCatalog(string gameDirectory)
    {
        image = File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe"));
        if (!Convert.ToHexString(SHA256.HashData(image)).Equals(NativeFieldRuntimeProfile.FF8EnExecutableHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Audio tables require the authenticated FF8_EN executable.");
        }
        using var reader = new PEReader(new MemoryStream(image));
        headers = reader.PEHeaders;
    }

    private int Offset(uint address)
    {
        long rva = checked((long)address - (long)headers.PEHeader!.ImageBase);
        foreach (SectionHeader section in headers.SectionHeaders)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + section.SizeOfRawData)
            {
                return checked((int)(rva - section.VirtualAddress + section.PointerToRawData));
            }
        }
        throw new InvalidDataException($"Audio table address {address:X8} is not in the executable.");
    }

    private int Table(uint address, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 127);
        return BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(Offset(address) + index * 4, 4));
    }

    public int SoundVolume(int volume) => Table(0xB7EB10, volume);
    public int SoundPan(int pan) => Table(0xB7ED10, pan);
    public int MusicVolume(int volume) => Table(0xB7F92C, volume);

    public string WorldMusicFile(int index)
    {
        _ = NativeWorldMusicResource.FileMusic(index);
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(Offset(0xC75EA0) + index * 16, 4));
        int start = Offset(pointer);
        int end = Array.IndexOf(image, (byte)0, start);
        string original = Encoding.ASCII.GetString(image, start, end - start);
        string name = original.Replace('\\', '/');
        if (!name.EndsWith(";1", StringComparison.Ordinal) || name[..^2] != $"dat/music{index}.obj")
        {
            throw new InvalidDataException("Native world music table has a different archive resource path.");
        }
        return name[..^2];
    }

    /// <summary>Original4B92A0/4B92C0 translate menu cue numbers through byte_B87D28.</summary>
    public uint MenuSound(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(number, 48);
        return image[Offset(0xB87D28) + number];
    }

    /// <summary>Original5015A0/501640 select one of seven original sound identities for each battle actor.</summary>
    public uint BattleActorSound(int actorId, int soundNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actorId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(actorId, 160);
        ArgumentOutOfRangeException.ThrowIfNegative(soundNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(soundNumber, 7);
        return BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(Offset(0xB8A418) + (actorId * 7 + soundNumber) * sizeof(uint), sizeof(uint)));
    }

    public static int ResolveMusicId(int music, int fieldId)
    {
        // NG-001: original46B530 remaps landing17 outside Dollet field339.
        return music == 17 && fieldId != 339 ? 99 : music;
    }

    public string MusicFile(int music, int fieldId)
    {
        music = ResolveMusicId(music, fieldId);
        string? stream = music switch
        {
            3 => "003-combat.wav",
            6 => "006-funsui.wav",
            10 => "010-ante.wav",
            11 => "011-wind.wav",
            30 => "030-flangchorus.wav",
            31 => "031-dubchorus.wav",
            32 => "032-solochorus.wav",
            33 => "033-femalechorus.wav",
            34 => "034-chorus.wav",
            37 => "037-reet.wav",
            38 => "038-soyo.wav",
            39 => "039-rouka.wav",
            40 => "040-night.wav",
            44 => "044-sea.wav",
            74 => "074-train.wav",
            87 => "087-mdmotor.wav",
            94 => "094-laswhite.wav",
            95 => "095-lasbl.wav",
            _ => null
        };
        if (stream is not null)
        {
            return Path.Combine("Data", "Music", "stream", stream);
        }
        ArgumentOutOfRangeException.ThrowIfNegative(music);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(music, 117);
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(Offset(0xB7F758) + music * 4, 4));
        int start = Offset(pointer);
        int end = Array.IndexOf(image, (byte)0, start);
        string name = Encoding.ASCII.GetString(image, start, end - start);
        if (Path.GetFileName(name) != name || !name.EndsWith(".sgt", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Original music table has no supported segment filename.");
        }
        return Path.Combine("Data", "Music", "dmusic", name);
    }
}
