using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTheEnd85LeafStep(NativeBattleTheEnd85Frame Frame, bool Complete)
{
    public bool HierarchyReleased { get; init; }
}
public sealed record NativeBattleTheEnd85Inputs(NativeBattleActionPacketsState Packets, NativeBattlePackedMatrix32 CameraMatrix, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleTheEnd85Frame, uint, NativeBattleTheEnd85Frame> InitializeTarget, Func<NativeBattleTheEnd85Frame, uint, NativeBattleTheEnd85LeafStep> AdvanceLeaf, Func<NativeBattleTheEnd85Frame, int, NativeBattleTheEnd85Frame> ApplyContact, Func<NativeBattleTheEnd85Frame, NativeBattleChoco97Operation, NativeBattleTheEnd85Frame> Execute, uint DirectorAddress);

public static partial class NativeBattleTheEnd85Controller
{
    public const uint MainDescriptor = 0x25BC570;
    public const uint MainBank = 0x25BC4A8;
    private const uint ActionParameters = 0x1D99A78;
    private static readonly (uint Descriptor, uint Bank, ushort Stride, int Count)[] PoolLayout =
    [
        (MainDescriptor, MainBank, 100, 2),
        (0x25C19E8, 0x25C19F8, 88, 4),
        (0x25BC478, 0x25BC3A0, 72, 3),
        (0x25C1748, 0x25C0F58, 676, 3),
        (0x25C0F40, 0x25BFE18, 732, 6),
        (0x25BC580, 0x25BC590, 96, 8),
        (0x25BC898, 0x25BC8A8, 228, 60),
        (0x25C19D8, 0x25C1758, 64, 10),
        (0x25C21B8, 0x25C1CC8, 48, 2)
    ];

    public static NativeBattleTheEnd85Frame Initialize784480(NativeBattleTheEnd85Frame frame)
    {
        var initialized = Initialize784480(frame.Memory, frame.Effect);
        return frame with { Memory = initialized.Memory };
    }

    public static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) Initialize784480(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        RequireBinding(effect);
        NativeBattleTheEnd85State state = memory.TheEnd85 ?? throw new NotSupportedException("Native The End initializer requires its retained module owner.");
        state.Validate();
        NativeBattleEffectResourcePointer pointer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x25BC390) ?? throw new NotSupportedException("Native The End initializer requires its actual published TIM resource pointer.");
        state = state.WriteDword(0x25BC390, unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)pointer.ArenaOffset));
        return (memory with { TheEnd85 = state }, effect);
    }

    public static NativeBattleTheEnd85Frame Initialize7844A0(NativeBattleTheEnd85Frame frame, NativeBattleActionPacketsState packets)
    {
        RequireBinding(frame.Effect);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native The End entry requires its current action parameters.");
        NativeBattleTheEnd85State state = RequireState(frame).WriteDword(0x25C21D4, 0).WriteDword(0x25BC894, 0);
        frame = frame with { Memory = frame.Memory with { TheEnd85 = state } };
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native The End entry requires its live TIM queue owner.");
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native The End entry requires its current loaded arena.");
            uint pointer = state.Dword(0x25BC390);
            (textures, _) = textures.EnqueueArenaTim505E30(arena, ArenaOffset(pointer, 8));
            frame = frame with { Memory = frame.Memory with { Textures = textures } };
        }
        NativeBattleEffectHierarchy hierarchy = new(PoolLayout.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, checked((ushort)pool.Count))).ToImmutableArray());
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x784630, 0x64);
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
        uint tim = state.Dword(0x25BC390);
        state = state.WriteDword(0x25C1B5C, tim).WriteDword(0x25C0F54, unchecked(tim + 0x8000)).WriteDword(0x25C1B58, unchecked(tim + 0x8000)).WriteDword(0x25C21D8, unchecked(tim + 0x10000)).WriteDword(0x25C0F50, unchecked(tim + 0x10000));
        return frame with { Memory = frame.Memory with { TheEnd85 = state }, Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = MainDescriptor } };
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int poolIndex = Enumerable.Range(0, effect.Pools.Length).Single(index => effect.Pools[index].DescriptorAddress == MainDescriptor);
        NativeBattleEffectTaskPool pool = effect.Pools[poolIndex];
        uint relative = unchecked(address - pool.BankAddress);
        if (relative % pool.Stride != 0x62 || relative / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native The End resource completion pointer is not a root byte.");
        }
        int slot = (int)(relative / pool.Stride);
        return effect with { Pools = effect.Pools.SetItem(poolIndex, pool.WriteSlot(slot, pool.Slots[slot].WriteByte(0x62, value))) };
    }

    private static NativeBattleTheEnd85State RequireState(NativeBattleTheEnd85Frame frame)
    {
        NativeBattleTheEnd85State state = frame.Memory.TheEnd85 ?? throw new NotSupportedException("Native The End retained process owners are missing.");
        state.Validate();
        return state;
    }

    private static void RequireBinding(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding is not { Effect: 85, Entry: 0x7844A0, Initializer: 0x784480 })
        {
            throw new InvalidDataException("Native The End entry requires its selected original binding.");
        }
    }

    private static int ArenaOffset(uint address, int length)
    {
        if (address < NativeBattleEffectNodeMemory.ArenaAddress || length < 0 || (ulong)address - NativeBattleEffectNodeMemory.ArenaAddress > (uint)(NativeBattleEffectArena.Length - length))
        {
            throw new NotSupportedException("Native The End pointer leaves its current physical arena.");
        }
        return checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
    }
}
