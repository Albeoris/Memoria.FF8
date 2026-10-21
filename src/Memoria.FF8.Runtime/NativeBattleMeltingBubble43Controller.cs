using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleMeltingBubble43Controller
{
    public static NativeBattleMeltingBubble43Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, NativeBattleMeltingBubble43Resources resources, NativeBattleMeltingBubble43State retained)
    {
        retained.Validate();
        effect.Validate();
        packets.Validate();
        resources.Validate();
        if (effect.Binding.Effect != 43 || effect.Binding.Entry != NativeBattleMeltingBubble43Profile.Entry)
        {
            throw new InvalidDataException("Melting Bubble requires its original module binding.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Melting Bubble requires its captured parameters.");
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleMeltingBubble43Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleMeltingBubble43Profile.RootCallback, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), parameters, packets));
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == NativeBattleMeltingBubble43Profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Melting Bubble requires its executed initializer pointer.");
        NativeBattleCuragaState nodes = retained.Nodes is { } previous ? previous with { SourceArenaOffset = source, AuxiliaryCursor = checked(source + 2 * NativeBattleMeltingBubble43Profile.DrawStep), CopiedCamera = null } : new(source, null, null, checked(source + 2 * NativeBattleMeltingBubble43Profile.DrawStep), null);
        NativeBattleMeltingBubble43State state = retained with { Nodes = nodes, Counter2656220 = 0, Counter265587C = 0 };
        state.Validate();
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Melting Bubble requires its texture registration owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Melting Bubble requires its loaded arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = NativeBattleMeltingBubble43Profile.Pools[0].Descriptor }, random, state);
    }

    public static NativeBattleMeltingBubble43Frame Advance(NativeBattleMeltingBubble43Frame frame, NativeBattleMeltingBubble43Resources resources, NativeBattleMeltingBubble43Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        resources.Validate();
        NativeBattleMeltingBubble43Frame current = frame;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleEffectHierarchyStep rootStep = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(0, Root);
        return current with { Effect = current.Effect with { Pools = rootStep.Hierarchy.Pools, ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : NativeBattleMeltingBubble43Profile.Pools[0].Descriptor } };

        NativeBattleEffectHierarchyCallback Root(NativeBattleEffectHierarchy hierarchy, uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            NativeBattleCuragaState nodes = current.State.Nodes!;
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            current = current with { Memory = current.Memory with { SharedEffectView2793E58 = inputs.Camera }, State = current.State with { Nodes = nodes with { PrimaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? 0 : NativeBattleMeltingBubble43Profile.DrawStep)), SecondaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? NativeBattleMeltingBubble43Profile.DrawStep : 2 * NativeBattleMeltingBubble43Profile.DrawStep)) } } };
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
                        int geometry = checked(emitters + 0x870);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Melting Bubble requires its actual effect arena.");
                        arena = arena.Write(emitters, new byte[0x870]).Write(geometry, new byte[0x10680]);
                        current = current with { Memory = current.Memory with { Arena = arena }, State = current.State with { Nodes = nodes with { Auxiliary25F8 = emitters, Auxiliary16F80 = geometry, AuxiliaryCursor = checked(geometry + 0x10680), Counter26D76C0 = 0, Counter26D82E8 = 0, Counter26D82D8 = 0, Counter26D75C4 = 0 } } };
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 3:
                    hierarchy = hierarchy.Write(address, task.WriteByte(0x63, 1));
                    hierarchy = hierarchy.Append8DC540(1, NativeBattleMeltingBubble43Profile.TargetCallback, 88, address, Select).Hierarchy;
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
                    throw new InvalidDataException("Melting Bubble root signed phase leaves its eleven original handlers.");
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
            NativeBattleMeltingBubble43LeafStep step = hierarchy.Read(address).Callback switch
            {
                NativeBattleMeltingBubble43Profile.TargetCallback => Target(address),
                NativeBattleMeltingBubble43Profile.GenericCallback => inputs.AdvanceGeneric(current, address),
                _ => throw new NotSupportedException("Melting Bubble requires its original child callback.")
            };
            current = step.Frame;
            return new(new(current.Effect.Pools), step.Result);
        }

        NativeBattleMeltingBubble43LeafStep Target(uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(hierarchy.Read(address), Models(), inputs.ReadPoseSource);
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Melting Bubble target requires its pose.");
            NativeBattlePoseSource pose = inputs.ReadPoseSource(task.ReadByte(0x2D));
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, pose, pose.Sample(sequence.Pose ?? throw new NotSupportedException("Melting Bubble target requires its selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Melting Bubble target requires its current pose frame."), sequence.PoseFlags109));
            hierarchy = hierarchy.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    NativeBattleEffectHierarchyAppend generic = hierarchy.Append8DC540(2, NativeBattleMeltingBubble43Profile.GenericCallback, 676, address, Select);
                    NativeBattleModulePoolTask node = generic.Hierarchy.Read(generic.Address).WriteDword(0x30, 0x15C6080).WriteWord(0x298, 0).WriteWord(0x29E, 45).WriteWord(0x29C, 0);
                    hierarchy = Next(generic.Hierarchy.Write(generic.Address, node), address);
                    break;
                case 1:
                    if (Word(task, 0x24) == 60)
                    {
                        uint root = task.ReadDword(0x10);
                        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x63, 0));
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 2:
                    if (Word(task, 0x24) >= 60)
                    {
                        int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[headerIndex];
                        int first = header.FirstContactIndex ?? throw new NotSupportedException("Melting Bubble requires its selected header's actual contact pointer.");
                        int contact = checked(first + unchecked((sbyte)task.ReadByte(0x2B)));
                        if (contact is < 0 or >= NativeBattleActionPacketsState.ContactCapacity)
                        {
                            throw new InvalidDataException("Melting Bubble selected contact leaves its physical contact arena.");
                        }
                        current = inputs.Execute(With(current, hierarchy), new NativeBattleMeltingBubble43Contact(contact));
                        hierarchy = new(current.Effect.Pools);
                        task = hierarchy.Read(address);
                        hierarchy = Next(hierarchy.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))), address);
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Melting Bubble target signed phase leaves its four original handlers.");
            }
            current = With(current, hierarchy);
            task = hierarchy.Read(address);
            if (Word(task, 0x24) == 0)
            {
                current = inputs.Execute(current, new NativeBattleMeltingBubble43Sound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 }));
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

    private static NativeBattleMeltingBubble43Frame With(NativeBattleMeltingBubble43Frame frame, NativeBattleEffectHierarchy hierarchy) => frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static bool Complete(NativeBattleModulePoolTask task) => (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address) => hierarchy.Write(address, hierarchy.Read(address).WriteByte(0x29, unchecked((byte)(hierarchy.Read(address).ReadByte(0x29) + 1))));
    private static void Require(NativeBattleMeltingBubble43Frame frame)
    {
        frame.State.Validate();
        frame.Effect.Validate();
        if (frame.Effect.Binding.Effect != 43 || frame.Effect.Binding.Entry != NativeBattleMeltingBubble43Profile.Entry || frame.State.Nodes is null || frame.Effect.Parameters is null || frame.Effect.Pools.Length != 3 || frame.Effect.ReturnedDescriptor != 0 && frame.Effect.ReturnedDescriptor != NativeBattleMeltingBubble43Profile.Pools[0].Descriptor)
        {
            throw new InvalidDataException("Melting Bubble requires its executed controller state.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < 3; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = NativeBattleMeltingBubble43Profile.Pools[index];
            if (pool.DescriptorAddress != expected.Descriptor || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Melting Bubble pools contradict their physical owners.");
            }
        }
    }
}
