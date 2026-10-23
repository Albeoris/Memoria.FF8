namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoisonGas171Controller
{
    public static NativeBattlePoisonGas171Frame Initialize(NativeBattlePoisonGas171Frame frame, NativeBattlePoisonGas171Resources resources, NativeBattlePoisonGas171Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Poison Gas requires its actual COMMON arena.");
        frame = Dword(frame, 0x23AF814, NativeBattleEffectNodeMemory.ArenaAddress);
        NativeBattleEffectTaskPool roots = ResetPool(frame, 0x23AD718, 0x23AD708, 16, 1);
        (roots, int root) = roots.Append(0x5DC8C0);
        roots = roots with { Slots = roots.Slots.SetItem(root, roots.Slots[root].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = ResetPool(frame, 0x23AE9E8, 0x23AE9F8, 36, 100);
        (children, int main) = children.Append(0x5DB340);
        children = children with { Slots = children.Slots.SetItem(main, children.Slots[main].WriteWord(12, 0)) };
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Poison Gas requires its actual first contact pointer.");
        frame = Dword(Dword(Dword(frame, 0x23AD700, inputs.Packets.ReadContact(first).Primary.ActorIndex), 0x23AF80C, Parameters(frame).Actor78), 0x23AF808, 0x1D99A78);
        foreach (uint bank in new uint[] { 0x23AE088, 0x23AD728 })
        {
            for (int index = 0; index < 100; index++)
            {
                frame = Dword(frame, bank + (uint)(index * 24), 0);
            }
        }
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Poison Gas requires its actual texture queue.")).Register5099A0(resources.Camera);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x23AF810)?.ArenaOffset ?? throw new NotSupportedException("Poison Gas requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = Dword(frame, 0x23AF810, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = 0x23AD718, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattlePoisonGas171Frame Advance(NativeBattlePoisonGas171Frame frame, NativeBattlePoisonGas171Resources resources, NativeBattlePoisonGas171Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23AD718 || frame.Effect.Pools[0].BankAddress != 0x23AD708 || frame.Effect.Pools[0].Stride != 16 || frame.Effect.Pools[0].Slots.Length != 1 || frame.Effect.Pools[1].DescriptorAddress != 0x23AE9E8 || frame.Effect.Pools[1].BankAddress != 0x23AE9F8 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Poison Gas requires its exclusive physical task owners.");
        }
        NativeBattlePoisonGas171Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != 0x5DC8C0)
            {
                throw new NotSupportedException("Poison Gas reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x23AD718u } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattlePoisonGas171Frame Frame, int Result) AdvanceRoot(NativeBattlePoisonGas171Frame frame, uint address, NativeBattlePoisonGas171Resources resources, NativeBattlePoisonGas171Inputs inputs)
    {
        ushort age = Read(frame, address).ReadWord(12);
        frame = Dword(frame, 0x23AF818, frame.State.ReadDword(0x23AF814) + ((age & 1) == 0 ? 0u : 0xC000u));
        NativeBattlePoisonGas171Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x5DB340 => AdvanceMain(current, child, resources, inputs),
                0x5DB620 => AdvanceBody(current, child, resources, inputs),
                0x5DBED0 or 0x5DC270 or 0x5DC700 => AdvanceSlots(current, child, resources, inputs),
                0x5DC5B0 => AdvanceMesh(current, child, resources, inputs),
                _ => throw new NotSupportedException("Poison Gas reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, step.Pool);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, step.Survivors == 0 ? 2 : 0);
    }
}
