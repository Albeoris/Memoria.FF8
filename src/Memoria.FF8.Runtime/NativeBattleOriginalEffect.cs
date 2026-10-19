using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>50AFC0's parameter block beginning1D99A78;7C is a logical physical-header reference.</summary>
public sealed record NativeBattleEffectParameters([property: JsonRequired] byte Actor78, [property: JsonRequired] byte Flags79, [property: JsonRequired] ushort TargetMask7A, [property: JsonRequired] int HeaderIndex7C, [property: JsonRequired] byte Parameter81, [property: JsonRequired] byte Parameter83)
{
    public byte? Parameter80 { get; init; }
    public byte? Parameter82 { get; init; }
}
public sealed record NativeBattleEffectResourcePointer([property: JsonRequired] uint GlobalAddress, [property: JsonRequired] int ArenaOffset);
public sealed record NativeBattleEffectHeapResourcePointer([property: JsonRequired] uint GlobalAddress, [property: JsonRequired] int AllocationIndex);

/// <summary>50AF20's selected module and the descriptor returned by its original entry.</summary>
public sealed record NativeBattleOriginalEffectState([property: JsonRequired] ushort RequestedEffect, [property: JsonRequired] int SelectedIndex, [property: JsonRequired] NativeBattleEffectBinding Binding, [property: JsonRequired] NativeBattleEffectParameters? Parameters, [property: JsonRequired] uint? ReturnedDescriptor)
{
    [JsonRequired]
    public ImmutableArray<NativeBattleEffectTaskPool> Pools { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<NativeBattleEffectResourcePointer> ResourcePointers { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleEffectHeapResourcePointer>? HeapResourcePointers { get; init; }
    [JsonRequired]
    public uint? FireDrawBuffer { get; init; }
    public ImmutableArray<int>? FireModelCompletion { get; init; }
    public int? DrawArenaOffset { get; init; }
    public NativeBattleQuezacotlState? Quezacotl { get; init; }
    public NativeBattleCureState? Cure { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleLifeState? Life { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoubleState? Double { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleTripleState? Triple { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBio22State? Bio22 { get; init; }
    public NativeBattleEffect311State? Gravija { get; init; }
    public NativeBattleCuraState? Cura { get; init; }
    public NativeBattleCuragaState? Curaga { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRestorationState? Restoration { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRecoveryState? Recovery { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFiragaState? Firaga { get; init; }

    public void Validate()
    {
        if (SelectedIndex is < 0 or >= 400 || Binding is null || Binding.Effect is < 1 or > 400 || Binding.Entry == 0 || Pools.IsDefault || Pools.Any(pool => pool is null) || ResourcePointers.IsDefault || ResourcePointers.Any(pointer => pointer is null || pointer.ArenaOffset is < 0 or > NativeBattleEffectArena.Length) || DrawArenaOffset is < 0 or >= NativeBattleEffectArena.Length || Parameters is { } parameters && (parameters.Actor78 >= 7 || parameters.HeaderIndex7C is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity) || ReturnedDescriptor is > 0 && !Pools.Any(pool => pool.DescriptorAddress == ReturnedDescriptor))
        {
            throw new InvalidDataException("Native effect service lacks its selected entry, parameter block or returned physical task descriptor.");
        }
        if (Binding.InitializerResources is not null || Binding.InitializerAssets is not null)
        {
            _ = NativeBattleEffectInitializerPolicy.Assets(Binding);
        }
        if (HeapResourcePointers is { } heapPointers && (heapPointers.IsDefaultOrEmpty || heapPointers.Any(pointer => pointer is null || pointer.GlobalAddress == 0 || pointer.AllocationIndex is < 0 or >= 256) || heapPointers.Select(pointer => pointer.GlobalAddress).Distinct().Count() != heapPointers.Length || heapPointers.Any(pointer => ResourcePointers.Any(arena => arena.GlobalAddress == pointer.GlobalAddress))))
        {
            throw new InvalidDataException("Native heap resource globals require explicit distinct allocation-slot owners, separate from arena pointers.");
        }
        Firaga?.Validate();
        foreach (NativeBattleEffectTaskPool pool in Pools)
        {
            pool.Validate();
        }
        if (FireModelCompletion is { } completion && (completion.IsDefault || completion.Length != 7))
        {
            throw new InvalidDataException("Native6298A0 requires its seven DEE360 completion DWORDs.");
        }
    }

    public NativeBattleEffectHeapAllocation ReadHeapResource(NativeBattleEffectMemory memory, uint globalAddress)
    {
        Validate();
        memory.Validate();
        if (HeapResourcePointers is not { } pointers || pointers.SingleOrDefault(pointer => pointer.GlobalAddress == globalAddress) is not { } owner)
        {
            throw new NotSupportedException("Native heap resource has no retained publication owner for this global.");
        }
        if (owner.AllocationIndex >= memory.AllocationCount || memory.Allocations[owner.AllocationIndex] is not { } allocation)
        {
            throw new NotSupportedException("Native heap resource publication points outside the retained allocated prefix.");
        }
        return allocation;
    }
}

public static class NativeBattleOriginalEffects
{
    public static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState selected)
    {
        if (selected.Binding is { Effect: 90, Entry: 0x762360, Initializer: 0x762320 })
        {
            return InitializeTonberry762320(memory, selected);
        }
        if (selected.Binding.Initializer == 0x5A8730 || selected.Binding.Cactuar199Resources is not null)
        {
            return InitializeCactuar5A8730(memory, selected);
        }
        ImmutableArray<NativeBattleEffectInitializerResource> resources = NativeBattleEffectInitializerPolicy.Resources(selected.Binding);
        ImmutableArray<NativeBattleEffectInitializerAsset> assets = NativeBattleEffectInitializerPolicy.Assets(selected.Binding);
        if (selected.Binding.Initializer == 0x6C3560)
        {
            if (selected.Binding.InitializerResources is not null || selected.Binding.InitializerAssets is not null)
            {
                throw new InvalidDataException("The specialized native initializer cannot replace its resource contract with a generic asset list.");
            }
            return NativeBattleQuezacotl.Initialize6C3560(memory, selected);
        }
        if (!resources.IsEmpty)
        {
            if (selected.Binding.Initializer == 0 || selected.Binding.InitializerIsRet == true || resources.Any(resource => string.IsNullOrEmpty(resource.Name) || resource.GlobalAddress == 0))
            {
                throw new InvalidDataException("Native effect resource descriptor contradicts its actual initializer.");
            }
            if (assets.Length != resources.Length || assets.Where((asset, index) => asset is null || asset.Name != resources[index].Name || asset.Data.IsDefault).Any())
            {
                throw new NotSupportedException($"Native initializer{selected.Binding.Initializer:X} requires every ordered original571B80 asset.");
            }
        }
        else if (selected.Binding.Initializer != 0 && selected.Binding.InitializerIsRet != true)
        {
            throw new NotSupportedException($"Native50AF20 effect{selected.Binding.Effect} initializer{selected.Binding.Initializer:X} requires its original resource-loading contract.");
        }
        memory = memory.BeginLoad(selected.SelectedIndex);
        ImmutableArray<NativeBattleEffectResourcePointer>.Builder pointers = ImmutableArray.CreateBuilder<NativeBattleEffectResourcePointer>(resources.Length);
        for (int index = 0; index < resources.Length; index++)
        {
            int offset = memory.Cursor;
            memory = memory.LoadIntoArena(assets[index].Data.AsSpan());
            pointers.Add(new(resources[index].GlobalAddress, offset));
        }
        if (pointers.Count > 0)
        {
            selected = selected with { ResourcePointers = pointers.MoveToImmutable() };
        }
        return (memory, selected);
    }

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) InitializeTonberry762320(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState selected)
    {
        NativeBattleEffectBinding binding = selected.Binding;
        if (binding.InitializerIsRet == true || binding.InitializerResource is not null || binding.InitializerResources is not null || binding.InitializerAsset is not null || binding.InitializerAssets is not null)
        {
            throw new InvalidDataException("Native Tonberry initializer requires its three ordered TIM inputs rather than a generic loader descriptor.");
        }
        NativeBattleTonberry90Resources resources = binding.Tonberry90Resources ?? throw new NotSupportedException("Native Tonberry initializer requires its original TIM assets.");
        ImmutableArray<NativeBattleEffectInitializerAsset> tims = resources.InitializerTims;
        string[] names = ["mag089_0.tim", "mag089_1.tim", "mag089_2.tim"];
        uint[] publications = [0x25A4C20, 0x259EEAC, 0x259EEB0];
        if (tims.IsDefault || tims.Length != names.Length || tims.Where((tim, index) => tim is null || tim.Name != names[index] || tim.Data.IsDefaultOrEmpty).Any())
        {
            throw new InvalidDataException("Native Tonberry initializer requires all three original TIM files in load order.");
        }
        memory = memory.BeginLoad(selected.SelectedIndex);
        ImmutableArray<NativeBattleEffectResourcePointer>.Builder pointers = ImmutableArray.CreateBuilder<NativeBattleEffectResourcePointer>(tims.Length);
        for (int index = 0; index < tims.Length; index++)
        {
            int offset = memory.Cursor;
            memory = memory.LoadIntoArena(tims[index].Data.AsSpan());
            pointers.Add(new(publications[index], offset));
        }
        selected = selected with { ResourcePointers = pointers.MoveToImmutable() };
        return NativeBattleTonberry90Controller.Initialize762320(memory, selected);
    }

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) InitializeCactuar5A8730(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState selected)
    {
        NativeBattleEffectBinding binding = selected.Binding;
        if (binding is not { Effect: 199, Entry: 0x5A8750, Initializer: 0x5A8730 } || binding.InitializerIsRet == true || binding.InitializerResource is not null || binding.InitializerResources is not null || binding.InitializerAsset is not null || binding.InitializerAssets is not null)
        {
            throw new InvalidDataException("Native Cactuar199 requires its exact5718E0 heap initializer rather than an arena-loader descriptor.");
        }
        NativeBattleCactuar199Resources resources = binding.Cactuar199Resources ?? throw new NotSupportedException("Native Cactuar199 requires its original heap initializer resource.");
        NativeBattleEffectHeapAllocation tim = resources.InitializerHeapTim;
        if (tim is null || tim.Identity != "mag198.tim" || tim.Data.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native Cactuar199 requires the complete original mag198.tim heap resource.");
        }
        NativeBattleEffectHeapLoad loaded = memory.BeginLoad(selected.SelectedIndex).LoadHeap5718E0(tim);
        NativeBattleOriginalEffectState initialized = selected with { ResourcePointers = [], HeapResourcePointers = [new(0x225A82C, loaded.AllocationIndex)] };
        initialized.Validate();
        return (loaded.Memory, initialized);
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
