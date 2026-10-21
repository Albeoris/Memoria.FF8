using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect60Controller
{
    public static NativeBattleEffect60Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, NativeBattleEffect60Resources resources, NativeBattleEffect60State retained)
    {
        retained.Validate();
        effect.Validate();
        packets.Validate();
        resources.Validate();
        if (effect.Binding.Effect != 60 || effect.Binding.Entry != NativeBattleEffect60Profile.Entry)
        {
            throw new InvalidDataException("Effect60 requires its original module binding.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Effect60 requires its captured parameters.");
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleEffect60Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleEffect60Profile.RootCallback, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), parameters, packets));
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == NativeBattleEffect60Profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect60 requires its executed initializer pointer.");
        NativeBattleCuragaState nodes = retained.Nodes is { } previous ? previous with { SourceArenaOffset = source, AuxiliaryCursor = checked(source + 2 * NativeBattleEffect60Profile.DrawStep), CopiedCamera = null } : new(source, null, null, checked(source + 2 * NativeBattleEffect60Profile.DrawStep), null);
        NativeBattleEffect60State state = retained with { Nodes = nodes, Counter2607050 = 0, Counter2605DD4 = 0 };
        state.Validate();
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Effect60 requires its texture registration owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Effect60 requires its loaded arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = NativeBattleEffect60Profile.Pools[0].Descriptor }, random, state);
    }

    public static NativeBattleEffect60Frame Advance(NativeBattleEffect60Frame frame, NativeBattleEffect60Resources resources, NativeBattleEffect60Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        resources.Validate();
        NativeBattleEffect60Frame current = frame;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleEffectHierarchyStep rootStep = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(0, Root);
        return current with { Effect = current.Effect with { Pools = rootStep.Hierarchy.Pools, ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : NativeBattleEffect60Profile.Pools[0].Descriptor } };

        NativeBattleEffectHierarchyCallback Root(NativeBattleEffectHierarchy hierarchy, uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            NativeBattleCuragaState nodes = current.State.Nodes!;
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            current = current with { Memory = current.Memory with { SharedEffectView2793E58 = inputs.Camera }, State = current.State with { Nodes = nodes with { PrimaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? 0 : NativeBattleEffect60Profile.DrawStep)), SecondaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? NativeBattleEffect60Profile.DrawStep : 2 * NativeBattleEffect60Profile.DrawStep)) } } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(), inputs.ReadPoseSource);
            hierarchy = hierarchy.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                case 1:
                case 5:
                    hierarchy = Next(hierarchy, address);
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        nodes = current.State.Nodes!;
                        int emitters = nodes.AuxiliaryCursor;
                        int geometry = checked(emitters + 0x438);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect60 requires its actual effect arena.");
                        arena = arena.Write(emitters, new byte[0x438]).Write(geometry, new byte[0xD200]);
                        current = current with { Memory = current.Memory with { Arena = arena }, State = current.State with { Nodes = nodes with { Auxiliary25F8 = emitters, Auxiliary16F80 = geometry, AuxiliaryCursor = checked(geometry + 0xD200), Counter26D76C0 = 0, Counter26D82E8 = 0, Counter26D82D8 = 0, Counter26D75C4 = 0 } } };
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 3:
                    hierarchy = hierarchy.Write(address, task.WriteByte(0x63, 1));
                    hierarchy = hierarchy.Append8DC540(1, NativeBattleEffect60Profile.TargetCallback, 88, address, Select).Hierarchy;
                    hierarchy = Next(hierarchy, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        if (unchecked((sbyte)task.ReadByte(0x2A)) < Word(task, 0x58))
                        {
                            hierarchy = hierarchy.Write(address, task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1))));
                        }
                        else
                        {
                            hierarchy = Next(hierarchy, address);
                        }
                    }
                    break;
                case 6:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 7:
                    current = inputs.Execute(With(current, hierarchy), new NativeBattleEffect60TextureLoad(address, current.State.SourceArenaOffset));
                    hierarchy = Next(new(current.Effect.Pools), address);
                    break;
                case 8:
                    if (task.ReadByte(0x62) != 0)
                    {
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 9:
                    hierarchy = Next(hierarchy.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))), address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("Effect60 root signed phase leaves its eleven original handlers.");
            }
            hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x5E, 0));
            current = current with { State = current.State with { Nodes = current.State.Nodes! with { PassCount26D82EA = 0, PassCount26D82DA = 0 } } };
            int survivors = 0;
            foreach (int pool in new[] { 1, 2, 3, 5, 4, 6 })
            {
                NativeBattleEffectHierarchyStep step = hierarchy.Advance(pool, Child);
                hierarchy = step.Hierarchy;
                survivors = unchecked((ushort)(survivors + step.Survivors));
                hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x5E, (ushort)survivors));
            }
            task = hierarchy.Read(address);
            task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            hierarchy = hierarchy.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? hierarchy.Complete8DC530(address) : hierarchy, complete ? 2 : 0);
        }

        NativeBattleEffectHierarchyCallback Child(NativeBattleEffectHierarchy hierarchy, uint address)
        {
            current = With(current, hierarchy);
            NativeBattleEffect60LeafStep step = hierarchy.Read(address).Callback switch
            {
                NativeBattleEffect60Profile.TargetCallback => Target(address),
                NativeBattleEffect60Profile.DirectorCallback => Director(address),
                NativeBattleEffect60Profile.GenericPrimaryCallback or NativeBattleEffect60Profile.GenericSecondaryCallback => inputs.AdvanceGeneric(current, address),
                NativeBattleEffect60Profile.ActPrimaryCallback or NativeBattleEffect60Profile.ActSecondaryCallback => NativeBattleEffect60Act.Advance(current, address, resources, inputs, Select),
                NativeBattleEffect60Profile.DebrisEmitterCallback => NativeBattleEffect60Particles.AdvanceEmitter(current, address, inputs, Select),
                NativeBattleEffect60Profile.DebrisCallback => NativeBattleEffect60Particles.Advance(current, address, inputs),
                NativeBattleEffect60Profile.StageCallback => NativeBattleEffect60Particles.AdvanceStage(current, address, inputs),
                _ => throw new NotSupportedException("Effect60 requires its original child callback.")
            };
            current = step.Frame;
            return new(new(current.Effect.Pools), step.Result);
        }

        NativeBattleEffect60LeafStep Target(uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(hierarchy.Read(address), Models(), inputs.ReadPoseSource);
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Effect60 target requires its pose.");
            NativeBattlePoseSource pose = inputs.ReadPoseSource(task.ReadByte(0x2D));
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, pose, pose.Sample(sequence.Pose ?? throw new NotSupportedException("Effect60 target requires its selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Effect60 target requires its current pose frame."), sequence.PoseFlags109));
            hierarchy = hierarchy.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    current = current with { State = InitializeShared(current.State, task, inputs) };
                    hierarchy = hierarchy.Append8DC540(2, NativeBattleEffect60Profile.DirectorCallback, 72, address, Select).Hierarchy;
                    hierarchy = Next(hierarchy, address);
                    break;
                case 1:
                    if (current.State.Word(0x48) == 1)
                    {
                        int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        int contact = checked((inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect60 requires its actual contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        current = inputs.Execute(With(current, hierarchy), new NativeBattleEffect60Contact(contact));
                        hierarchy = Next(new(current.Effect.Pools), address);
                    }
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0 || current.State.Word(0x4C) == 1)
                    {
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 3:
                    uint root = task.ReadDword(0x10);
                    hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x63, 0));
                    hierarchy = Next(hierarchy.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))), address);
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Effect60 target signed phase leaves its five original handlers.");
            }
            return NativeBattleEffect60Act.Finish(With(current, hierarchy), address);
        }

        NativeBattleEffect60LeafStep Director(uint address)
        {
            current = current with { State = current.State.WithWord(0x46, current.State.Word(0x46) + 1) };
            if (current.State.Word(0x40) != current.State.Word(0x42))
            {
                current = current with { State = current.State.WithWord(0x40, current.State.Word(0x42)).WithWord(0x46, 0) };
                if (current.State.Word(0x40) == 1)
                {
                    current = SpawnStage(current, address, Select);
                }
            }
            if (current.State.Word(0x42) != current.State.Word(0x44))
            {
                current = current with { State = current.State.WithWord(0x42, current.State.Word(0x44)) };
            }
            NativeBattleModulePoolTask task = NativeBattleEffect60Act.Read(current, address);
            bool next = false;
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    if (Request(current.State, 0, 1, out NativeBattleEffect60State state))
                    {
                        current = current with { State = state };
                        next = true;
                    }
                    break;
                case 1:
                    if (current.State.Word(0x40) >= 1)
                    {
                        current = inputs.Execute(current, new NativeBattleEffect60Sound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 }));
                        next = true;
                    }
                    break;
                case 2:
                    next = current.State.Word(0x46) >= 4;
                    break;
                case 3:
                    if (current.State.Word(0x46) >= 36)
                    {
                        current = current with { State = current.State.WithWord(0x48, 1) };
                        next = true;
                    }
                    break;
                case 4:
                    if (Request(current.State, 1, 2, out state))
                    {
                        current = current with { State = state };
                        next = true;
                    }
                    break;
                case 5:
                    task = NativeBattleEffect60Act.Read(current, address);
                    current = NativeBattleEffect60Act.Put(current, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    next = true;
                    break;
                case 6:
                    break;
                default:
                    throw new InvalidDataException("Effect60 director signed phase leaves its seven original handlers.");
            }
            if (next)
            {
                current = NativeBattleEffect60Act.Put(current, address, NativeBattleEffect60Act.Next(NativeBattleEffect60Act.Read(current, address)));
            }
            return NativeBattleEffect60Act.Finish(current, address);
        }
        ImmutableArray<NativeBattleGraphicsModel> Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToImmutableArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
    }

    public static NativeBattleEffect60State InitializeShared(NativeBattleEffect60State retained, NativeBattleModulePoolTask task, NativeBattleEffect60Inputs inputs)
    {
        NativeBattleEffectArena actor = NativeBattleEffect60Act.RequirePacked8(inputs.ReadPackedPosition8(task.ReadByte(0x2C)));
        NativeBattleEffectArena target = NativeBattleEffect60Act.RequirePacked8(inputs.ReadPackedPosition8(task.ReadByte(0x2D)));
        NativeBattleEffectArena shared = NativeBattleEffectArena.Clear();
        shared = CopyPacked(actor, shared, 0);
        shared = CopyPacked(target, shared, 8);
        NativeBattleEffect60State state = retained with { Shared84 = shared };
        short x = unchecked((short)(state.Word(8) - state.Word(0)));
        short z = unchecked((short)(state.Word(12) - state.Word(4)));
        int angle = NativePrecision53.RoundInteger(Math.Atan2(x, z) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
        return state.WithWord(0x4A, (angle - 0x800) & 0xFFF);
    }

    public static bool Request(NativeBattleEffect60State state, short expected, short requested, out NativeBattleEffect60State updated)
    {
        bool accepted = state.Word(0x44) == expected && state.Word(0x40) == expected;
        updated = accepted ? state.WithWord(0x44, requested) : state;
        return accepted;
    }

    private static NativeBattleEffect60Frame SpawnStage(NativeBattleEffect60Frame frame, uint parent, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        foreach ((uint callback, uint source, ushort delay) in new (uint, uint, ushort)[] { (NativeBattleEffect60Profile.GenericPrimaryCallback, 0x1797CC0, 6), (NativeBattleEffect60Profile.GenericSecondaryCallback, 0x179C998, 36) })
        {
            NativeBattleEffectHierarchyAppend generic = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(3, callback, 676, parent, selectActors);
            NativeBattleModulePoolTask task = generic.Hierarchy.Read(generic.Address).WriteDword(0x30, source).WriteWord(0x298, delay).WriteWord(0x29E, 40).WriteWord(0x29C, 4);
            frame = With(frame, generic.Hierarchy.Write(generic.Address, task));
        }
        frame = NativeBattleEffect60Act.Append(frame, parent, NativeBattleEffect60Profile.ActPrimaryCallback, 0x17A0D04, 228, 0, selectActors, out _);
        NativeBattleEffectHierarchyAppend emitter = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(5, NativeBattleEffect60Profile.DebrisEmitterCallback, 140, parent, selectActors);
        frame = With(frame, emitter.Hierarchy);
        NativeBattleEffectHierarchyAppend stage = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(5, NativeBattleEffect60Profile.StageCallback, 140, parent, selectActors);
        return With(frame, stage.Hierarchy);
    }

    private static NativeBattleEffectArena CopyPacked(NativeBattleEffectArena source, NativeBattleEffectArena destination, int offset)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, 8);
        for (int index = 0; index < 8; index++)
        {
            destination = known[index] ? destination.Write(offset + index, new byte[] { bytes[index] }) : destination.MarkUnknown(offset + index, 1);
        }
        return destination;
    }
    private static NativeBattleEffect60Frame With(NativeBattleEffect60Frame frame, NativeBattleEffectHierarchy hierarchy) => frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address) => hierarchy.Write(address, NativeBattleEffect60Act.Next(hierarchy.Read(address)));
    private static void Require(NativeBattleEffect60Frame frame)
    {
        frame.State.Validate();
        frame.Effect.Validate();
        if (frame.Effect.Binding.Effect != 60 || frame.Effect.Binding.Entry != NativeBattleEffect60Profile.Entry || frame.State.Nodes is null || frame.Effect.Parameters is null || frame.Effect.Pools.Length != 7 || frame.Effect.ReturnedDescriptor != 0 && frame.Effect.ReturnedDescriptor != NativeBattleEffect60Profile.Pools[0].Descriptor)
        {
            throw new InvalidDataException("Effect60 requires its executed controller state.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < 7; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = NativeBattleEffect60Profile.Pools[index];
            if (pool.DescriptorAddress != expected.Descriptor || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect60 pools contradict their physical owners.");
            }
        }
    }
}
