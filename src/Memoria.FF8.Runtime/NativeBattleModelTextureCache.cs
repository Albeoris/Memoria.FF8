using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The texture-relevant fields of one physical52-byte resource row; unused retained words remain unknown.</summary>
public sealed record NativeBattleModelTextureCacheRow([property: JsonRequired] byte Kind, [property: JsonRequired] byte? ModelId, [property: JsonRequired] ushort? TextureMask, [property: JsonRequired] NativeBattleModelTextureBinding? Binding)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EnemyTextureAllocationId { get; init; }
}
public sealed record NativeBattleModelTextureCacheLoad(NativeBattleModelTextureCache Cache, int ResourceIndex, NativeBattleModelTextureBinding Binding, NativeBattleModelTextureLoad? Upload);

/// <summary>508260's original resource rows and allocation mask, with explicit actor ownership for hosted aliases.</summary>
public sealed record NativeBattleModelTextureCache([property: JsonRequired] ImmutableArray<NativeBattleModelTextureCacheRow> Rows, [property: JsonRequired] ushort BlockedSlots)
{
    private const ushort NativeEnemyBlockedSlots = 63;

    [JsonIgnore]
    public BattleModelRoster? PresentationRoster { get; init; }

    [JsonIgnore]
    public ImmutableDictionary<int, BattleModelOwner>? PresentationActors { get; init; }

    public ImmutableDictionary<byte, ImmutableArray<byte>> EnemyTextureMotion { get; init; } = ImmutableDictionary<byte, ImmutableArray<byte>>.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int NextEnemyTextureAllocationId { get; init; }
    public static NativeBattleModelTextureCache FromColdProcess() => new(Enumerable.Range(0, NativeBattleModelResourceRowAddress.NativeRowCount).Select(_ => new NativeBattleModelTextureCacheRow(0, 0, 0, null)).ToImmutableArray(), 0);

    public static NativeBattleModelTextureCache Initialize508260(NativeBattleModelTextureCache? retained) => retained?.Reset508260() ?? new(Enumerable.Range(0, NativeBattleModelResourceRowAddress.NativeRowCount).Select(_ => new NativeBattleModelTextureCacheRow(0, null, null, null)).ToImmutableArray(), NativeEnemyBlockedSlots);

    public NativeBattleModelTextureCache Reset508260()
    {
        Validate();
        return this with { Rows = Rows.Select(row => row with { Kind = 0 }).ToImmutableArray(), BlockedSlots = NativeEnemyBlockedSlots, PresentationActors = PresentationActors?.Clear() };
    }

