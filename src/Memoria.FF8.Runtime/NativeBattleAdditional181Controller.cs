namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAdditional181Controller
{
    public static NativeBattleAdditional181Frame Initialize(NativeBattleAdditional181Frame frame, NativeBattleAdditional181Resources resources, NativeBattleAdditional181Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Additional requires its actual COMMON arena.");
        frame = Dword(frame, 0x2307D8C, NativeBattleEffectNodeMemory.ArenaAddress);
        NativeBattleEffectTaskPool roots = ResetPool(frame, 0x23065F0, 0x23065E0, 16, 1);
        (roots, int root) = roots.Append(0x5CC7D0);
        roots = roots with { Slots = roots.Slots.SetItem(root, roots.Slots[root].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = ResetPool(frame, 0x2306F60, 0x2306F70, 36, 100);
        (children, int main) = children.Append(0x5CB610);
        children = children with { Slots = children.Slots.SetItem(main, children.Slots[main].WriteWord(12, 0).WriteWord(14, 5)) };
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Additional requires its actual first contact pointer.");
        frame = Dword(Dword(Dword(frame, 0x23065D8, inputs.Packets.ReadContact(first).Primary.ActorIndex), 0x2307D84, Parameters(frame).Actor78), 0x2307D80, 0x1D99A78);
        for (int index = 0; index < 100; index++)
        {
            frame = Dword(frame, 0x2306600u + (uint)(index * 24), 0);
        }
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Additional requires its actual texture queue.")).Register5099A0(resources.Camera);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2307D88)?.ArenaOffset ?? throw new NotSupportedException("Additional requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = Dword(frame, 0x2307D88, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = 0x23065F0, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattleAdditional181Frame Advance(NativeBattleAdditional181Frame frame, NativeBattleAdditional181Resources resources, NativeBattleAdditional181Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23065F0 || frame.Effect.Pools[0].BankAddress != 0x23065E0 || frame.Effect.Pools[0].Stride != 16 || frame.Effect.Pools[0].Slots.Length != 1 || frame.Effect.Pools[1].DescriptorAddress != 0x2306F60 || frame.Effect.Pools[1].BankAddress != 0x2306F70 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Additional requires its exclusive physical task owners.");
        }
        NativeBattleAdditional181Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != 0x5CC7D0)
            {
                throw new NotSupportedException("Additional reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x23065F0u } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattleAdditional181Frame Frame, int Result) AdvanceRoot(NativeBattleAdditional181Frame frame, uint address, NativeBattleAdditional181Resources resources, NativeBattleAdditional181Inputs inputs)
    {
        ushort age = Read(frame, address).ReadWord(12);
        frame = Dword(frame, 0x2307D90, frame.State.ReadDword(0x2307D8C) + ((age & 1) == 0 ? 0u : 0xC000u));
        NativeBattleAdditional181Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x5CB610 => AdvanceMain(current, child, resources, inputs),
                0x5CB9B0 => AdvanceBody(current, child, resources, inputs),
                0x5CC000 or 0x5CC160 => AdvanceMesh(current, child, resources, inputs),
                0x5CC2A0 or 0x5CC560 => AdvanceSlots(current, child, resources, inputs),
                0x5CC720 => AdvanceSprite(current, child, resources, inputs),
                _ => throw new NotSupportedException("Additional reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, step.Pool);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, step.Survivors == 0 ? 2 : 0);
    }
}
