using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets;

public sealed record NativeTutorialBrowserCategory(int Id, byte TitleIndex, byte TextFamily, byte CallbackMode, string Title, string Help, ImmutableArray<NativeTutorialBrowserPage> Pages);

public sealed record NativeTutorialPageText(ushort X, byte Y, byte TextIndex, string Value);

/// <summary>Original mmag page metadata, exact string table and sprite content, independent of inventory magazine learning.</summary>
public sealed record NativeTutorialBrowserPage(byte Id, byte TextResource, byte ContentGroup, byte ContentIndex, int ContentResource, ImmutableArray<byte> Metadata, ImmutableArray<byte> TextBytes, ImmutableArray<byte> ContentBytes, ImmutableArray<NativeTutorialText> Texts, ImmutableArray<NativeTutorialPageText> Placements);

public sealed class NativeTutorialBrowserCatalog
{
    private NativeTutorialBrowserCatalog(string executableHash, string resourceHash, ImmutableArray<NativeTutorialBrowserCategory> categories, ImmutableArray<byte> ranges, ImmutableArray<byte> metadata, ImmutableArray<NativeTutorialText> tutorialLabels)
    {
        ExecutableHash = executableHash;
        ResourceHash = resourceHash;
        Categories = categories;
        RangeBytes = ranges;
        MetadataBytes = metadata;
        TutorialLabels = tutorialLabels;
    }

    public string ExecutableHash { get; }
    public string ResourceHash { get; }
    public ImmutableArray<NativeTutorialBrowserCategory> Categories { get; }
    public ImmutableArray<byte> RangeBytes { get; }
    public ImmutableArray<byte> MetadataBytes { get; }
    public ImmutableArray<NativeTutorialText> TutorialLabels { get; }

    public static NativeTutorialBrowserCatalog Read(byte[] executable, byte[] menuHeader, byte[] menuContent, byte[] ranges, byte[] metadata)
    {
        return Read(new NativeExecutableMemory(executable), menuHeader, menuContent, ranges, metadata);
    }

