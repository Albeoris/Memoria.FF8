using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The texture-relevant fields of one physical52-byte resource row; unused retained words remain unknown.</summary>
public sealed record NativeBattleModelTextureCacheRow([property: JsonRequired] byte Kind, [property: JsonRequired] byte? ModelId, [property: JsonRequired] ushort? TextureMask, [property: JsonRequired] NativeBattleModelTextureBinding? Binding);
public sealed record NativeBattleModelTextureCacheLoad(NativeBattleModelTextureCache Cache, int ResourceIndex, NativeBattleModelTextureBinding Binding, NativeBattleModelTextureLoad? Upload);

/// <summary>508260's eleven resource rows and1D98B54 allocation mask, restricted to original texture ownership.</summary>
public sealed record NativeBattleModelTextureCache([property: JsonRequired] ImmutableArray<NativeBattleModelTextureCacheRow> Rows, [property: JsonRequired] ushort BlockedSlots)
{
    public ImmutableDictionary<byte, ImmutableArray<byte>> EnemyTextureMotion { get; init; } = ImmutableDictionary<byte, ImmutableArray<byte>>.Empty;
    public static NativeBattleModelTextureCache FromColdProcess() => new(Enumerable.Range(0, 11).Select(_ => new NativeBattleModelTextureCacheRow(0, 0, 0, null)).ToImmutableArray(), 0);

    public static NativeBattleModelTextureCache Initialize508260(NativeBattleModelTextureCache? retained) => retained?.Reset508260() ?? new(Enumerable.Range(0, 11).Select(_ => new NativeBattleModelTextureCacheRow(0, null, null, null)).ToImmutableArray(), 63);

    public NativeBattleModelTextureCache Reset508260()
    {
        Validate();
        return this with { Rows = Rows.Select(row => row with { Kind = 0 }).ToImmutableArray(), BlockedSlots = 63 };
    }

    public void Validate()
    {
        if (EnemyTextureMotion is null || EnemyTextureMotion.Values.Any(table => table.IsDefault) || Rows.IsDefault || Rows.Length != 11 || Rows.Any(row => row is null || row.Kind > 3 || row.Kind != 0 && (row.ModelId is null || row.Binding is null) || row.Kind is 1 or 3 && row.TextureMask is null || row.Binding is { } binding && (binding.PaletteRows.IsDefault || binding.PaletteRows.Length != 16 || binding.PaletteRows.Any(value => value is >= 15))))
        {
            throw new InvalidDataException("Native texture cache requires its eleven physical resource rows, actual row types and known material bindings for allocated rows.");
        }
    }

    public NativeBattleModelTextureCacheLoad LoadPlayer(byte modelId, int physicalIndex, NativeBattleEffectHeapAllocation pack)
    {
        RequirePlayerIndex(physicalIndex);
        ushort mask = (ushort)(3 << (2 * physicalIndex));
        NativeBattleModelTextureLoad upload = NativeBattleModelTextures.Prepare507400(pack, unchecked((ushort)~mask));
        int index = ExistingOrFree(1, modelId);
        NativeBattleModelTextureCacheRow row = Rows[index] with { Kind = 1, ModelId = modelId, TextureMask = mask, Binding = upload.Binding };
        return new(this with { Rows = Rows.SetItem(index, row) }, index, upload.Binding, upload);
    }

    public NativeBattleModelTextureCacheLoad LoadLinkedWeapon(byte modelId, int physicalIndex, NativeBattleEffectHeapAllocation pack)
    {
        RequirePlayerIndex(physicalIndex);
        ushort mask = (ushort)(1 << (physicalIndex + 12));
        NativeBattleModelTextureLoad upload = NativeBattleModelTextures.Prepare507400(pack, unchecked((ushort)~mask));
        int index = ExistingOrFree(2, modelId);
        //507BF0/507E20 do not publish WORD2; only the relocated material binding is newly known here.
        NativeBattleModelTextureCacheRow row = Rows[index] with { Kind = 2, ModelId = modelId, Binding = upload.Binding };
        return new(this with { Rows = Rows.SetItem(index, row) }, index, upload.Binding, upload);
    }

    public NativeBattleModelTextureCacheLoad LoadEnemy(byte modelId, NativeBattleEffectHeapAllocation pack)
    {
        Validate();
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
        if ((uint)resourceIndex >= 11)
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
        return this with { Rows = rows, BlockedSlots = shared ? BlockedSlots : (ushort)(BlockedSlots & ~mask), EnemyTextureMotion = shared ? EnemyTextureMotion : EnemyTextureMotion.Remove(row.ModelId!.Value) };
    }

    private int ExistingOrFree(byte kind, byte modelId)
    {
        Validate();
        int free = -1;
        for (int index = 0; index < Rows.Length; index++)
        {
            NativeBattleModelTextureCacheRow row = Rows[index];
            if (row.Kind == kind && row.ModelId == modelId)
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

    private static void RequirePlayerIndex(int index)
    {
        if ((uint)index >= 3)
        {
            throw new InvalidDataException("Native player texture allocation requires its physical party index0..2.");
        }
    }
}
