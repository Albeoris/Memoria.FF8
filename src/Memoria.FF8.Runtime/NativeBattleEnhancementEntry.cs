using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEnhancementPool(uint Descriptor, uint Bank, ushort Stride, ushort Capacity);

/// <summary>The two authenticated entry layouts; this profile does not substitute their distinct private callbacks.</summary>
public sealed class NativeBattleEnhancementProfile
{
    public static NativeBattleEnhancementProfile Double { get; } = new(0x8D5080, 0x276FB00, 0x8D5180, 0x8D52A0, 0x6000, [new(0x2775058, 0x2774F90, 100, 2), new(0x277A1C0, 0x277A1D0, 92, 4), new(0x277A348, 0x277A358, 64, 40), new(0x277A1A8, 0x2775068, 104, 200), new(0x2774F68, 0x276FB08, 108, 200), new(0x277AE98, 0x277AD58, 80, 4)]);
    public static NativeBattleEnhancementProfile Triple { get; } = new(0x89DEF0, 0x2714F28, 0x89DFF0, 0x89E110, 0x8000, [new(0x271B0F0, 0x271B028, 100, 2), new(0x2720898, 0x27208A8, 92, 4), new(0x2720A20, 0x2720A30, 64, 40), new(0x2720880, 0x271B100, 112, 200), new(0x271B010, 0x2714F30, 124, 200), new(0x2721570, 0x2721430, 80, 4)]);
    public static NativeBattleEnhancementProfile Bio22 { get; } = new(0x89A6B0, 0x2704568, 0x89A7B0, 0x89A8D0, 0xC000, [new(0x2705250, 0x2705188, 100, 2), new(0x2714960, 0x2714970, 140, 4), new(0x2714BA8, 0x2714BB8, 56, 10), new(0x270E540, 0x2706200, 112, 300), new(0x2705150, 0x2704570, 152, 20), new(0x27044E0, 0x27044F0, 60, 2)]);

    private NativeBattleEnhancementProfile(uint entry, uint initializerPointer, uint rootCallback, uint targetCallback, int drawBufferStride, ImmutableArray<NativeBattleEnhancementPool> pools)
    {
        Entry = entry;
        InitializerPointer = initializerPointer;
        RootCallback = rootCallback;
        TargetCallback = targetCallback;
        DrawBufferStride = drawBufferStride;
        Pools = pools;
    }

    public uint Entry { get; }
    public uint InitializerPointer { get; }
    public uint RootCallback { get; }
    public uint TargetCallback { get; }
    public int DrawBufferStride { get; }
    public ImmutableArray<NativeBattleEnhancementPool> Pools { get; }
}

public sealed record NativeBattleEnhancementEntryResult(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);

/// <summary>8D5080/89DEF0 share this entry algorithm with their original explicit pool and resource operands.</summary>
public static class NativeBattleEnhancementEntry
{
    public static NativeBattleEnhancementEntryResult Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleEffectHeapAllocation animation, NativeBattleEnhancementProfile profile)
    {
        effect.Validate();
        packets.Validate();
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native enhancement entry requires its50AFC0 parameters.");
        if (effect.Binding.Entry != profile.Entry)
        {
            throw new InvalidDataException("Native enhancement entry disagrees with its explicit original layout.");
        }
        NativeBattleEffectHierarchy hierarchy = new(profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.RootCallback, profile.Pools[0].Stride);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native enhancement entry requires its physical first-contact pointer.");
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78).WriteByte(0x2D, packets.ReadContact(first).Primary.ActorIndex).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17));
        hierarchy = hierarchy.Write(root.Address, task);
        if ((parameters.Flags79 & 1) == 0)
        {
            int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.InitializerPointer)?.ArenaOffset ?? throw new NotSupportedException("Native enhancement entry requires its executed initializer pointer.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native enhancement entry requires its texture queue.");
            textures = textures.Register5099A0(animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native enhancement entry requires its571870 arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = profile.Pools[0].Descriptor });
    }
}
