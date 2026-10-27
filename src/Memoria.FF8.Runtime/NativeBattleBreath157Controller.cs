namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath157Controller
{
    public static NativeBattleBreath157Frame Initialize(NativeBattleBreath157Frame frame, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Breath requires its actual COMMON arena.");
        frame = Dword(frame, 0x23ECE18, NativeBattleEffectNodeMemory.ArenaAddress);
        NativeBattleEffectTaskPool roots = ResetPool(frame, 0x23EB618, 0x23EB5F8, 16, 2);
        (roots, int root) = roots.Append(0x605460);
        roots = roots.WriteSlot(root, roots.Slots[root].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = ResetPool(frame, 0x23EBF90, 0x23EBFA0, 36, 100);
        (children, int director) = children.Append(0x603F50);
        children = children.WriteSlot(director, children.Slots[director].WriteWord(12, 0).WriteWord(14, 0));
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Breath requires its actual first contact.");
        frame = Dword(Dword(Dword(frame, 0x23EB5F0, inputs.Packets.ReadContact(first).Primary.ActorIndex), 0x23ECDF0, Parameters(frame).Actor78), 0x23ECDEC, 0x1D99A78);
        for (int index = 0; index < 100; index++)
        {
            frame = Dword(frame, 0x23EB628u + (uint)(index * 24), 0);
        }
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Breath requires its actual texture queue.")).Register5099A0(resources.Camera);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x23ECDF4)?.ArenaOffset ?? throw new NotSupportedException("Breath requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = Dword(frame, 0x23ECDF4, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = 0x23EB618, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattleBreath157Frame Advance(NativeBattleBreath157Frame frame, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23EB618 || frame.Effect.Pools[1].DescriptorAddress != 0x23EBF90 || frame.Effect.Pools[0].BankAddress != 0x23EB5F8 || frame.Effect.Pools[0].Stride != 16 || frame.Effect.Pools[0].Slots.Length != 2 || frame.Effect.Pools[1].BankAddress != 0x23EBFA0 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Breath requires its exclusive physical root and child owners.");
        }
        NativeBattleBreath157Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            uint address = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x605460 => AdvanceRoot(current, address, resources, inputs),
                0x6054D0 => AdvanceWaiter(current, address, inputs),
                _ => throw new NotSupportedException("Breath reached an unsupported root callback.")
            };
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x23EB618u } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattleBreath157Frame Frame, int Result) AdvanceRoot(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs)
    {
        ushort age = Read(frame, address).ReadWord(12);
        frame = Dword(frame, 0x23ECE1C, frame.State.ReadDword(0x23ECE18) + ((age & 1) == 0 ? 0u : 0xC000u));
        NativeBattleBreath157Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x603F50 => AdvanceDirector(current, child, resources, inputs),
                0x604780 => AdvanceMain(current, child, resources, inputs),
                0x6043A0 => NativeBattleBreath157Children.Advance6043A0(current, child, resources, inputs),
                0x6044D0 => NativeBattleBreath157Children.Advance6044D0(current, child, resources, inputs),
                0x6049F0 => NativeBattleBreath157Children.Advance6049F0(current, child, resources, inputs),
                0x604B30 => NativeBattleBreath157Children.Advance604B30(current, child, resources, inputs),
                0x604C80 => NativeBattleBreath157Children.Advance604C80(current, child, resources, inputs),
                0x604E90 => NativeBattleBreath157Children.Advance604E90(current, child, resources, inputs),
                0x6050A0 => NativeBattleBreath157Children.Advance6050A0(current, child, resources, inputs),
                0x6052A0 => NativeBattleBreath157Children.Advance6052A0(current, child, resources, inputs),
                _ => throw new NotSupportedException("Breath reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, step.Pool);
        if (step.Survivors == 0)
        {
            NativeBattleEffectTaskPool roots = frame.Effect.Pools[0];
            (roots, int slot) = roots.Append(0x6054D0);
            roots = roots.WriteSlot(slot, roots.Slots[slot].WriteWord(12, 0).WriteWord(14, 0));
            frame = WithPool(frame, 0, roots);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, step.Survivors == 0 ? 2 : 0);
    }

    public static (NativeBattleBreath157Frame Frame, int Result) AdvanceWaiter(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Inputs inputs)
    {
        if (Read(frame, address).ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleBreath157ReleaseCommon(frame.State.ReadDword(0x23ECE18), address));
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        bool completed = task.ReadWord(14) != 0;
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return (frame, completed ? 2 : 0);
    }

    private static (NativeBattleBreath157Frame Frame, uint Address) RequireAppend(NativeBattleBreath157Frame frame, uint callback)
    {
        var appended = Append(frame, callback);
        return appended.Address == 0 ? throw new NotSupportedException("Breath exhausted its actual child task bank.") : appended;
    }
}
