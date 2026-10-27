using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record NativeTutorialDescriptor(int Id, byte TitleIndex, byte CallbackMode, byte CharacterId, byte GuardianForceResource, byte CharacterResource, byte TextResource, byte InputResource, byte Flags, uint UnlockId);

public sealed record NativeTutorialText(int Index, ushort Offset, string Value, ImmutableArray<byte> EncodedBytes);

/// <summary>One original menu29 tutorial, retaining its sector-padded resources and exact input WORD stream.</summary>
public sealed record NativeTutorialDefinition(NativeTutorialDescriptor Descriptor, ImmutableArray<byte> TextBytes, ImmutableArray<byte> InputBytes, ImmutableArray<byte> CharacterBytes, ImmutableArray<byte> GuardianForceBytes, ImmutableArray<NativeTutorialText> Texts, ImmutableArray<ushort> Inputs)
{
    public int Id => Descriptor.Id;
    public string Title { get; init; } = "";
    public string Help { get; init; } = "";
}

/// <summary>Original English tutorial descriptors and menu resources; fixture construction never touches the live party.</summary>
public sealed partial class NativeTutorialCatalog
{
    private readonly byte[] initial;
    private readonly byte[] kernel;

    private NativeTutorialCatalog(string executableHash, string resourceHash, ImmutableArray<NativeTutorialDefinition> tutorials, ImmutableArray<NativeTutorialText> statusLabels, ImmutableArray<NativeTutorialText> switchLabels, byte[] initial, byte[] kernel)
    {
        ExecutableHash = executableHash;
        ResourceHash = resourceHash;
        Tutorials = tutorials;
        StatusLabels = statusLabels;
        SwitchLabels = switchLabels;
        this.initial = initial;
        this.kernel = kernel;
    }

    public string ExecutableHash { get; }
    public string ResourceHash { get; }
    public ImmutableArray<NativeTutorialDefinition> Tutorials { get; }
    public ImmutableArray<NativeTutorialText> StatusLabels { get; }
    public ImmutableArray<NativeTutorialText> SwitchLabels { get; }

    public NativeTutorialDefinition Get(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(id, Tutorials.Length);
        return Tutorials[id];
    }

    public NativeCharacterVitals CreateIsolatedParty(int id)
    {
        NativeTutorialDefinition tutorial = Get(id);
        byte[] template = initial.ToArray();
        tutorial.GuardianForceBytes.AsSpan(0, 16 * 68).CopyTo(template);
        tutorial.CharacterBytes.AsSpan(0, 8 * 152).CopyTo(template.AsSpan(1088));
        for (int row = 0; row < 8; row++)
        {
            Span<byte> character = template.AsSpan(1088 + row * 152, 152);
            BinaryPrimitives.WriteUInt16LittleEndian(character, 9999);
            character[8] = (byte)row;
            BinaryPrimitives.WriteUInt16LittleEndian(character[150..], 0);
        }
        for (int row = 0; row < 16; row++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(template.AsSpan(row * 68 + 18), 9999);
            initial.AsSpan(row * 68, 12).CopyTo(template.AsSpan(row * 68, 12));
        }
        bool switchTutorial = (tutorial.Descriptor.Flags & 1) != 0;
        template.AsSpan(2724, 3).Fill(255);
        NativeCharacterVitals state = NativeCharacterInitialization.Read(template, kernel);
        byte[] party = switchTutorial ? [1, 0, 5] : [0, 1, 4];
        // Native4C99E0 installs these slots regardless of ordinary membership, then calls495EF0.
        return (state with { CombatParty = party }).RebuildParty().DeepCopy();
    }

    public NativeLimitProgress CreateIsolatedLimitProgress(int id)
    {
        NativeCharacterVitals party = CreateIsolatedParty(id);
        // Native4C99E0 initializes the tutorial copy's saved flags and eight Angelo counters before4BFD40.
        NativeLimitProgress result = new(party.InitOutSha256, 1, 0x4F, 0x11, 0, ImmutableArray.CreateRange(Enumerable.Repeat(byte.MaxValue, 8))) { SpecialMagicFlags = 1, PetPalsFlags = 0x13, Options978 = 0 };
        result.Validate();
        return result;
    }

    public static NativeTutorialCatalog Read(byte[] executable, byte[] menuHeader, byte[] menuContent, byte[] initial, byte[] kernel)
    {
        return Read(new NativeExecutableMemory(executable), menuHeader, menuContent, initial, kernel);
    }

