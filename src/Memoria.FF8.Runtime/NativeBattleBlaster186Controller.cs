namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlaster186Controller
{
    public static NativeBattleBlaster186Frame Initialize(NativeBattleBlaster186Frame frame, NativeBattleBlaster186Resources resources, NativeBattleBlaster186Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Blaster requires its actual COMMON arena.");
        frame = Dword(frame, 0x22BC120, NativeBattleEffectNodeMemory.ArenaAddress);
        NativeBattleEffectTaskPool roots = ResetPool(frame, 0x22B7628, 0x22B7608, 16, 2);
        (roots, int root) = roots.Append(0x5C0C80);
        roots = roots.WriteSlot(root, roots.Slots[root].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = ResetPool(frame, 0x22B9930, 0x22B9940, 36, 100);
        (children, int director) = children.Append(0x5BF7B0);
        children = children.WriteSlot(director, children.Slots[director].WriteWord(12, 0));
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Blaster requires its actual first contact.");
        frame = Dword(Dword(Dword(frame, 0x22B7600, inputs.Packets.ReadContact(first).Primary.ActorIndex), 0x22BC0FC, Parameters(frame).Actor78), 0x22BC0F8, 0x1D99A78);
        foreach ((uint bank, int count) in new (uint, int)[] { (0x22B7638, 100) })
        {
            for (int index = 0; index < count; index++)
            {
                frame = Dword(frame, bank + (uint)(index * 24), 0);
            }
        }
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Blaster requires its actual texture queue.")).Register5099A0(resources.Camera);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x22BC100)?.ArenaOffset ?? throw new NotSupportedException("Blaster requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = Dword(frame, 0x22BC100, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = 0x22B7628, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattleBlaster186Frame Advance(NativeBattleBlaster186Frame frame, NativeBattleBlaster186Resources resources, NativeBattleBlaster186Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x22B7628 || frame.Effect.Pools[1].DescriptorAddress != 0x22B9930 || frame.Effect.Pools[0].BankAddress != 0x22B7608 || frame.Effect.Pools[0].Stride != 16 || frame.Effect.Pools[0].Slots.Length != 2 || frame.Effect.Pools[1].BankAddress != 0x22B9940 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Blaster requires its exclusive physical root and child owners.");
        }
        NativeBattleBlaster186Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            uint address = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x5C0C80 => AdvanceRoot(current, address, resources, inputs),
                0x5C0CF0 => AdvanceWaiter(current, address, inputs),
                _ => throw new NotSupportedException("Blaster reached an unsupported root callback.")
            };
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x22B7628u } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattleBlaster186Frame Frame, int Result) AdvanceRoot(NativeBattleBlaster186Frame frame, uint address, NativeBattleBlaster186Resources resources, NativeBattleBlaster186Inputs inputs)
    {
        ushort age = Read(frame, address).ReadWord(12);
        frame = Dword(frame, 0x22BC124, frame.State.ReadDword(0x22BC120) + ((age & 1) == 0 ? 0u : 0xC000u));
        NativeBattleBlaster186Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x5BF7B0 => AdvanceMain(current, child, resources, inputs),
                0x5BFC50 => AdvanceEnvelope(current, child, inputs),
                0x5BFCF0 => AdvanceBeam(current, child, resources, inputs),
                0x5C0190 => AdvancePulse(current, child, inputs),
                0x5C0040 or 0x5C0280 or 0x5C0750 => AdvanceMesh(current, child, resources, inputs),
                0x5BFEC0 or 0x5C0690 => AdvanceSprite(current, child, resources, inputs),
                0x5C0410 or 0x5C0900 => AdvanceSlots(current, child, resources, inputs),
                _ => throw new NotSupportedException("Blaster reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, step.Pool);
        if (step.Survivors == 0)
        {
            NativeBattleEffectTaskPool roots = frame.Effect.Pools[0];
            (roots, int slot) = roots.Append(0x5C0CF0);
            roots = roots.WriteSlot(slot, roots.Slots[slot].WriteWord(12, 0).WriteWord(14, 0));
            frame = WithPool(frame, 0, roots);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, step.Survivors == 0 ? 2 : 0);
    }

    public static (NativeBattleBlaster186Frame Frame, int Result) AdvanceWaiter(NativeBattleBlaster186Frame frame, uint address, NativeBattleBlaster186Inputs inputs)
    {
        if (Read(frame, address).ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleBlaster186ReleaseCommon(frame.State.ReadDword(0x22BC120), address));
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        bool completed = task.ReadWord(14) != 0;
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return (frame, completed ? 2 : 0);
    }

}
