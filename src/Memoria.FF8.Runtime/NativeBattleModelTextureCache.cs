using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The texture-relevant fields of one physical52-byte resource row; unused retained words remain unknown.</summary>
public sealed record NativeBattleModelTextureCacheRow([property: JsonRequired] byte Kind, [property: JsonRequired] byte? ModelId, [property: JsonRequired] ushort? TextureMask, [property: JsonRequired] NativeBattleModelTextureBinding? Binding);
public sealed record NativeBattleModelTextureCacheLoad(NativeBattleModelTextureCache Cache, int ResourceIndex, NativeBattleModelTextureBinding Binding, NativeBattleModelTextureLoad? Upload);

/// <summary>508260's original resource rows and allocation mask, with explicit actor ownership for hosted aliases.</summary>
public sealed record NativeBattleModelTextureCache([property: JsonRequired] ImmutableArray<NativeBattleModelTextureCacheRow> Rows, [property: JsonRequired] ushort BlockedSlots)
{
    private const ushort NativeEnemyBlockedSlots = 63;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGraphicsRoster? PresentationRoster { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<int, int>? PresentationActors { get; init; }

    public ImmutableDictionary<byte, ImmutableArray<byte>> EnemyTextureMotion { get; init; } = ImmutableDictionary<byte, ImmutableArray<byte>>.Empty;
    public static NativeBattleModelTextureCache FromColdProcess() => new(Enumerable.Range(0, NativeBattleModelResourceRowAddress.NativeRowCount).Select(_ => new NativeBattleModelTextureCacheRow(0, 0, 0, null)).ToImmutableArray(), 0);

    public static NativeBattleModelTextureCache Initialize508260(NativeBattleModelTextureCache? retained) => retained?.Reset508260() ?? new(Enumerable.Range(0, NativeBattleModelResourceRowAddress.NativeRowCount).Select(_ => new NativeBattleModelTextureCacheRow(0, null, null, null)).ToImmutableArray(), NativeEnemyBlockedSlots);

    public NativeBattleModelTextureCache Reset508260()
    {
        Validate();
        return this with { Rows = Rows.Select(row => row with { Kind = 0 }).ToImmutableArray(), BlockedSlots = NativeEnemyBlockedSlots, PresentationActors = PresentationActors?.Clear() };
    }

    public NativeBattleModelTextureCache WithPresentationRoster(NativeBattleGraphicsRoster roster, ImmutableDictionary<int, int>? rowOwners = null)
    {
        Validate();
        int required = NativeBattleModelResourceRowAddress.CapacityForRoster(roster);
        if (PresentationRoster is { } current && !current.Rows.SequenceEqual(roster.Rows))
        {
            throw new InvalidDataException("Texture cache already owns a different presentation roster.");
        }
        if (PresentationActors is { } existing && rowOwners is not null && (existing.Count != rowOwners.Count || existing.Any(pair => !rowOwners.TryGetValue(pair.Key, out int actor) || actor != pair.Value)))
        {
            throw new InvalidDataException("Texture cache already owns different presentation aliases.");
        }
        ImmutableArray<NativeBattleModelTextureCacheRow> rows = Rows;
        if (rows.Length < required)
        {
            rows = rows.AddRange(Enumerable.Repeat(new NativeBattleModelTextureCacheRow(0, null, null, null), required - rows.Length));
        }
        NativeBattleModelTextureCache result = this with
        {
            Rows = rows,
            PresentationRoster = roster,
            PresentationActors = PresentationActors ?? rowOwners ?? ImmutableDictionary<int, int>.Empty
        };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (EnemyTextureMotion is null || EnemyTextureMotion.Values.Any(table => table.IsDefault) || Rows.IsDefault || Rows.Length < NativeBattleModelResourceRowAddress.NativeRowCount || Rows.Length > NativeBattleModelResourceRowAddress.MaximumRowCount || Rows.Any(row => row is null || row.Kind > 3 || row.Kind != 0 && (row.ModelId is null || row.Binding is null) || row.Kind is 1 or 3 && row.TextureMask is null || row.Binding is { } binding && (binding.PaletteRows.IsDefault || binding.PaletteRows.Length != 16 || binding.PaletteRows.Any(value => value is >= 15))))
        {
            throw new InvalidDataException("Texture cache requires valid resource rows and known material bindings for allocated rows.");
        }

        if (PresentationRoster is not { } roster)
        {
            if (Rows.Length != NativeBattleModelResourceRowAddress.NativeRowCount || PresentationActors is not null)
            {
                throw new InvalidDataException("Native texture cache requires its eleven original aliases without presentation owners.");
            }
            return;
        }
        roster.Validate();
        if (PresentationActors is not { } owners || owners.Count != Rows.Count(row => row.Kind != 0))
        {
            throw new InvalidDataException("Hosted texture aliases require an actor owner for each occupied row.");
        }
        foreach ((int row, int actor) in owners)
        {
            if ((uint)row >= Rows.Length || Rows[row].Kind == 0)
            {
                throw new InvalidDataException("A hosted texture alias owner references an unoccupied resource row.");
            }
            RequirePresentationActor(roster, actor, requireParty: Rows[row].Kind is 1 or 2);
        }
    }

    public NativeBattleModelTextureCacheLoad LoadPlayer(byte modelId, int physicalIndex, NativeBattleEffectHeapAllocation pack)
    {
        int textureSlot = PlayerTextureSlot(physicalIndex);
        ushort mask = (ushort)(3 << (2 * textureSlot));
        NativeBattleModelTextureLoad upload = NativeBattleModelTextures.Prepare507400(pack, unchecked((ushort)~mask));
        int index = ExistingOrFree(1, modelId, physicalIndex);
        NativeBattleModelTextureCacheRow row = Rows[index] with { Kind = 1, ModelId = modelId, TextureMask = mask, Binding = upload.Binding };
        return new(PublishRow(index, row, physicalIndex), index, upload.Binding, upload);
    }

    public NativeBattleModelTextureCacheLoad LoadLinkedWeapon(byte modelId, int physicalIndex, NativeBattleEffectHeapAllocation pack)
    {
        int textureSlot = PlayerTextureSlot(physicalIndex);
        ushort mask = (ushort)(1 << (textureSlot + 12));
        NativeBattleModelTextureLoad upload = NativeBattleModelTextures.Prepare507400(pack, unchecked((ushort)~mask));
        int index = ExistingOrFree(2, modelId, physicalIndex);
        //507BF0/507E20 do not publish WORD2; only the relocated material binding is newly known here.
        NativeBattleModelTextureCacheRow row = Rows[index] with { Kind = 2, ModelId = modelId, Binding = upload.Binding };
        return new(PublishRow(index, row, physicalIndex), index, upload.Binding, upload);
    }

    public NativeBattleModelTextureCacheLoad LoadEnemy(byte modelId, NativeBattleEffectHeapAllocation pack, int? actorRow = null)
    {
        Validate();
        if (PresentationRoster is { } roster)
        {
            int actor = actorRow ?? throw new InvalidDataException("Hosted enemy texture allocation requires its actor row.");
            RequirePresentationActor(roster, actor, requireParty: false);
            int hostedIndex = ExistingOrFree(3, modelId, actor);
            NativeBattleModelTextureLoad hosted = NativeBattleModelTextures.Prepare507400(pack, NativeEnemyBlockedSlots);
            NativeBattleModelTextureCacheRow hostedRow = Rows[hostedIndex] with { Kind = 3, ModelId = modelId, TextureMask = hosted.AllocatedSlots, Binding = hosted.Binding };
            return new(PublishRow(hostedIndex, hostedRow, actor), hostedIndex, hosted.Binding, hosted);
        }
        if (actorRow is not null)
        {
            throw new InvalidDataException("Native enemy texture allocation has no hosted actor owner.");
        }

        int index = FirstFree();
        NativeBattleModelTextureCacheRow? existing = Rows.FirstOrDefault(row => row.Kind == 3 && row.ModelId == modelId);
        if (existing is not null)
        {
            //507740 clones the52-byte row; its material tail and allocation mask remain shared.
            NativeBattleModelTextureBinding binding = existing.Binding ?? throw new InvalidDataException("Native507740 has no original shared material relocation.");
            return new(this with { Rows = Rows.SetItem(index, existing) }, index, binding, null);
        }
        NativeBattleModelTextureLoad upload = NativeBattleModelTextures.Prepare507400(pack, BlockedSlots);
        NativeBattleModelTextureCacheRow row = Rows[index] with { Kind = 3, ModelId = modelId, TextureMask = upload.AllocatedSlots, Binding = upload.Binding };
        NativeBattleModelTextureCache cache = this with { Rows = Rows.SetItem(index, row), BlockedSlots = (ushort)(BlockedSlots | upload.AllocatedSlots) };
        return new(cache, index, upload.Binding, upload);
    }

    public NativeBattleModelTextureCache Release5080D0(int resourceIndex)
    {
        Validate();
        if ((uint)resourceIndex >= Rows.Length)
        {
            throw new InvalidDataException("Native5080D0 requires the actual row+12 resource pointer.");
        }
        NativeBattleModelTextureCacheRow row = Rows[resourceIndex];
        if (row.Kind != 3)
        {
            return this;
        }
        ImmutableArray<NativeBattleModelTextureCacheRow> rows = Rows.SetItem(resourceIndex, row with { Kind = 0 });
        bool shared = rows.Any(candidate => candidate.Kind == 3 && candidate.ModelId == row.ModelId);
        ushort mask = row.TextureMask ?? throw new InvalidDataException("Native5080D0 lost its original resource texture WORD2.");
        return this with { Rows = rows, PresentationActors = PresentationActors?.Remove(resourceIndex), BlockedSlots = shared || PresentationRoster is not null ? BlockedSlots : (ushort)(BlockedSlots & ~mask), EnemyTextureMotion = shared ? EnemyTextureMotion : EnemyTextureMotion.Remove(row.ModelId!.Value) };
    }

    private int ExistingOrFree(byte kind, byte modelId, int actorRow)
    {
        Validate();
        int free = -1;
        for (int index = 0; index < Rows.Length; index++)
        {
            NativeBattleModelTextureCacheRow row = Rows[index];
            if (row.Kind == kind && row.ModelId == modelId && (PresentationActors is null || PresentationActors[index] == actorRow))
            {
                return index;
            }
            if (free < 0 && row.Kind == 0)
            {
                free = index;
            }
        }
        return free >= 0 ? free : throw new NotSupportedException("Native5073D0 has no free physical resource row; a null native allocation is unsupported.");
    }

    private int FirstFree()
    {
        for (int index = 0; index < Rows.Length; index++)
        {
            if (Rows[index].Kind == 0)
            {
                return index;
            }
        }
        throw new NotSupportedException("Native5073D0 has no free physical resource row; a null native allocation is unsupported.");
    }

    private NativeBattleModelTextureCache PublishRow(int index, NativeBattleModelTextureCacheRow row, int actorRow)
    {
        return this with { Rows = Rows.SetItem(index, row), PresentationActors = PresentationActors?.SetItem(index, actorRow) };
    }

    private int PlayerTextureSlot(int index)
    {
        if (PresentationRoster is { } roster)
        {
            RequirePresentationActor(roster, index, requireParty: true);
            return index < 3 ? index : 0;
        }
        if ((uint)index >= 3)
        {
            throw new InvalidDataException("Native player texture allocation requires its physical party index0..2.");
        }
        return index;
    }

    private static void RequirePresentationActor(NativeBattleGraphicsRoster roster, int index, bool requireParty)
    {
        if ((uint)index >= roster.ActorCount || roster.Rows[index] is not { } actor || requireParty && !actor.IsParty)
        {
            throw new InvalidDataException("A hosted texture alias requires a bound actor on its actual side.");
        }
    }
}