    public static NativeTutorialCatalog Read(IGameNativeMemorySource memory, byte[] menuHeader, byte[] menuContent, byte[] initial, byte[] kernel)
    {
        ArgumentNullException.ThrowIfNull(memory);
        NativeInitialData.Validate(initial);
        if (menuHeader.Length != 256 * 8)
        {
            throw new InvalidDataException("Native tutorials require the complete original menu directory and initialization template.");
        }
        byte[] descriptors = memory.Read(0xB88360, 9 * 12);
        ImmutableArray<NativeTutorialText> titles = ReadGroupedTexts(Resource(2), 13);
        ImmutableArray<NativeTutorialDefinition>.Builder tutorials = ImmutableArray.CreateBuilder<NativeTutorialDefinition>(9);
        for (int id = 0; id < 9; id++)
        {
            ReadOnlySpan<byte> row = descriptors.AsSpan(id * 12, 12);
            NativeTutorialDescriptor descriptor = new(id, row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[7], BinaryPrimitives.ReadUInt32LittleEndian(row[8..]));
            ImmutableArray<byte> texts = Resource(descriptor.TextResource);
            ImmutableArray<byte> inputs = Resource(descriptor.InputResource);
            ImmutableArray<byte> characters = Resource(descriptor.CharacterResource);
            ImmutableArray<byte> guardianForces = Resource(descriptor.GuardianForceResource);
            if (characters.Length < 8 * 152 || guardianForces.Length < 16 * 68 || inputs.Length % 2 != 0)
            {
                throw new InvalidDataException("Native tutorial resource cannot supply its complete initialization records or WORD inputs.");
            }
            ImmutableArray<ushort> words = Enumerable.Range(0, inputs.Length / 2).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(inputs.AsSpan(index * 2, 2))).ToImmutableArray();
            tutorials.Add(new NativeTutorialDefinition(descriptor, texts, inputs, characters, guardianForces, ReadTexts(texts), words) { Title = titles[descriptor.TitleIndex * 2].Value, Help = titles[descriptor.TitleIndex * 2 + 1].Value });
        }
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(menuHeader);
        hash.AppendData(menuContent);
        return new NativeTutorialCatalog(memory.SourceHash, Convert.ToHexString(hash.GetHashAndReset()), tutorials.MoveToImmutable(), ReadGroupedTexts(Resource(2), 7), ReadGroupedTexts(Resource(2), 4), initial.ToArray(), kernel.ToArray());

        ImmutableArray<byte> Resource(int slot)
        {
            uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(menuHeader.AsSpan(slot * 8));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(menuHeader.AsSpan(slot * 8 + 4));
            if (rawOffset is 0 or uint.MaxValue || size == 0 || (ulong)rawOffset - 1 + size > (ulong)menuContent.Length)
            {
                throw new InvalidDataException($"Native tutorial menu resource{slot} is vacant or outside mngrp.bin.");
            }
            return ImmutableArray.CreateRange(menuContent.AsSpan(checked((int)rawOffset - 1), checked((int)size)).ToArray());
        }
    }

    internal static ImmutableArray<NativeTutorialText> ReadGroupedTexts(ImmutableArray<byte> bytes, int table)
    {
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan());
        if (table < 0 || table >= count || count > (bytes.Length - 2) / 2)
        {
            throw new InvalidDataException("Native menu text group lacks its requested table.");
        }
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2 + table * 2, 2));
        int end = bytes.Length;
        for (int next = table + 1; next < count; next++)
        {
            int nextOffset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2 + next * 2, 2));
            if (nextOffset != 0)
            {
                end = nextOffset;
                break;
            }
        }
        if (offset < 2 + count * 2 || offset >= end || end > bytes.Length)
        {
            throw new InvalidDataException("Native menu text table is vacant or outside its group.");
        }
        return ReadTexts(ImmutableArray.CreateRange(bytes.AsSpan(offset, end - offset).ToArray()));
    }

    internal static ImmutableArray<NativeTutorialText> ReadTexts(ImmutableArray<byte> bytes)
    {
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan());
        if (count > (bytes.Length - 2) / 2)
        {
            throw new InvalidDataException("Native tutorial string table has an incomplete offset directory.");
        }
        ImmutableArray<NativeTutorialText>.Builder result = ImmutableArray.CreateBuilder<NativeTutorialText>(count);
        int previous = 2 + count * 2;
        for (int index = 0; index < count; index++)
        {
            ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2 + index * 2, 2));
            if (offset == 0)
            {
                result.Add(new NativeTutorialText(index, offset, "", []));
                continue;
            }
            if (offset < previous || offset >= bytes.Length)
            {
                throw new InvalidDataException("Native tutorial text offsets are unordered or outside their resource.");
            }
            int end = bytes.Length;
            for (int next = index + 1; next < count; next++)
            {
                int nextOffset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2 + next * 2, 2));
                if (nextOffset != 0)
                {
                    end = nextOffset;
                    break;
                }
            }
            if (end < offset || end > bytes.Length)
            {
                throw new InvalidDataException("Native tutorial text span is outside its resource.");
            }
            ReadOnlySpan<byte> reserved = bytes.AsSpan(offset, end - offset);
            int textLength = 0;
            if (!reserved.IsEmpty && (reserved[0] != 0 || reserved.Length > 1 && reserved[1] != 0))
            {
                int searchStart = reserved[0] == 0 ? 1 : 0;
                int terminator = reserved[searchStart..].IndexOf((byte)0);
                textLength = terminator < 0 ? reserved.Length : searchStart + terminator;
            }
            ImmutableArray<byte> encoded = ImmutableArray.CreateRange(reserved[..textLength].ToArray());
            result.Add(new NativeTutorialText(index, offset, FF8Encoding.CreateEuropean().GetString(encoded.AsSpan()), encoded));
            previous = offset;
        }
        return result.MoveToImmutable();
    }
}

public sealed partial class GameAssets
{
    private NativeTutorialCatalog? nativeTutorials;

    public NativeTutorialCatalog ReadNativeTutorialCatalog()
    {
        if (nativeTutorials is not null)
        {
            return nativeTutorials;
        }
        using IFlArchive menu = OpenArchive("menu");
        using IFlArchive main = OpenArchive("main");
        return nativeTutorials = NativeTutorialCatalog.Read(ReadNativeMemory(), Read(menu, "mngrphd.bin"), Read(menu, "mngrp.bin"), Read(main, "init.out"), Read(main, "kernel.bin"));
    }
}
