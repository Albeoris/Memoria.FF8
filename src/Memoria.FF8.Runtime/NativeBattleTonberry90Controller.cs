using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleTonberry90Controller
{
    public const uint MainDescriptor = 0x259F088;
    public const uint MainBank = 0x259EFC0;
    private const uint ActionParameters = 0x1D99A78;
    private static readonly (uint Descriptor, uint Bank, ushort Stride, int Count)[] PoolLayout =
    [
        (MainDescriptor, MainBank, 0x64, 2),
        (0x25A2B80, 0x25A2B90, 0x58, 4),
        (0x259EF90, 0x259EEB8, 0x48, 3),
        (0x25A2B60, 0x25A2120, 0x520, 2),
        (0x259F098, 0x259F0A8, 0x7C, 100),
        (0x25A3F90, 0x25A3E48, 0x148, 1),
        (0x25A4BF0, 0x25A3FA8, 0x30, 2)
    ];

    public static NativeBattleTonberry90Frame Initialize762320(NativeBattleTonberry90Frame frame)
    {
        var initialized = Initialize762320(frame.Memory, frame.Effect);
        return frame with { Memory = initialized.Memory };
    }

    public static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) Initialize762320(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        RequireBinding(effect);
        NativeBattleTonberry90State state = memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry initializer requires its retained module owner.");
        state.Validate();
        foreach (uint address in new uint[] { 0x25A4C20, 0x259EEAC, 0x259EEB0 })
        {
            NativeBattleEffectResourcePointer pointer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == address) ?? throw new NotSupportedException("Native Tonberry initializer requires its actual published TIM resource pointers.");
            state = state.WriteDword(address, unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)pointer.ArenaOffset));
        }
        return (memory with { Tonberry90 = state }, effect);
    }

    public static NativeBattleTonberry90Frame Initialize762360(NativeBattleTonberry90Frame frame, NativeBattleActionPacketsState packets)
    {
        RequireBinding(frame.Effect);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Tonberry entry requires its current action parameters.");
        NativeBattleTonberry90State state = RequireState(frame).WriteDword(0x25A4C18, 0).WriteDword(0x25A2118, 0);
        frame = frame with { Memory = frame.Memory with { Tonberry90 = state } };
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Tonberry entry requires its live TIM queue owner.");
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Tonberry entry requires its current loaded arena.");
            foreach (uint address in new uint[] { 0x25A4C20, 0x259EEAC, 0x259EEB0 })
            {
                uint pointer = state.ReadDword(address);
                (textures, _) = textures.EnqueueArenaTim505E30(arena, ArenaOffset(pointer, 8));
            }
            frame = frame with { Memory = frame.Memory with { Textures = textures } };
        }
        NativeBattleEffectHierarchy hierarchy = new(PoolLayout.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, checked((ushort)pool.Count))).ToImmutableArray());
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x7624D0, 0x64);
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
        uint tim = state.ReadDword(0x25A4C20);
        state = state.WriteDword(0x25A2CF4, tim).WriteDword(0x25A2B74, unchecked(tim + 0x6000)).WriteDword(0x25A2CF0, unchecked(tim + 0x6000)).WriteDword(0x25A4C1C, unchecked(tim + 0xC000)).WriteDword(0x25A2B70, unchecked(tim + 0xC000));
        return frame with { Memory = frame.Memory with { Tonberry90 = state }, Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = MainDescriptor } };
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int poolIndex = Enumerable.Range(0, effect.Pools.Length).Single(index => effect.Pools[index].DescriptorAddress == MainDescriptor);
        NativeBattleEffectTaskPool pool = effect.Pools[poolIndex];
        uint relative = unchecked(address - pool.BankAddress);
        if (relative % pool.Stride != 0x62 || relative / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Tonberry resource completion pointer is not a root byte.");
        }
        int slot = (int)(relative / pool.Stride);
        return effect with { Pools = effect.Pools.SetItem(poolIndex, pool.WriteSlot(slot, pool.Slots[slot].WriteByte(0x62, value))) };
    }

    private static NativeBattleTonberry90State RequireState(NativeBattleTonberry90Frame frame)
    {
        NativeBattleTonberry90State state = frame.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry retained process owners are missing.");
        state.Validate();
        return state;
    }

    private static void RequireBinding(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding is not { Effect: 90, Entry: 0x762360, Initializer: 0x762320 })
        {
            throw new InvalidDataException("Native Tonberry entry requires its selected original binding.");
        }
    }

    private static int ArenaOffset(uint address, int length)
    {
        if (address < NativeBattleEffectNodeMemory.ArenaAddress || length < 0 || (ulong)address - NativeBattleEffectNodeMemory.ArenaAddress > (uint)(NativeBattleEffectArena.Length - length))
        {
            throw new NotSupportedException("Native Tonberry pointer leaves its current physical arena.");
        }
        return checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
    }
}
