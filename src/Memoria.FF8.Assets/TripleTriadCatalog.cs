using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.TextEncoding;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.KernelBin;

namespace Memoria.FF8.Assets;

/// <summary>The authenticated native image's 110 card definitions and original localized names.</summary>
public sealed class TripleTriadCatalog
{
    public const int CardCount = 110;
    private const uint RankTableAddress = 0xB96508;
    private const uint NameTableAddress = 0xC75074;
    private const int NativeNameTableLength = 1200;
    private readonly ImmutableArray<string> originalNames;
    private ResourceService? resources;

    private TripleTriadCatalog(ImmutableArray<TripleTriadCard> cards, ImmutableArray<string> names, ImmutableArray<byte> tradePriorities, TripleTriadNativeAiParameters aiParameters, ImmutableArray<TripleTriadRefinement> refinements)
    {
        Cards = cards;
        originalNames = names;
        TradePriorities = tradePriorities;
        AiParameters = aiParameters;
        Refinements = refinements;
    }

    public ImmutableArray<TripleTriadCard> Cards { get; }
    public ImmutableArray<string> Names => resources?.FindText(StaticTextResource.CardNames)?.Texts ?? originalNames;
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
        return resources?.ReadText(StaticTextResource.CardNames, cardId, originalNames[cardId]) ?? originalNames[cardId];
    }

    public int TradePriority(int cardId)
    {
        if ((uint)cardId >= CardCount)
        {
            throw new ArgumentOutOfRangeException(nameof(cardId));
        }
        return TradePriorities[cardId];
    }

    public static TripleTriadCatalog Load(string gameDirectory, ResourceService? resources = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        using IResourceSource provider = GameDataSourceFactory.Open(gameDirectory);
        resources ??= new(provider);
        IGameNativeMemorySource memory = resources.GetSource<IGameNativeMemorySource>() ?? throw new NotSupportedException("The resource source has no authenticated native card memory contract.");
        byte[] names = provider.Descriptor.Edition == GameDataEdition.Remastered ? resources.Read(new ResourceKey("text/card-names")) : memory.Read(NameTableAddress, NativeNameTableLength);
        MngrpArchive archive = MngrpArchive.Read(resources.Read(new ResourceKey("menu/mngrp.bin")), resources.Read(new ResourceKey("menu/mngrphd.bin")), FF8Encoding.CreateEuropean());
        byte[] kernelBytes = resources.Read(new ResourceKey("main/kernel.bin"));
        _ = NativeKernelDirectory.Read(kernelBytes);
        KernelData kernel = KernelData.ReadFromArray(kernelBytes, FF8Encoding.CreateEuropean());
        if (kernel.MenuAbilities.Count != 24)
        {
            throw new InvalidDataException("Native Card Mod requires its complete kernel menu ability records.");
        }
        var cardAbility = kernel.MenuAbilities[23];
        TripleTriadCatalog native = Read(memory, names);
        TripleTriadCatalog catalog = new(native.Cards, native.originalNames, native.TradePriorities, native.AiParameters, NativeRefinementGroupReader.ReadCardRefinements(archive, new NativeRefinementAbility(cardAbility.Name ?? "", cardAbility.Index, cardAbility.Start, cardAbility.End)));
        catalog.resources = resources;
        return catalog;
    }

    public static TripleTriadCatalog ReadExecutable(byte[] executable, byte[]? refinementData = null)
    {
        ArgumentNullException.ThrowIfNull(executable);
        return Read(new NativeExecutableMemory(executable), refinementData: refinementData);
    }

    public static TripleTriadCatalog Read(IGameNativeMemorySource memory, byte[]? nameData = null, byte[]? refinementData = null)
    {
        ArgumentNullException.ThrowIfNull(memory);
        if (!NativeFieldRuntimeProfile.IsSupportedImage(memory.SourceHash))
        {
            throw new NotSupportedException("Triple Triad definitions require authenticated native data.");
        }
        nameData ??= memory.Read(NameTableAddress, NativeNameTableLength);
        // Native 534AD0 selectors 2..6 read the eight-byte rows at B96508.
        // 4EF750 draws selectors 2..5 at B88A68's top/bottom/left/right coordinates.
        // Native 5348E0 reads the count-prefixed WORD string table through B964F8.
        if (nameData.Length < 2 + CardCount * 2 || BinaryPrimitives.ReadUInt16LittleEndian(nameData) != CardCount)
        {
            throw new InvalidDataException("Original Triple Triad name count is invalid.");
        }
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        // The match uses a second byte-identical card table at C74D00; byte5
        // is the native reward-selection priority consumed by 535800/53ADF0.
        byte[] ranks = memory.Read(RankTableAddress, CardCount * 8);
        if (!ranks.AsSpan().SequenceEqual(memory.Read(0xC74D00, CardCount * 8)))
        {
            throw new InvalidDataException("Original menu and match card definition tables disagree.");
        }
        ImmutableArray<TripleTriadCard>.Builder cards = ImmutableArray.CreateBuilder<TripleTriadCard>(CardCount);
        ImmutableArray<string>.Builder names = ImmutableArray.CreateBuilder<string>(CardCount);
        ImmutableArray<byte>.Builder priorities = ImmutableArray.CreateBuilder<byte>(CardCount);
        for (int id = 0; id < CardCount; id++)
        {
            ReadOnlySpan<byte> row = ranks.AsSpan(id * 8, 8);
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
            int relative = BinaryPrimitives.ReadUInt16LittleEndian(nameData.AsSpan(2 + id * 2));
            if (relative < 2 + CardCount * 2 || relative >= nameData.Length)
            {
                throw new InvalidDataException($"Original Triple Triad card {id} name has invalid bounds.");
            }
            int zero = Array.IndexOf(nameData, (byte)0, relative);
            if (zero < 0)
            {
                throw new InvalidDataException($"Original Triple Triad card {id} name has invalid bounds.");
            }
            names.Add(encoding.GetString(nameData.AsSpan(relative, zero - relative)));
        }
        ImmutableArray<TripleTriadNativeAiWeights>.Builder profiles = ImmutableArray.CreateBuilder<TripleTriadNativeAiWeights>(8);
        byte[] aiProfiles = memory.Read(0xC75BF8, 8 * 20);
        for (int index = 0; index < 8; index++)
        {
            ReadOnlySpan<byte> row = aiProfiles.AsSpan(index * 20, 20);
            profiles.Add(new TripleTriadNativeAiWeights(BinaryPrimitives.ReadInt32LittleEndian(row), BinaryPrimitives.ReadInt32LittleEndian(row[4..]), BinaryPrimitives.ReadInt32LittleEndian(row[8..]), BinaryPrimitives.ReadInt32LittleEndian(row[12..]), BinaryPrimitives.ReadInt32LittleEndian(row[16..])));
        }
        TripleTriadNativeAiParameters ai = new(memory.Read(0xC75BAF, 73).ToImmutableArray(), profiles.MoveToImmutable());
        return new TripleTriadCatalog(cards.MoveToImmutable(), names.MoveToImmutable(), priorities.MoveToImmutable(), ai, refinementData is null ? [] : ReadRefinements(refinementData));
    }

    public static ImmutableArray<TripleTriadRefinement> ReadRefinements(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        NativeMenuDataReader.ValidateRefinementTable(data, 4);
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
