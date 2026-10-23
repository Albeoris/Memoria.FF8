using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleOilBlast137Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleOilBlast137Controller
{
    public static NativeBattleOilBlast137Frame Initialize(NativeBattleOilBlast137Frame frame, NativeBattleOilBlast137Resources resources, NativeBattleOilBlast137Inputs inputs)
    {
        Require(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Oil Blast requires its executed texture arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Oil Blast requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Oil Blast requires its current texture state.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0)) };
        textures = textures.Register5099A0(resources.Camera);
        return frame with
        {
            State = frame.State.SeedColdSource(resources).WriteDword(InitializerGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, NativeBattleEffectNodeMemory.ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)),
            Memory = frame.Memory with { Textures = textures },
            Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }
        };
    }

    public static NativeBattleOilBlast137Frame Advance(NativeBattleOilBlast137Frame frame, NativeBattleOilBlast137Resources resources, NativeBattleOilBlast137Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        NativeBattleOilBlast137Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.DescriptorAddress != RootDescriptor || pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Oil Blast requires its actual root callback.");
            }
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            (current, int result) = AdvanceRoot(current, address, resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    public static (NativeBattleOilBlast137Frame Frame, int Result) AdvanceRoot(NativeBattleOilBlast137Frame frame, uint address, NativeBattleOilBlast137Resources resources, NativeBattleOilBlast137Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint working = frame.State.ReadDword(WorkingGlobal);
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, root.WriteDword(16, first ? 0u : 1u));
        if (Word(root, 12) == 1)
        {
            NativeBattleActionHeaderSlot header = Header(frame, inputs);
            int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Oil Blast requires its first captured contact.");
            uint target = ModelAddress(inputs.Packets.ReadContact(firstContact).Primary.ActorIndex);
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, checked(working + MainOffset), MainStride, 1);
            NativeBattleEffectTaskPool secondary = NativeBattleEffectTaskPool.Initialize(SecondaryDescriptor, checked(working + SecondaryOffset), SecondaryStride, SecondaryCount);
            NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, working, ParticleStride, ParticleCount);
            (main, int slot) = main.Append(MainCallback);
            main = main with { Slots = main.Slots.SetItem(slot, main.Slots[slot].WriteBytes(12, new byte[MainStride - 12]).WriteDword(16, target)) };
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, secondary, particles] }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [main, secondary, particles]) };
            frame = Write(frame, address, Read(frame, address).WriteWord(14, 1));
            NativeBattleOilBlast137PointStep anchor = inputs.ReadBone502170(frame, target, 0xF1, 0);
            frame = anchor.Frame;
            uint mainAddress = main.BankAddress;
            frame = Write(frame, mainAddress, PutVector(Read(frame, mainAddress), 0x1C, anchor.Point));
        }
        int survivors = 0;
        if (Read(frame, address).ReadWord(14) != 0)
        {
            for (int index = 1; index < 4; index++)
            {
                int ownerIndex = index;
                NativeBattleOilBlast137Frame current = frame;
                NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
                {
                    current = WithPool(current, ownerIndex, pool);
                    uint child = checked(pool.BankAddress + (uint)(slot * pool.Stride));
                    (current, int result) = pool.Slots[slot].Callback switch
                    {
                        MainCallback when ownerIndex == 1 => AdvanceMain(current, child, resources, inputs),
                        SecondaryCallback when ownerIndex == 2 => AdvanceSecondary(current, child, resources, inputs),
                        ParticleCallback when ownerIndex == 3 => AdvanceParticle(current, child, resources, inputs),
                        _ => throw new NotSupportedException("Oil Blast reached an unsupported pool callback.")
                    };
                    return new(current.Effect.Pools[ownerIndex], result);
                });
                frame = WithPool(current, index, step.Pool);
                if (index == 1)
                {
                    survivors = step.Survivors;
                }
            }
            if (survivors == 0)
            {
                return (frame, 2);
            }
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Word(Read(frame, address), 12) + 1)))), 0);
    }

    private static void Require(NativeBattleOilBlast137Frame frame)
    {
        if (frame.Effect.Binding.Effect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Oil Blast requires its selected original binding.");
        }
        frame.State.Validate();
    }

    private static NativeBattleActionHeaderSlot Header(NativeBattleOilBlast137Frame frame, NativeBattleOilBlast137Inputs inputs) => inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];

}
