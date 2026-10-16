using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>50AFC0's parameter block beginning1D99A78;7C is a logical physical-header reference.</summary>
public sealed record NativeBattleEffectParameters([property: JsonRequired] byte Actor78, [property: JsonRequired] byte Flags79, [property: JsonRequired] ushort TargetMask7A, [property: JsonRequired] int HeaderIndex7C, [property: JsonRequired] byte Parameter81, [property: JsonRequired] byte Parameter83)
{
    public byte? Parameter80 { get; init; }
}
public sealed record NativeBattleEffectResourcePointer([property: JsonRequired] uint GlobalAddress, [property: JsonRequired] int ArenaOffset);

/// <summary>50AF20's selected module and the descriptor returned by its original entry.</summary>
public sealed record NativeBattleOriginalEffectState([property: JsonRequired] ushort RequestedEffect, [property: JsonRequired] int SelectedIndex, [property: JsonRequired] NativeBattleEffectBinding Binding, [property: JsonRequired] NativeBattleEffectParameters? Parameters, [property: JsonRequired] uint? ReturnedDescriptor)
{
    [JsonRequired]
    public ImmutableArray<NativeBattleEffectTaskPool> Pools { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<NativeBattleEffectResourcePointer> ResourcePointers { get; init; } = [];
    [JsonRequired]
    public uint? FireDrawBuffer { get; init; }
    public ImmutableArray<int>? FireModelCompletion { get; init; }
    public int? DrawArenaOffset { get; init; }

    public void Validate()
    {
        if (SelectedIndex is < 0 or >= 400 || Binding is null || Binding.Effect is < 1 or > 400 || Binding.Entry == 0 || Pools.IsDefault || Pools.Any(pool => pool is null) || ResourcePointers.IsDefault || ResourcePointers.Any(pointer => pointer is null || pointer.ArenaOffset is < 0 or > NativeBattleEffectArena.Length) || DrawArenaOffset is < 0 or >= NativeBattleEffectArena.Length || Parameters is { } parameters && (parameters.Actor78 >= 7 || parameters.HeaderIndex7C is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity) || ReturnedDescriptor is > 0 && !Pools.Any(pool => pool.DescriptorAddress == ReturnedDescriptor))
        {
            throw new InvalidDataException("Native effect service lacks its selected entry, parameter block or returned physical task descriptor.");
        }
        foreach (NativeBattleEffectTaskPool pool in Pools)
        {
            pool.Validate();
        }
        if (FireModelCompletion is { } completion && (completion.IsDefault || completion.Length != 7))
        {
            throw new InvalidDataException("Native6298A0 requires its seven DEE360 completion DWORDs.");
        }
    }
}

public static class NativeBattleOriginalEffects
{
    public static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState selected)
    {
        NativeBattleEffectInitializerAsset? asset = selected.Binding.InitializerAsset;
        NativeBattleEffectInitializerResource? resource = selected.Binding.InitializerResource;
        if (resource is not null)
        {
            if (selected.Binding.Initializer == 0 || selected.Binding.InitializerIsRet == true || string.IsNullOrEmpty(resource.Name) || resource.GlobalAddress == 0)
            {
                throw new InvalidDataException("Native effect resource descriptor contradicts its actual initializer.");
            }
            if (asset is null || asset.Name != resource.Name || asset.Data.IsDefault)
            {
                throw new NotSupportedException($"Native initializer{selected.Binding.Initializer:X} requires its actual {resource.Name} asset loaded by571B80.");
            }
        }
        else if (selected.Binding.Initializer != 0 && selected.Binding.InitializerIsRet != true)
        {
            throw new NotSupportedException($"Native50AF20 effect{selected.Binding.Effect} initializer{selected.Binding.Initializer:X} requires its original resource-loading contract.");
        }
        memory = memory.BeginLoad(selected.SelectedIndex);
        if (resource is not null)
        {
            int offset = memory.Cursor;
            memory = memory.LoadIntoArena(asset!.Data.AsSpan());
            selected = selected with { ResourcePointers = [new(resource.GlobalAddress, offset)] };
        }
        return (memory, selected);
    }

    public static NativeBattleOriginalEffectState Select(ushort effect, ImmutableArray<NativeBattleEffectBinding> catalog)
    {
        if (catalog.IsDefault || catalog.Length != 400 || catalog.Where((binding, index) => binding is null || binding.Effect != index + 1).Any())
        {
            throw new InvalidDataException("Native50AF20 requires the complete authenticated400-row effect catalog.");
        }
        int index = effect - 1;
        if (index is < 0 or >= 400)
        {
            index = 0;
        }
        //1D99A68 is assigned before the null-entry fallback; preserve the requested/clamped index.
        NativeBattleEffectBinding binding = catalog[catalog[index].Entry == 0 ? 0 : index];
        if (binding.Entry == 0)
        {
            throw new InvalidDataException("Native fallback effect1 has no executable entry.");
        }
        return new(effect, index, binding, null, null);
    }
}
