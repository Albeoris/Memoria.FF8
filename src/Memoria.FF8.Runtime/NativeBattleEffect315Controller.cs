using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect315Controller
{
    public static NativeBattleEffect315Frame Initialize(NativeBattleEffect315Frame frame, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null || frame.State.ChildBankStorage is null)
        {
            throw new InvalidDataException("Effect315 requires its exclusive inactive task bank owners.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect315 requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect315 requires its executed texture initializer.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int header = Parameters(frame).HeaderIndex7C;
        int first = packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect315 requires its actual first current contact pointer.");
        uint target = packets.ReadContact(first).Primary.ActorIndex;
        NativeBattleEffectTaskPool root = RetainPayload(NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount), frame.State.RootBankStorage);
        NativeBattleEffectTaskPool children = RetainPayload(NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount), frame.State.ChildBankStorage);
        (root, int rootSlot) = root.Append(0x59B000);
        (children, int mainSlot) = children.Append(0x598E70);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(0xC, 0));
        children = children.WriteSlot(mainSlot, children.Slots[mainSlot].WriteWord(0xC, 0));
        frame = frame with { Effect = frame.Effect with { Pools = [root, children], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }, State = frame.State.WriteDword(TargetGlobal, target).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, Parameters(frame).Actor78).WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null, ChildBankStorage = null } };
        for (int index = 0; index < ParticleCount; index++)
        {
            frame = WriteBytes(frame, checked(ParticleAddress + (uint)(index * ParticleStride)), NativeBattleEffectArena.Clear(), 4);
        }
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Effect315 requires its current texture process.");
        textures = textures.Register5099A0(resources.Camera);
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        return frame with { Memory = frame.Memory with { Textures = textures } };
    }

    public static NativeBattleEffect315Frame Advance(NativeBattleEffect315Frame frame, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Effect315 requires its actual two exclusive active task banks.");
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = Pools[index];
            if (pool.DescriptorAddress != expected.Descriptor || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect315 task pools leave their original physical dimensions.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleEffect315Frame Frame, int Survivors) AdvancePool(NativeBattleEffect315Frame frame, int index, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        NativeBattleEffect315Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleEffect315Frame Frame, int Result) AdvanceTask(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs) => Read(frame, address).Callback switch
    {
        0x598E70 => AdvanceMain(frame, address, resources, inputs),
        0x599470 => AdvanceDelayedParticles(frame, address, resources, inputs),
        0x599860 => AdvanceVerticalParticles(frame, address, resources, inputs, false),
        0x599A90 => AdvanceVerticalParticles(frame, address, resources, inputs, true),
        0x599CC0 => AdvanceBodyTask(frame, address, resources, inputs, false),
        0x59A3E0 => AdvanceBodyTask(frame, address, resources, inputs, true),
        0x59A500 => AdvanceGrowingMesh(frame, address, resources, inputs),
        0x59A670 => AdvanceScrollingMesh(frame, address, resources, inputs),
        0x59A7F0 => AdvanceOrbitParticles(frame, address, resources, inputs),
        0x59AAF0 => AdvanceRandomMesh(frame, address, resources, inputs),
        0x59AC70 => AdvanceRingMesh(frame, address, resources, inputs),
        0x59ADB0 => AdvanceModelColor(frame, address, resources, inputs, false),
        0x59AEF0 => AdvanceModelColor(frame, address, resources, inputs, true),
        0x59B000 => AdvanceRoot(frame, address, resources, inputs),
        0x59B070 => AdvanceRelease(frame, address, inputs),
        _ => throw new NotSupportedException("Effect315 reached an unsupported native task callback.")
    };

    private static (NativeBattleEffect315Frame Frame, int Result) AdvanceRoot(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        int age = Read(frame, address).ReadWord(0xC);
        frame = Global(frame, CursorGlobal, checked(frame.State.ReadDword(ArenaGlobal) + ((age & 1) != 0 ? SecondPacketOffset : 0u)));
        (frame, int survivors) = AdvancePool(frame, 1, resources, inputs);
        if (survivors == 0)
        {
            (frame, uint waiter) = Spawn(frame, 0x59B070, RootDescriptor);
            frame = Write(frame, waiter, Read(frame, waiter).WriteWord(0xC, 0).WriteWord(0xE, 0));
        }
        return (Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(age + 1)))), survivors == 0 ? 2 : 0);
    }

    private static (NativeBattleEffect315Frame Frame, int Result) AdvanceRelease(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Inputs inputs)
    {
        int age = Read(frame, address).ReadWord(0xC);
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect315ReleaseCommon508630(frame.State.ReadDword(ArenaGlobal), address + 0xE));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(age + 1))));
        return (frame, Read(frame, address).ReadWord(0xE) != 0 ? 2 : 0);
    }

    public static NativeBattleEffect315Frame WriteCompletionByte(NativeBattleEffect315Frame frame, uint address, byte value)
    {
        if (address < RootBank + 0xE || address >= RootBank + RootBankLength || (address - RootBank) % RootStride != 0xE)
        {
            throw new InvalidDataException("Effect315 completion leaves its actual physical root byte.");
        }
        return WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
    }

    private static NativeBattleEffectTaskPool RetainPayload(NativeBattleEffectTaskPool pool, NativeBattleEffectArena storage)
    {
        for (int slot = 0; slot < pool.Slots.Length; slot++)
        {
            int length = pool.Stride - 12;
            NativeBattleEffectArena payload = NativeBattleEffectNodeContextMemory.Copy(storage, slot * pool.Stride + 12, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
            pool = pool.WriteSlot(slot, CopyTask(pool.Slots[slot], 12, payload, length));
        }
        return pool;
    }

    private static NativeBattleEffectParameters Parameters(NativeBattleEffect315Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Effect315 requires its actual current parameters.");
    private static bool Paused(NativeBattleEffect315Frame frame, NativeBattleEffect315Inputs inputs) => (inputs.ReadFlags(frame) & 0x201) != 0;
    private static NativeBattleEffect315Frame Global(NativeBattleEffect315Frame frame, uint address, uint value) => frame with { State = frame.State.WriteDword(address, value) };
    private static int Caster(NativeBattleEffect315Frame frame) => checked((int)frame.State.ReadDword(CasterGlobal));
    private static NativeBattleVector Vector315(NativeBattleEffectArena source, int offset = 0) => new(Word315(source, offset), Word315(source, offset + 2), Word315(source, offset + 4));
    private static short Word315(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
    private static int Int315(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt32LittleEndian(source.Read(offset, 4));
    private static uint Uint315(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));
    private static (NativeBattleEffect315Frame Frame, uint Address) Spawn(NativeBattleEffect315Frame frame, uint callback, uint descriptor = ChildDescriptor)
    {
        (frame, uint address) = Append(frame, descriptor, callback);
        if (address == 0)
        {
            throw new NotSupportedException("Effect315 exhausted its required native task bank.");
        }
        return (frame, address);
    }
    private static NativeBattleEffect315Frame Random315(NativeBattleEffect315Frame frame, out int value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort sample);
        value = sample;
        return frame with { Random = random };
    }
    private static void RequireBinding(NativeBattleEffect315Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect315 requires its original selected entry and initializer.");
        }
    }
}
