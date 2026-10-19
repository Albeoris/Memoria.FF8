using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDoomtrain191Controller
{
    public const uint MainDescriptor = 0x24FBF80;
    public const uint MainBank = 0x24FBF60;
    public const uint ChildDescriptor = 0x24FC330;
    public const uint ChildBank = 0x24FC340;

    public static NativeBattleDoomtrain191Frame Initialize63E730(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleDoomtrain191Inputs inputs, NativeBattleDoomtrain191Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 191, Entry: 0x63E730, Initializer: 0x575560 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Doomtrain requires its actual entry, initializer and action parameters.");
        }
        NativeBattleDoomtrain191State state = memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain requires its retained module state.");
        state.Validate();
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Doomtrain requires its established571B50 arena.");
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Doomtrain requires the actual first contact pointer.");
        int actor = inputs.Packets.ReadContact(first).Primary.ActorIndex;
        int[] offsets = [0x2E000, 0x2F000, 0x30000, 0x31000, 0x32000, 0x33000];
        int[] counts = [170, 170, 150, 120, 130, 590];
        for (int pool = 0; pool < offsets.Length; pool++)
        {
            for (int index = 0; index < counts[pool]; index++)
            {
                arena = arena.Write(offsets[pool] + 24 * index, new byte[4]);
            }
        }
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Doomtrain requires its retained texture queue.");
        (textures, _) = textures.EnqueueTim505E30(resources.ModelTim);
        NativeBattleGraphicsModel target = inputs.ReadModel(actor);
        short yaw = target.Rotation14 ?? throw new NotSupportedException("Native Doomtrain requires its first target's current yaw.");
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(0, unchecked((short)(yaw + 0x800)), 0));
        NativeBattleVector center = inputs.ReadCentroid(parameters.TargetMask7A);
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(target.GeometryWorldMatrix ?? throw new NotSupportedException("Native Doomtrain requires its first target's current geometry matrix."), new(0, 0, 0));
        rotation = rotation with { X = offset.X, Y = offset.Y, Z = unchecked(center.Z + offset.Z) };
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x10, 2);
        (main, int mainSlot) = main.Append(0x6472C0);
        main = Put(main, mainSlot, main.Slots[mainSlot].WriteWord(0xC, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 0x24, 100);
        (children, int childSlot) = children.Append(0x63F2D0);
        children = Put(children, childSlot, children.Slots[childSlot].WriteWord(0xC, 0));
        state = state with { BaseMatrix = Pack(rotation, state.BaseMatrix, false), ArenaAddress = NativeBattleEffectNodeMemory.ArenaAddress, WorkPointers = offsets.Append(0x37000).Select(value => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)value)).ToImmutableArray(), Caster = parameters.Actor78, FirstTarget = actor, LookupCounter = 0 };
        return new(memory with { Arena = arena, Textures = textures, Doomtrain191 = state }, effect with { Pools = [main, children], ReturnedDescriptor = MainDescriptor }, random);
    }

    public static NativeBattleDoomtrain191Frame Advance6472C0(NativeBattleDoomtrain191Frame frame, NativeBattleDoomtrain191Inputs inputs, NativeBattleDoomtrain191Resources resources)
    {
        NativeBattleEffectTaskPool main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor);
        NativeBattleEffectTaskPool children = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ChildDescriptor);
        NativeBattleEffectTaskStep root = main.Advance((pool, slot) =>
        {
            main = pool;
            NativeBattleEffectTaskStep advanced = children.Advance((childPool, childSlot) =>
            {
                children = childPool;
                frame = Publish(frame, main, children);
                NativeBattleModulePoolTask task = children.Slots[childSlot];
                int result;
                if (task.Callback == 0x63F2D0)
                {
                    result = Timeline(childSlot, task);
                }
                else if (task.Callback == 0x63E9C0)
                {
                    result = Camera(childSlot, task);
                }
                else
                {
                    NativeBattleDoomtrain191LeafStep step = inputs.AdvanceLeaf(frame, childSlot);
                    frame = step.Frame;
                    children = frame.Effect.Pools.Single(current => current.DescriptorAddress == ChildDescriptor);
                    result = step.Result;
                }
                return new(children, result);
            });
            children = advanced.Pool;
            return new(Put(main, slot, main.Slots[slot].WriteWord(0xC, unchecked((ushort)(main.Slots[slot].ReadWord(0xC) + 1)))), advanced.Survivors == 0 ? 2 : 0);
        });
        return Publish(frame, root.Pool, children) with { Effect = frame.Effect with { Pools = [root.Pool, children], ReturnedDescriptor = root.Survivors == 0 ? 0u : MainDescriptor } };

        int Timeline(int slot, NativeBattleModulePoolTask task)
        {
            NativeBattlePackedMatrix32 camera = inputs.ReadCameraMatrix();
            NativeBattleDoomtrain191State state = State();
            NativeBattleActionMatrix view = NativeBattlePoseMatrices.Compose56C2F0(camera.ReadMatrix(), state.BaseMatrix.ReadMatrix());
            Set(state with { CameraBackup = camera, BaseView = Pack(view, state.BaseView, true) });
            uint flags = inputs.ReadGraphicsFlags();
            if ((flags & 0x201) != 0 && ((flags & 1) != 0 || inputs.PollFile() < 0))
            {
                return 0;
            }
            short tick = unchecked((short)task.ReadWord(0xC));
            if (tick == 0)
            {
                NativeBattleGuardianCameraTask started = NativeBattleGuardianCameraProgram.Begin63E960(frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Native Doomtrain requires its process-retained camera banks."), resources.CameraProgram.Data, 0);
                frame = frame with { Memory = frame.Memory with { GuardianCameraProcess = started.Process } };
                int cameraSlot = Append(0x63E9C0);
                children = Put(children, cameraSlot, WriteDword(WriteDword(WriteDword(children.Slots[cameraSlot].WriteWord(0xC, 0).WriteWord(0xE, 0), 0x14, 0xE3CA88), 0x18, 0x24FBE88), 0x1C, 0x24FC290));
                CaptureTargets();
                Execute(new NativeBattleDoomtrain191StreamStart(0xF78380));
                Execute(new NativeBattleDoomtrain191StartVibration());
            }
            switch (tick)
            {
                case 1:
                    SpawnRigs();
                    break;
                case 4:
                    Execute(new NativeBattleDoomtrain191Sound(0xE3C8A8));
                    break;
                case 5:
                    Execute(new NativeBattleDoomtrain191Load(0x22B, 0x10000));
                    break;
                case 15:
                    if (inputs.PollFile() < 0)
                    {
                        return 0;
                    }
                    Execute(new NativeBattleDoomtrain191Tim(0x10000));
                    Execute(new NativeBattleDoomtrain191Load(0x22C, 0));
                    Execute(new NativeBattleDoomtrain191Invoke(0x63F7A0));
                    break;
                case 20:
                    Execute(new NativeBattleDoomtrain191Invoke(0x640390));
                    Append(0x641AF0);
                    break;
                case 30:
                case 85:
                    if (inputs.PollFile() < 0)
                    {
                        return 0;
                    }
                    Execute(new NativeBattleDoomtrain191Load(tick == 30 ? (short)0x22D : (short)0x22E, tick == 30 ? 0x10000 : null));
                    if (tick == 85)
                    {
                        Execute(new NativeBattleDoomtrain191Invoke(0x641530));
                    }
                    break;
                case 95:
                case 178:
                case 313:
                case 355:
                    if (inputs.PollFile() < 0)
                    {
                        return 0;
                    }
                    Execute(new NativeBattleDoomtrain191StreamStart(null));
                    break;
                case 103:
                    Execute(new NativeBattleDoomtrain191Invoke(0x63F930));
                    Execute(new NativeBattleDoomtrain191Invoke(0x6418A0));
                    break;
                case 124:
                    Execute(new NativeBattleDoomtrain191StreamPlay());
                    Append(0x641EA0);
                    break;
                case 160:
                    Execute(new NativeBattleDoomtrain191Sound(0xE3C8AC));
                    break;
                case 168:
                case 293:
                    Execute(new NativeBattleDoomtrain191Load(tick == 168 ? (short)0x22F : (short)0x230, null));
                    break;
                case 294:
                    Execute(new NativeBattleDoomtrain191Sound(0xE3C8B0));
                    Execute(new NativeBattleDoomtrain191StreamPlay());
                    break;
                case 335:
                    Execute(new NativeBattleDoomtrain191StreamPlay());
                    Execute(new NativeBattleDoomtrain191Load(0x231, null));
                    break;
                case 336:
                    Execute(new NativeBattleDoomtrain191Sound(0xE3C8B4));
                    break;
                case 366:
                    Execute(new NativeBattleDoomtrain191StreamPlay());
                    break;
                case 405:
                    Execute(new NativeBattleDoomtrain191Invoke(0x63F850));
                    Execute(new NativeBattleDoomtrain191Tim(0x10000));
                    break;
                case 406:
                    Append(0x641E60);
                    break;
                case 417:
                    Execute(new NativeBattleDoomtrain191Sound(0xE3C8B8));
                    break;
                case 420:
                    Execute(new NativeBattleDoomtrain191StopVibration());
                    break;
            }
            if (tick == 420)
            {
                Execute(new NativeBattleDoomtrain191Contacts());
            }
            ushort next = unchecked((ushort)(task.ReadWord(0xC) + 1));
            children = Put(children, slot, children.Slots[slot].WriteWord(0xC, next));
            if (unchecked((short)next) <= 423)
            {
                return 0;
            }
            Execute(new NativeBattleDoomtrain191Invoke(0x63F780));
            Execute(new NativeBattleDoomtrain191Invoke(0x63F8F0));
            Execute(new NativeBattleDoomtrain191Invoke(0x5713E0));
            return 2;
        }

        int Camera(int slot, NativeBattleModulePoolTask poolTask)
        {
            NativeBattleDoomtrain191State state = State();
            NativeBattleGuardianCameraTask task = new(frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Native Doomtrain requires its retained camera banks."), resources.CameraProgram.Data, checked((int)(ReadDword(poolTask, 0x14) - 0xE3CA88)), poolTask.ReadWord(0xC), false);
            NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, state.BaseMatrix.ReadMatrix(), inputs.ReadCameraPose(), (inputs.ReadGraphicsFlags() & 0x201) != 0);
            children = Put(children, slot, WriteDword(poolTask.WriteWord(0xC, step.Task.Frame), 0x14, unchecked(0xE3CA88u + (uint)step.Task.Offset)));
            frame = frame with { Memory = frame.Memory with { GuardianCameraProcess = step.Task.Process } };
            if (step.Published)
            {
                Execute(new NativeBattleDoomtrain191CameraPublish(step));
            }
            return step.Result;
        }

        void CaptureTargets()
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C];
            int count = header.ReadByte(0x10);
            int first = count == 0 ? 0 : header.FirstContactIndex ?? throw new NotSupportedException("Native Doomtrain target capture requires its current contact pointer.");
            List<int> targets = [];
            for (int contact = 0; contact < count; contact++)
            {
                int source = inputs.Packets.ReadContact(first + contact).Primary.ActorIndex;
                if (targets.Contains(source))
                {
                    continue;
                }
                targets.Add(source);
                HashSet<int> visited = [source];
                int? linked = inputs.ReadModel(source).ReadLinkedModelIndex140();
                while (linked is int actor && actor != source)
                {
                    if (!visited.Add(actor))
                    {
                        throw new InvalidDataException("Native Doomtrain linked-model ring does not return to its source.");
                    }
                    NativeBattleGraphicsModel model = inputs.ReadModel(actor);
                    if ((model.Flags0 & 2) != 0)
                    {
                        targets.Add(actor);
                    }
                    linked = model.ReadLinkedModelIndex140() ?? throw new InvalidDataException("Native Doomtrain linked-model ring dereferenced NULL before its source.");
                }
            }
            ImmutableArray<NativeBattleDoomtrain191ActorSnapshot?> snapshots = State().ActorSnapshots;
            foreach (int actor in targets)
            {
                NativeBattleModelTransform transform = inputs.ReadModel(actor).Transform ?? throw new NotSupportedException("Native Doomtrain requires each captured actor's current position and scale.");
                snapshots = snapshots.SetItem(actor, new(RawVector(transform.Position28, transform.Vertical34), RawVector(transform.Scale148, transform.Scale154)));
            }
            Set(State() with { Targets = targets.ToImmutableArray(), ActorSnapshots = snapshots });
        }

        void SpawnRigs()
        {
            int first = Append(0x63FB40);
            children = Put(children, first, children.Slots[first].WriteWord(0xC, 0).WriteWord(0xE, 0).WriteWord(0x10, 0xC80).WriteWord(0x12, 0).WriteWord(0x14, 0xF380).WriteWord(0x16, 0x67).WriteWord(0x18, 0xFC00).WriteWord(0x1A, 1).WriteWord(0x1C, 0x1000).WriteWord(0x20, 1));
            int second = Append(0x63FB40);
            children = Put(children, second, children.Slots[second].WriteWord(0xC, 0).WriteWord(0xE, 1).WriteWord(0x10, 0xF380).WriteWord(0x12, 0).WriteWord(0x14, 0xC80).WriteWord(0x16, 0x67).WriteWord(0x18, 0x400).WriteWord(0x1A, 1).WriteWord(0x1C, 0x1000).WriteWord(0x20, 0));
        }

        int Append(uint callback)
        {
            (children, int slot) = children.Append(callback);
            return slot;
        }

        void Execute(NativeBattleDoomtrain191Operation operation)
        {
            frame = inputs.Execute(Publish(frame, main, children), operation);
            children = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ChildDescriptor);
        }

        NativeBattleDoomtrain191State State() => frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain lost its retained module owner.");
        void Set(NativeBattleDoomtrain191State value) => frame = frame with { Memory = frame.Memory with { Doomtrain191 = value } };
    }

    private static NativeBattleDoomtrain191Frame Publish(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool main, NativeBattleEffectTaskPool children) => frame with { Effect = frame.Effect with { Pools = [main, children] } };
    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool with { Slots = pool.Slots.SetItem(slot, task) };

    private static uint ReadDword(NativeBattleModulePoolTask task, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(offset - 12));

    private static NativeBattleModulePoolTask WriteDword(NativeBattleModulePoolTask task, int offset, uint value)
    {
        byte[] bytes = task.Payload.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset - 12), value);
        return task with { Payload = bytes.ToImmutableArray() };
    }

    private static ImmutableArray<byte> RawVector(NativeBattleVector vector, short fourth)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)vector.X));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), unchecked((short)vector.Y));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), unchecked((short)vector.Z));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), fourth);
        return bytes.ToImmutableArray();
    }

    private static NativeBattlePackedMatrix32 Pack(NativeBattleActionMatrix matrix, NativeBattlePackedMatrix32 previous, bool unknownPadding)
    {
        byte[] bytes = previous.Bytes.ToArray();
        bool[] known = Enumerable.Repeat(true, 32).ToArray();
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2 * index), matrix.Rotation[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), matrix.Z);
        known[18] = !unknownPadding && previous.Known[18];
        known[19] = !unknownPadding && previous.Known[19];
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
}
