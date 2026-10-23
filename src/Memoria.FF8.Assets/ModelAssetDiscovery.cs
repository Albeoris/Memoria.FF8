using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public enum ModelAssetDiscoveryKind
{
    Object,
    Clip,
    Diagnostic,
    Progress,
    Coverage
}

/// <summary>A supported native binding. Load retains the complete owner rig; ClipIndex selects one finite clip.</summary>
public sealed record ModelAssetDiscoveryItem(string Id, string Name, ModelAssetDiscoveryKind Kind, string OwnerId, string ResourcePath, string Bank, int ClipIndex, string Provenance, Func<EditableModel>? Load = null)
{
    public string? Error { get; init; }
    public bool UsesOwnerSkeleton { get; init; }
    public string? ReplacementPart { get; init; }
    public string? AlternativeGroup { get; init; }
    public string? RigOwnerId { get; init; }
    public int NativeFrameCount { get; init; }
    public float SecondsPerFrame { get; init; } = 1f / ModelExchange.FramesPerSecond;
    public string? GroupId { get; init; }
}

/// <summary>Resource addresses used by the current explicit Squall external bank loaders.</summary>
public sealed record ModelExternalBankDescriptor(string Bank, ushort MagicId, uint Address);

public static class ModelAssetDiscoveryBindings
{
    public static IReadOnlyList<ModelExternalBankDescriptor> SquallExternalBanks { get; } = Array.AsReadOnly(new ModelExternalBankDescriptor[]
    {
        new("renzokuken-160", 160, 0xEA1BA0), new("renzokuken-141", 141, 0xEA8AB4), new("renzokuken-259", 259, 0xE9264C), new("renzokuken-332", 332, 0xE7E368), new("renzokuken-333", 333, 0xE7B0C0),
        new("finisher-163", 163, 0xD99DC8), new("finisher-162", 162, 0xDA0100), new("finisher-164", 164, 0xD88AA0), new("finisher-165", 165, 0xD780C8)
    });

    public static string ClipTarget(string ownerId, string bank, int slot) => $"{ownerId}/clips/{bank}/{slot}";

    public static string ResolvePartyFile(IGameNativeMemorySource memory, int character, bool weapon, int selector)
    {
        if ((uint)character >= 11 || selector is < 0 or > 255 || weapon && character == 7)
        {
            throw new NotSupportedException("This party model has no native file selector binding.");
        }
        uint table = weapon ? 0xB8B940u : 0xB8B914u;
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(table + (uint)character * 4, 4));
        if (pointer == 0)
        {
            throw new NotSupportedException("The native party selector table is absent.");
        }
        short file = BinaryPrimitives.ReadInt16LittleEndian(memory.Read(checked(pointer + (uint)selector * 2), 2));
        if (file < 0)
        {
            throw new NotSupportedException("The native party selector has an empty file slot.");
        }
        return ResolveBattleFile(memory, checked((ushort)file));
    }

    public static string ResolveBattleFile(IGameNativeMemorySource memory, ushort file)
    {
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCC + (uint)file * 4, 4));
        byte[] name = memory.Read(pointer, 128);
        int end = Array.IndexOf(name, (byte)0);
        if (end <= 0)
        {
            throw new InvalidDataException("Native battle filename is not terminated.");
        }
        string value = Encoding.ASCII.GetString(name, 0, end).ToLowerInvariant();
        if (value.IndexOfAny(['/', '\\', ':']) >= 0)
        {
            throw new InvalidDataException("Native battle filename must be a resource member.");
        }
        return "battle/" + value;
    }

    public static byte[] ReadExternalBank(IGameNativeMemorySource memory, uint address, int expectedSectionCount = 3)
    {
        byte[] header = memory.Read(address, 20);
        if (expectedSectionCount is not (2 or 3) || BinaryPrimitives.ReadInt32LittleEndian(header) != expectedSectionCount)
        {
            throw new NotSupportedException("External skeletal bank differs from its explicit body-only or paired directory binding.");
        }
        int length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4 + expectedSectionCount * 4));
        if (length < 8 + expectedSectionCount * 4 || length > 16 * 1024 * 1024)
        {
            throw new InvalidDataException("External skeletal bank length exceeds its bounds.");
        }
        return memory.Read(address, length);
    }
}

