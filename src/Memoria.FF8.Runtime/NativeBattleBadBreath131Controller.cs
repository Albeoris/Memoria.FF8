using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBadBreath131Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBadBreath131Controller
{
    public static NativeBattleBadBreath131Frame Initialize(NativeBattleBadBreath131Frame frame, NativeBattleBadBreath131Resources resources, NativeBattleBadBreath131Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Bad Breath requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Bad Breath requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(Arena(frame), tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0)) };
        return frame with
        {
            State = frame.State.SeedColdSources(resources).WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)),
            Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) },
            Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }
        };
    }

    public static NativeBattleBadBreath131Frame Advance(NativeBattleBadBreath131Frame frame, NativeBattleBadBreath131Resources resources, NativeBattleBadBreath131Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 4) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Bad Breath requires its original root and three working pools.");
        }
        NativeBattleBadBreath131Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Bad Breath reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    public static (NativeBattleBadBreath131Frame Frame, int Result) AdvanceRoot(NativeBattleBadBreath131Frame frame, uint address, NativeBattleBadBreath131Resources resources, NativeBattleBadBreath131Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool first = root.ReadDword(16) != 0;
        uint working = Working(frame);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, root.WriteDword(16, first ? 0u : 1u));
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            NativeBattleEffectTaskPool main = ResetPool(frame, MainDescriptor, working + MainOffset, MainStride, 1);
            NativeBattleEffectTaskPool targets = ResetPool(frame, TargetDescriptor, working + TargetOffset, TargetStride, TargetCount);
            NativeBattleEffectTaskPool particles = ResetPool(frame, ParticleDescriptor, working, ParticleStride, ParticleCount);
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, targets, particles] }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [main, targets, particles]) };
            (frame, uint childAddress) = Append(frame, 1, MainCallback);
            if (childAddress == 0)
            {
                throw new NotSupportedException("Bad Breath exhausted its single main task.");
            }
            NativeBattleModulePoolTask task = Read(frame, childAddress).WriteBytes(12, new byte[40]);
            int firstContact = FirstContact(frame, inputs);
            int count = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].ReadByte(16);
            if (count is < 1 or > 3)
            {
                throw new NotSupportedException("Bad Breath requires one to three actual captured targets.");
            }
            task = task.WriteWord(14, (ushort)count);
            short meanZ = 0;
            for (int index = 0; index < count; index++)
            {
                int actor = inputs.Packets.ReadContact(firstContact + index).Primary.ActorIndex;
                task = task.WriteDword(16 + index * 4, ModelAddress(actor)).WriteByte(0x1C + index, (byte)index);
                meanZ = unchecked((short)(meanZ + Transform(inputs.ReadModel(actor)).Position28.Z));
            }
            task = task.WriteWord(0x20, unchecked((ushort)(meanZ / count)));
            for (int index = 0; index < count - 1; index++)
            {
                for (int next = index + 1; next < count; next++)
                {
                    uint left = task.ReadDword(16 + index * 4);
                    uint right = task.ReadDword(16 + next * 4);
                    if (Transform(inputs.ReadModel(Actor(left))).Position28.X > Transform(inputs.ReadModel(Actor(right))).Position28.X)
                    {
                        byte old = task.ReadByte(0x1C + index);
                        task = task.WriteDword(16 + index * 4, right).WriteDword(16 + next * 4, left).WriteByte(0x1C + index, task.ReadByte(0x1C + next)).WriteByte(0x1C + next, old);
                    }
                }
            }
            frame = Write(frame, childAddress, task);
            frame = frame with { State = frame.State.WriteMesh(MeshAAddress, Preprocess6B41D0(frame.State.ReadMesh(MeshAAddress))).WriteMesh(MeshBAddress, Preprocess6B41D0(frame.State.ReadMesh(MeshBAddress))) };
        }
        int survivors = 0;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            for (int index = 1; index < 4; index++)
            {
                int ownerIndex = index;
                NativeBattleBadBreath131Frame current = frame;
                NativeBattleEffectTaskStep advanced = frame.Effect.Pools[index].Advance((pool, slot) =>
                {
                    current = WithPool(current, ownerIndex, pool);
                    uint child = checked(pool.BankAddress + (uint)(slot * pool.Stride));
                    (current, int result) = pool.Slots[slot].Callback switch
                    {
                        MainCallback when ownerIndex == 1 => AdvanceMain(current, child, resources, inputs),
                        TargetCallback when ownerIndex == 2 => AdvanceTarget(current, child, resources, inputs),
                        ParticleCallback when ownerIndex == 3 => AdvanceParticle(current, child, resources, inputs),
                        _ => throw new NotSupportedException("Bad Breath reached an unsupported child callback.")
                    };
                    return new(current.Effect.Pools[ownerIndex], result);
                });
                frame = WithPool(current, index, advanced.Pool);
                survivors += advanced.Survivors;
            }
            if (survivors == 0)
            {
                return (frame, 2);
            }
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }

    private static NativeBattleModelTransform Transform(NativeBattleGraphicsModel model) => model.Transform ?? throw new NotSupportedException("Bad Breath requires its actual model transform.");
    private static int FirstContact(NativeBattleBadBreath131Frame frame, NativeBattleBadBreath131Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length || inputs.Packets.Headers[index].ReadByte(16) == 0)
        {
            throw new NotSupportedException("Bad Breath requires its actual captured contact header.");
        }
        return inputs.Packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Bad Breath requires its actual contact pointer.");
    }
    private static void RequireBinding(NativeBattleBadBreath131Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Bad Breath requires its original selected entry.");
        }
    }
}
