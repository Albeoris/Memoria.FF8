using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWhiteWind297Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleWhiteWind297Controller
{
    public static NativeBattleWhiteWind297Frame Initialize(NativeBattleWhiteWind297Frame frame, NativeBattleWhiteWind297Resources resources, NativeBattleWhiteWind297Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        inputs.Packets.Validate();
        NativeBattleEffectParameters parameters = Parameters(frame);
        if (parameters.Actor78 >= 7)
        {
            throw new InvalidDataException("Blue Magic White Wind caster leaves the original seven-model bank.");
        }
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Blue Magic White Wind requires its executed TIM initializer pointer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Blue Magic White Wind requires its texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Blue Magic White Wind requires its actual effect arena."), tim);
        frame = frame with
        {
            Memory = frame.Memory with { Textures = textures },
            State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(parameters.Actor78))
        };
        frame = inputs.Execute(frame, new NativeBattleWhiteWind297BindCaster(parameters.Actor78, ArenaAddress + BindingOffset, 0x1056888));
        frame = inputs.Execute(frame, new NativeBattleWhiteWind297RingSequence(parameters.Actor78, 1));
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        textures = (frame.Memory.Textures ?? throw new NotSupportedException("Blue Magic White Wind binding lost its texture queue.")).Register5099A0(resources.Camera);
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleWhiteWind297Frame Advance(NativeBattleWhiteWind297Frame frame, NativeBattleWhiteWind297Resources resources, NativeBattleWhiteWind297Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 3) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor || frame.Effect.Pools[0].BankAddress != RootBank || frame.Effect.Pools[0].Stride != 20 || frame.Effect.Pools[0].Slots.Length != 1)
        {
            throw new InvalidDataException("Blue Magic White Wind lost its actual root and child task descriptors.");
        }
        NativeBattleWhiteWind297Frame current = frame;
        NativeBattleEffectTaskStep advanced = current.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            (current, int result) = AdvanceRoot(current, slot, resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, advanced.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = advanced.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static (NativeBattleWhiteWind297Frame Frame, int Result) AdvanceRoot(NativeBattleWhiteWind297Frame frame, int slot, NativeBattleWhiteWind297Resources resources, NativeBattleWhiteWind297Inputs inputs)
    {
        NativeBattleModulePoolTask root = Task(frame, 0, slot);
        if (root.Callback != RootCallback)
        {
            throw new NotSupportedException("Blue Magic White Wind root callback is unsupported.");
        }
        uint working = Working(frame);
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Put(frame, 0, slot, root.WriteDword(16, first ? 0u : 1u));
        root = Task(frame, 0, slot);
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Put(frame, 0, slot, root.WriteByte(15, 1));
            NativeBattleEffectTaskPool scene = NativeBattleEffectTaskPool.Initialize(SceneDescriptor, working + SceneOffset, 0x33C, 1);
            NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(TargetDescriptor, working, 0x290, 3);
            (scene, int index) = scene.Append(SceneCallback);
            scene = scene.WriteSlot(index, scene.Slots[index].WriteWord(12, 0));
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], scene, targets] } };
            frame = frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [scene, targets]) };
        }
        int survivors = 1;
        if (Task(frame, 0, slot).ReadByte(15) != 0)
        {
            NativeBattleActionMatrix normalized = NativeBattleGfMatrices.Normalize67AAE0(inputs.ReadView(frame).ReadMatrix());
            frame = frame with { State = frame.State.WriteView(NormalizedView, WriteMatrix(frame.State.ReadView(NormalizedView, 32), normalized), 32) };
            survivors = 0;
            for (int pool = 1; pool <= 2; pool++)
            {
                int selected = pool;
                NativeBattleWhiteWind297Frame current = frame;
                NativeBattleEffectTaskStep step = current.Effect.Pools[selected].Advance((owner, child) =>
                {
                    current = WithPool(current, selected, owner);
                    uint callback = Task(current, selected, child).Callback;
                    (current, int result) = callback switch
                    {
                        SceneCallback => AdvanceScene(current, selected, child, resources, inputs),
                        TargetCallback => AdvanceTarget(current, selected, child, resources, inputs),
                        _ => throw new NotSupportedException("Blue Magic White Wind selected an unsupported child callback.")
                    };
                    return new(current.Effect.Pools[selected], result);
                });
                frame = WithPool(current, selected, step.Pool);
                survivors = checked(survivors + step.Survivors);
            }
        }
        if (survivors == 0)
        {
            return (frame, 2);
        }
        root = Task(frame, 0, slot);
        return (Put(frame, 0, slot, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)))), 0);
    }

    private static (NativeBattleWhiteWind297Frame Frame, int Result) AdvanceScene(NativeBattleWhiteWind297Frame frame, int pool, int slot, NativeBattleWhiteWind297Resources resources, NativeBattleWhiteWind297Inputs inputs)
    {
        int age = unchecked((short)Task(frame, pool, slot).ReadWord(12));
        int caster = Actor(frame.State.ReadDword(CasterGlobal));
        if ((uint)age < 22)
        {
            if (age == 0)
            {
                frame = Anchor(frame, pool, slot, 16, caster, inputs);
                frame = InitializeAct(frame, pool, slot, 0x18, 0x188, resources.Caster);
            }
            NativeBattleEffectArena context = BillboardContext(frame, Point(Task(frame, pool, slot), 16), inputs);
            (frame, _) = Play(frame, pool, slot, 0x18, 0x188, resources.Caster, context, inputs);
        }
        if ((uint)(age - 7) < 22)
        {
            if (age == 7)
            {
                NativeBattleActionHeaderSlot header = Header(frame, inputs);
                int count = header.ReadByte(16);
                if (count == 0)
                {
                    throw new InvalidDataException("Blue Magic White Wind sweep requires its original nonempty captured target list.");
                }
                short z = 0;
                for (int index = 0; index < count; index++)
                {
                    int actor = inputs.Packets.ReadContact(FirstContact(header) + index).Primary.ActorIndex;
                    z = unchecked((short)(z + (inputs.ReadModel(actor).Transform ?? throw new NotSupportedException("White Wind requires current target position.")).Position28.Z));
                }
                NativeBattleModulePoolTask task = Task(frame, pool, slot).WriteWord(0x1A0, 0).WriteWord(0x1A2, unchecked((ushort)Height(inputs.ReadModel(caster)))).WriteWord(0x1A4, unchecked((ushort)(z / count)));
                frame = Put(frame, pool, slot, task);
                frame = InitializeAct(frame, pool, slot, 0x1A8, 0x194, resources.Sweep);
            }
            NativeBattleVector point = Point(Task(frame, pool, slot), 0x1A0);
            NativeBattleActionMatrix world = new([4096, 0, 0, 0, 2048, 0, 0, 0, 2048], point.X, point.Y, point.Z, 4096);
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world);
            (frame, _) = Play(frame, pool, slot, 0x1A8, 0x194, resources.Sweep, Context(frame, matrix), inputs);
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleWhiteWind297Sound(resources.Sound, 128));
        }
        int target = age - 19;
        NativeBattleActionHeaderSlot captured = Header(frame, inputs);
        if ((uint)target < 12 && (target & 3) == 0 && target / 4 < captured.ReadByte(16) && frame.Effect.Pools[2].Slots.Any(task => (task.Flags & 1) == 0))
        {
            NativeBattleEffectTaskPool targets = frame.Effect.Pools[2];
            (targets, int child) = targets.Append(TargetCallback);
            frame = WithPool(frame, 2, targets);
            int actor = inputs.Packets.ReadContact(FirstContact(captured) + target / 4).Primary.ActorIndex;
            frame = Put(frame, 2, child, Task(frame, 2, child).WriteDword(12, ModelAddress(actor)));
            frame = Anchor(frame, 2, child, 16, actor, inputs);
            frame = InitializeAct(frame, 2, child, 0x18, 0x278, resources.Target);
        }
        short nextAge = unchecked((short)(age + 1));
        frame = Put(frame, pool, slot, Task(frame, pool, slot).WriteWord(12, unchecked((ushort)nextAge)));
        return (frame, nextAge >= 29 ? 2 : 0);
    }

    private static (NativeBattleWhiteWind297Frame Frame, int Result) AdvanceTarget(NativeBattleWhiteWind297Frame frame, int pool, int slot, NativeBattleWhiteWind297Resources resources, NativeBattleWhiteWind297Inputs inputs)
    {
        NativeBattleModulePoolTask task = Task(frame, pool, slot);
        int actor = Actor(task.ReadDword(12));
        (frame, int remaining) = Play(frame, pool, slot, 0x18, 0x278, resources.Target, BillboardContext(frame, Point(task, 16), inputs), inputs);
        if (remaining != 10)
        {
            return (frame, 0);
        }
        NativeBattleActionHeaderSlot header = Header(frame, inputs);
        for (int index = 0; index < header.ReadByte(16); index++)
        {
            int selected = checked(FirstContact(header) + index);
            if (inputs.Packets.ReadContact(selected).Primary.ActorIndex == actor)
            {
                frame = inputs.Execute(frame, new NativeBattleWhiteWind297Contact(selected));
            }
        }
        return (frame, 2);
    }

    private static NativeBattleWhiteWind297Frame Anchor(NativeBattleWhiteWind297Frame frame, int pool, int slot, int offset, int actor, NativeBattleWhiteWind297Inputs inputs)
    {
        NativeBattleWhiteWind297AnchorStep result = inputs.ReadAnchor571400(frame, actor);
        NativeBattleModuleAnchor anchor = result.Anchor;
        NativeBattleModulePoolTask task = Task(result.Frame, pool, slot).WriteWord(offset, unchecked((ushort)anchor.X)).WriteWord(offset + 2, unchecked((ushort)Height(inputs.ReadModel(actor)))).WriteWord(offset + 4, unchecked((ushort)anchor.Z)).WriteWord(offset + 6, unchecked((ushort)anchor.W));
        return Put(result.Frame, pool, slot, task);
    }
    private static NativeBattleWhiteWind297Frame InitializeAct(NativeBattleWhiteWind297Frame frame, int pool, int slot, int offset, int size, NativeBattleGfAnimationSource source)
    {
        NativeBattleModulePoolTask task = Task(frame, pool, slot);
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(task.ReadBytes(offset, size).ToImmutableArray()).Initialize7016B0(source, size);
        return Put(frame, pool, slot, task.WriteBytes(offset, workspace.Data.AsSpan()));
    }
    private static (NativeBattleWhiteWind297Frame Frame, int Remaining) Play(NativeBattleWhiteWind297Frame frame, int pool, int slot, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleEffectArena context, NativeBattleWhiteWind297Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace workspace = new(Task(frame, pool, slot).ReadBytes(offset, size).ToImmutableArray());
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            frame = Put(frame, pool, slot, Task(frame, pool, slot).WriteBytes(offset, current.Data.AsSpan()));
            frame = inputs.EmitNode(frame, source, context, node);
            return new(Task(frame, pool, slot).ReadBytes(offset, size).ToImmutableArray());
        });
        frame = Put(frame, pool, slot, Task(frame, pool, slot).WriteBytes(offset, advanced.Workspace.Data.AsSpan()));
        return (frame, advanced.Remaining);
    }
    private static NativeBattleEffectArena BillboardContext(NativeBattleWhiteWind297Frame frame, NativeBattleVector point, NativeBattleWhiteWind297Inputs inputs) => Context(frame, NativeBattleWhiteWind226.Billboard673810(inputs.ReadView(frame).ReadMatrix(), point));
    private static NativeBattleEffectArena Context(NativeBattleWhiteWind297Frame frame, NativeBattleActionMatrix matrix)
    {
        NativeBattleEffectArena context = WriteMatrix(NativeBattleEffectArena.Clear().MarkUnknown(0, 40), matrix);
        return Dword(Dword(context, 0x20, unchecked((uint)-128)), 0x24, checked(Working(frame) + MorphOffset));
    }
    private static short Height(NativeBattleGraphicsModel model) => NativeBattleWhiteWind226.AnchorY(model.MinimumY146 ?? throw new NotSupportedException("White Wind requires the actual actor WORD92."), (model.Transform ?? throw new NotSupportedException("White Wind requires the actual actor WORD24.")).Angle36);
    private static NativeBattleVector Point(NativeBattleModulePoolTask task, int offset) => new(unchecked((short)task.ReadWord(offset)), unchecked((short)task.ReadWord(offset + 2)), unchecked((short)task.ReadWord(offset + 4)));
    private static NativeBattleEffectArena WriteMatrix(NativeBattleEffectArena arena, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            byte[] value = new byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(value, matrix.Rotation[index]);
            arena = arena.Write(index * 2, value);
        }
        return Dword(Dword(Dword(arena, 20, unchecked((uint)matrix.X)), 24, unchecked((uint)matrix.Y)), 28, unchecked((uint)matrix.Z));
    }
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleModulePoolTask Task(NativeBattleWhiteWind297Frame frame, int pool, int slot) => frame.Effect.Pools[pool].Slots[slot];
    private static NativeBattleWhiteWind297Frame WithPool(NativeBattleWhiteWind297Frame frame, int pool, NativeBattleEffectTaskPool value) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(pool, value) } };
    private static NativeBattleWhiteWind297Frame Put(NativeBattleWhiteWind297Frame frame, int pool, int slot, NativeBattleModulePoolTask task) => WithPool(frame, pool, frame.Effect.Pools[pool].WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, Task(frame, pool, slot).Next, task.Callback)));
    private static NativeBattleEffectParameters Parameters(NativeBattleWhiteWind297Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Blue Magic White Wind requires its captured action parameters.");
    private static NativeBattleActionHeaderSlot Header(NativeBattleWhiteWind297Frame frame, NativeBattleWhiteWind297Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length)
        {
            throw new InvalidDataException("Blue Magic White Wind captured header leaves its actual arena.");
        }
        return inputs.Packets.Headers[index];
    }
    private static int FirstContact(NativeBattleActionHeaderSlot header) => header.FirstContactIndex ?? throw new NotSupportedException("Blue Magic White Wind requires its original captured contact pointer.");
    private static uint ModelAddress(int actor)
    {
        if ((uint)actor >= 7)
        {
            throw new InvalidDataException("Blue Magic White Wind actor leaves its seven-model bank.");
        }
        return checked(0x1D972C0u + (uint)actor * 156);
    }
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Blue Magic White Wind actor pointer leaves its native model bank.");
        }
        return (int)(offset / 156);
    }
    private static uint Working(NativeBattleWhiteWind297Frame frame)
    {
        uint working = frame.State.ReadDword(WorkingGlobal);
        if (working != ArenaAddress)
        {
            throw new InvalidDataException("Blue Magic White Wind requires the actual fixed571B50 arena base.");
        }
        return working;
    }
    private static void RequireBinding(NativeBattleWhiteWind297Frame frame)
    {
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != 297 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Blue Magic White Wind requires its own original module binding.");
        }
    }
}
