namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCannonBlow151Controller
{
    public static NativeBattleCannonBlow151Frame Initialize(NativeBattleCannonBlow151Frame frame, NativeBattleCannonBlow151Resources resources, NativeBattleCannonBlow151Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Cannon Blow requires its actual COMMON arena.");
        frame = Dword(frame, 0x244FD58, NativeBattleEffectNodeMemory.ArenaAddress);
        NativeBattleEffectTaskPool roots = ResetPool(frame, 0x244D698, 0x244D678, 16, 2);
        (roots, int root) = roots.Append(0x60E270);
        roots = roots with { Slots = roots.Slots.SetItem(root, roots.Slots[root].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = ResetPool(frame, 0x244E9E8, 0x244E9F8, 36, 100);
        (children, int director) = children.Append(0x60CA90);
        children = children with { Slots = children.Slots.SetItem(director, children.Slots[director].WriteWord(12, 0).WriteWord(14, 0)) };
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Cannon Blow requires its actual first contact.");
        frame = Dword(Dword(Dword(frame, 0x244D670, inputs.Packets.ReadContact(first).Primary.ActorIndex), 0x244F92C, Parameters(frame).Actor78), 0x244F928, 0x1D99A78);
        foreach ((uint bank, int count) in new (uint, int)[] { (0x244E008, 100), (0x244D6A8, 100) })
        {
            for (int index = 0; index < count; index++)
            {
                frame = Dword(frame, bank + (uint)(index * 24), 0);
            }
        }
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Cannon Blow requires its actual texture queue.")).Register5099A0(resources.Camera);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x244F930)?.ArenaOffset ?? throw new NotSupportedException("Cannon Blow requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = Dword(frame, 0x244F930, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = 0x244D698, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattleCannonBlow151Frame Advance(NativeBattleCannonBlow151Frame frame, NativeBattleCannonBlow151Resources resources, NativeBattleCannonBlow151Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x244D698 || frame.Effect.Pools[1].DescriptorAddress != 0x244E9E8 || frame.Effect.Pools[0].BankAddress != 0x244D678 || frame.Effect.Pools[0].Stride != 16 || frame.Effect.Pools[0].Slots.Length != 2 || frame.Effect.Pools[1].BankAddress != 0x244E9F8 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Cannon Blow requires its exclusive physical root and child owners.");
        }
        NativeBattleCannonBlow151Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            uint address = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x60E270 => AdvanceRoot(current, address, resources, inputs),
                0x60E2E0 => AdvanceWaiter(current, address, inputs),
                _ => throw new NotSupportedException("Cannon Blow reached an unsupported root callback.")
            };
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x244D698u } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattleCannonBlow151Frame Frame, int Result) AdvanceRoot(NativeBattleCannonBlow151Frame frame, uint address, NativeBattleCannonBlow151Resources resources, NativeBattleCannonBlow151Inputs inputs)
    {
        ushort age = Read(frame, address).ReadWord(12);
        frame = Dword(frame, 0x244FD5C, frame.State.ReadDword(0x244FD58) + ((age & 1) == 0 ? 0u : 0xC000u));
        NativeBattleCannonBlow151Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x60CA90 => AdvanceMain(current, child, resources, inputs),
                0x60D260 or 0x60D670 or 0x60DE40 or 0x60E060 => AdvanceMesh(current, child, resources, inputs),
                0x60D790 or 0x60E1B0 => AdvanceSprite(current, child, resources, inputs),
                0x60D3B0 or 0x60D850 or 0x60DA10 or 0x60DC20 => AdvanceSlots(current, child, resources, inputs),
                _ => throw new NotSupportedException("Cannon Blow reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, step.Pool);
        if (step.Survivors == 0)
        {
            NativeBattleEffectTaskPool roots = frame.Effect.Pools[0];
            (roots, int slot) = roots.Append(0x60E2E0);
            roots = roots with { Slots = roots.Slots.SetItem(slot, roots.Slots[slot].WriteWord(12, 0).WriteWord(14, 0)) };
            frame = WithPool(frame, 0, roots);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, step.Survivors == 0 ? 2 : 0);
    }

    public static (NativeBattleCannonBlow151Frame Frame, int Result) AdvanceWaiter(NativeBattleCannonBlow151Frame frame, uint address, NativeBattleCannonBlow151Inputs inputs)
    {
        if (Read(frame, address).ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleCannonBlow151ReleaseCommon(frame.State.ReadDword(0x244FD58), address));
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        bool completed = task.ReadWord(14) != 0;
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return (frame, completed ? 2 : 0);
    }

}