public sealed partial class GameAssets
{
    /// <summary>Discovers explicit loader bindings on demand without constructing a gameplay session.</summary>
    public IEnumerable<ModelAssetDiscoveryItem> DiscoverModelAssets(ModelCatalogEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new(entry.Id + "/discovering", "Discovering", ModelAssetDiscoveryKind.Progress, entry.Id, entry.ResourcePath, "", -1, "selected resource");
        bool incomplete = false;
        EditableModel? selected = null;
        string? error = null;
        try
        {
            selected = LoadCatalogModel(entry);
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            error = exception.Message;
        }
        if (selected is null)
        {
            yield return DiscoveryDiagnostic(entry, "selected", error ?? "The selected model is unavailable.");
            yield return DiscoveryCoverage(entry, true);
            yield break;
        }
        EditableModel selectedModel = selected;
        foreach (ModelAssetDiscoveryItem item in entry.Category == "Battle weapon" ? [] : DiscoverEditable(entry, selectedModel, "embedded", "embedded bank", () => selectedModel, true))
        {
            yield return item;
        }
        if (entry.Category == "Field")
        {
            foreach (ModelAssetDiscoveryItem item in DiscoverFieldReferences(entry, cancellationToken))
            {
                incomplete |= item.Kind == ModelAssetDiscoveryKind.Diagnostic;
                yield return item;
            }
        }
        else if (TryPartyAddress(entry.ResourcePath, out int character, out bool weapon))
        {
            foreach (ModelAssetDiscoveryItem item in DiscoverPartyLinks(entry, character, weapon, cancellationToken))
            {
                incomplete |= item.Kind == ModelAssetDiscoveryKind.Diagnostic;
                yield return item;
            }
        }
        else if (entry.Category is "World character" or "World static")
        {
            yield return new(entry.Id + "/scope", "Only this shape's loader-owned bank is supported; no cross-shape or battle bindings.", ModelAssetDiscoveryKind.Diagnostic, entry.Id, entry.ResourcePath, "", -1, "world loader scope");
            incomplete = true;
        }
        else if (entry.ResourcePath.StartsWith("battle/", StringComparison.OrdinalIgnoreCase) && entry.Category is not ("Battle enemy" or "Battle stage"))
        {
            yield return DiscoveryDiagnostic(entry, "bank-scope", "GF/effect and proxy resources have no general standalone association contract; only this decoded embedded bank is exposed.");
            incomplete = true;
        }
        yield return DiscoveryCoverage(entry, incomplete);
    }

    private static IEnumerable<ModelAssetDiscoveryItem> DiscoverEditable(ModelCatalogEntry entry, EditableModel model, string bank, string provenance, Func<EditableModel> load, bool includeObject, string? groupOwner = null)
    {
        if (includeObject)
        {
            yield return new(entry.Id, entry.Name, ModelAssetDiscoveryKind.Object, entry.Id, entry.ResourcePath, bank, -1, provenance, load);
        }
        for (int index = 0; index < model.Animations.Count; index++)
        {
            EditableAnimation clip = model.Animations[index];
            int frames = clip.Channels.Select(channel => channel.Times.Length).DefaultIfEmpty(0).Max();
            yield return new(ModelAssetDiscoveryBindings.ClipTarget(entry.Id, bank, index), $"{bank} / Clip {index:000}", ModelAssetDiscoveryKind.Clip, entry.Id, entry.ResourcePath, bank, index, provenance, load)
            {
                NativeFrameCount = frames,
                SecondsPerFrame = 1f / model.NativeFramesPerSecond,
                GroupId = ModelAssetDiscoveryBindings.ClipTarget(groupOwner ?? entry.Id, bank, index)
            };
        }
    }

    private static bool TryPartyAddress(string resource, out int character, out bool weapon)
    {
        string name = Path.GetFileName(resource.Replace('\\', '/'));
        character = -1;
        weapon = false;
        if (!resource.StartsWith("battle/", StringComparison.OrdinalIgnoreCase) || name.Length != 10 || name[0] != 'd' || name[2] is not ('c' or 'w') || !name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        weapon = name[2] == 'w';
        return int.TryParse(name.AsSpan(1, 1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out character) && character < 11;
    }

    private static ModelAssetDiscoveryItem DiscoveryDiagnostic(ModelCatalogEntry entry, string key, string message) => new(entry.Id + "/diagnostic/" + key, message, ModelAssetDiscoveryKind.Diagnostic, entry.Id, entry.ResourcePath, "", -1, "unsupported or unavailable source") { Error = message };
    private static ModelAssetDiscoveryItem DiscoveryCoverage(ModelCatalogEntry entry, bool incomplete) => new(entry.Id + "/coverage", incomplete ? "Incomplete: see source diagnostics" : "Complete for supported sources", ModelAssetDiscoveryKind.Coverage, entry.Id, entry.ResourcePath, "", -1, "discovery scope") { Error = incomplete ? "Some sources are unsupported or unavailable." : null };
}

internal static partial class BattleResourceReader
{
    internal static BattleModel ReadLinkedAnimationBank(BattleModel owner, byte[] bank, bool weapon)
    {
        using BinaryReader reader = Reader(bank);
        int[] offsets = Directory(reader);
        if (offsets.Length is not (2 or 3) || weapon && offsets.Length != 3)
        {
            throw new NotSupportedException("External animation bank requires a body-only or paired directory.");
        }
        int section = weapon ? 2 : 0;
        IReadOnlyList<BattleAnimation> animations = Animations(reader, offsets[section], owner.Bones.Count);
        int end = weapon ? bank.Length : offsets[1];
        return owner with
        {
            Animations = animations,
            NativePoseSource = owner.NativePoseSource is { } pose ? pose with { Animations = bank.AsSpan(offsets[section], end - offsets[section]).ToArray().ToImmutableArray() } : null
        };
    }
}
