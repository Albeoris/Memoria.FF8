using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleNeedlesFamilyController
{
    public static NativeBattleNeedlesFamilyFrame Initialize(NativeBattleNeedlesFamilyFrame frame, NativeBattleNeedlesFamilyResources resources, NativeBattleNeedlesFamilyInputs inputs)
    {
        Require(frame, resources);
        resources.Validate();
        NativeBattleNeedlesFamilyProfile profile = frame.State.Profile;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Needles requires its captured action parameters.");
        int contact = FirstContact(frame, inputs);
        int source = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Needles requires its executed texture initializer.");
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(profile.RootDescriptor, profile.RootBank, 16, 1);
        (root, int rootSlot) = root.Append(profile.RootCallback);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, profile.ChildBank, 36, 100);
        (children, int scheduler) = children.Append(profile.SchedulerCallback);
        children = children.WriteSlot(scheduler, children.Slots[scheduler].WriteWord(12, 0));
        NativeBattleNeedlesFamilyState state = frame.State.WriteDword(profile.BaseAddress, inputs.Packets.ReadContact(contact).Primary.ActorIndex).WriteDword(profile.ParametersGlobal, 0x1D99A78).WriteDword(profile.CasterGlobal, parameters.Actor78).WriteDword(profile.InitializerGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)source));
        for (int index = 0; index < NativeBattleNeedlesFamilyProfile.RecordCount; index++)
        {
            state = state.WriteDword(profile.NeedleBank + (uint)(index * 56), 0).WriteDword(profile.SparkBank + (uint)(index * 24), 0);
        }
        frame = frame with { State = state, Effect = frame.Effect with { Pools = [root, children], ReturnedDescriptor = profile.RootDescriptor, DrawArenaOffset = null } };
        frame = inputs.Execute(frame, new NativeBattleNeedlesFamilyRegisterCamera(resources.Camera));
        return inputs.Execute(frame, new NativeBattleNeedlesFamilyEnqueueTim(source));
    }

    public static NativeBattleNeedlesFamilyFrame Advance(NativeBattleNeedlesFamilyFrame frame, NativeBattleNeedlesFamilyResources resources, NativeBattleNeedlesFamilyInputs inputs)
    {
        Require(frame, resources);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        NativeBattleNeedlesFamilyProfile profile = frame.State.Profile;
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != profile.RootDescriptor || frame.Effect.Pools[1].DescriptorAddress != profile.ChildDescriptor)
        {
            throw new InvalidDataException("Needles requires its original root and child task pools.");
        }
        NativeBattleNeedlesFamilyFrame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            NativeBattleModulePoolTask root = pool.Slots[slot];
            if (root.Callback != profile.RootCallback)
            {
                throw new NotSupportedException("Needles reached an unsupported root callback.");
            }
            uint start = profile.PacketBase + (uint)((root.ReadWord(12) & 1) * NativeBattleNeedlesFamilyProfile.PacketStep);
            current = current with { State = current.State.WriteDword(profile.CursorGlobal, start) };
            NativeBattleNeedlesFamilyFrame scan = current;
            NativeBattleEffectTaskStep children = scan.Effect.Pools[1].Advance((owner, childSlot) =>
            {
                scan = WithPool(scan, 1, owner);
                NativeBattleModulePoolTask task = owner.Slots[childSlot];
                (scan, int result) = task.Callback == profile.SchedulerCallback ? Scheduler(scan, childSlot, resources, inputs) : task.Callback == profile.MainCallback ? Main(scan, childSlot, resources, inputs) : throw new NotSupportedException("Needles reached an unsupported child callback.");
                return new(scan.Effect.Pools[1], result);
            });
            current = WithPool(scan, 1, children.Pool);
            current = Put(current, 0, slot, current.Effect.Pools[0].Slots[slot].WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1))));
            return new(current.Effect.Pools[0], children.Survivors == 0 ? 2 : 0);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : profile.RootDescriptor } };
    }

    private static (NativeBattleNeedlesFamilyFrame Frame, int Result) Scheduler(NativeBattleNeedlesFamilyFrame frame, int slot, NativeBattleNeedlesFamilyResources resources, NativeBattleNeedlesFamilyInputs inputs)
    {
        if (Paused(frame, inputs))
        {
            return (frame, 0);
        }
        NativeBattleNeedlesFamilyProfile profile = frame.State.Profile;
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[slot];
        short age = unchecked((short)task.ReadWord(12));
        if (age == 0)
        {
            NativeBattleNeedlesFamilyPointStep anchor = inputs.ReadBone502170(frame, checked((byte)frame.State.ReadDword(profile.BaseAddress)), 0xF0, 4096);
            frame = anchor.Frame;
            task = Vector(frame.Effect.Pools[1].Slots[slot], 0x10, anchor.Position);
            frame = Put(frame, 1, slot, task);
        }
        int birth = profile.Effect == 177 ? 1 : 36;
        if (age == birth)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
            if (pool.Slots.All(candidate => (candidate.Flags & 1) != 0))
            {
                throw new NotSupportedException("Needles scheduler exhausted its original child pool.");
            }
            (pool, int child) = pool.Append(profile.MainCallback);
            NativeBattleModulePoolTask main = pool.Slots[child].WriteWord(12, 0).WriteDword(0x10, task.ReadDword(0x10)).WriteDword(0x14, task.ReadDword(0x14));
            pool = pool.WriteSlot(child, main);
            frame = WithPool(frame, 1, pool);
        }
        if (age == (profile.Effect == 177 ? 50 : 85))
        {
            frame = inputs.Execute(frame, new NativeBattleNeedlesFamilyContact(FirstContact(frame, inputs)));
        }
        if (age == birth)
        {
            frame = inputs.Execute(frame, new NativeBattleNeedlesFamilySound(resources.Sound));
        }
        short next = unchecked((short)(age + 1));
        frame = Put(frame, 1, slot, frame.Effect.Pools[1].Slots[slot].WriteWord(12, unchecked((ushort)next)));
        return (frame, next > (profile.Effect == 177 ? 55 : 90) ? 2 : 0);
    }

    private static (NativeBattleNeedlesFamilyFrame Frame, int Result) Main(NativeBattleNeedlesFamilyFrame frame, int slot, NativeBattleNeedlesFamilyResources resources, NativeBattleNeedlesFamilyInputs inputs)
    {
        NativeBattleNeedlesFamilyProfile profile = frame.State.Profile;
        (frame, uint temporary80) = Allocate(frame, 80);
        frame = Store(frame, temporary80 + 0x20, 0);
        frame = Store(frame, temporary80 + 0x24, 0);
        frame = Store(frame, temporary80 + 0x28, unchecked((uint)-4096));
        int needles = 0;
        int sparks = 0;
        short age = unchecked((short)frame.Effect.Pools[1].Slots[slot].ReadWord(12));
        if (!Paused(frame, inputs) && age is >= 0 and <= 39)
        {
            frame = Spawn(frame, temporary80, inputs);
        }
        frame = Release(frame, 80);
        (frame, uint temporary220) = Allocate(frame, 220);
        frame = Write(frame, temporary220 + 0x6C, resources.NeedleVertices.Bytes.AsSpan());
        frame = Write(frame, temporary220 + 0x8C, resources.NeedleFaces.Bytes.AsSpan());
        NativeBattlePackedMatrix32 camera = inputs.ReadCamera(frame);
        frame = CopyPacked(frame, temporary220 + 0x2C, camera);
        for (int index = 0; index < NativeBattleNeedlesFamilyProfile.RecordCount; index++)
        {
            uint address = profile.NeedleBank + (uint)(index * 56);
            if ((frame.State.Read(address, 1)[0] & 1) == 0)
            {
                continue;
            }
            frame = inputs.Execute(frame, new NativeBattleNeedlesFamilyDrawNeedle(temporary220, index));
            if (Paused(frame, inputs))
            {
                continue;
            }
            short remaining = unchecked((short)(Word(frame.State, address + 4) - 1));
            frame = frame with { State = PutWord(frame.State, address + 4, remaining) };
            if (remaining >= 0)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    uint position = address + 0x2C + (uint)(axis * 4);
                    frame = frame with { State = frame.State.WriteDword(position, unchecked(frame.State.ReadDword(position) + (uint)Word(frame.State, address + 0x10 + (uint)(axis * 2)))) };
                }
                needles++;
            }
            else
            {
                frame = frame with { State = frame.State.WriteDword(address, 0) };
                int empty = Find(frame.State, profile.SparkBank, 24);
                if (empty >= 0)
                {
                    uint spark = profile.SparkBank + (uint)(empty * 24);
                    NativeBattleNeedlesFamilyState state = PutWord(frame.State.WriteDword(spark, 1), spark + 4, 6);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        state = PutWord(state, spark + 8 + (uint)(axis * 2), unchecked((short)frame.State.ReadDword(address + 0x2C + (uint)(axis * 4))));
                    }
                    frame = frame with { State = state };
                }
            }
        }
        frame = Release(frame, 220);
        (frame, uint sprite40) = Allocate(frame, 40);
        frame = Write(frame, sprite40, resources.SparkSprite.Bytes.AsSpan());
        (frame, uint temporary180) = Allocate(frame, 180);
        frame = Store(frame, temporary180, sprite40);
        frame = StoreWord(frame, temporary180 + 0x24, 0);
        for (int index = 0; index < NativeBattleNeedlesFamilyProfile.RecordCount; index++)
        {
            uint address = profile.SparkBank + (uint)(index * 24);
            if ((frame.State.Read(address, 1)[0] & 1) == 0)
            {
                continue;
            }
            frame = StoreWord(frame, temporary180 + 4, 0);
            frame = inputs.Execute(frame, new NativeBattleNeedlesFamilyDrawSpark(sprite40, temporary180, index));
            if (Paused(frame, inputs))
            {
                continue;
            }
            short remaining = unchecked((short)(Word(frame.State, address + 4) - 1));
            if (remaining > 0)
            {
                frame = frame with { State = PutWord(frame.State, address + 4, remaining) };
                sparks++;
            }
            else
            {
                frame = frame with { State = frame.State.WriteDword(address, 0) };
            }
        }
        frame = Release(Release(frame, 180), 40);
        if (Paused(frame, inputs))
        {
            return (frame, 0);
        }
        frame = Put(frame, 1, slot, frame.Effect.Pools[1].Slots[slot].WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, (needles | sparks) == 0 ? 2 : 0);
    }

    private static NativeBattleNeedlesFamilyFrame Spawn(NativeBattleNeedlesFamilyFrame frame, uint temporary, NativeBattleNeedlesFamilyInputs inputs)
    {
        NativeBattleNeedlesFamilyProfile profile = frame.State.Profile;
        byte caster = checked((byte)frame.State.ReadDword(profile.CasterGlobal));
        byte target = checked((byte)frame.State.ReadDword(profile.BaseAddress));
        int casterBones = inputs.ReadBoneCount(caster);
        int targetBones = inputs.ReadBoneCount(target);
        if (casterBones == 0 || targetBones == 0)
        {
            throw new NotSupportedException("Needles requires both actual nonempty bone directories.");
        }
        for (int spawned = 0; spawned < 25; spawned++)
        {
            int index = Find(frame.State, profile.NeedleBank, 56);
            if (index < 0)
            {
                break;
            }
            uint record = profile.NeedleBank + (uint)(index * 56);
            frame = frame with { State = frame.State.WriteDword(record, 1) };
            frame = Random(frame, out ushort lifetime);
            short duration = (short)((lifetime % 4) + 4);
            frame = frame with { State = PutWord(frame.State, record + 4, duration) };
            frame = Random(frame, out ushort casterChoice);
            NativeBattleNeedlesFamilyPointStep origin = inputs.ReadBone502170(frame, caster, (byte)(casterChoice % casterBones), (short)(casterChoice & 4095));
            frame = WritePoint(origin.Frame, temporary, origin.Position);
            NativeBattleVector start = origin.Position;
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Random(frame, out ushort roll);
                short value = unchecked((short)(Component(start, axis) + roll % 200 - 100));
                start = SetComponent(start, axis, value);
                frame = StoreWord(frame, temporary + (uint)(axis * 2), value);
            }
            frame = Random(frame, out ushort targetChoice);
            NativeBattleNeedlesFamilyPointStep endpoint = inputs.ReadBone502170(frame, target, (byte)(targetChoice % targetBones), (short)(targetChoice & 4095));
            frame = WritePoint(endpoint.Frame, temporary + 8, endpoint.Position);
            NativeBattleVector end = endpoint.Position;
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Random(frame, out ushort roll);
                short value = unchecked((short)(Component(end, axis) + roll % 200 - 100));
                end = SetComponent(end, axis, value);
                frame = StoreWord(frame, temporary + 8 + (uint)(axis * 2), value);
            }
            int dx = end.X - start.X;
            int dy = end.Y - start.Y;
            int dz = end.Z - start.Z;
            int[] delta = [dx, dy, dz];
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Store(frame, temporary + 0x30 + (uint)(axis * 4), unchecked((uint)delta[axis]));
                frame = frame with { State = PutWord(frame.State, record + 0x10 + (uint)(axis * 2), unchecked((short)(delta[axis] / duration))) };
            }
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
            int[] normalized = [direction.X, direction.Y, direction.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Store(frame, temporary + 0x30 + (uint)(axis * 4), unchecked((uint)normalized[axis]));
            }
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
            int[] cross = [alignment.Axis.X, alignment.Axis.Y, alignment.Axis.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Store(frame, temporary + 0x40 + (uint)(axis * 4), unchecked((uint)cross[axis]));
            }
            ImmutableArray<short> rotation = NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis);
            frame = WriteRotation(frame, record + 0x18, rotation);
            frame = Random(frame, out ushort scaleRoll);
            int scale = scaleRoll % (profile.Effect == 177 ? 1024 : 1536) + (profile.Effect == 177 ? 768 : 1152);
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Store(frame, temporary + 0x10 + (uint)(axis * 4), (uint)scale);
            }
            rotation = NativeBattlePoseMatrices.Scale56BEF0(new(rotation, 0, 0, 0, 4096), scale, scale, scale).Rotation;
            frame = WriteRotation(frame, record + 0x18, rotation);
            for (int axis = 0; axis < 3; axis++)
            {
                frame = frame with { State = frame.State.WriteDword(record + 0x2C + (uint)(axis * 4), unchecked((uint)(int)Component(start, axis))) };
            }
        }
        return frame;
    }

    private static int FirstContact(NativeBattleNeedlesFamilyFrame frame, NativeBattleNeedlesFamilyInputs inputs) => inputs.Packets.Headers[(frame.Effect.Parameters ?? throw new NotSupportedException("Needles requires its captured header.")).HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Needles requires its actual current first contact.");
    private static void Require(NativeBattleNeedlesFamilyFrame frame, NativeBattleNeedlesFamilyResources resources)
    {
        NativeBattleNeedlesFamilyProfile profile = frame.State.Profile;
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != profile.Effect || frame.Effect.Binding.Entry != profile.Entry || frame.Effect.Binding.Initializer != profile.Initializer || resources.Effect != profile.Effect)
        {
            throw new InvalidDataException("Needles requires its selected original module binding and owners.");
        }
    }
    private static bool Paused(NativeBattleNeedlesFamilyFrame frame, NativeBattleNeedlesFamilyInputs inputs) => (inputs.ReadGraphicsFlags(frame) & 0x201) != 0;
    private static int Find(NativeBattleNeedlesFamilyState state, uint bank, int stride)
    {
        for (int index = 0; index < NativeBattleNeedlesFamilyProfile.RecordCount; index++)
        {
            if (state.ReadDword(bank + (uint)(index * stride)) == 0)
            {
                return index;
            }
        }
        return -1;
    }
    private static NativeBattleNeedlesFamilyFrame Random(NativeBattleNeedlesFamilyFrame frame, out ushort value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out value);
        return frame with { Random = random };
    }
    private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
    private static short Component(NativeBattleVector vector, int axis) => axis == 0 ? vector.X : axis == 1 ? vector.Y : vector.Z;
    private static NativeBattleVector SetComponent(NativeBattleVector vector, int axis, short value) => axis == 0 ? vector with { X = value } : axis == 1 ? vector with { Y = value } : vector with { Z = value };
    private static short Word(NativeBattleNeedlesFamilyState state, uint address) => BinaryPrimitives.ReadInt16LittleEndian(state.Read(address, 2));
    private static NativeBattleNeedlesFamilyState PutWord(NativeBattleNeedlesFamilyState state, uint address, short value) => state.Write(address, BitConverter.GetBytes(value));
    private static NativeBattleNeedlesFamilyFrame WriteRotation(NativeBattleNeedlesFamilyFrame frame, uint address, ImmutableArray<short> rotation)
    {
        for (int index = 0; index < 9; index++)
        {
            frame = frame with { State = PutWord(frame.State, address + (uint)(index * 2), rotation[index]) };
        }
        return frame;
    }
    private static NativeBattleNeedlesFamilyFrame WithPool(NativeBattleNeedlesFamilyFrame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleNeedlesFamilyFrame Put(NativeBattleNeedlesFamilyFrame frame, int pool, int slot, NativeBattleModulePoolTask task) => WithPool(frame, pool, frame.Effect.Pools[pool].WriteSlot(slot, task));
    private static (NativeBattleNeedlesFamilyFrame Frame, uint Address) Allocate(NativeBattleNeedlesFamilyFrame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Needles requires its actual temporary arena."), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    private static NativeBattleNeedlesFamilyFrame Release(NativeBattleNeedlesFamilyFrame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging ?? throw new NotSupportedException("Needles lost its temporary arena."), length) };
    private static NativeBattleNeedlesFamilyFrame Write(NativeBattleNeedlesFamilyFrame frame, uint address, ReadOnlySpan<byte> data) => frame with { Staging = (frame.Staging ?? throw new NotSupportedException("Needles lost its addressed memory.")).Write(address, data) };
    private static NativeBattleNeedlesFamilyFrame Store(NativeBattleNeedlesFamilyFrame frame, uint address, uint value) => Write(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleNeedlesFamilyFrame StoreWord(NativeBattleNeedlesFamilyFrame frame, uint address, short value) => Write(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleNeedlesFamilyFrame WritePoint(NativeBattleNeedlesFamilyFrame frame, uint address, NativeBattleVector value) => StoreWord(StoreWord(StoreWord(frame, address, value.X), address + 2, value.Y), address + 4, value.Z);
    private static NativeBattleNeedlesFamilyFrame CopyPacked(NativeBattleNeedlesFamilyFrame frame, uint address, NativeBattlePackedMatrix32 matrix)
    {
        matrix.Validate();
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, matrix.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!matrix.Known[index])
            {
                view = view.MarkUnknown(index, 1);
            }
        }
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Needles lost its addressed memory.");
        return frame with { Staging = NativeBattleTemporaryMemory.PublishView(staging, address, view, 32) };
    }
}
