using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGatlingGun150Controller
{
    public static NativeBattleGatlingGun150Frame Initialize(NativeBattleGatlingGun150Frame frame, NativeBattleGatlingGun150Resources resources, NativeBattleGatlingGun150Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectParameters parameters = Parameters(frame);
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Gatling Gun requires its captured first contact pointer.");
        frame = Dword(Dword(Dword(frame, FirstTargetGlobal, inputs.Packets.ReadContact(contact).Primary.ActorIndex), ParametersGlobal, 0x1D99A78), CasterGlobal, parameters.Actor78);
        NativeBattleEffectTaskPool root = ResetPool(frame, RootDescriptor, RootBank, RootStride, RootCount);
        (root, int rootSlot) = root.Append(RootCallback);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = ResetPool(frame, ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, int director) = children.Append(MainCallback);
        children = children.WriteSlot(director, children.Slots[director].WriteWord(12, 0));
        foreach (uint bank in new[] { ParticleBBase, ParticleABase })
        {
            for (int index = 0; index < ParticleCount; index++)
            {
                frame = Dword(frame, bank + (uint)(index * ParticleStride), 0);
            }
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Gatling Gun requires its current resource arena.");
        NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Gatling Gun requires its actual texture queue.")).Register5099A0(resources.Camera);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Gatling Gun requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = Dword(frame, InitializerGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [root, children], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattleGatlingGun150Frame Advance(NativeBattleGatlingGun150Frame frame, NativeBattleGatlingGun150Resources resources, NativeBattleGatlingGun150Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor || frame.Effect.Pools[0].BankAddress != RootBank || frame.Effect.Pools[0].Stride != RootStride || frame.Effect.Pools[0].Slots.Length != RootCount || frame.Effect.Pools[1].DescriptorAddress != ChildDescriptor || frame.Effect.Pools[1].BankAddress != ChildBank || frame.Effect.Pools[1].Stride != ChildStride || frame.Effect.Pools[1].Slots.Length != ChildCount || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Gatling Gun requires its exclusive physical root and child owners.");
        }
        NativeBattleGatlingGun150Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Gatling Gun reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : RootDescriptor } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattleGatlingGun150Frame Frame, int Result) AdvanceRoot(NativeBattleGatlingGun150Frame frame, uint address, NativeBattleGatlingGun150Resources resources, NativeBattleGatlingGun150Inputs inputs)
    {
        ushort age = Read(frame, address).ReadWord(12);
        frame = Dword(frame, CursorGlobal, PacketBase + ((age & 1) == 0 ? 0u : (uint)PacketBankBytes));
        NativeBattleGatlingGun150Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                MainCallback => AdvanceDirector(current, child, resources, inputs),
                0x60EAE0 or 0x60EFF0 => AdvanceSpawner(current, child, inputs),
                0x60E6D0 or 0x60EBD0 or 0x60F1C0 => AdvanceMesh(current, child, resources, inputs),
                0x60ED10 => AdvanceSprite(current, child, resources, inputs),
                0x60E820 or 0x60EDA0 or 0x60F330 => AdvanceSlots(current, child, resources, inputs),
                _ => throw new NotSupportedException("Gatling Gun reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, step.Pool);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, step.Survivors == 0 ? 2 : 0);
    }
}
