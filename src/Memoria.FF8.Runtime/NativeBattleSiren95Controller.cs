using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSiren95Controller
{
    public const uint MainDescriptor = 0x257FA80;
    public const uint MainBank = 0x257F9B8;
    private const uint ActionParameters = 0x1D99A78;
    private static readonly (uint Descriptor, uint Bank, ushort Stride, int Count)[] PoolLayout =
    [
        (MainDescriptor, MainBank, 0x64, 2),
        (0x258BE30, 0x258BE40, 0x58, 4),
        (0x257F988, 0x257F8B0, 0x48, 3),
        (0x258BA90, 0x258A028, 0x2A4, 10),
        (0x258A010, 0x2587670, 0x534, 8),
        (0x257FA90, 0x257FAA0, 0xE0, 110),
        (0x2585EF0, 0x2585F00, 0x64, 60),
        (0x258BE20, 0x258BBA0, 0x40, 10),
        (0x258EC40, 0x258EB00, 0x140, 1),
        (0x258FB48, 0x258EDD8, 0x30, 2)
    ];

    public static NativeBattleSiren95Frame Initialize739D80(NativeBattleSiren95Frame frame)
    {
        var initialized = Initialize739D80(frame.Memory, frame.Effect);
        return frame with { Memory = initialized.Memory };
    }

    public static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) Initialize739D80(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        RequireBinding(effect);
        NativeBattleSiren95State state = memory.Siren95 ?? throw new NotSupportedException("Native Siren initializer requires its retained module owner.");
        state.Validate();
        NativeBattleEffectResourcePointer pointer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x257F8A0) ?? throw new NotSupportedException("Native Siren initializer requires its actual published TIM resource pointer.");
        state = state.WriteDword(0x257F8A0, unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)pointer.ArenaOffset));
        return (memory with { Siren95 = state }, effect);
    }

    public static NativeBattleSiren95Frame Initialize739DA0(NativeBattleSiren95Frame frame, NativeBattleActionPacketsState packets)
    {
        RequireBinding(frame.Effect);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Siren entry requires its current action parameters.");
        NativeBattleSiren95State state = RequireState(frame).WriteDword(0x258FB70, 0).WriteDword(0x2585E38, 0);
        frame = frame with { Memory = frame.Memory with { Siren95 = state } };
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Siren entry requires its live TIM queue owner.");
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Siren entry requires its current loaded arena.");
            uint pointer = state.ReadDword(0x257F8A0);
            (textures, _) = textures.EnqueueArenaTim505E30(arena, ArenaOffset(pointer, 8));
            frame = frame with { Memory = frame.Memory with { Textures = textures } };
        }
        NativeBattleEffectHierarchy hierarchy = new(PoolLayout.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, checked((ushort)pool.Count))).ToImmutableArray());
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x739F40, 0x64);
        hierarchy = appended.Hierarchy;
        NativeBattleModulePoolTask root = hierarchy.Read(appended.Address).WriteDword(0x0C, ActionParameters);
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, ActionParameters, unchecked((sbyte)root.ReadByte(0x2A)), unchecked((sbyte)root.ReadByte(0x2B)));
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        root = root.WriteByte(0x2C, actors.Actor).WriteByte(0x2D, actors.Target).WriteByte(0x29, 0).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17));
        byte last = unchecked((byte)(root.ReadWord(0x5A) - 1));
        if (last < root.ReadWord(0x58))
        {
            last = unchecked((byte)root.ReadWord(0x58));
        }
        hierarchy = hierarchy.Write(appended.Address, root.WriteByte(0x2F, last));
        uint tim = state.ReadDword(0x257F8A0);
        state = state.WriteDword(0x258BFA4, tim).WriteDword(0x258A024, unchecked(tim + 0xC00)).WriteDword(0x258BFA0, unchecked(tim + 0xC00)).WriteDword(0x258FB74, unchecked(tim + 0x1800)).WriteDword(0x258A020, unchecked(tim + 0x1800));
        return frame with { Memory = frame.Memory with { Siren95 = state }, Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = MainDescriptor } };
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int poolIndex = Enumerable.Range(0, effect.Pools.Length).Single(index => effect.Pools[index].DescriptorAddress == MainDescriptor);
        NativeBattleEffectTaskPool pool = effect.Pools[poolIndex];
        uint relative = unchecked(address - pool.BankAddress);
        if (relative % pool.Stride != 0x62 || relative / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Siren resource completion pointer is not a root byte.");
        }
        int slot = (int)(relative / pool.Stride);
        return effect with { Pools = effect.Pools.SetItem(poolIndex, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(0x62, value)) }) };
    }

    private static NativeBattleSiren95State RequireState(NativeBattleSiren95Frame frame)
    {
        NativeBattleSiren95State state = frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren retained process owners are missing.");
        state.Validate();
        return state;
    }

    private static void RequireBinding(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding is not { Effect: 95, Entry: 0x739DA0, Initializer: 0x739D80 })
        {
            throw new InvalidDataException("Native Siren entry requires its selected original binding.");
        }
    }

    private static int ArenaOffset(uint address, int length)
    {
        if (address < NativeBattleEffectNodeMemory.ArenaAddress || length < 0 || (ulong)address - NativeBattleEffectNodeMemory.ArenaAddress > (uint)(NativeBattleEffectArena.Length - length))
        {
            throw new NotSupportedException("Native Siren pointer leaves its current physical arena.");
        }
        return checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
    }
}
