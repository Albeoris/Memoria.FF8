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
        if (!inputs.Packets.Roster.Contains(parameters.Source))
        {
            throw new InvalidDataException("Blue Magic White Wind requires a bound caster model.");
        }
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Blue Magic White Wind requires its executed TIM initializer pointer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Blue Magic White Wind requires its texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Blue Magic White Wind requires its actual effect arena."), tim);
        frame = frame with
        {
            Memory = frame.Memory with { Textures = textures },
            State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteModelOwner(CasterGlobal, parameters.Source, inputs.Packets.ModelAliases)
        };
        frame = inputs.Execute(frame, new NativeBattleWhiteWind297BindCaster(parameters.Source, ArenaAddress + BindingOffset, 0x1056888));
        frame = inputs.Execute(frame, new NativeBattleWhiteWind297RingSequence(parameters.Source, 1));
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
        if (inputs.StorageContract == BattleContactTaskStorageContract.ExpandableArena && frame.Effect.Pools.Length == 3)
        {
            NativeBattleEffectTaskPool targets = frame.Effect.Pools[2];
            NativeBattleEffectTaskPool scene = frame.Effect.Pools[1];
            int capacity = Math.Max(3, Header(frame, inputs).ReadContactCount());
            bool validAddress = capacity == 3 ? targets.BankAddress == Working(frame) : NativeBattlePresentationTaskStorage.IsHostedBank(targets.BankAddress, 0x290, capacity);
            if (!validAddress || targets.DescriptorAddress != TargetDescriptor || targets.Stride != 0x290 || targets.Slots.Length != capacity ||
                scene.DescriptorAddress != SceneDescriptor || scene.BankAddress != Working(frame) + SceneOffset || scene.Stride != 0x33C || scene.Slots.Length != 1)
            {
                throw new InvalidDataException("Blue Magic White Wind lost its reserved target or scene task owner.");
            }
        }
        NativeBattleWhiteWind297Frame current = frame;
        NativeBattleEffectTaskStep advanced = current.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            (current, int result) = AdvanceRoot(current, slot, resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, advanced.Pool);
        if (advanced.Survivors == 0 && inputs.StorageContract == BattleContactTaskStorageContract.ExpandableArena && current.Effect.Pools.Length == 3 &&
            NativeBattlePresentationTaskStorage.IsHostedBank(current.Effect.Pools[2].BankAddress, current.Effect.Pools[2].Stride, current.Effect.Pools[2].Slots.Length))
        {
            NativeBattleEffectTaskPool targets = current.Effect.Pools[2];
            NativeBattleTaskBankOwner owner = targets.RequireOwner();
            const int prefix = 3 * 0x290;
            NativeBattleEffectArena source = NativeBattleEffectArena.BorrowWorking(owner.Storage, owner.StorageOffset, prefix);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Blue Magic White Wind requires its original target bank destination.");
            int destination = checked((int)(Working(current) - ArenaAddress));
            arena = NativeBattleEffectNodeContextMemory.Copy(source, 0, arena, destination, prefix);
            current = current with { Memory = current.Memory with { Arena = arena }, Effect = current.Effect with { Pools = current.Effect.Pools.RemoveAt(2) } };
        }
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
        uint targetBank = working;
        ushort capacity = 3;
        if (inputs.StorageContract == BattleContactTaskStorageContract.ExpandableArena && root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            capacity = checked((ushort)Math.Max(3, Header(frame, inputs).ReadContactCount()));
            if (capacity > 3)
            {
                (NativeBattleEffectMemory memory, targetBank) = NativeBattlePresentationTaskStorage.Reserve(frame.Memory, frame.Effect, inputs.Packets.Roster, 0x290, capacity);
                frame = frame with { Memory = memory };
            }
        }
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Put(frame, 0, slot, root.WriteDword(16, first ? 0u : 1u));
        root = Task(frame, 0, slot);
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Put(frame, 0, slot, root.WriteByte(15, 1));
            NativeBattleEffectTaskPool scene = NativeBattleEffectTaskPool.Initialize(SceneDescriptor, working + SceneOffset, 0x33C, 1);
            NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(TargetDescriptor, targetBank, 0x290, capacity);
            (scene, int index) = scene.Append(SceneCallback);
            scene = scene.WriteSlot(index, scene.Slots[index].WriteWord(12, 0));
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], scene, targets] } };
            frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [scene, targets]) };
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
        BattleModelOwner caster = frame.State.ReadModelOwner(CasterGlobal);
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
                int count = header.ReadContactCount();
                if (count == 0)
                {
                    throw new InvalidDataException("Blue Magic White Wind sweep requires its original nonempty captured target list.");
                }
                short z = 0;
                for (int index = 0; index < count; index++)
                {
                    BattleModelOwner actor = inputs.Packets.ReadContact(header.FirstContact.Advance(index)).Primary.Recipient;
                    z = unchecked((short)(z + (inputs.ReadModel(actor).Transform ?? throw new NotSupportedException("White Wind requires current target position.")).Position28.Z));
                }
                NativeBattleModulePoolTask task = Task(frame, pool, slot).WriteWord(0x1A0, 0).WriteWord(0x1A2, unchecked((ushort)Height(inputs, caster))).WriteWord(0x1A4, unchecked((ushort)(z / count)));
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
        bool expanded = inputs.StorageContract == BattleContactTaskStorageContract.ExpandableArena;
        if ((uint)target < 12 && (target & 3) == 0)
        {
            int start = target / 4;
            int count = captured.ReadContactCount();
            int end = expanded && age == 27 ? count : Math.Min(count, start + 1);
            for (int index = start; index < end; index++)
            {
                NativeBattleEffectTaskPool targets = frame.Effect.Pools[2];
                if (!expanded && !targets.Slots.Any(task => (task.Flags & 1) == 0))
                {
                    break;
                }
                (targets, int child) = targets.Append(TargetCallback);
                frame = WithPool(frame, 2, targets);
                NativeBattleContactPacket packet = inputs.Packets.ReadContact(captured.FirstContact.Advance(index));
                BattleModelOwner actor = packet.Primary.Recipient;
                BattleContactOccurrence occurrence = packet.Occurrence ?? throw new InvalidDataException("White Wind contact has no occurrence reference.");
                NativeBattleModulePoolTask targetTask = Task(frame, 2, child);
                targetTask.WriteExecutionReference(12, 4, new BattleExecutionReference.OrderedContactRecipients([(actor, occurrence)]), inputs.Packets.ModelAliases.EncodeAddress(actor));
                frame = Put(frame, 2, child, targetTask);
                frame = Anchor(frame, 2, child, 16, actor, inputs);
                frame = InitializeAct(frame, 2, child, 0x18, 0x278, resources.Target);
            }
        }
        short nextAge = unchecked((short)(age + 1));
        frame = Put(frame, pool, slot, Task(frame, pool, slot).WriteWord(12, unchecked((ushort)nextAge)));
        return (frame, nextAge >= 29 ? 2 : 0);
    }

    private static (NativeBattleWhiteWind297Frame Frame, int Result) AdvanceTarget(NativeBattleWhiteWind297Frame frame, int pool, int slot, NativeBattleWhiteWind297Resources resources, NativeBattleWhiteWind297Inputs inputs)
    {
        NativeBattleModulePoolTask task = Task(frame, pool, slot);
        (BattleModelOwner actor, BattleContactOccurrence captured) = task.ReadExecutionReference<BattleExecutionReference.OrderedContactRecipients>(12, 4).Recipients.Single();
        (frame, int remaining) = Play(frame, pool, slot, 0x18, 0x278, resources.Target, BillboardContext(frame, Point(task, 16), inputs), inputs);
        if (remaining != 10)
        {
            return (frame, 0);
        }
        NativeBattleActionHeaderSlot header = Header(frame, inputs);
        if (inputs.StorageContract == BattleContactTaskStorageContract.ExpandableArena)
        {
            int relative = captured.Ordinal - header.FirstContact.Ordinal;
            if ((uint)relative >= (uint)header.ReadContactCount())
            {
                throw new InvalidDataException("White Wind target occurrence left its selected header.");
            }
            if (inputs.Packets.ReadContact(captured).Primary.Recipient != actor)
            {
                throw new InvalidDataException("White Wind target occurrence lost its captured model owner.");
            }
            return (inputs.Execute(frame, new NativeBattleWhiteWind297Contact(captured)), 2);
        }
        for (int index = 0; index < header.ReadContactCount(); index++)
        {
            NativeBattleContactPacket packet = inputs.Packets.ReadContact(header.FirstContact.Advance(index));
            if (packet.Primary.Recipient == actor)
            {
                BattleContactOccurrence occurrence = packet.Occurrence ?? throw new InvalidDataException("White Wind contact has no occurrence reference.");
                frame = inputs.Execute(frame, new NativeBattleWhiteWind297Contact(occurrence));
            }
        }
        return (frame, 2);
    }

    private static NativeBattleWhiteWind297Frame Anchor(NativeBattleWhiteWind297Frame frame, int pool, int slot, int offset, BattleModelOwner actor, NativeBattleWhiteWind297Inputs inputs)
    {
        NativeBattleWhiteWind297AnchorStep result = inputs.ReadAnchor571400(frame, actor);
        NativeBattleModuleAnchor anchor = result.Anchor;
        NativeBattleModulePoolTask task = Task(result.Frame, pool, slot).WriteWord(offset, unchecked((ushort)anchor.X)).WriteWord(offset + 2, unchecked((ushort)Height(inputs, actor))).WriteWord(offset + 4, unchecked((ushort)anchor.Z)).WriteWord(offset + 6, unchecked((ushort)anchor.W));
        return Put(result.Frame, pool, slot, task);
    }
    private static NativeBattleWhiteWind297Frame InitializeAct(NativeBattleWhiteWind297Frame frame, int pool, int slot, int offset, int size, NativeBattleGfAnimationSource source)
    {
        NativeBattleModulePoolTask task = Task(frame, pool, slot);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, offset, size).Initialize7016B0(source, size);
        return Put(frame, pool, slot, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, offset, workspace));
    }
    private static (NativeBattleWhiteWind297Frame Frame, int Remaining) Play(NativeBattleWhiteWind297Frame frame, int pool, int slot, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleEffectArena context, NativeBattleWhiteWind297Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(Task(frame, pool, slot), offset, size);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            frame = Put(frame, pool, slot, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(Task(frame, pool, slot), offset, current));
            if (inputs.EmitNodePresentation is { } emitPresentation)
            {
                NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(297, Task(frame, pool, slot), node.ResourceOffset) with { ResourceGeneration = unchecked((((long)source.Address << 32) | (uint)offset) * 397 ^ (ushort)node.GeometryIndex) };
                frame = emitPresentation(frame, source, context, node, identity);
            }
            else
            {
                frame = inputs.EmitNode(frame, source, context, node);
            }
            return NativeBattleGfAnimationWorkspace.Borrow(Task(frame, pool, slot), offset, size);
        });
        frame = Put(frame, pool, slot, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(Task(frame, pool, slot), offset, advanced.Workspace));
        return (frame, advanced.Remaining);
    }
    private static NativeBattleEffectArena BillboardContext(NativeBattleWhiteWind297Frame frame, NativeBattleVector point, NativeBattleWhiteWind297Inputs inputs) => Context(frame, NativeBattleWhiteWind226.Billboard673810(inputs.ReadView(frame).ReadMatrix(), point));
    private static NativeBattleEffectArena Context(NativeBattleWhiteWind297Frame frame, NativeBattleActionMatrix matrix)
    {
        NativeBattleEffectArena context = WriteMatrix(NativeBattleEffectArena.Clear().MarkUnknown(0, 40), matrix);
        return Dword(Dword(context, 0x20, unchecked((uint)-128)), 0x24, checked(Working(frame) + MorphOffset));
    }
    private static short Height(NativeBattleWhiteWind297Inputs inputs, BattleModelOwner actor)
    {
        NativeBattleGraphicsModel model = inputs.ReadModel(actor);
        short minimum = inputs.ReadMinimumY?.Invoke(actor) ?? model.MinimumY146 ?? throw new NotSupportedException("White Wind requires the actual actor minimum posed height.");
        return NativeBattleWhiteWind226.AnchorY(minimum, (model.Transform ?? throw new NotSupportedException("White Wind requires the actual actor WORD24.")).Angle36);
    }
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
        BattleActionHeaderReference index = Parameters(frame).Header;
        return inputs.Packets.ReadHeader(index);
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
