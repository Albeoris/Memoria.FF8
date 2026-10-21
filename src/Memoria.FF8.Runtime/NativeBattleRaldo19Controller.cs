using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRaldo19Controller
{
    public static NativeBattleRaldo19Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, NativeBattleRaldo19Resources resources, NativeBattleRaldo19State retained)
    {
        retained.Validate();
        effect.Validate();
        packets.Validate();
        resources.Validate();
        if (effect.Binding.Effect != 19 || effect.Binding.Entry != NativeBattleRaldo19Profile.Entry)
        {
            throw new InvalidDataException("Raldo entry requires its original module binding.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Raldo entry requires its captured action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleRaldo19Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleRaldo19Profile.RootCallback, 100);
        NativeBattleModulePoolTask task = NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), parameters, packets);
        hierarchy = root.Hierarchy.Write(root.Address, task);
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == NativeBattleRaldo19Profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Raldo requires its executed initializer resource pointer.");
        if (source < 0 || source > NativeBattleEffectArena.Length - 2 * NativeBattleRaldo19Profile.DrawStep)
        {
            throw new InvalidDataException("Raldo initializer-relative banks leave their original arena.");
        }
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Raldo requires its retained texture registration owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Raldo requires its loaded effect arena."), source);
            memory = memory with { Textures = textures };
        }
        NativeBattleRaldo19State state = retained with { Nodes = new(source, null, null, checked(source + 2 * NativeBattleRaldo19Profile.DrawStep), null), Counter273048C = 0, Counter272DDDC = 0 };
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x272B9C8 }, random, state);
    }

    public static NativeBattleRaldo19Frame Advance(NativeBattleRaldo19Frame frame, NativeBattleRaldo19Resources resources, NativeBattleRaldo19Inputs inputs)
    {
        RequireFrame(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        resources.Validate();
        inputs.Camera.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleRaldo19Frame current = frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyStep main = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != NativeBattleRaldo19Profile.RootCallback)
            {
                throw new NotSupportedException("Raldo root pool contains another callback.");
            }
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            NativeBattleCuragaState nodes = current.State.Nodes!;
            NativeBattlePackedMatrix32 copiedCamera = inputs.PackedCamera ?? NativeBattlePackedMatrix32.FromMatrix(inputs.Camera, inputs.CameraPadding18);
            nodes = nodes with { CopiedCamera = copiedCamera.ReadMatrix(), PrimaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? 0 : NativeBattleRaldo19Profile.DrawStep)), SecondaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? NativeBattleRaldo19Profile.DrawStep : 2 * NativeBattleRaldo19Profile.DrawStep)) };
            current = current with { Memory = current.Memory with { SharedEffectView2793E58 = copiedCamera }, State = current.State with { Nodes = nodes } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(), inputs.ReadPoseSource);
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                case 1:
                case 5:
                case 7:
                case 8:
                    state = Next(state, address);
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        nodes = current.State.Nodes!;
                        int first = nodes.AuxiliaryCursor;
                        int second = checked(first + 0x438);
                        int end = checked(second + 0xB7C0);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Raldo requires its actual auxiliary arena.");
                        arena = arena.Write(first, new byte[0x438]).Write(second, new byte[0xB7C0]);
                        nodes = nodes with { Auxiliary25F8 = first, Auxiliary16F80 = second, AuxiliaryCursor = end, Counter26D76C0 = 0, Counter26D82E8 = 0, Counter26D82D8 = 0, Counter26D75C4 = 0 };
                        current = current with { Memory = current.Memory with { Arena = arena }, State = current.State with { Nodes = nodes } };
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = state.Append8DC540(1, NativeBattleRaldo19Profile.TargetCallback, 88, address, Select).Hierarchy;
                    state = Next(state, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        if (unchecked((sbyte)task.ReadByte(0x2A)) < Word(task, 0x58))
                        {
                            task = task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1)));
                            state = state.Write(address, task);
                        }
                        else
                        {
                            state = Next(state, address);
                        }
                    }
                    break;
                case 6:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        state = Next(state, address);
                    }
                    break;
                case 9:
                    state = state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    state = Next(state, address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("Raldo signed root phase leaves its eleven original handlers.");
            }
            state = state.Write(address, state.Read(address).WriteWord(0x5E, 0));
            current = current with { State = current.State with { Nodes = current.State.Nodes! with { PassCount26D82EA = 0, PassCount26D82DA = 0 } } };
            int survivors = 0;
            for (int pool = 1; pool < 6; pool++)
            {
                NativeBattleEffectHierarchyStep children = state.Advance(pool, Child);
                state = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
                state = state.Write(address, state.Read(address).WriteWord(0x5E, (ushort)survivors));
            }
            task = state.Read(address);
            task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = Complete(task);
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        });
        return current with { Effect = current.Effect with { Pools = main.Hierarchy.Pools, ReturnedDescriptor = main.Survivors == 0 ? 0u : 0x272B9C8u } };

        NativeBattleEffectHierarchyCallback Child(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            switch (task.Callback)
            {
                case NativeBattleRaldo19Profile.TargetCallback:
                    return Target(state, address);
                case NativeBattleRaldo19Profile.DirectorCallback:
                    return Director(state, address);
                case NativeBattleRaldo19Profile.GenericCallback:
                    if (unchecked((sbyte)task.ReadByte(0x29)) == 0 && Word(task, 0x24) >= Word(task, 0x298))
                    {
                        state = state.Write(address, PrepareMode4Caster(task, inputs.ReadModel(task.ReadByte(0x2C))));
                    }
                    return Leaf(state, new NativeBattleRaldo19GenericTask(address));
                case NativeBattleRaldo19Profile.BallCallback:
                    return Leaf(state, new NativeBattleRaldo19BallTask(address));
                case 0x8A9F10:
                case 0x8AA1D0:
                case 0x8AAA30:
                    return Leaf(state, new NativeBattleRaldo19DebrisTask(address));
                case 0x8DDC30:
                    return Leaf(state, new NativeBattleRaldo19OverlayTask(address));
                default:
                    throw new NotSupportedException("Raldo child pool contains an unsupported original callback.");
            }
        }

        NativeBattleEffectHierarchyCallback Target(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(), inputs.ReadPoseSource);
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Raldo target requires its current pose.");
            NativeBattlePoseSource source = inputs.ReadPoseSource(task.ReadByte(0x2D));
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(sequence.Pose ?? throw new NotSupportedException("Raldo target requires its selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Raldo target requires its current pose frame."), sequence.PoseFlags109));
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    current = current with { State = InitializeShared(current.State, task, inputs) };
                    state = state.Append8DC540(2, NativeBattleRaldo19Profile.DirectorCallback, 72, address, Select).Hierarchy;
                    state = Next(state, address);
                    break;
                case 1:
                    if (current.State.Word(0x48) == 1)
                    {
                        int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        int contact = checked((inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Raldo requires its actual selected contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        state = Execute(state, new NativeBattleRaldo19Contact(contact));
                        state = Next(state, address);
                    }
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0 || current.State.Word(0x4C) == 1)
                    {
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    uint root = task.ReadDword(0x10);
                    state = state.Write(root, state.Read(root).WriteByte(0x63, 0));
                    state = state.Write(address, state.Read(address).WriteByte(0x26, (byte)(state.Read(address).ReadByte(0x26) | 1)));
                    state = Next(state, address);
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Raldo signed target phase leaves its five original handlers.");
            }
            task = state.Read(address);
            task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = Complete(task);
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        }

        NativeBattleEffectHierarchyCallback Leaf(NativeBattleEffectHierarchy state, NativeBattleRaldo19Operation operation)
        {
            NativeBattleRaldo19LeafStep step = inputs.AdvanceLeaf(current with { Effect = current.Effect with { Pools = state.Pools } }, operation);
            current = step.Frame;
            return new(new(current.Effect.Pools), step.Result);
        }

        NativeBattleEffectHierarchyCallback Director(NativeBattleEffectHierarchy state, uint address)
        {
            current = current with { State = current.State.WithWord(0x46, current.State.Word(0x46) + 1) };
            if (current.State.Word(0x40) != current.State.Word(0x42))
            {
                current = current with { State = current.State.WithWord(0x40, current.State.Word(0x42)).WithWord(0x46, 0) };
                if (current.State.Word(0x40) == 1)
                {
                    NativeBattleEffectHierarchyAppend generic = state.Append8DC540(3, NativeBattleRaldo19Profile.GenericCallback, 676, address, Select);
                    if (generic.Address == 0)
                    {
                        throw new NotSupportedException("Raldo generic allocation returned null before its native field stores.");
                    }
                    state = generic.Hierarchy.Write(generic.Address, generic.Hierarchy.Read(generic.Address).WriteDword(0x30, NativeBattleRaldo19Profile.ExecutableAddress).WriteWord(0x298, 28).WriteWord(0x29C, 4).WriteWord(0x29E, 0));
                    NativeBattleEffectHierarchyAppend ball = state.Append8DC540(4, NativeBattleRaldo19Profile.BallCallback, 184, address, Select);
                    if (ball.Address == 0)
                    {
                        throw new NotSupportedException("Raldo ball allocation returned null before its original actor selection.");
                    }
                    state = ball.Hierarchy;
                    NativeBattleModelTransform caster = inputs.ReadModel(state.Read(address).ReadByte(0x2C)).Transform ?? throw new NotSupportedException("Raldo actor selection requires its actual caster position.");
                    for (byte actor = 0; actor < 7; actor++)
                    {
                        NativeBattleGraphicsModel model = inputs.ReadModel(actor);
                        if ((model.Flags0 & 2) == 0)
                        {
                            continue;
                        }
                        if ((model.ModelId4 ?? throw new NotSupportedException("Raldo actor selection requires the enabled model identity.")) != 0x70)
                        {
                            continue;
                        }
                        NativeBattleModelTransform candidate = model.Transform ?? throw new NotSupportedException("Raldo actor selection requires its physical position.");
                        if (candidate.Position28 == caster.Position28)
                        {
                            state = state.Write(ball.Address, state.Read(ball.Address).WriteByte(0x2C, actor));
                        }
                    }
                }
            }
            if (current.State.Word(0x42) != current.State.Word(0x44))
            {
                current = current with { State = current.State.WithWord(0x42, current.State.Word(0x44)) };
            }
            NativeBattleModulePoolTask task = state.Read(address);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    if (current.State.Word(0x44) == current.State.Word(0x40) && current.State.Word(0x44) == 0)
                    {
                        current = current with { State = current.State.WithWord(0x44, 1) };
                        state = Next(state, address);
                    }
                    break;
                case 1:
                    if (current.State.Word(0x40) >= 1)
                    {
                        state = Next(state, address);
                    }
                    break;
                case 2:
                    if (current.State.Word(0x46) >= 4)
                    {
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    if (current.State.Word(0x46) > 45)
                    {
                        state = Next(state, address);
                    }
                    break;
                case 4:
                    state = state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    state = Next(state, address);
                    break;
                case 5:
                    break;
                default:
                    throw new InvalidDataException("Raldo signed director phase leaves its six original handlers.");
            }
            task = state.Read(address).WriteWord(0x24, unchecked((ushort)(state.Read(address).ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = Complete(task);
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        }

        NativeBattleEffectHierarchy Execute(NativeBattleEffectHierarchy state, NativeBattleRaldo19Operation operation)
        {
            current = current with { Effect = current.Effect with { Pools = state.Pools } };
            current = inputs.Execute(current, operation);
            return new(current.Effect.Pools);
        }

        NativeBattleGraphicsModel[] Models()
        {
            return Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        }

        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target)
        {
            return NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        }
    }

    public static NativeBattleModulePoolTask PrepareMode4Caster(NativeBattleModulePoolTask task, NativeBattleGraphicsModel model)
    {
        if (task.Callback != NativeBattleRaldo19Profile.GenericCallback || unchecked((short)task.ReadWord(0x29C)) != 4 || model.Index != task.ReadByte(0x2C))
        {
            throw new InvalidDataException("Raldo generic capture requires its actual mode4 task.");
        }
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Raldo generic capture requires the actor's packed position.");
        uint xy = unchecked((ushort)transform.Position28.X) | (uint)unchecked((ushort)transform.Position28.Y) << 16;
        uint zw = unchecked((ushort)transform.Position28.Z) | (uint)unchecked((ushort)transform.Vertical34) << 16;
        return task.WriteDword(0x290, xy).WriteWord(0x292, unchecked((ushort)-768)).WriteDword(0x294, zw);
    }

    public static NativeBattleRaldo19State InitializeShared(NativeBattleRaldo19State retained, NativeBattleModulePoolTask task, NativeBattleRaldo19Inputs inputs)
    {
        NativeBattleModelTransform actor = inputs.ReadModel(task.ReadByte(0x2C)).Transform ?? throw new NotSupportedException("Raldo requires its actual caster position.");
        NativeBattleModelTransform target = inputs.ReadModel(task.ReadByte(0x2D)).Transform ?? throw new NotSupportedException("Raldo requires its actual target position.");
        NativeBattleRaldo19State state = retained with { Shared84 = new byte[84].ToImmutableArray() };
        state = state.WithWord(0, actor.Position28.X).WithWord(2, actor.Position28.Y).WithWord(4, actor.Position28.Z).WithWord(6, actor.Vertical34).WithWord(8, target.Position28.X).WithWord(10, target.Position28.Y).WithWord(12, target.Position28.Z).WithWord(14, target.Vertical34);
        short x = unchecked((short)(target.Position28.X - actor.Position28.X));
        short z = unchecked((short)(target.Position28.Z - actor.Position28.Z));
        int angle = NativePrecision53.RoundInteger(Math.Atan2(x, z) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
        return state.WithWord(0x4A, (angle - 0x800) & 0xFFF);
    }

    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        return hierarchy.Write(address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    private static bool Complete(NativeBattleModulePoolTask task)
    {
        return (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
    }

    private static short Word(NativeBattleModulePoolTask task, int offset)
    {
        return unchecked((short)task.ReadWord(offset));
    }

    private static void RequireFrame(NativeBattleRaldo19Frame frame)
    {
        frame.Effect.Validate();
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != 19 || frame.Effect.Binding.Entry != NativeBattleRaldo19Profile.Entry || frame.State.Nodes is null || frame.Effect.Parameters is null || frame.Effect.ReturnedDescriptor is not (0 or 0x272B9C8) || frame.Effect.Pools.Length != 6)
        {
            throw new InvalidDataException("Raldo requires its executed original root controller.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < 6; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = NativeBattleRaldo19Profile.Pools[index];
            if (pool.DescriptorAddress != expected.Descriptor || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Raldo hierarchy contradicts its actual physical pool owners.");
            }
        }
    }
}
