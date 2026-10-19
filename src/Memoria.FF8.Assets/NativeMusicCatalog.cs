using Memoria.FF8.Resources;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Original46C850 filename table and original DirectSound/DirectMusic gain tables.</summary>
public sealed class NativeMusicCatalog
{
    private readonly CatalogData data;

    public NativeMusicCatalog(string gameDirectory) : this(ReadInstalledData(gameDirectory))
    {
    }

    public NativeMusicCatalog(ResourceService resources) : this(ReadData(resources.GetSource<IGameNativeMemorySource>() ?? throw new NotSupportedException("The resource source has no authenticated native audio memory contract.")))
    {
    }

    private NativeMusicCatalog(CatalogData data)
    {
        this.data = data;
    }

    private static CatalogData ReadInstalledData(string gameDirectory)
    {
        using IResourceSource source = GameDataSourceFactory.Open(gameDirectory);
        return ReadData(source as IGameNativeMemorySource ?? throw new NotSupportedException("The resource source has no authenticated native audio memory contract."));
    }

    private static CatalogData ReadData(IGameNativeMemorySource memory)
    {
        string ReadString(uint address)
        {
            List<byte> bytes = [];
            for (uint offset = 0; offset < 256; offset++)
            {
                byte value = memory.Read(checked(address + offset), 1)[0];
                if (value == 0)
                {
                    return Encoding.ASCII.GetString(bytes.ToArray());
                }
                bytes.Add(value);
            }
            throw new InvalidDataException("Native audio filename has no bounded terminator.");
        }

        byte[] musicPointers = memory.Read(0xB7F758, 117 * 4);
        string[] musicFiles = Enumerable.Range(0, 117).Select(index => ReadString(BinaryPrimitives.ReadUInt32LittleEndian(musicPointers.AsSpan(index * 4, 4)))).ToArray();
        byte[] worldPointers = memory.Read(0xC75EA0, 6 * 16);
        string[] worldFiles = Enumerable.Range(0, 6).Select(index => ReadString(BinaryPrimitives.ReadUInt32LittleEndian(worldPointers.AsSpan(index * 16, 4)))).ToArray();
        return new CatalogData(memory.SourceHash, memory.Read(0xB7EB10, 128 * 4), memory.Read(0xB7ED10, 128 * 4), memory.Read(0xB7F92C, 128 * 4), memory.Read(0xB8EE6C, 36), BinaryPrimitives.ReadInt32LittleEndian(memory.Read(0xB8EE68, 4)), memory.Read(0xB80F9C, 0x58), memory.Read(0xB87D28, 48), memory.Read(0xB8A418, 160 * 7 * sizeof(uint)), musicFiles, worldFiles);
    }

    private sealed record CatalogData(string SourceHash, byte[] SoundVolumes, byte[] SoundPans, byte[] MusicVolumes, byte[] BattleTemplate, int BattleMusicOffset, byte[] BattleEncounters, byte[] MenuSounds, byte[] ActorSounds, string[] MusicFiles, string[] WorldFiles);

    private static int Table(byte[] bytes, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 127);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(index * 4, 4));
    }

    public int SoundVolume(int volume) => Table(data.SoundVolumes, volume);
    public int SoundPan(int pan) => Table(data.SoundPans, pan);
    public int MusicVolume(int volume) => Table(data.MusicVolumes, volume);

    public NativeBattleMusicEntryResources ReadBattleEntryResources()
    {
        ImmutableArray<byte> template = data.BattleTemplate.ToImmutableArray();
        int musicOffset = data.BattleMusicOffset;
        ReadOnlySpan<byte> table = data.BattleEncounters;
        if (BinaryPrimitives.ReadUInt16LittleEndian(table[^2..]) != ushort.MaxValue)
        {
            throw new InvalidDataException("Native47CC70 encounter table lost its original FFFF terminator.");
        }
        ImmutableArray<ushort>.Builder encounters = ImmutableArray.CreateBuilder<ushort>(43);
        for (int offset = 0; offset < table.Length - 2; offset += 2)
        {
            encounters.Add(BinaryPrimitives.ReadUInt16LittleEndian(table.Slice(offset, 2)));
        }
        NativeBattleMusicEntryResources resources = new(data.SourceHash, template, musicOffset, encounters.MoveToImmutable());
        resources.Validate();
        _ = resources.SelectPayload(255);
        return resources;
    }

    public string WorldMusicFile(int index)
    {
        _ = NativeWorldMusicResource.FileMusic(index);
        string original = data.WorldFiles[index];
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
        return data.MenuSounds[number];
    }

    /// <summary>Original5015A0/501640 select one of seven original sound identities for each battle actor.</summary>
    public static ImmutableArray<uint> ReadBattleActorSounds(byte[] executable)
    {
        return ReadBattleActorSounds(new NativeExecutableMemory(executable));
    }

    public static ImmutableArray<uint> ReadBattleActorSounds(IGameNativeMemorySource memory)
    {
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(memory, 0xB8A418, 160 * 7 * sizeof(uint));
        ImmutableArray<uint>.Builder sounds = ImmutableArray.CreateBuilder<uint>(160 * 7);
        for (int offset = 0; offset < bytes.Length; offset += sizeof(uint))
        {
            sounds.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, sizeof(uint))));
        }
        return sounds.MoveToImmutable();
    }

    /// <summary>Original5015A0/501640 select one of seven original sound identities for each battle actor.</summary>
    public uint BattleActorSound(int actorId, int soundNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actorId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(actorId, 160);
        ArgumentOutOfRangeException.ThrowIfNegative(soundNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(soundNumber, 7);
        return BinaryPrimitives.ReadUInt32LittleEndian(data.ActorSounds.AsSpan((actorId * 7 + soundNumber) * sizeof(uint), sizeof(uint)));
    }

    public static int ResolveMusicId(int music, int fieldId)
    {
        // NG-001: original46B530 remaps landing17 outside Dollet field339.
        return music == 17 && fieldId != 339 ? 99 : music;
    }

    public string MusicFile(int music, int fieldId)
    {
        return MusicFileResolved(ResolveMusicId(music, fieldId));
    }

    public string MusicFileResolved(int music)
    {
        string? stream = NativeMusicRouting.GetStreamFileName(music);
        if (stream is not null)
        {
            return Path.Combine("Data", "Music", "stream", stream);
        }
        ArgumentOutOfRangeException.ThrowIfNegative(music);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(music, 117);
        string name = data.MusicFiles[music];
        if (Path.GetFileName(name) != name || !name.EndsWith(".sgt", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Original music table has no supported segment filename.");
        }
        return Path.Combine("Data", "Music", "dmusic", name);
    }
}
