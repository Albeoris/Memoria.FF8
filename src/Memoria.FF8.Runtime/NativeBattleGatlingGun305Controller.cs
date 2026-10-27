using static Memoria.FF8.Runtime.NativeBattleGatlingGun305Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGatlingGun305Controller
{
    public static NativeBattleGatlingGun305Frame Initialize(NativeBattleGatlingGun305Frame frame, NativeBattleGatlingGun305Resources resources, NativeBattleGatlingGun305Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        int first = FirstContact(frame, inputs);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Gatling Gun requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Gatling Gun requires its current texture queue.");
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
        (root, int rootSlot) = root.Append(RootCallback);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
        (children, int mainSlot) = children.Append(MainCallback);
        children = children.WriteSlot(mainSlot, children.Slots[mainSlot].WriteWord(12, 0));
        NativeBattleGatlingGun305State state = frame.State.WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(TargetGlobal, inputs.Packets.ReadContact(first).Primary.ActorIndex).WriteDword(CasterGlobal, Parameters(frame).Actor78).ClearSlotHeads();
        textures = textures.Register5099A0(resources.Camera);
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Gatling Gun requires its current resource arena."), tim);
        return frame with { State = state, Effect = frame.Effect with { Pools = [root, children], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }, Memory = frame.Memory with { Textures = textures } };
    }

    public static NativeBattleGatlingGun305Frame Advance(NativeBattleGatlingGun305Frame frame, NativeBattleGatlingGun305Resources resources, NativeBattleGatlingGun305Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor || frame.Effect.Pools[1].DescriptorAddress != ChildDescriptor)
        {
            throw new InvalidDataException("Gatling Gun requires its two original task pools.");
        }
        NativeBattleGatlingGun305Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Gatling Gun reached an unsupported root callback.");
            }
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            NativeBattleModulePoolTask root = Read(current, address);
            current = current with { State = current.State.WriteDword(CursorGlobal, PacketBase + ((root.ReadByte(12) & 1) == 0 ? 0u : PacketBankBytes)) };
            NativeBattleEffectTaskStep children = current.Effect.Pools[1].Advance((childPool, childSlot) =>
            {
                current = WithPool(current, 1, childPool);
                (current, int result) = Child(current, checked(childPool.BankAddress + (uint)(childSlot * childPool.Stride)), resources, inputs);
                return new(current.Effect.Pools[1], result);
            });
            current = WithPool(current, 1, children.Pool);
            current = Write(current, address, Read(current, address).WriteWord(12, unchecked((ushort)(Read(current, address).ReadWord(12) + 1))));
            return new(current.Effect.Pools[0], children.Survivors == 0 ? 2 : 0);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static (NativeBattleGatlingGun305Frame Frame, int Result) Child(NativeBattleGatlingGun305Frame frame, uint address, NativeBattleGatlingGun305Resources resources, NativeBattleGatlingGun305Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            MainCallback => Main(frame, address, resources, inputs),
            0x5A5C50 or 0x5A6160 or 0x5A6750 => Mesh(frame, address, inputs),
            0x5A6070 or 0x5A6580 => Birth(frame, address, inputs),
            0x5A62A0 => Sprite(frame, address, inputs),
            0x5A5DA0 or 0x5A6330 or 0x5A68C0 => NativeBattleGatlingGun305Slots.Advance(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Gatling Gun reached an unsupported child callback.")
        };
    }
}
