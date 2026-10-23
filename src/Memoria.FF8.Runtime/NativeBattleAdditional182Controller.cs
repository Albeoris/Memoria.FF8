namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAdditional182Controller
{
    public static NativeBattleAdditional182Frame Initialize(NativeBattleAdditional182Frame frame, NativeBattleAdditional182Resources resources, NativeBattleAdditional182Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Additional requires its actual COMMON arena.");
        frame = Dword(frame, 0x23065CC, NativeBattleEffectNodeMemory.ArenaAddress);
        NativeBattleEffectTaskPool roots = ResetPool(frame, 0x2304238, 0x2304228, 16, 1);
        (roots, int root) = roots.Append(0x5CB4E0);
        roots = roots with { Slots = roots.Slots.SetItem(root, roots.Slots[root].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = ResetPool(frame, 0x23056E8, 0x23056F8, 36, 100);
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Additional requires its actual first contact.");
        bool set = (inputs.Packets.ReadContact(first).Primary.HitFlags3 & 4) != 0;
        (children, int director) = children.Append(set ? 0x5CA0E0u : 0x5CA970u);
        children = children with { Slots = children.Slots.SetItem(director, children.Slots[director].WriteWord(12, 0)) };
        frame = Dword(Dword(Dword(frame, 0x2304220, inputs.Packets.ReadContact(first).Primary.ActorIndex), 0x230650C, Parameters(frame).Actor78), 0x2306508, 0x1D99A78);
        foreach ((uint bank, int count) in new (uint, int)[] { (0x2304248, 120), (0x2304D88, 100) })
        {
            for (int index = 0; index < count; index++)
            {
                frame = Dword(frame, bank + (uint)(index * 24), 0);
            }
        }
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Additional requires its actual texture queue.")).Register5099A0(set ? resources.CameraSet : resources.CameraClear);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2306510)?.ArenaOffset ?? throw new NotSupportedException("Additional requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = Dword(frame, 0x2306510, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = 0x2304238, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattleAdditional182Frame Advance(NativeBattleAdditional182Frame frame, NativeBattleAdditional182Resources resources, NativeBattleAdditional182Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x2304238 || frame.Effect.Pools[1].DescriptorAddress != 0x23056E8 || frame.Effect.Pools[0].BankAddress != 0x2304228 || frame.Effect.Pools[0].Stride != 16 || frame.Effect.Pools[0].Slots.Length != 1 || frame.Effect.Pools[1].BankAddress != 0x23056F8 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Additional requires its exclusive physical root and child owners.");
        }
        NativeBattleAdditional182Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            uint address = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x5CB4E0 => AdvanceRoot(current, address, resources, inputs),
                _ => throw new NotSupportedException("Additional reached an unsupported root callback.")
            };
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x2304238u } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattleAdditional182Frame Frame, int Result) AdvanceRoot(NativeBattleAdditional182Frame frame, uint address, NativeBattleAdditional182Resources resources, NativeBattleAdditional182Inputs inputs)
    {
        ushort age = Read(frame, address).ReadWord(12);
        frame = Dword(frame, 0x23065D0, frame.State.ReadDword(0x23065CC) + ((age & 1) == 0 ? 0u : 0xC000u));
        NativeBattleAdditional182Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x5CA0E0 or 0x5CA970 => AdvanceMain(current, child, resources, inputs),
                0x5CA6B0 or 0x5CA810 or 0x5CB130 => AdvanceMesh(current, child, resources, inputs),
                0x5CA490 or 0x5CAED0 => AdvanceSlots(current, child, resources, inputs),
                0x5CB390 => AdvanceActorFade(current, child, resources, inputs),
                _ => throw new NotSupportedException("Additional reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, step.Pool);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, step.Survivors == 0 ? 2 : 0);
    }

}
