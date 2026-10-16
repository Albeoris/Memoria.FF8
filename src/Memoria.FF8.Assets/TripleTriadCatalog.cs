using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>The authenticated English executable's 110 card definitions and original names.</summary>
public sealed class TripleTriadCatalog
{
    public const int CardCount = 110;
    private const int RankTableOffset = 0x796508;
    private const int NameTableOffset = 0x875074;

    private TripleTriadCatalog(ImmutableArray<TripleTriadCard> cards, ImmutableArray<string> names, ImmutableArray<byte> tradePriorities, TripleTriadNativeAiParameters aiParameters, ImmutableArray<TripleTriadRefinement> refinements)
    {
        Cards = cards;
        Names = names;
        TradePriorities = tradePriorities;
        AiParameters = aiParameters;
        Refinements = refinements;
    }

    public ImmutableArray<TripleTriadCard> Cards { get; }
    public ImmutableArray<string> Names { get; }
    public ImmutableArray<byte> TradePriorities { get; }
    public TripleTriadNativeAiParameters AiParameters { get; }
    /// <summary>Original Card Mod recipes. Empty when only the executable was supplied to ReadExecutable.</summary>
    public ImmutableArray<TripleTriadRefinement> Refinements { get; }

    public string Name(int cardId)
    {
        if ((uint)cardId >= CardCount)
        {
            throw new ArgumentOutOfRangeException(nameof(cardId));
        }
        return Names[cardId];
    }

    public int TradePriority(int cardId)
    {
        if ((uint)cardId >= CardCount)
        {
            throw new ArgumentOutOfRangeException(nameof(cardId));
        }
        return TradePriorities[cardId];
    }

    public static TripleTriadCatalog Load(string gameDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        using IFlArchive archive = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "menu.fs"), FlArchiveRepresentation.Files);
        IFlArchiveEntry entry = archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals("m004.bin", StringComparison.OrdinalIgnoreCase));
        using Stream input = entry.OpenForRead();
        using MemoryStream data = new();
        input.CopyTo(data);
        return ReadExecutable(File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")), data.ToArray());
    }

    public static TripleTriadCatalog ReadExecutable(byte[] executable, byte[]? refinementData = null)
    {
        ArgumentNullException.ThrowIfNull(executable);
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Triple Triad definitions require the authenticated original English executable.");
        }
        // Native 534AD0 selectors 2..6 read the eight-byte rows at B96508.
        // 4EF750 draws selectors 2..5 at B88A68's top/bottom/left/right coordinates.
        // Native 5348E0 reads the count-prefixed WORD string table through B964F8.
        if (BinaryPrimitives.ReadUInt16LittleEndian(executable.AsSpan(NameTableOffset)) != CardCount)
        {
            throw new InvalidDataException("Original Triple Triad name count is invalid.");
        }
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        // The match uses a second byte-identical card table at C74D00; byte5
        // is the native reward-selection priority consumed by 535800/53ADF0.
        if (!executable.AsSpan(RankTableOffset, CardCount * 8).SequenceEqual(executable.AsSpan(0x874D00, CardCount * 8)))
        {
            throw new InvalidDataException("Original menu and match card definition tables disagree.");
        }
        ImmutableArray<TripleTriadCard>.Builder cards = ImmutableArray.CreateBuilder<TripleTriadCard>(CardCount);
        ImmutableArray<string>.Builder names = ImmutableArray.CreateBuilder<string>(CardCount);
        ImmutableArray<byte>.Builder priorities = ImmutableArray.CreateBuilder<byte>(CardCount);
        for (int id = 0; id < CardCount; id++)
        {
            ReadOnlySpan<byte> row = executable.AsSpan(RankTableOffset + id * 8, 8);
            if (row[..4].ContainsAnyExceptInRange((byte)1, (byte)10))
            {
                throw new InvalidDataException($"Original Triple Triad card {id} has an invalid side rank.");
            }
            TripleTriadElement element = row[4] switch
            {
                0 => TripleTriadElement.None,
                1 => TripleTriadElement.Fire,
                2 => TripleTriadElement.Ice,
                4 => TripleTriadElement.Thunder,
                8 => TripleTriadElement.Earth,
                16 => TripleTriadElement.Poison,
                32 => TripleTriadElement.Wind,
                64 => TripleTriadElement.Water,
                128 => TripleTriadElement.Holy,
                _ => throw new InvalidDataException($"Original Triple Triad card {id} has an invalid element.")
            };
            cards.Add(new TripleTriadCard(id, row[0], row[3], row[1], row[2], element));
            priorities.Add(row[5]);
            int relative = BinaryPrimitives.ReadUInt16LittleEndian(executable.AsSpan(NameTableOffset + 2 + id * 2));
            int position = NameTableOffset + relative;
            int zero = Array.IndexOf(executable, (byte)0, position);
            if (relative < 2 + CardCount * 2 || zero < 0)
            {
                throw new InvalidDataException($"Original Triple Triad card {id} name has invalid bounds.");
            }
            names.Add(encoding.GetString(executable.AsSpan(position, zero - position)));
        }
        ImmutableArray<TripleTriadNativeAiWeights>.Builder profiles = ImmutableArray.CreateBuilder<TripleTriadNativeAiWeights>(8);
        for (int index = 0; index < 8; index++)
        {
            ReadOnlySpan<byte> row = executable.AsSpan(0x875BF8 + index * 20, 20);
            profiles.Add(new TripleTriadNativeAiWeights(BinaryPrimitives.ReadInt32LittleEndian(row), BinaryPrimitives.ReadInt32LittleEndian(row[4..]), BinaryPrimitives.ReadInt32LittleEndian(row[8..]), BinaryPrimitives.ReadInt32LittleEndian(row[12..]), BinaryPrimitives.ReadInt32LittleEndian(row[16..])));
        }
        TripleTriadNativeAiParameters ai = new(executable.AsSpan(0x875BAF, 73).ToArray().ToImmutableArray(), profiles.MoveToImmutable());
        return new TripleTriadCatalog(cards.MoveToImmutable(), names.MoveToImmutable(), priorities.MoveToImmutable(), ai, refinementData is null ? [] : ReadRefinements(refinementData));
    }

    public static ImmutableArray<TripleTriadRefinement> ReadRefinements(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (Convert.ToHexString(SHA256.HashData(data)) != "0DAD31178F36AF9EA4FDDF364B0FBBA2CA44C3B04C4581ED4F212D16DA503483")
        {
            throw new NotSupportedException("Triple Triad refinement requires the authenticated original m004.bin.");
        }
        ImmutableArray<TripleTriadRefinement>.Builder result = ImmutableArray.CreateBuilder<TripleTriadRefinement>(CardCount);
        for (int id = 0; id < CardCount; id++)
        {
            ReadOnlySpan<byte> row = data.AsSpan(id * 8, 8);
            if (row[4] != 1 || row[5] != id || row[6] == 0)
            {
                throw new InvalidDataException($"Original Triple Triad refinement row {id} has invalid input fields.");
            }
            result.Add(new TripleTriadRefinement(id, row[6], row[7], BinaryPrimitives.ReadUInt16LittleEndian(row[2..])));
        }
        return result.MoveToImmutable();
    }
}
