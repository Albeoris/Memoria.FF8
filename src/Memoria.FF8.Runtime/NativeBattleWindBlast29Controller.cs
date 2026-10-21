using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleWindBlast29Controller
{
    public static NativeBattleWindBlast29Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, NativeBattleWindBlast29Resources resources, NativeBattleWindBlast29State retained)
    {
        retained.Validate();
        effect.Validate();
        packets.Validate();
        resources.Validate();
        if (effect.Binding.Effect != 29 || effect.Binding.Entry != NativeBattleWindBlast29Profile.Entry)
        {
            throw new InvalidDataException("Wind Blast requires its original module binding.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Wind Blast requires its captured parameters.");
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleWindBlast29Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleWindBlast29Profile.RootCallback, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), parameters, packets));
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == NativeBattleWindBlast29Profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Wind Blast requires its executed initializer pointer.");
        NativeBattleCuragaState nodes = retained.Nodes is { } previous ? previous with { SourceArenaOffset = source, AuxiliaryCursor = checked(source + 2 * NativeBattleWindBlast29Profile.DrawStep), CopiedCamera = null } : new(source, null, null, checked(source + 2 * NativeBattleWindBlast29Profile.DrawStep), null);
        NativeBattleWindBlast29State state = retained with { Nodes = nodes, Counter26D74E8 = 0, Counter26D6B44 = 0 };
        state.Validate();
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Wind Blast requires its texture registration owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Wind Blast requires its loaded arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = NativeBattleWindBlast29Profile.Pools[0].Descriptor }, random, state);
    }

    public static NativeBattleWindBlast29Frame Advance(NativeBattleWindBlast29Frame frame, NativeBattleWindBlast29Resources resources, NativeBattleWindBlast29Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        resources.Validate();
        NativeBattleWindBlast29Frame current = frame;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleEffectHierarchyStep rootStep = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(0, Root);
        return current with { Effect = current.Effect with { Pools = rootStep.Hierarchy.Pools, ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : NativeBattleWindBlast29Profile.Pools[0].Descriptor } };

        NativeBattleEffectHierarchyCallback Root(NativeBattleEffectHierarchy hierarchy, uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            NativeBattleCuragaState nodes = current.State.Nodes!;
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            current = current with { Memory = current.Memory with { SharedEffectView2793E58 = inputs.Camera }, State = current.State with { Nodes = nodes with { PrimaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? 0 : NativeBattleWindBlast29Profile.DrawStep)), SecondaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? NativeBattleWindBlast29Profile.DrawStep : 2 * NativeBattleWindBlast29Profile.DrawStep)) } } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(), inputs.ReadPoseSource);
            hierarchy = hierarchy.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                case 1:
                case 5:
                case 7:
                case 8:
                    hierarchy = Next(hierarchy, address);
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        nodes = current.State.Nodes!;
                        int emitters = nodes.AuxiliaryCursor;
                        int geometry = checked(emitters + 0x10E0);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Wind Blast requires its actual effect arena.");
                        arena = arena.Write(emitters, new byte[0x10E0]).Write(geometry, new byte[0xD200]);
                        current = current with { Memory = current.Memory with { Arena = arena }, State = current.State with { Nodes = nodes with { Auxiliary25F8 = emitters, Auxiliary16F80 = geometry, AuxiliaryCursor = checked(geometry + 0xD200), Counter26D76C0 = 0, Counter26D82E8 = 0, Counter26D82D8 = 0, Counter26D75C4 = 0 } } };
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 3:
                    hierarchy = hierarchy.Write(address, task.WriteByte(0x63, 1));
                    hierarchy = hierarchy.Append8DC540(1, NativeBattleWindBlast29Profile.TargetCallback, 88, address, Select).Hierarchy;
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
                case 9:
                    hierarchy = Next(hierarchy.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))), address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("Wind Blast root signed phase leaves its eleven original handlers.");
            }
            hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x5E, 0));
            current = current with { State = current.State with { Nodes = current.State.Nodes! with { PassCount26D82EA = 0, PassCount26D82DA = 0 } } };
            int survivors = 0;
            for (int pool = 1; pool < 3; pool++)
            {
                NativeBattleEffectHierarchyStep step = hierarchy.Advance(pool, Child);
                hierarchy = step.Hierarchy;
                survivors = unchecked((ushort)(survivors + step.Survivors));
                hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x5E, (ushort)survivors));
            }
            task = hierarchy.Read(address);
            task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            hierarchy = hierarchy.Write(address, task);
            bool complete = Complete(task);
            return new(complete ? hierarchy.Complete8DC530(address) : hierarchy, complete ? 2 : 0);
        }

        NativeBattleEffectHierarchyCallback Child(NativeBattleEffectHierarchy hierarchy, uint address)
        {
            current = With(current, hierarchy);
            NativeBattleWindBlast29LeafStep step = hierarchy.Read(address).Callback switch
            {
                NativeBattleWindBlast29Profile.TargetCallback => Target(address),
                NativeBattleWindBlast29Profile.GenericCallback => inputs.AdvanceGeneric(current, address),
                _ => throw new NotSupportedException("Wind Blast requires its original child callback.")
            };
            current = step.Frame;
            return new(new(current.Effect.Pools), step.Result);
        }

        NativeBattleWindBlast29LeafStep Target(uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(hierarchy.Read(address), Models(), inputs.ReadPoseSource);
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Wind Blast target requires its pose.");
            NativeBattlePoseSource pose = inputs.ReadPoseSource(task.ReadByte(0x2D));
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, pose, pose.Sample(sequence.Pose ?? throw new NotSupportedException("Wind Blast target requires its selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Wind Blast target requires its current pose frame."), sequence.PoseFlags109));
            hierarchy = hierarchy.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    NativeBattleEffectHierarchyAppend generic = hierarchy.Append8DC540(2, NativeBattleWindBlast29Profile.GenericCallback, 676, address, Select);
                    NativeBattleModulePoolTask node = generic.Hierarchy.Read(generic.Address).WriteDword(0x30, 0x17D27DC).WriteWord(0x298, 12).WriteWord(0x29E, 45).WriteWord(0x29C, 1);
                    hierarchy = Next(generic.Hierarchy.Write(generic.Address, node), address);
                    break;
                case 1:
                    if (Word(task, 0x24) == 41)
                    {
                        uint root = task.ReadDword(0x10);
                        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x63, 0));
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 2:
                    if (Word(task, 0x24) >= 41)
                    {
                        int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[headerIndex];
                        int count = header.ReadByte(16);
                        if (count != 0)
                        {
                            int first = header.FirstContactIndex ?? throw new NotSupportedException("Wind Blast requires its header's actual contact pointer.");
                            if (first < 0 || first > NativeBattleActionPacketsState.ContactCapacity - count)
                            {
                                throw new InvalidDataException("Wind Blast contact fan-out exceeds its physical header arena.");
                            }
                            for (int index = 0; index < count; index++)
                            {
                                current = inputs.Execute(With(current, hierarchy), new NativeBattleWindBlast29Contact(checked(first + index)));
                                hierarchy = new(current.Effect.Pools);
                            }
                        }
                        task = hierarchy.Read(address);
                        hierarchy = Next(hierarchy.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))), address);
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Wind Blast target signed phase leaves its four original handlers.");
            }
            current = With(current, hierarchy);
            task = hierarchy.Read(address);
            if (Word(task, 0x24) == 0)
            {
                current = inputs.Execute(current, new NativeBattleWindBlast29Sound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 }));
            }
            hierarchy = new(current.Effect.Pools);
            task = hierarchy.Read(address).WriteWord(0x24, unchecked((ushort)(hierarchy.Read(address).ReadWord(0x24) + 1)));
            hierarchy = hierarchy.Write(address, task);
            bool complete = Complete(task);
            return new(With(current, complete ? hierarchy.Complete8DC530(address) : hierarchy), complete ? 2 : 0);
        }
        ImmutableArray<NativeBattleGraphicsModel> Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToImmutableArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
    }

    private static NativeBattleWindBlast29Frame With(NativeBattleWindBlast29Frame frame, NativeBattleEffectHierarchy hierarchy) => frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static bool Complete(NativeBattleModulePoolTask task) => (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address) => hierarchy.Write(address, hierarchy.Read(address).WriteByte(0x29, unchecked((byte)(hierarchy.Read(address).ReadByte(0x29) + 1))));
    private static void Require(NativeBattleWindBlast29Frame frame)
    {
        frame.State.Validate();
        frame.Effect.Validate();
        if (frame.Effect.Binding.Effect != 29 || frame.Effect.Binding.Entry != NativeBattleWindBlast29Profile.Entry || frame.State.Nodes is null || frame.Effect.Parameters is null || frame.Effect.Pools.Length != 3 || frame.Effect.ReturnedDescriptor != 0 && frame.Effect.ReturnedDescriptor != NativeBattleWindBlast29Profile.Pools[0].Descriptor)
        {
            throw new InvalidDataException("Wind Blast requires its executed controller state.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < 3; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = NativeBattleWindBlast29Profile.Pools[index];
            if (pool.DescriptorAddress != expected.Descriptor || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Wind Blast pools contradict their physical owners.");
            }
        }
    }
}
