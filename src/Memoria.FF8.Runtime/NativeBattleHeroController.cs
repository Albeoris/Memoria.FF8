using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleHeroController
{
    public static NativeBattleHeroFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleHeroResources resources, int workingArenaOffset)
    {
        effect.Validate();
        resources.Validate();
        NativeBattleHeroProfile profile = NativeBattleHeroProfile.ForEffect(effect.Binding.Effect);
        if (resources.Effect != profile.Effect || effect.Binding.Entry != profile.Entry || effect.Parameters is null)
        {
            throw new InvalidDataException("The Hero entry requires its actual module, parameters and resources.");
        }
        NativeBattleHeroState state = new(profile.Effect, workingArenaOffset, null);
        state.Validate();
        int tim = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("The Hero entry requires its executed TIM initializer pointer.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("The Hero entry requires its retained texture registration owner.");
        (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("The Hero entry requires its actual effect arena."), tim);
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(profile.MainDescriptor, profile.MainBank, 20, 1);
        (roots, int root) = roots.Append(profile.Root);
        roots = Put(roots, root, roots.Slots[root].WriteDword(12, 0).WriteDword(16, 0));
        return new(memory with { Textures = textures }, effect with { Pools = [roots], ReturnedDescriptor = profile.MainDescriptor }, random, state);
    }

    public static NativeBattleHeroFrame Advance(NativeBattleHeroFrame frame, NativeBattleHeroResources resources, NativeBattleHeroInputs inputs)
    {
        Require(frame, resources);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        inputs.Packets.Validate();
        NativeBattleHeroProfile profile = frame.Profile;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C];
        NativeBattleHeroFrame current = frame;
        NativeBattleEffectTaskStep rootStep = frame.Effect.Pools[0].Advance((rootPool, rootIndex) =>
        {
            current = WithPool(current, 0, rootPool);
            NativeBattleModulePoolTask root = rootPool.Slots[rootIndex];
            if (root.Callback != profile.Root)
            {
                throw new NotSupportedException("The Hero root queue contains another callback.");
            }
            bool previous = root.ReadDword(16) != 0;
            current = current with { State = current.State with { PacketCursor = checked(current.State.WorkingArenaOffset + profile.FirstDrawOffset + (previous ? 0 : 0x10000)) } };
            root = root.WriteDword(16, previous ? 0u : 1u);
            bool initialized = false;
            if (root.ReadWord(12) == 1 && (profile.Effect == 310 || root.ReadByte(15) == 0))
            {
                if (root.ReadByte(15) == 0)
                {
                    current = InitializeChildren(current);
                    root = root.WriteByte(15, 1);
                }
                int ordinal = profile.Effect == 309 ? 0 : root.ReadByte(14);
                if (profile.Effect == 309 || ordinal < header.ReadByte(16))
                {
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("The Hero root requires its actual first physical contact pointer.");
                    byte actor = checked((byte)inputs.Packets.ReadContact(checked(first + ordinal)).Primary.ActorIndex);
                    current = AppendChild(current, resources, inputs, actor);
                    initialized = true;
                    if (profile.Effect == 310)
                    {
                        root = root.WriteByte(14, unchecked((byte)(ordinal + 1)));
                    }
                }
            }
            current = WithPool(current, 0, Put(current.Effect.Pools[0], rootIndex, root));
            int survivors = 0;
            if (root.ReadByte(15) != 0)
            {
                NativeBattleEffectTaskStep children = current.Effect.Pools[1].Advance((childPool, childIndex) =>
                {
                    current = WithPool(current, 1, childPool);
                    current = AdvanceChild(current, resources, inputs, childIndex, initialized);
                    short age = unchecked((short)current.Effect.Pools[1].Slots[childIndex].ReadWord(12));
                    return new(current.Effect.Pools[1], age >= 40 ? 2 : 0);
                });
                current = WithPool(current, 1, children.Pool);
                survivors = children.Survivors;
            }
            if (root.ReadByte(15) != 0 && survivors == 0)
            {
                return new(current.Effect.Pools[0], 2);
            }
            root = current.Effect.Pools[0].Slots[rootIndex];
            ushort next = unchecked((ushort)(root.ReadWord(12) + 1));
            if (profile.Effect == 310 && unchecked((short)next) > 4)
            {
                next = 0;
            }
            return new(Put(current.Effect.Pools[0], rootIndex, root.WriteWord(12, next)), 0);
        });
        current = WithPool(current, 0, rootStep.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = rootStep.Survivors == 0 ? 0 : profile.MainDescriptor } };
    }

    public static short Radius(short modelScale)
    {
        return unchecked((short)((modelScale >> 1) + 0x400));
    }

    public static short AnchorY(short modelAngle, short modelHeight)
    {
        return unchecked((short)(modelAngle + modelHeight / 8 + modelHeight / 2));
    }

    public static (NativeBattleModulePoolTask Task, NativeCrtRandomState Random) InitializeSpokes(NativeBattleModulePoolTask task, NativeCrtRandomState random, int first)
    {
        for (int index = 0; index < 16; index++)
        {
            int row = first + index * 16;
            task = task.WriteWord(row, 0).WriteWord(row + 8, 0);
            random = random.Draw(out ushort yaw);
            task = task.WriteWord(row + 2, unchecked((ushort)((yaw & 0x7FF) - 0x400))).WriteWord(row + 4, unchecked((ushort)(index << 8)));
            random = random.Draw(out ushort firstVelocity);
            task = task.WriteWord(row + 10, unchecked((ushort)((firstVelocity & 0x3F) - 0x20)));
            random = random.Draw(out ushort secondVelocity);
            task = task.WriteWord(row + 12, unchecked((ushort)((secondVelocity & 0x3F) - 0x20)));
        }
        return (task, random);
    }

    private static NativeBattleHeroFrame InitializeChildren(NativeBattleHeroFrame frame)
    {
        NativeBattleHeroProfile profile = frame.Profile;
        uint bank = checked(0x20DFAB8u + (uint)frame.State.WorkingArenaOffset);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, bank, profile.ChildStride, profile.ChildCapacity);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("The Hero child bank requires its actual arena storage.");
        for (int index = 0; index < children.Slots.Length; index++)
        {
            int offset = checked(frame.State.WorkingArenaOffset + index * profile.ChildStride + 12);
            (byte[] data, bool[] known) = arena.ReadSnapshot(offset, profile.ChildStride - 12);
            NativeBattleModulePoolTask task = children.Slots[index].WriteWithKnownness(12, data, known);
            foreach ((int address, NativeBattleResourcePointer? pointer) in arena.EnumerateResourcePointers())
            {
                if (pointer is not null && address >= offset && address <= offset + data.Length - 4)
                {
                    task = task.WriteResourcePointer(address - offset + 12, pointer);
                }
            }
            children = Put(children, index, task);
        }
        NativeBattleOriginalEffectState effect = frame.Effect with { Pools = [frame.Effect.Pools[0], children] };
        return frame with { Effect = effect, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [children]) };
    }

    private static NativeBattleHeroFrame AppendChild(NativeBattleHeroFrame frame, NativeBattleHeroResources resources, NativeBattleHeroInputs inputs, byte actor)
    {
        NativeBattleHeroProfile profile = frame.Profile;
        NativeBattleGraphicsModel model = inputs.ReadModel(actor);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("The Hero child requires its actual model scale and angle WORDs.");
        (NativeBattleEffectTaskPool pool, int slot) = frame.Effect.Pools[1].Append(profile.Child);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)Radius(transform.Scale38)));
        if (profile.Effect == 310)
        {
            task = task.WriteDword(16, checked(0x1D972C0u + 156u * actor));
        }
        frame = WithPool(frame, 1, Put(pool, slot, task));
        if (inputs.ObserveAnchor571400 is { } observe)
        {
            frame = observe(frame, actor);
        }
        NativeBattleModuleAnchor anchor = inputs.ReadAnchor571400(actor);
        short height = model.MinimumY146 ?? throw new NotSupportedException("The Hero child requires the actual model WORD92.");
        anchor = anchor with { Y = AnchorY(transform.Angle36, height) };
        pool = frame.Effect.Pools[1];
        task = pool.Slots[slot];
        task = WriteAnchor(task, profile.AnchorOffset, anchor);
        task = InitializeWorkspace(task, profile.MainOffset, 0x13C, resources.Main);
        task = InitializeWorkspace(task, profile.OrbitOffset, 0xBC, resources.Orbit);
        task = InitializeWorkspace(task, profile.OrbitOffset + 0xBC, 0xBC, resources.Orbit);
        (task, NativeCrtRandomState random) = InitializeSpokes(task, frame.Random, profile.SpokeOffset);
        return WithPool(frame with { Random = random }, 1, Put(pool, slot, task));
    }

    private static NativeBattleHeroFrame AdvanceChild(NativeBattleHeroFrame frame, NativeBattleHeroResources resources, NativeBattleHeroInputs inputs, int slot, bool initialized)
    {
        NativeBattleHeroProfile profile = frame.Profile;
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[slot];
        if (task.Callback != profile.Child)
        {
            throw new NotSupportedException("The Hero child queue contains another callback.");
        }
        short age = unchecked((short)task.ReadWord(12));
        int radius = unchecked((short)task.ReadWord(14));
        NativeBattleModuleAnchor anchor = ReadAnchor(task, profile.AnchorOffset);
        NativeBattleHeroFrame current = frame;
        int scratch = checked(frame.State.WorkingArenaOffset + profile.ScratchOffset);
        if (unchecked((uint)(int)age) < 18)
        {
            int progress = (age << 12) / 18;
            int angle = progress / 2;
            int shrinking = unchecked((4096 - progress) * radius) / 4096;
            for (int index = 0; index < 2; index++)
            {
                short x = unchecked((short)(anchor.X + unchecked(NativeBattleCameraMath.Sine(angle) * shrinking) / 4096));
                short z = unchecked((short)(anchor.Z + unchecked(NativeBattleCameraMath.Cosine(angle) * shrinking) / 4096));
                NativeBattleHeroDrawContext context = new(new(x, anchor.Y, z), null, radius, radius, radius, null, 0, scratch);
                current = AdvanceWorkspace(current, inputs, resources.Orbit, slot, profile.OrbitOffset + index * 0xBC, 0xBC, context, initialized);
                angle += 0x800;
            }
        }
        if (unchecked((ushort)age) < 40)
        {
            NativeBattleHeroDrawContext context = new(new(anchor.X, anchor.Y, anchor.Z), anchor.W, radius, radius, radius, null, -(radius / 8), scratch);
            current = AdvanceWorkspace(current, inputs, resources.Main, slot, profile.MainOffset, 0x13C, context, initialized);
        }
        int fanAge = age - 16;
        if (unchecked((uint)fanAge) < 20)
        {
            int fade = fanAge <= 5 ? 4096 - (fanAge << 12) / 5 : ((fanAge - 5) << 12) / 15;
            current = Execute(current, inputs, new NativeBattleHeroFanBegin(136));
            for (int index = 0; index < 16; index++)
            {
                task = current.Effect.Pools[1].Slots[slot];
                int row = profile.SpokeOffset + index * 16;
                NativeBattleVector angles = new(unchecked((short)task.ReadWord(row)), unchecked((short)task.ReadWord(row + 2)), unchecked((short)task.ReadWord(row + 4)));
                current = Execute(current, inputs, new NativeBattleHeroFan(angles, anchor, radius, fade, -(radius / 8), profile.FanSource, index));
                task = current.Effect.Pools[1].Slots[slot];
                for (int axis = 0; axis < 3; axis++)
                {
                    int offset = row + axis * 2;
                    task = task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + task.ReadWord(row + 8 + axis * 2))));
                }
                current = WithPool(current, 1, Put(current.Effect.Pools[1], slot, task));
            }
            current = Execute(current, inputs, new NativeBattleHeroFanEnd(136));
        }
        if (age == 1)
        {
            current = Execute(current, inputs, new NativeBattleHeroSound(resources.Sound, 0, 128));
        }
        if (age == 32)
        {
            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[current.Effect.Parameters!.HeaderIndex7C];
            int first = header.FirstContactIndex ?? throw new NotSupportedException("The Hero contact callback requires its actual physical contact pointer.");
            if (profile.Effect == 309)
            {
                current = Execute(current, inputs, new NativeBattleHeroContact(first));
            }
            else
            {
                uint pointer = current.Effect.Pools[1].Slots[slot].ReadDword(16);
                int actor = unchecked((int)(pointer - 0x1D972C0)) / 156;
                for (int index = 0; index < header.ReadByte(16); index++)
                {
                    if (inputs.Packets.ReadContact(first + index).Primary.ActorIndex == actor)
                    {
                        current = Execute(current, inputs, new NativeBattleHeroContact(first + index));
                    }
                }
            }
        }
        task = current.Effect.Pools[1].Slots[slot];
        return WithPool(current, 1, Put(current.Effect.Pools[1], slot, task.WriteWord(12, unchecked((ushort)(age + 1)))));
    }

    private static NativeBattleHeroFrame AdvanceWorkspace(NativeBattleHeroFrame frame, NativeBattleHeroInputs inputs, NativeBattleGfAnimationSource source, int slot, int offset, int length, NativeBattleHeroDrawContext context, bool initialized)
    {
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[slot];
        NativeBattleGfAnimationWorkspace workspace = new(task.ReadBytes(offset, length).ToImmutableArray());
        NativeBattleHeroFrame current = frame;
        NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (state, node) =>
        {
            NativeBattleHeroDrawContext selected = NativeBattleHeroGeometry.IsHidden(node) ? context : context with { ScaleEnabled = inputs.Stack.RequireScale(initialized) };
            current = Execute(current, inputs, new NativeBattleHeroAct(source, node, selected));
            return state;
        });
        task = current.Effect.Pools[1].Slots[slot].WriteBytes(offset, step.Workspace.Data.AsSpan());
        return WithPool(current, 1, Put(current.Effect.Pools[1], slot, task));
    }

    private static NativeBattleModulePoolTask InitializeWorkspace(NativeBattleModulePoolTask task, int offset, int length, NativeBattleGfAnimationSource source)
    {
        NativeBattleGfAnimationWorkspace workspace = new(ImmutableArray.CreateRange(new byte[length]));
        return task.WriteBytes(offset, workspace.Initialize7016B0(source, length).Data.AsSpan());
    }

    private static NativeBattleHeroFrame Execute(NativeBattleHeroFrame frame, NativeBattleHeroInputs inputs, NativeBattleHeroOperation operation)
    {
        NativeBattleHeroFrame result = inputs.Execute(frame, operation);
        if (result.State.Effect != frame.State.Effect || result.Effect.Binding != frame.Effect.Binding || result.State.WorkingArenaOffset != frame.State.WorkingArenaOffset)
        {
            throw new InvalidDataException("The Hero operation changed its native module or working owner.");
        }
        return result;
    }

    private static NativeBattleModulePoolTask WriteAnchor(NativeBattleModulePoolTask task, int offset, NativeBattleModuleAnchor anchor)
    {
        return task.WriteWord(offset, unchecked((ushort)anchor.X)).WriteWord(offset + 2, unchecked((ushort)anchor.Y)).WriteWord(offset + 4, unchecked((ushort)anchor.Z)).WriteWord(offset + 6, unchecked((ushort)anchor.W));
    }

    private static NativeBattleModuleAnchor ReadAnchor(NativeBattleModulePoolTask task, int offset)
    {
        return new(unchecked((short)task.ReadWord(offset)), unchecked((short)task.ReadWord(offset + 2)), unchecked((short)task.ReadWord(offset + 4)), unchecked((short)task.ReadWord(offset + 6)));
    }

    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int index, NativeBattleModulePoolTask task) => pool.WriteSlot(index, task);

    private static NativeBattleHeroFrame WithPool(NativeBattleHeroFrame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };

    private static void Require(NativeBattleHeroFrame frame, NativeBattleHeroResources resources)
    {
        frame.State.Validate();
        resources.Validate();
        if (frame.Effect.Binding.Effect != frame.State.Effect || frame.Effect.Binding.Entry != frame.Profile.Entry || resources.Effect != frame.State.Effect || frame.Effect.Parameters is null || frame.Effect.Pools.IsDefaultOrEmpty || frame.Effect.Pools[0].DescriptorAddress != frame.Profile.MainDescriptor)
        {
            throw new InvalidDataException("The Hero callback lost its actual module, parameter or task owner.");
        }
    }
}
