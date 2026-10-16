using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

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
        string executableHash = Convert.ToHexString(SHA256.HashData(executable));
        if (executableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Native tutorial browser requires the authenticated English executable.");
        }
        if (menuHeader.Length != 256 * 8 || ranges.Length != 12 || metadata.Length % 68 != 0)
        {
            throw new InvalidDataException("Native tutorial browser requires complete original menu, mtmag and mmag tables.");
        }
        using PEReader pe = new(new MemoryStream(executable));
        ImmutableArray<NativeTutorialText> tutorialLabels = NativeTutorialCatalog.ReadGroupedTexts(Resource(2), 13);
        ImmutableArray<NativeTutorialText> mainLabels = NativeTutorialCatalog.ReadGroupedTexts(Resource(0), 0);
        ImmutableArray<NativeTutorialBrowserCategory>.Builder categories = ImmutableArray.CreateBuilder<NativeTutorialBrowserCategory>(7);
        for (int id = 0; id < 7; id++)
        {
            ReadOnlySpan<byte> row = executable.AsSpan(Offset(0xB88340u + (uint)id * 4, 4), 4);
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
                    uint branch = BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(Offset(0x4C998Cu + (uint)group * 4, 4), 4));
                    ReadOnlySpan<byte> selector = executable.AsSpan(Offset(branch, 9), 9);
                    // Native4C9920 loads the local index, then adds this group's original resource base.
                    if (!selector[..4].SequenceEqual(new byte[] { 0x8B, 0x44, 0x24, 0x08 }))
                    {
                        throw new NotSupportedException("Native tutorial content group no longer uses its proved stack argument selector.");
                    }
                    int resourceBase = selector[4] == 0x83 && selector[5] == 0xC0 ? selector[6] : selector[4] == 0x05 ? checked((int)BinaryPrimitives.ReadUInt32LittleEndian(selector[5..])) : throw new NotSupportedException("Native tutorial content group no longer uses its proved ADD EAX selector.");
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
        return new NativeTutorialBrowserCatalog(executableHash, Convert.ToHexString(hash.GetHashAndReset()), categories.MoveToImmutable(), ImmutableArray.CreateRange(ranges), ImmutableArray.CreateRange(metadata), tutorialLabels);

        int Offset(uint address, int length)
        {
            long rva = address - (long)pe.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = pe.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + length <= section.VirtualAddress + section.SizeOfRawData);
            return checked((int)(rva - section.VirtualAddress + section.PointerToRawData));
        }

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
        using IFlArchive menu = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "menu.fs"), FlArchiveRepresentation.Files);
        return nativeTutorialBrowser = NativeTutorialBrowserCatalog.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")), Read(menu, "mngrphd.bin"), Read(menu, "mngrp.bin"), Read(menu, "mtmag.bin"), Read(menu, "mmag.bin"));
    }
}
