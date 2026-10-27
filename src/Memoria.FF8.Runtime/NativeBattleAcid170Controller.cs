namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid170Controller
{
    public static NativeBattleAcid170Frame Initialize(NativeBattleAcid170Frame frame, NativeBattleAcid170Resources resources, NativeBattleAcid170Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Acid requires its actual COMMON arena.");
        frame = Dword(frame, 0x23B1348, NativeBattleEffectNodeMemory.ArenaAddress);
        NativeBattleEffectTaskPool roots = ResetPool(frame, 0x23AF838, 0x23AF828, 16, 1);
        (roots, int root) = roots.Append(0x5DEDF0);
        roots = roots.WriteSlot(root, roots.Slots[root].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = ResetPool(frame, 0x23B01A8, 0x23B01B8, 36, 100);
        (children, int main) = children.Append(0x5DC9F0);
        children = children.WriteSlot(main, children.Slots[main].WriteWord(12, 0).WriteWord(14, 0));
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Acid requires its actual first physical contact pointer.");
        frame = Dword(Dword(Dword(frame, 0x23AF820, inputs.Packets.ReadContact(first).Primary.ActorIndex), 0x23B10E8, Parameters(frame).Actor78), 0x23B0FD0, 0x1D99A78);
        for (int index = 0; index < 100; index++)
        {
            frame = Dword(frame, 0x23AF848 + (uint)(index * 24), 0);
        }
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Acid requires its actual texture queue.")).Register5099A0(resources.Camera);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x23B1338)?.ArenaOffset ?? throw new NotSupportedException("Acid requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = Dword(frame, 0x23B1338, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = 0x23AF838, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattleAcid170Frame Advance(NativeBattleAcid170Frame frame, NativeBattleAcid170Resources resources, NativeBattleAcid170Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23AF838 || frame.Effect.Pools[0].BankAddress != 0x23AF828 || frame.Effect.Pools[0].Stride != 16 || frame.Effect.Pools[0].Slots.Length != 1 || frame.Effect.Pools[1].DescriptorAddress != 0x23B01A8 || frame.Effect.Pools[1].BankAddress != 0x23B01B8 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Acid requires its exclusive physical task owners.");
        }
        NativeBattleAcid170Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != 0x5DEDF0)
            {
                throw new NotSupportedException("Acid reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x23AF838u } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattleAcid170Frame Frame, int Result) AdvanceRoot(NativeBattleAcid170Frame frame, uint address, NativeBattleAcid170Resources resources, NativeBattleAcid170Inputs inputs)
    {
        ushort age = Read(frame, address).ReadWord(12);
        frame = Dword(frame, 0x23B134C, unchecked(frame.State.ReadDword(0x23B1348) + ((age & 1) == 0 ? 0u : 0x8000u)));
        NativeBattleAcid170Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x5DC9F0 => AdvanceMain(current, child, resources, inputs),
                0x5DCAF0 => AdvanceCurve(current, child, resources, inputs),
                0x5DCF20 => AdvanceSprite(current, child, resources, inputs),
                0x5DD030 or 0x5DE0B0 => AdvanceMesh(current, child, resources, inputs),
                0x5DDE90 or 0x5DEA10 => AdvanceSlots(current, child, resources, inputs),
                0x5DE200 => AdvanceBody(current, child, resources, inputs),
                _ => throw new NotSupportedException("Acid reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, step.Pool);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, step.Survivors == 0 ? 2 : 0);
    }
}
