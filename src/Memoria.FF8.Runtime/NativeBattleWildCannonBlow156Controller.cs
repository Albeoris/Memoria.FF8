namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWildCannonBlow156Controller
{
    public static NativeBattleWildCannonBlow156Frame Initialize(NativeBattleWildCannonBlow156Frame frame, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Wild Cannon Blow requires its actual COMMON arena.");
        frame = Dword(frame, 0x23F07E8, NativeBattleEffectNodeMemory.ArenaAddress);
        NativeBattleEffectTaskPool roots = ResetPool(frame, 0x23ECE68, 0x23ECE48, 16, 2);
        (roots, int root) = roots.Append(0x606C20);
        roots = roots with { Slots = roots.Slots.SetItem(root, roots.Slots[root].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = ResetPool(frame, 0x23EF478, 0x23EF488, 36, 100);
        (children, int director) = children.Append(0x605620);
        children = children with { Slots = children.Slots.SetItem(director, children.Slots[director].WriteWord(12, 0).WriteWord(14, 0)) };
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Wild Cannon Blow requires its actual first contact.");
        frame = Dword(Dword(Dword(frame, 0x23ECE40, inputs.Packets.ReadContact(first).Primary.ActorIndex), 0x23F03BC, Parameters(frame).Actor78), 0x23F03B8, 0x1D99A78);
        foreach ((uint bank, int count) in new (uint, int)[] { (0x23ECE78, 150), (0x23EDC88, 150), (0x23EEA98, 100) })
        {
            for (int index = 0; index < count; index++)
            {
                frame = Dword(frame, bank + (uint)(index * 24), 0);
            }
        }
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Wild Cannon Blow requires its actual texture queue.")).Register5099A0(resources.Camera);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x23F03C0)?.ArenaOffset ?? throw new NotSupportedException("Wild Cannon Blow requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = Dword(frame, 0x23F03C0, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = 0x23ECE68, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattleWildCannonBlow156Frame Advance(NativeBattleWildCannonBlow156Frame frame, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23ECE68 || frame.Effect.Pools[1].DescriptorAddress != 0x23EF478 || frame.Effect.Pools[0].BankAddress != 0x23ECE48 || frame.Effect.Pools[0].Stride != 16 || frame.Effect.Pools[0].Slots.Length != 2 || frame.Effect.Pools[1].BankAddress != 0x23EF488 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Wild Cannon Blow requires its exclusive physical root and child owners.");
        }
        NativeBattleWildCannonBlow156Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            uint address = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x606C20 => AdvanceRoot(current, address, resources, inputs),
                0x606C90 => AdvanceWaiter(current, address, inputs),
                _ => throw new NotSupportedException("Wild Cannon Blow reached an unsupported root callback.")
            };
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x23ECE68u } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) AdvanceRoot(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs)
    {
        ushort age = Read(frame, address).ReadWord(12);
        frame = Dword(frame, 0x23F07EC, frame.State.ReadDword(0x23F07E8) + ((age & 1) == 0 ? 0u : 0xC000u));
        NativeBattleWildCannonBlow156Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x605620 => AdvanceDirector(current, child, resources, inputs),
                0x6056E0 => AdvanceMain(current, child, resources, inputs),
                0x605DF0 => NativeBattleWildCannonBlow156Children.Advance605DF0(current, child, resources, inputs),
                0x605F40 => NativeBattleWildCannonBlow156Children.Advance605F40(current, child, resources, inputs),
                0x606200 => NativeBattleWildCannonBlow156Children.Advance606200(current, child, resources, inputs),
                0x606320 => NativeBattleWildCannonBlow156Children.Advance606320(current, child, resources, inputs),
                0x6063E0 => NativeBattleWildCannonBlow156Children.Advance6063E0(current, child, resources, inputs),
                0x6065A0 => NativeBattleWildCannonBlow156Children.Advance6065A0(current, child, resources, inputs),
                0x6067B0 => NativeBattleWildCannonBlow156Children.Advance6067B0(current, child, resources, inputs),
                0x606910 => NativeBattleWildCannonBlow156Children.Advance606910(current, child, resources, inputs),
                0x606A60 => NativeBattleWildCannonBlow156Children.Advance606A60(current, child, resources, inputs),
                _ => throw new NotSupportedException("Wild Cannon Blow reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, step.Pool);
        if (step.Survivors == 0)
        {
            NativeBattleEffectTaskPool roots = frame.Effect.Pools[0];
            (roots, int slot) = roots.Append(0x606C90);
            roots = roots with { Slots = roots.Slots.SetItem(slot, roots.Slots[slot].WriteWord(12, 0).WriteWord(14, 0)) };
            frame = WithPool(frame, 0, roots);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, step.Survivors == 0 ? 2 : 0);
    }

    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) AdvanceWaiter(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Inputs inputs)
    {
        if (Read(frame, address).ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleWildCannonBlow156ReleaseCommon(frame.State.ReadDword(0x23F07E8), address));
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        bool completed = task.ReadWord(14) != 0;
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return (frame, completed ? 2 : 0);
    }

    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) AdvanceDirector(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age is 1 or 4 or 7 or 10)
        {
            (frame, uint child) = RequireAppend(frame, 0x6056E0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, task.ReadWord(14)));
            frame = Write(frame, address, Read(frame, address).WriteWord(14, unchecked((ushort)(task.ReadWord(14) + 1))));
        }
        if (age == 36)
        {
            frame = inputs.Execute(frame, new NativeBattleWildCannonBlow156ContactsAll(Parameters(frame).HeaderIndex7C));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleWildCannonBlow156Sound(resources.SoundA, 0, 128));
            frame = inputs.Execute(frame, new NativeBattleWildCannonBlow156Sound(resources.SoundB, 0, 128));
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) > 45 ? 2 : 0);
    }

    private static (NativeBattleWildCannonBlow156Frame Frame, uint Address) RequireAppend(NativeBattleWildCannonBlow156Frame frame, uint callback)
    {
        var appended = Append(frame, callback);
        return appended.Address == 0 ? throw new NotSupportedException("Wild Cannon Blow exhausted its actual child task bank.") : appended;
    }
}
