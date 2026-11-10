using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7BattleArenaPart(string ResourceName, PcPolygonModel Polygon);
public sealed record Ff7BattleArenaTexture(string ResourceName, PcTexture Texture);

public sealed record Ff7BattleArenaResources(int LocationId, string Prefix, ReadOnlyMemory<byte> Header, ReadOnlyMemory<byte> StageData, IReadOnlyList<Ff7BattleArenaPart> Geometry, IReadOnlyList<Ff7BattleArenaTexture> Textures)
{
    private const int AlphabetSize = 26;
    private const int NameCapacity = AlphabetSize * AlphabetSize;
    private const int FirstStagePrefix = 370;
    private const int FirstTextureSuffix = 2;
    private const int FirstGeometrySuffix = 12;
    private const int HeaderSize = 52;

    public static Ff7BattleArenaResources Load(LgpArchive archive, int locationId)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentOutOfRangeException.ThrowIfNegative(locationId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(locationId, NameCapacity - FirstStagePrefix);

        string prefix = EncodeName(FirstStagePrefix + locationId);
        byte[] header = Read(archive, prefix + "aa");
        if (header.Length != HeaderSize || BinaryPrimitives.ReadUInt32LittleEndian(header) != 1 || BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12)) != 0)
        {
            throw new NotSupportedException($"FFVII arena {locationId} requires a native zero-bone stage header.");
        }

        int partCount = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(20));
        int textureCount = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(24));
        if (partCount <= 0 || partCount > NameCapacity - FirstGeometrySuffix || textureCount < 0 || textureCount > FirstGeometrySuffix - FirstTextureSuffix)
        {
            throw new InvalidDataException($"FFVII arena {locationId} has invalid geometry or texture member counts.");
        }

        byte[] stageData = Read(archive, prefix + "ab");
        Ff7BattleArenaTexture[] textures = new Ff7BattleArenaTexture[textureCount];
        for (int index = 0; index < textures.Length; index++)
        {
            string name = prefix + EncodeName(FirstTextureSuffix + index);
            textures[index] = new(name, PcTexture.Read(Read(archive, name)));
        }

        Ff7BattleArenaPart[] geometry = new Ff7BattleArenaPart[partCount];
        for (int index = 0; index < geometry.Length; index++)
        {
            string name = prefix + EncodeName(FirstGeometrySuffix + index);
            PcPolygonModel polygon = PcPolygonModel.Read(Read(archive, name));
            foreach (PcModelGroup group in polygon.Groups)
            {
                if (group.TexturesUsed != 0 && (uint)group.TextureNumber >= textures.Length)
                {
                    throw new InvalidDataException($"FFVII arena part '{name}' references absent texture {group.TextureNumber}.");
                }
            }
            geometry[index] = new(name, polygon);
        }

        return new(locationId, prefix, header, stageData, Array.AsReadOnly(geometry), Array.AsReadOnly(textures));
    }

    private static string EncodeName(int value)
    {
        return new([(char)('a' + value / AlphabetSize), (char)('a' + value % AlphabetSize)]);
    }

    private static byte[] Read(LgpArchive archive, string name)
    {
        IReadOnlyList<LgpEntry> entries = archive.Find(name);
        if (entries.Count != 1)
        {
            throw new InvalidDataException($"FFVII arena resource '{name}' resolves to {entries.Count} members.");
        }
        return archive.ReadEntry(entries[0]);
    }
}
