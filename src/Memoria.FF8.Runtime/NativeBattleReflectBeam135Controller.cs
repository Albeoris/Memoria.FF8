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
        root = root.WriteSlot(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        return frame with
        {
            State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteModelOwner(CasterGlobal, Parameters(frame).Source, inputs.Packets.ModelAliases),
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
        bool expanded = inputs.StorageContract == BattleContactTaskStorageContract.ExpandableArena;
        if (expanded && frame.Effect.Pools.Length == 5)
        {
            ValidateExpandedBanks(frame);
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
        if (expanded && roots.Survivors == 0 && current.Effect.Pools.Length == 5)
        {
            current = RetireHostedBanks(current);
        }
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    public static (NativeBattleReflectBeam135Frame Frame, int Result) AdvanceRoot(NativeBattleReflectBeam135Frame frame, uint address, NativeBattleReflectBeam135Resources resources, NativeBattleReflectBeam135Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool first = root.ReadDword(16) != 0;
        uint working = Working(frame);
        bool expanded = inputs.StorageContract == BattleContactTaskStorageContract.ExpandableArena;
        int count = 0;
        ushort targetCapacity = TargetCount;
        ushort particleCapacity = ParticleCount;
        uint targetBank = working + TargetOffset;
        uint particleBank = working;
        if (expanded && root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            count = inputs.Packets.ReadHeader(Parameters(frame).Header).ReadContactCount();
            (targetCapacity, particleCapacity) = Capacities(count);
            if (count > TargetCount)
            {
                (NativeBattleEffectMemory memory, targetBank) = NativeBattlePresentationTaskStorage.Reserve(frame.Memory, frame.Effect, inputs.Packets.Roster, TargetStride, targetCapacity);
                (memory, particleBank) = NativeBattlePresentationTaskStorage.Reserve(memory, frame.Effect, inputs.Packets.Roster, ParticleStride, particleCapacity);
                frame = frame with { Memory = memory };
            }
        }
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, root.WriteDword(16, first ? 0u : 1u));
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, working + MainOffset, MainStride, 1);
            NativeBattleEffectTaskPool secondary = NativeBattleEffectTaskPool.Initialize(SecondaryDescriptor, working + SecondaryOffset, SecondaryStride, SecondaryCount);
            NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(TargetDescriptor, targetBank, TargetStride, targetCapacity);
            NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, particleBank, ParticleStride, particleCapacity);
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, secondary, targets, particles] }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [main, secondary, targets, particles]) };
            (frame, uint childAddress) = Append(frame, 1, MainCallback);
            if (childAddress == 0)
            {
                throw new NotSupportedException("Reflect Beam exhausted its single main task.");
            }
            NativeBattleModulePoolTask task = Read(frame, childAddress).WriteBytes(12, new byte[724]);
            if (expanded)
            {
                (frame, task) = CaptureExpandedSweep(frame, task, count, inputs);
                task = InitializeWorkspace(task, 0x28, 0x2B8, resources.MainAct);
                frame = Write(frame, childAddress, task);
            }
            else
            {
                frame = CaptureOriginalSweep(frame, task, childAddress, resources, inputs);
            }
        }
        int survivors = 0;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            for (int index = 1; index < 5; index++)
            {
                BattleModelOwner caster = frame.State.ReadModelOwner(CasterGlobal);
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

    private static NativeBattleReflectBeam135Frame CaptureOriginalSweep(NativeBattleReflectBeam135Frame frame, NativeBattleModulePoolTask task, uint childAddress, NativeBattleReflectBeam135Resources resources, NativeBattleReflectBeam135Inputs inputs)
    {
        BattleContactCursor firstContact = FirstContact(frame, inputs);
        int count = inputs.Packets.ReadHeader(Parameters(frame).Header).ReadContactCount();
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
            BattleModelOwner actor = inputs.Packets.ReadContact(firstContact.Advance(index)).Primary.Recipient;
            WriteModelOwner(task, 0x14 + index * 4, actor, inputs.Packets.ModelAliases);
            task = task.WriteByte(0x10 + index, (byte)index);
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
                BattleModelOwner left = ReadModelOwner(task, 0x14 + index * 4);
                BattleModelOwner right = ReadModelOwner(task, 0x14 + next * 4);
                if (Transform(inputs.ReadModel(left)).Position28.X < Transform(inputs.ReadModel(right)).Position28.X)
                {
                    byte old = task.ReadByte(0x10 + index);
                    WriteModelOwner(task, 0x14 + index * 4, right, inputs.Packets.ModelAliases);
                    WriteModelOwner(task, 0x14 + next * 4, left, inputs.Packets.ModelAliases);
                    task = task.WriteByte(0x10 + index, task.ReadByte(0x10 + next)).WriteByte(0x10 + next, old);
                }
            }
        }
        task = InitializeWorkspace(task, 0x28, 0x2B8, resources.MainAct);
        return Write(frame, childAddress, task);
    }

    private static (NativeBattleReflectBeam135Frame Frame, NativeBattleModulePoolTask Task) CaptureExpandedSweep(NativeBattleReflectBeam135Frame frame, NativeBattleModulePoolTask task, int count, NativeBattleReflectBeam135Inputs inputs)
    {
        BattleContactCursor first = FirstContact(frame, inputs);
        var recipients = new NativeBattleReflectBeam135Recipient[count];
        long x = 0;
        long y = 0;
        long z = 0;
        for (int index = 0; index < count; index++)
        {
            NativeBattleContactPacket packet = inputs.Packets.ReadContact(first.Advance(index));
            BattleModelOwner owner = packet.Primary.Recipient;
            BattleContactOccurrence occurrence = packet.Occurrence ?? throw new InvalidDataException("Reflect Beam requires a captured contact occurrence.");
            NativeBattleReflectBeam135AnchorStep anchor = inputs.ReadAnchor(frame, owner);
            frame = anchor.Frame;
            x += anchor.Anchor.X;
            y += anchor.Anchor.Y;
            z += anchor.Anchor.Z;
            recipients[index] = new(owner, occurrence, Transform(inputs.ReadModel(owner)).Position28.X, index);
        }
        for (int index = 0; index < count - 1; index++)
        {
            for (int next = index + 1; next < count; next++)
            {
                if (recipients[index].X < recipients[next].X)
                {
                    (recipients[index], recipients[next]) = (recipients[next], recipients[index]);
                }
            }
        }
        task = task.WriteContactCount(14, count).WriteTraversalPosition(0x13, 0);
        for (int index = 0; index < Math.Min(count, (int)TargetCount); index++)
        {
            int order = recipients[index].Order;
            task = order <= byte.MaxValue ? task.WriteByte(0x10 + index, (byte)order) : task.MarkPayloadUnknown(0x10 + index, 1);
            if (index != 0)
            {
                WriteModelOwner(task, 0x14 + index * 4, recipients[index].Owner, inputs.Packets.ModelAliases);
            }
        }
        task.WriteExecutionReference(0x14, 4, new BattleExecutionReference.ReflectBeam135Sweep(recipients.ToImmutableArray()), inputs.Packets.ModelAliases.EncodeAddress(recipients[0].Owner));
        return (frame, task.WriteWord(0x20, unchecked((ushort)(x / count))).WriteWord(0x22, unchecked((ushort)(y / count))).WriteWord(0x24, unchecked((ushort)(z / count))));
    }

    private static (ushort Targets, ushort Particles) Capacities(int count)
    {
        if (count < 1)
        {
            throw new InvalidDataException("Reflect Beam requires a captured target occurrence.");
        }
        return (checked((ushort)Math.Max(TargetCount, count)), checked((ushort)(ParticleCount + checked(28 * Math.Max(0, count - TargetCount)))));
    }

    private static void ValidateExpandedBanks(NativeBattleReflectBeam135Frame frame)
    {
        uint working = Working(frame);
        ValidateBank(frame.Effect.Pools[0], RootDescriptor, RootBank, RootStride, 1, 1);
        ValidateBank(frame.Effect.Pools[1], MainDescriptor, working + MainOffset, MainStride, 1, 1);
        ValidateBank(frame.Effect.Pools[2], SecondaryDescriptor, working + SecondaryOffset, SecondaryStride, SecondaryCount, SecondaryCount);
        (ushort targets, ushort particles) = Capacities(frame.Effect.Pools[1].Slots[0].ReadContactCount(14));
        NativeBattleEffectTaskPool target = frame.Effect.Pools[3];
        NativeBattleEffectTaskPool particle = frame.Effect.Pools[4];
        ValidateBank(target, TargetDescriptor, working + TargetOffset, TargetStride, TargetCount, targets);
        ValidateBank(particle, ParticleDescriptor, working, ParticleStride, ParticleCount, particles);
        if (target.BankAddress < particle.BankAddress + (ulong)ParticleStride * particles && particle.BankAddress < target.BankAddress + (ulong)TargetStride * targets)
        {
            throw new InvalidDataException("Reflect Beam target and particle banks overlap.");
        }
    }

    private static void ValidateBank(NativeBattleEffectTaskPool pool, uint descriptor, uint originalAddress, ushort stride, ushort originalCount, ushort count)
    {
        bool located = count == originalCount ? pool.BankAddress == originalAddress : NativeBattlePresentationTaskStorage.IsHostedBank(pool.BankAddress, stride, count);
        if (!located || pool.DescriptorAddress != descriptor || pool.Stride != stride || pool.Slots.Length != count)
        {
            throw new InvalidDataException("Reflect Beam lost its actual addressed task bank.");
        }
    }

    private static NativeBattleReflectBeam135Frame RetireHostedBanks(NativeBattleReflectBeam135Frame frame)
    {
        uint working = Working(frame);
        NativeBattleEffectArena arena = Arena(frame);
        var pools = frame.Effect.Pools.ToBuilder();
        Transfer(4, working, ParticleCount);
        Transfer(3, working + TargetOffset, TargetCount);
        return frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Pools = pools.ToImmutable() } };

        void Transfer(int index, uint originalAddress, ushort originalCount)
        {
            NativeBattleEffectTaskPool pool = pools[index];
            if (pool.BankAddress == originalAddress)
            {
                return;
            }
            int length = originalCount * pool.Stride;
            NativeBattleTaskBankOwner owner = pool.RequireOwner();
            NativeBattleEffectArena source = NativeBattleEffectArena.BorrowWorking(owner.Storage, owner.StorageOffset, length);
            arena = NativeBattleEffectNodeContextMemory.Copy(source, 0, arena, checked((int)(originalAddress - ArenaAddress)), length);
            pools.RemoveAt(index);
        }
    }
}