    public static NativeTutorialBrowserCatalog Read(IGameNativeMemorySource memory, byte[] menuHeader, byte[] menuContent, byte[] ranges, byte[] metadata)
    {
        ArgumentNullException.ThrowIfNull(memory);
        if (menuHeader.Length != 256 * 8 || ranges.Length != 12 || metadata.Length % 68 != 0)
        {
            throw new InvalidDataException("Native tutorial browser requires complete original menu, mtmag and mmag tables.");
        }
        byte[] descriptors = memory.Read(0xB88340, 7 * 4);
        int[] contentResourceBases = [28, 20, 24, 44, 48, 71, 180];
        ImmutableArray<NativeTutorialText> tutorialLabels = NativeTutorialCatalog.ReadGroupedTexts(Resource(2), 13);
        ImmutableArray<NativeTutorialText> mainLabels = NativeTutorialCatalog.ReadGroupedTexts(Resource(0), 0);
        ImmutableArray<NativeTutorialBrowserCategory>.Builder categories = ImmutableArray.CreateBuilder<NativeTutorialBrowserCategory>(7);
        for (int id = 0; id < 7; id++)
        {
            ReadOnlySpan<byte> row = descriptors.AsSpan(id * 4, 4);
            ImmutableArray<NativeTutorialText> labels = row[1] == 0 ? mainLabels : tutorialLabels;
            int labelIndex = row[0] * 2;
            if (row[1] > 1 || labelIndex + 1 >= labels.Length)
            {
                throw new InvalidDataException("Native tutorial browser category has an invalid title selector.");
            }
            int range = row[2] switch
            {
                25 => 0,
                26 => 4,
                31 => 8,
                _ => -1
            };
            ImmutableArray<NativeTutorialBrowserPage>.Builder pages = ImmutableArray.CreateBuilder<NativeTutorialBrowserPage>();
            if (range >= 0)
            {
                int first = ranges[range];
                int last = ranges[range + 1];
                if (first > last || last >= metadata.Length / 68)
                {
                    throw new InvalidDataException("Native tutorial browser page range exceeds mmag.bin.");
                }
                for (int page = first; page <= last; page++)
                {
                    ReadOnlySpan<byte> pageRow = metadata.AsSpan(page * 68, 68);
                    int textResource = pageRow[21] + 87;
                    int group = pageRow[22];
                    if (group >= 7)
                    {
                        throw new InvalidDataException("Native tutorial page has an unsupported content resource group.");
                    }
                    // Native4C9920 selects the same menu resource groups in both supported editions.
                    int resourceBase = contentResourceBases[group];
                    int contentResource = pageRow[23] + resourceBase;
                    ImmutableArray<byte> textBytes = Resource(textResource);
                    ImmutableArray<NativeTutorialText> texts = NativeTutorialCatalog.ReadTexts(textBytes);
                    ImmutableArray<NativeTutorialPageText>.Builder placements = ImmutableArray.CreateBuilder<NativeTutorialPageText>();
                    for (int slot = 0; slot < 4; slot++)
                    {
                        ReadOnlySpan<byte> placement = pageRow.Slice(52 + slot * 4, 4);
                        if (placement[3] == 255)
                        {
                            continue;
                        }
                        if (placement[3] >= texts.Length)
                        {
                            throw new InvalidDataException("Native tutorial page refers to an absent text entry.");
                        }
                        placements.Add(new NativeTutorialPageText(BinaryPrimitives.ReadUInt16LittleEndian(placement), placement[2], placement[3], texts[placement[3]].Value));
                    }
                    pages.Add(new NativeTutorialBrowserPage((byte)page, (byte)textResource, (byte)group, pageRow[23], contentResource, ImmutableArray.CreateRange(pageRow.ToArray()), textBytes, Resource(contentResource), texts, placements.ToImmutable()));
                }
            }
            categories.Add(new NativeTutorialBrowserCategory(id, row[0], row[1], row[2], labels[labelIndex].Value, labels[labelIndex + 1].Value, pages.ToImmutable()));
        }
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(menuHeader);
        hash.AppendData(menuContent);
        hash.AppendData(ranges);
        hash.AppendData(metadata);
        return new NativeTutorialBrowserCatalog(memory.SourceHash, Convert.ToHexString(hash.GetHashAndReset()), categories.MoveToImmutable(), ImmutableArray.CreateRange(ranges), ImmutableArray.CreateRange(metadata), tutorialLabels);

        ImmutableArray<byte> Resource(int slot)
        {
            if ((uint)slot >= 256)
            {
                throw new InvalidDataException("Native tutorial page resource selector is outside the original menu directory.");
            }
            uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(menuHeader.AsSpan(slot * 8));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(menuHeader.AsSpan(slot * 8 + 4));
            if (rawOffset is 0 or uint.MaxValue || size == 0 || (ulong)rawOffset - 1 + size > (ulong)menuContent.Length)
            {
                throw new InvalidDataException($"Native tutorial browser resource{slot} is vacant or outside mngrp.bin.");
            }
            return ImmutableArray.CreateRange(menuContent.AsSpan(checked((int)rawOffset - 1), checked((int)size)).ToArray());
        }
    }
}

public sealed partial class GameAssets
{
    private NativeTutorialBrowserCatalog? nativeTutorialBrowser;

    public NativeTutorialBrowserCatalog ReadNativeTutorialBrowserCatalog()
    {
        if (nativeTutorialBrowser is not null)
        {
            return nativeTutorialBrowser;
        }
        using IFlArchive menu = OpenArchive("menu");
        return nativeTutorialBrowser = NativeTutorialBrowserCatalog.Read(ReadNativeMemory(), Read(menu, "mngrphd.bin"), Read(menu, "mngrp.bin"), Read(menu, "mtmag.bin"), Read(menu, "mmag.bin"));
    }
}