    public NativeBattleModelTextureCache WithPresentationRoster(BattleModelRoster roster, ImmutableDictionary<int, BattleModelOwner>? rowOwners = null)
    {
        Validate();
        int required = NativeBattleModelResourceRowAddress.CapacityForRoster(roster);
        if (PresentationRoster is { } current && !current.Owners.SequenceEqual(roster.Owners))
        {
            throw new InvalidDataException("Texture cache already owns a different presentation roster.");
        }
        if (PresentationActors is { } existing && rowOwners is not null && (existing.Count != rowOwners.Count || existing.Any(pair => !rowOwners.TryGetValue(pair.Key, out BattleModelOwner? actor) || actor != pair.Value)))
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
            PresentationActors = PresentationActors ?? rowOwners ?? ImmutableDictionary<int, BattleModelOwner>.Empty
        };
        result.Validate();
        return result;
    }

    internal NativeBattleModelTextureCache RebindPresentationRoster(BattleModelRoster expected, BattleModelRoster replacement, IEnumerable<BattleModelOwner> retired)
    {
        Validate();
        HashSet<BattleModelOwner> retiredOwners = NativeBattleModelCpuBanks.ValidatePresentationRebind(PresentationRoster, expected, replacement, retired);
        ImmutableDictionary<int, BattleModelOwner> currentOwners = PresentationActors!;
        var owners = currentOwners.ToBuilder();
        var rows = Rows.ToBuilder();
        foreach ((int index, BattleModelOwner owner) in currentOwners)
        {
            if (retiredOwners.Contains(owner))
            {
                rows[index] = rows[index] with { Kind = 0 };
                owners.Remove(index);
            }
        }
        int required = NativeBattleModelResourceRowAddress.CapacityForRoster(replacement);
        if (rows.Count < required)
        {
            rows.AddRange(Enumerable.Repeat(new NativeBattleModelTextureCacheRow(0, null, null, null), required - rows.Count));
        }
        NativeBattleModelTextureCache result = this with
        {
            Rows = rows.ToImmutable(),
            PresentationRoster = replacement,
            PresentationActors = owners.ToImmutable()
        };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (EnemyTextureMotion is null || EnemyTextureMotion.Values.Any(table => table.IsDefault) || Rows.IsDefault || Rows.Length < NativeBattleModelResourceRowAddress.NativeRowCount || Rows.Length > NativeBattleModelResourceRowAddress.MaximumEncodedRowCount || Rows.Any(row => row is null || row.Kind > 3 || row.Kind != 0 && (row.ModelId is null || row.Binding is null) || row.Kind is 1 or 3 && row.TextureMask is null || row.Binding is { } binding && (binding.PaletteRows.IsDefault || binding.PaletteRows.Length != 16 || binding.PaletteRows.Any(value => value is >= 15))))
        {
            throw new InvalidDataException("Texture cache requires valid resource rows and known material bindings for allocated rows.");
        }
        if (NextEnemyTextureAllocationId < 0 || Rows.Any(row => row.EnemyTextureAllocationId is int id && (row.Kind is not (0 or 3) || (uint)id >= NextEnemyTextureAllocationId)))
        {
            throw new InvalidDataException("Enemy texture rows must refer to a published allocation generation.");
        }

        if (PresentationRoster is not { } roster)
        {
            if (PresentationActors is not null)
            {
                throw new InvalidDataException("The original texture allocator has no per-model aliases.");
            }
            return;
        }
        if (PresentationActors is not { } owners || owners.Count != Rows.Count(row => row.Kind != 0))
        {
            throw new InvalidDataException("Hosted texture aliases require an actor owner for each occupied row.");
        }
        foreach ((int row, BattleModelOwner actor) in owners)
        {
            if ((uint)row >= Rows.Length || Rows[row].Kind == 0)
            {
                throw new InvalidDataException("A hosted texture alias owner references an unoccupied resource row.");
            }
            RequirePresentationActor(roster, actor, requireParty: Rows[row].Kind is 1 or 2);
        }
    }

    public NativeBattleModelTextureCacheLoad LoadPlayer(byte modelId, int textureSlot, NativeBattleEffectHeapAllocation pack, BattlePartyModelOwner? owner = null)
    {
        ValidatePlayerTextureSlot(textureSlot, owner);
        ushort mask = (ushort)(3 << (2 * textureSlot));
        NativeBattleModelTextureLoad upload = NativeBattleModelTextures.Prepare507400(pack, unchecked((ushort)~mask));
        int index = ExistingOrFree(1, modelId, owner);
        NativeBattleModelTextureCacheRow row = Rows[index] with { Kind = 1, ModelId = modelId, TextureMask = mask, Binding = upload.Binding, EnemyTextureAllocationId = null };
        return new(PublishRow(index, row, owner), index, upload.Binding, upload);
    }

    public NativeBattleModelTextureCacheLoad LoadLinkedWeapon(byte modelId, int textureSlot, NativeBattleEffectHeapAllocation pack, BattlePartyModelOwner? owner = null)
    {
        ValidatePlayerTextureSlot(textureSlot, owner);
        ushort mask = (ushort)(1 << (textureSlot + 12));
        NativeBattleModelTextureLoad upload = NativeBattleModelTextures.Prepare507400(pack, unchecked((ushort)~mask));
        int index = ExistingOrFree(2, modelId, owner);
        //507BF0/507E20 do not publish WORD2; only the relocated material binding is newly known here.
        NativeBattleModelTextureCacheRow row = Rows[index] with { Kind = 2, ModelId = modelId, Binding = upload.Binding, EnemyTextureAllocationId = null };
        return new(PublishRow(index, row, owner), index, upload.Binding, upload);
    }

    public NativeBattleModelTextureCacheLoad LoadEnemy(byte modelId, NativeBattleEffectHeapAllocation pack, BattleModelOwner? owner = null)
    {
        Validate();
        if (PresentationRoster is { } roster)
        {
            BattleModelOwner actor = owner ?? throw new InvalidDataException("Hosted enemy texture allocation requires its actor row.");
            RequirePresentationActor(roster, actor, requireParty: false);
            int hostedIndex = ExistingOrFree(3, modelId, actor);
            NativeBattleModelTextureLoad hosted = NativeBattleModelTextures.Prepare507400(pack, NativeEnemyBlockedSlots);
            NativeBattleModelTextureCacheRow hostedRow = Rows[hostedIndex] with { Kind = 3, ModelId = modelId, TextureMask = hosted.AllocatedSlots, Binding = hosted.Binding, EnemyTextureAllocationId = NextEnemyTextureAllocationId };
            NativeBattleModelTextureCache hostedCache = PublishRow(hostedIndex, hostedRow, actor) with { NextEnemyTextureAllocationId = checked(NextEnemyTextureAllocationId + 1) };
            return new(hostedCache, hostedIndex, hosted.Binding, hosted);
        }
        if (owner is not null)
        {
            throw new InvalidDataException("Native enemy texture allocation has no hosted actor owner.");
        }

        int index = FirstFree();
        NativeBattleModelTextureCacheRow? existing = Rows.Take(AllocationExtent).FirstOrDefault(row => row.Kind == 3 && row.ModelId == modelId);
        if (existing is not null)
        {
            //507740 clones the52-byte row; its material tail and allocation mask remain shared.
            NativeBattleModelTextureBinding binding = existing.Binding ?? throw new InvalidDataException("Native507740 has no original shared material relocation.");
            return new(this with { Rows = Rows.SetItem(index, existing) }, index, binding, null);
        }
        NativeBattleModelTextureLoad upload = NativeBattleModelTextures.Prepare507400(pack, BlockedSlots);
        NativeBattleModelTextureCacheRow row = Rows[index] with { Kind = 3, ModelId = modelId, TextureMask = upload.AllocatedSlots, Binding = upload.Binding, EnemyTextureAllocationId = null };
        NativeBattleModelTextureCache cache = this with { Rows = Rows.SetItem(index, row), BlockedSlots = (ushort)(BlockedSlots | upload.AllocatedSlots) };
        return new(cache, index, upload.Binding, upload);
    }

    internal NativeBattleModelTextureCacheLoad CloneHostedEnemy(NativeBattleModelLoadTask task, int sourceIndex)
    {
        int index = SelectHostedLoadAlias(task, task.Owner);
        NativeBattleModelTextureCacheRow source = Rows[sourceIndex];
        if (sourceIndex == index || Rows[index].Kind != 0 || source.Kind != 3 || source.ModelId != task.ModelId || source.Binding is not { } binding)
        {
            throw new InvalidDataException("Native507740 requires an occupied source and a free destination for the same model.");
        }
        int id = source.EnemyTextureAllocationId ?? NextEnemyTextureAllocationId;
        NativeBattleModelTextureCacheRow shared = source with { EnemyTextureAllocationId = id };
        NativeBattleModelTextureCache cache = this with
        {
            Rows = Rows.SetItem(sourceIndex, shared).SetItem(index, shared),
            PresentationActors = PresentationActors!.Add(index, task.Owner),
            NextEnemyTextureAllocationId = source.EnemyTextureAllocationId is null ? checked(id + 1) : NextEnemyTextureAllocationId
        };
        return new(cache, index, binding, null);
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
        bool modelRetained = rows.Any(candidate => candidate.Kind == 3 && candidate.ModelId == row.ModelId);
        ushort mask = row.TextureMask ?? throw new InvalidDataException("Native5080D0 lost its original resource texture WORD2.");
        return this with { Rows = rows, PresentationActors = PresentationActors?.Remove(resourceIndex), BlockedSlots = modelRetained || PresentationRoster is not null ? BlockedSlots : (ushort)(BlockedSlots & ~mask), EnemyTextureMotion = modelRetained ? EnemyTextureMotion : EnemyTextureMotion.Remove(row.ModelId!.Value) };
    }

    internal NativeBattleModelTextureCache ReleaseEjectedModel508230(BattleModelOwner owner, byte modelId)
    {
        ImmutableArray<NativeBattleModelTextureCacheRow> rows = Rows;
        ImmutableDictionary<int, BattleModelOwner>? owners = PresentationActors;
        for (int index = 0; index < Rows.Length; index++)
        {
            NativeBattleModelTextureCacheRow row = Rows[index];
            if (row.Kind != 0 && row.ModelId == modelId && (PresentationActors is null || PresentationActors.GetValueOrDefault(index) == owner))
            {
                rows = rows.SetItem(index, row with { Kind = 0 });
                owners = owners?.Remove(index);
            }
        }
        return this with { Rows = rows, PresentationActors = owners };
    }

    internal int SelectHostedLoadAlias(NativeBattleModelLoadTask task, BattleModelOwner owner)
    {
        Validate();
        BattleModelRoster roster = PresentationRoster ?? throw new InvalidDataException("Hosted texture loading requires its presentation roster.");
        bool party = task.Callback is 0x5077B0 or 0x5079B0 or 0x507BF0;
        RequirePresentationActor(roster, owner, requireParty: party);
        return task.Callback switch
        {
            0x5077B0 or 0x5079B0 => ExistingOrFree(1, task.ModelId, owner),
            0x507BF0 => ExistingOrFree(2, task.ModelId, owner),
            0x507120 => ExistingOrFree(3, task.ModelId, owner),
            0x507F80 => FirstFree(),
            _ => throw new InvalidDataException("Hosted texture alias selection requires an allocating model callback.")
        };
    }

    private int ExistingOrFree(byte kind, byte modelId, BattleModelOwner? owner)
    {
        Validate();
        int free = -1;
        for (int index = 0; index < AllocationExtent; index++)
        {
            NativeBattleModelTextureCacheRow row = Rows[index];
            if (row.Kind == kind && row.ModelId == modelId && (PresentationActors is null || PresentationActors[index] == owner))
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

    private int AllocationExtent => PresentationActors is null ? NativeBattleModelResourceRowAddress.NativeRowCount : Rows.Length;

    private int FirstFree()
    {
        for (int index = 0; index < AllocationExtent; index++)
        {
            if (Rows[index].Kind == 0)
            {
                return index;
            }
        }
        throw new NotSupportedException("Native5073D0 has no free physical resource row; a null native allocation is unsupported.");
    }

    private NativeBattleModelTextureCache PublishRow(int index, NativeBattleModelTextureCacheRow row, BattleModelOwner? owner)
    {
        return this with { Rows = Rows.SetItem(index, row), PresentationActors = PresentationActors?.SetItem(index, owner ?? throw new InvalidDataException("An owned texture alias requires its model owner.")) };
    }

    private void ValidatePlayerTextureSlot(int slot, BattlePartyModelOwner? owner)
    {
        if ((uint)slot >= 3)
        {
            throw new InvalidDataException("Player texture allocation requires its original texture slot0..2.");
        }
        if (PresentationRoster is { } roster)
        {
            RequirePresentationActor(roster, owner ?? throw new InvalidDataException("A hosted player texture requires its model owner."), requireParty: true);
        }
        else if (owner is not null)
        {
            throw new InvalidDataException("The original texture cache has no hosted per-model aliases.");
        }
    }

    private static void RequirePresentationActor(BattleModelRoster roster, BattleModelOwner owner, bool requireParty)
    {
        if (!roster.Contains(owner) || requireParty && owner is not BattlePartyModelOwner)
        {
            throw new InvalidDataException("A hosted texture alias requires a bound actor on its actual side.");
        }
    }
}
