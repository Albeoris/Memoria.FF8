namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMorph172Controller
{
    public static NativeBattleMorph172Frame Initialize(NativeBattleMorph172Frame frame, NativeBattleMorph172Resources resources, NativeBattleMorph172Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Morph requires its actual texture arena.");
        NativeBattleEffectTaskPool roots = ResetPool(frame, 0x239A8F8, 0x239A8E8, 16, 1);
        (roots, int root) = roots.Append(0x5DB210);
        roots = roots with { Slots = roots.Slots.SetItem(root, roots.Slots[root].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = ResetPool(frame, 0x239BEA8, 0x239C6E0, 36, 100);
        (children, int main) = children.Append(0x5D9D80);
        children = children with { Slots = children.Slots.SetItem(main, children.Slots[main].WriteWord(12, 0)) };
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Morph requires its actual first physical contact pointer.");
        frame = Dword(Dword(Dword(frame, 0x239A8E0, inputs.Packets.ReadContact(first).Primary.ActorIndex), 0x239D4F4, Parameters(frame).Actor78), 0x239D4F0, 0x1D99A78);
        for (int index = 0; index < 100; index++)
        {
            frame = Dword(frame, 0x239A908 + (uint)(index * 24), 0);
        }
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Morph requires its actual texture queue.")).Register5099A0(resources.Camera);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x239D6F8)?.ArenaOffset ?? throw new NotSupportedException("Morph requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = Dword(frame, 0x239D6F8, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = 0x239A8F8, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattleMorph172Frame Advance(NativeBattleMorph172Frame frame, NativeBattleMorph172Resources resources, NativeBattleMorph172Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x239A8F8 || frame.Effect.Pools[0].BankAddress != 0x239A8E8 || frame.Effect.Pools[0].Stride != 16 || frame.Effect.Pools[0].Slots.Length != 1 || frame.Effect.Pools[1].DescriptorAddress != 0x239BEA8 || frame.Effect.Pools[1].BankAddress != 0x239C6E0 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Morph requires its exclusive physical task owners.");
        }
        NativeBattleMorph172Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != 0x5DB210)
            {
                throw new NotSupportedException("Morph reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x239A8F8u } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattleMorph172Frame Frame, int Result) AdvanceRoot(NativeBattleMorph172Frame frame, uint address, NativeBattleMorph172Resources resources, NativeBattleMorph172Inputs inputs)
    {
        ushort age = Read(frame, address).ReadWord(12);
        frame = Dword(frame, 0x23AD6FC, (age & 1) == 0 ? 0x239D6FCu : 0x23A56FCu);
        NativeBattleMorph172Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x5D9D80 => AdvanceMain(current, child, resources, inputs),
                0x5D9F20 or 0x5DA0F0 => AdvanceMesh(current, child, resources, inputs),
                0x5DA2C0 => AdvanceSlots(current, child, resources, inputs),
                0x5DA520 => AdvanceBody(current, child, resources, inputs),
                _ => throw new NotSupportedException("Morph reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, step.Pool);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, step.Survivors == 0 ? 2 : 0);
    }
}
