using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleReflectBeam135Controller
{
    public static NativeBattleReflectBeam135Frame Initialize(NativeBattleReflectBeam135Frame frame, NativeBattleReflectBeam135Resources resources, NativeBattleReflectBeam135Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Reflect Beam requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Reflect Beam requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(Arena(frame), tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0)) };
        return frame with
        {
            State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)),
            Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) },
            Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }
        };
    }

    public static NativeBattleReflectBeam135Frame Advance(NativeBattleReflectBeam135Frame frame, NativeBattleReflectBeam135Resources resources, NativeBattleReflectBeam135Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 5) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Reflect Beam requires its original root and four working pools.");
        }
        NativeBattleReflectBeam135Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Reflect Beam reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    public static (NativeBattleReflectBeam135Frame Frame, int Result) AdvanceRoot(NativeBattleReflectBeam135Frame frame, uint address, NativeBattleReflectBeam135Resources resources, NativeBattleReflectBeam135Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool first = root.ReadDword(16) != 0;
        uint working = Working(frame);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, root.WriteDword(16, first ? 0u : 1u));
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, working + MainOffset, MainStride, 1);
            NativeBattleEffectTaskPool secondary = NativeBattleEffectTaskPool.Initialize(SecondaryDescriptor, working + SecondaryOffset, SecondaryStride, SecondaryCount);
            NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(TargetDescriptor, working + TargetOffset, TargetStride, TargetCount);
            NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, working, ParticleStride, ParticleCount);
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, secondary, targets, particles] }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [main, secondary, targets, particles]) };
            (frame, uint childAddress) = Append(frame, 1, MainCallback);
            if (childAddress == 0)
            {
                throw new NotSupportedException("Reflect Beam exhausted its single main task.");
            }
            NativeBattleModulePoolTask task = Read(frame, childAddress).WriteBytes(12, new byte[724]);
            int firstContact = FirstContact(frame, inputs);
            int count = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].ReadByte(16);
            if (count is < 1 or > 3)
            {
                throw new NotSupportedException("Reflect Beam requires one to three actual captured targets.");
            }
            task = task.WriteWord(14, (ushort)count);
            short meanX = 0;
            short meanY = 0;
            short meanZ = 0;
            for (int index = 0; index < count; index++)
            {
                int actor = inputs.Packets.ReadContact(firstContact + index).Primary.ActorIndex;
                task = task.WriteDword(0x14 + index * 4, ModelAddress(actor)).WriteByte(0x10 + index, (byte)index);
                NativeBattleReflectBeam135AnchorStep anchor = inputs.ReadAnchor(frame, actor);
                frame = anchor.Frame;
                meanX = unchecked((short)(meanX + anchor.Anchor.X));
                meanY = unchecked((short)(meanY + anchor.Anchor.Y));
                meanZ = unchecked((short)(meanZ + anchor.Anchor.Z));
            }
            task = task.WriteWord(0x20, unchecked((ushort)(meanX / count))).WriteWord(0x22, unchecked((ushort)(meanY / count))).WriteWord(0x24, unchecked((ushort)(meanZ / count)));
            for (int index = 0; index < count - 1; index++)
            {
                for (int next = index + 1; next < count; next++)
                {
                    uint left = task.ReadDword(0x14 + index * 4);
                    uint right = task.ReadDword(0x14 + next * 4);
                    if (Transform(inputs.ReadModel(Actor(left))).Position28.X < Transform(inputs.ReadModel(Actor(right))).Position28.X)
                    {
                        byte old = task.ReadByte(0x10 + index);
                        task = task.WriteDword(0x14 + index * 4, right).WriteDword(0x14 + next * 4, left).WriteByte(0x10 + index, task.ReadByte(0x10 + next)).WriteByte(0x10 + next, old);
                    }
                }
            }
            task = InitializeWorkspace(task, 0x28, 0x2B8, resources.MainAct);
            frame = Write(frame, childAddress, task);
        }
        int survivors = 0;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            for (int index = 1; index < 5; index++)
            {
                byte caster = checked((byte)Actor(frame.State.ReadDword(CasterGlobal)));
                if (index == 2)
                {
                    frame = inputs.Execute(frame, new NativeBattleReflectBeam135ComposeCaster(caster));
                }
                int ownerIndex = index;
                NativeBattleReflectBeam135Frame current = frame;
                NativeBattleEffectTaskStep advanced = frame.Effect.Pools[index].Advance((pool, slot) =>
                {
                    current = WithPool(current, ownerIndex, pool);
                    uint child = checked(pool.BankAddress + (uint)(slot * pool.Stride));
                    (current, int result) = pool.Slots[slot].Callback switch
                    {
                        MainCallback when ownerIndex == 1 => AdvanceMain(current, child, resources, inputs),
                        SecondaryCallback when ownerIndex == 2 => AdvanceSecondary(current, child, resources, inputs),
                        TargetCallback when ownerIndex == 3 => AdvanceTarget(current, child, resources, inputs),
                        ParticleACallback or ParticleBCallback or ParticleCCallback when ownerIndex == 4 => AdvanceParticle(current, child, resources, inputs),
                        _ => throw new NotSupportedException("Reflect Beam reached an unsupported child callback.")
                    };
                    return new(current.Effect.Pools[ownerIndex], result);
                });
                frame = WithPool(current, index, advanced.Pool);
                survivors += advanced.Survivors;
                if (index == 2)
                {
                    frame = inputs.Execute(frame, new NativeBattleReflectBeam135RestoreCaster(caster));
                }
            }
            if (survivors == 0)
            {
                return (frame, 2);
            }
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }

}
