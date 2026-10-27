namespace Memoria.FF8.Runtime;

public static partial class NativeBattleChainGun190Controller
{
    public static NativeBattleChainGun190Frame Initialize(NativeBattleChainGun190Frame frame, NativeBattleChainGun190Resources resources, NativeBattleChainGun190Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectTaskPool roots = ResetPool(frame, 0x2281B30, 0x2281B20, 16, 1);
        (roots, int root) = roots.Append(0x5BC900);
        roots = roots.WriteSlot(root, roots.Slots[root].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = ResetPool(frame, 0x2283108, 0x2283118, 36, 100);
        (children, int main) = children.Append(0x5BB380);
        children = children.WriteSlot(main, children.Slots[main].WriteWord(12, 0));
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Chain Gun requires its actual first contact pointer.");
        frame = Dword(Dword(Dword(frame, 0x2281B18, inputs.Packets.ReadContact(first).Primary.ActorIndex), 0x2283F2C, Parameters(frame).Actor78), 0x2283F28, 0x1D99A78);
        foreach (uint bank in new uint[] { 0x22824A0, 0x2281B40 })
        {
            for (int index = 0; index < 100; index++)
            {
                frame = Dword(frame, bank + (uint)(index * 24), 0);
            }
        }
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Chain Gun requires its current texture queue.")).Register5099A0(resources.Camera);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2283F30)?.ArenaOffset ?? throw new NotSupportedException("Chain Gun requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Chain Gun texture upload requires its current resource arena."), tim);
        frame = Dword(frame, 0x2283F30, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = 0x2281B30, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattleChainGun190Frame Advance(NativeBattleChainGun190Frame frame, NativeBattleChainGun190Resources resources, NativeBattleChainGun190Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x2281B30 || frame.Effect.Pools[0].BankAddress != 0x2281B20 || frame.Effect.Pools[0].Stride != 16 || frame.Effect.Pools[0].Slots.Length != 1 || frame.Effect.Pools[1].DescriptorAddress != 0x2283108 || frame.Effect.Pools[1].BankAddress != 0x2283118 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Chain Gun requires its exclusive physical task owners.");
        }
        NativeBattleChainGun190Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != 0x5BC900)
            {
                throw new NotSupportedException("Chain Gun reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x2281B30u } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattleChainGun190Frame Frame, int Result) AdvanceRoot(NativeBattleChainGun190Frame frame, uint address, NativeBattleChainGun190Resources resources, NativeBattleChainGun190Inputs inputs)
    {
        ushort age = Read(frame, address).ReadWord(12);
        frame = Dword(frame, 0x2293F40, (age & 1) == 0 ? 0x2283F40u : 0x228BF40u);
        NativeBattleChainGun190Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x5BB380 => AdvanceMain(current, child, resources, inputs),
                0x5BB5A0 or 0x5BBAA0 or 0x5BC080 => AdvanceMesh(current, child, resources, inputs),
                0x5BB6F0 or 0x5BBC70 or 0x5BC1F0 => AdvanceSlots(current, child, resources, inputs),
                0x5BB9B0 or 0x5BBEC0 => AdvanceImpact(current, child, resources, inputs),
                0x5BBBE0 => AdvanceSprite(current, child, resources, inputs),
                _ => throw new NotSupportedException("Chain Gun reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, step.Pool);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, step.Survivors == 0 ? 2 : 0);
    }
}
