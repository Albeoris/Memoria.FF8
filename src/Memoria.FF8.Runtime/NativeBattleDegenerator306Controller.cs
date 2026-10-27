using static Memoria.FF8.Runtime.NativeBattleDegenerator306Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDegenerator306Controller
{
    public static NativeBattleDegenerator306Frame Initialize(NativeBattleDegenerator306Frame frame, NativeBattleDegenerator306Resources resources, NativeBattleDegenerator306Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        int first = FirstContact(frame, inputs);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Degenerator requires its executed texture initializer pointer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Degenerator requires its texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Degenerator requires its current arena."), tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        (root, int index) = root.Append(RootCallback);
        root = root.WriteSlot(index, root.Slots[index].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
        (children, int main) = children.Append(MainCallback);
        children = children.WriteSlot(main, children.Slots[main].WriteWord(12, 0));
        NativeBattleDegenerator306State state = frame.State.ClearSlotHeads().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(TargetGlobal, inputs.Packets.ReadContact(first).Primary.ActorIndex).WriteDword(CasterGlobal, Parameters(frame).Actor78).WriteDword(ParametersGlobal, 0x1D99A78);
        return frame with { State = state, Effect = frame.Effect with { Pools = [root, children], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }, Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) } };
    }

    public static NativeBattleDegenerator306Frame Advance(NativeBattleDegenerator306Frame frame, NativeBattleDegenerator306Resources resources, NativeBattleDegenerator306Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor || frame.Effect.Pools[1].DescriptorAddress != ChildDescriptor)
        {
            throw new InvalidDataException("Degenerator requires its two original task pools.");
        }
        NativeBattleDegenerator306Frame current = frame;
        NativeBattleEffectTaskStep root = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            (current, int result) = pool.Slots[slot].Callback switch
            {
                RootCallback => Root(current, address, resources, inputs),
                CompletionCallback => Completion(current, address, inputs),
                _ => throw new NotSupportedException("Degenerator reached an unsupported root callback.")
            };
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, root.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = root.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static (NativeBattleDegenerator306Frame Frame, int Result) Root(NativeBattleDegenerator306Frame frame, uint address, NativeBattleDegenerator306Resources resources, NativeBattleDegenerator306Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint cursor = frame.State.ReadDword(WorkingGlobal) + ((task.ReadByte(12) & 1) != 0 ? PacketBankBytes : 0u);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, cursor) };
        NativeBattleDegenerator306Frame current = frame;
        NativeBattleEffectTaskStep children = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            (current, int result) = Child(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            return new(current.Effect.Pools[1], result);
        });
        current = WithPool(current, 1, children.Pool);
        current = Write(current, address, Read(current, address).WriteWord(12, unchecked((ushort)(Read(current, address).ReadWord(12) + 1))));
        return (current, children.Survivors == 0 ? 2 : 0);
    }

    private static (NativeBattleDegenerator306Frame Frame, int Result) Completion(NativeBattleDegenerator306Frame frame, uint address, NativeBattleDegenerator306Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadWord(12) == 1)
        {
            frame = Write(frame, address, task.WriteWord(14, 0));
            frame = inputs.Execute(frame, new NativeBattleDegenerator306CommonTexture(frame.State.ReadDword(WorkingGlobal) + FileBufferOffset, address + 14));
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return (frame, task.ReadWord(14) == 0 ? 0 : 2);
    }
}
