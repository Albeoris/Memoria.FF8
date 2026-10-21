using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePsychoBlast23Controller
{
    public static NativeBattlePsychoBlast23Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, NativeBattlePsychoBlast23Resources resources, NativeBattlePsychoBlast23State retained)
    {
        retained.Validate();
        effect.Validate();
        packets.Validate();
        resources.Validate();
        if (effect.Binding.Effect != 23 || effect.Binding.Entry != NativeBattlePsychoBlast23Profile.Entry)
        {
            throw new InvalidDataException("Psycho Blast requires its original module binding.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Psycho Blast requires its captured parameters.");
        NativeBattleEffectHierarchy hierarchy = new(NativeBattlePsychoBlast23Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattlePsychoBlast23Profile.RootCallback, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), parameters, packets));
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == NativeBattlePsychoBlast23Profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Psycho Blast requires its executed initializer pointer.");
        NativeBattleCuragaState nodes = retained.Nodes is { } previous ? previous with { SourceArenaOffset = source, AuxiliaryCursor = checked(source + 2 * NativeBattlePsychoBlast23Profile.DrawStep), CopiedCamera = null } : new(source, null, null, checked(source + 2 * NativeBattlePsychoBlast23Profile.DrawStep), null);
        NativeBattlePsychoBlast23State state = retained with { Nodes = nodes, Counter27044D0 = 0, Counter2703654 = 0 };
        state.Validate();
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Psycho Blast requires its texture registration owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Psycho Blast requires its loaded arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = NativeBattlePsychoBlast23Profile.Pools[0].Descriptor }, random, state);
    }

    public static NativeBattlePsychoBlast23Frame Advance(NativeBattlePsychoBlast23Frame frame, NativeBattlePsychoBlast23Resources resources, NativeBattlePsychoBlast23Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        resources.Validate();
        NativeBattlePsychoBlast23Frame current = frame;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleEffectHierarchyStep rootStep = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(0, Root);
        return current with { Effect = current.Effect with { Pools = rootStep.Hierarchy.Pools, ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : NativeBattlePsychoBlast23Profile.Pools[0].Descriptor } };

        NativeBattleEffectHierarchyCallback Root(NativeBattleEffectHierarchy hierarchy, uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            NativeBattleCuragaState nodes = current.State.Nodes!;
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            current = current with { Memory = current.Memory with { SharedEffectView2793E58 = inputs.Camera }, State = current.State with { Nodes = nodes with { PrimaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? 0 : NativeBattlePsychoBlast23Profile.DrawStep)), SecondaryDrawCursor = checked(nodes.SourceArenaOffset + (odd ? NativeBattlePsychoBlast23Profile.DrawStep : 2 * NativeBattlePsychoBlast23Profile.DrawStep)) } } };
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
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Psycho Blast requires its actual effect arena.");
                        arena = arena.Write(emitters, new byte[0x438]).Write(geometry, new byte[0xD200]);
                        current = current with { Memory = current.Memory with { Arena = arena }, State = current.State with { Nodes = nodes with { Auxiliary25F8 = emitters, Auxiliary16F80 = geometry, AuxiliaryCursor = checked(geometry + 0xD200), Counter26D76C0 = 0, Counter26D82E8 = 0, Counter26D82D8 = 0, Counter26D75C4 = 0 } } };
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 3:
                    hierarchy = hierarchy.Write(address, task.WriteByte(0x63, 1));
                    hierarchy = hierarchy.Append8DC540(1, NativeBattlePsychoBlast23Profile.TargetCallback, 88, address, Select).Hierarchy;
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
                    current = inputs.Execute(With(current, hierarchy), new NativeBattlePsychoBlast23TextureLoad(address, current.State.SourceArenaOffset));
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
                    throw new InvalidDataException("Psycho Blast root signed phase leaves its eleven original handlers.");
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
            NativeBattlePsychoBlast23LeafStep step = hierarchy.Read(address).Callback switch
            {
                NativeBattlePsychoBlast23Profile.TargetCallback => Target(address),
                NativeBattlePsychoBlast23Profile.DirectorCallback => Director(address),
                NativeBattlePsychoBlast23Profile.GenericCallback => inputs.AdvanceGeneric(current, address),
                NativeBattlePsychoBlast23Profile.ActPrimaryCallback or NativeBattlePsychoBlast23Profile.ActDelayedCallback or NativeBattlePsychoBlast23Profile.ActFollowupCallback => NativeBattlePsychoBlast23Act.Advance(current, address, resources, inputs, Select),
                NativeBattlePsychoBlast23Profile.DebrisEmitterCallback => NativeBattlePsychoBlast23Particles.AdvanceEmitter(current, address, inputs, Select),
                NativeBattlePsychoBlast23Profile.DebrisCallback => NativeBattlePsychoBlast23Particles.Advance(current, address, inputs),
                0x8DDC30 => inputs.AdvanceOverlay(current, address),
                _ => throw new NotSupportedException("Psycho Blast requires its original child callback.")
            };
            current = step.Frame;
            return new(new(current.Effect.Pools), step.Result);
        }

        NativeBattlePsychoBlast23LeafStep Target(uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(hierarchy.Read(address), Models(), inputs.ReadPoseSource);
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Psycho Blast target requires its pose.");
            NativeBattlePoseSource pose = inputs.ReadPoseSource(task.ReadByte(0x2D));
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, pose, pose.Sample(sequence.Pose ?? throw new NotSupportedException("Psycho Blast target requires its selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Psycho Blast target requires its current pose frame."), sequence.PoseFlags109));
            hierarchy = hierarchy.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    current = current with { State = InitializeShared(current.State, task, inputs) };
                    hierarchy = hierarchy.Append8DC540(2, NativeBattlePsychoBlast23Profile.DirectorCallback, 72, address, Select).Hierarchy;
                    hierarchy = Next(hierarchy, address);
                    break;
                case 1:
                    if (current.State.Word(0x48) == 1)
                    {
                        int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        int contact = checked((inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Psycho Blast requires its actual contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        current = inputs.Execute(With(current, hierarchy), new NativeBattlePsychoBlast23Contact(contact));
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
                    throw new InvalidDataException("Psycho Blast target signed phase leaves its five original handlers.");
            }
            return NativeBattlePsychoBlast23Act.Finish(With(current, hierarchy), address);
        }

        NativeBattlePsychoBlast23LeafStep Director(uint address)
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
            NativeBattleModulePoolTask task = NativeBattlePsychoBlast23Act.Read(current, address);
            bool next = false;
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    if (Request(current.State, 0, 1, out NativeBattlePsychoBlast23State state))
                    {
                        current = InitializeBasis(current with { State = state }, task, inputs);
                        next = true;
                    }
                    break;
                case 1:
                    next = current.State.Word(0x40) >= 1;
                    break;
                case 2:
                    if (current.State.Word(0x46) >= 4)
                    {
                        current = inputs.Execute(current, new NativeBattlePsychoBlast23Sound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 }));
                        next = true;
                    }
                    break;
                case 3:
                    if (Request(current.State, 1, 2, out state))
                    {
                        current = current with { State = state };
                        next = true;
                    }
                    break;
                case 4:
                    task = NativeBattlePsychoBlast23Act.Read(current, address);
                    current = NativeBattlePsychoBlast23Act.Put(current, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    next = true;
                    break;
                case 5:
                    break;
                default:
                    throw new InvalidDataException("Psycho Blast director signed phase leaves its six original handlers.");
            }
            if (next)
            {
                current = NativeBattlePsychoBlast23Act.Put(current, address, NativeBattlePsychoBlast23Act.Next(NativeBattlePsychoBlast23Act.Read(current, address)));
            }
            return NativeBattlePsychoBlast23Act.Finish(current, address);
        }
        ImmutableArray<NativeBattleGraphicsModel> Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToImmutableArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
    }

    public static NativeBattlePsychoBlast23State InitializeShared(NativeBattlePsychoBlast23State retained, NativeBattleModulePoolTask task, NativeBattlePsychoBlast23Inputs inputs)
    {
        NativeBattleModelTransform target = inputs.ReadModel(task.ReadByte(0x2D)).Transform ?? throw new NotSupportedException("Psycho Blast requires its actual target position.");
        NativeBattlePsychoBlast23State state = retained with { Shared84 = new byte[84].ToImmutableArray() };
        state = state.WithWord(8, target.Position28.X).WithWord(10, target.Position28.Y).WithWord(12, target.Position28.Z).WithWord(14, target.Vertical34);
        int angle = NativePrecision53.RoundInteger(Math.Atan2(target.Position28.X, target.Position28.Z) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
        return state.WithWord(0x4A, (angle - 0x800) & 0xFFF);
    }

    public static bool Request(NativeBattlePsychoBlast23State state, short expected, short requested, out NativeBattlePsychoBlast23State updated)
    {
        bool accepted = state.Word(0x44) == expected && state.Word(0x40) == expected;
        updated = accepted ? state.WithWord(0x44, requested) : state;
        return accepted;
    }

    private static NativeBattlePsychoBlast23Frame SpawnStage(NativeBattlePsychoBlast23Frame frame, uint parent, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleEffectHierarchyAppend generic = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(3, NativeBattlePsychoBlast23Profile.GenericCallback, 676, parent, selectActors);
        NativeBattleModulePoolTask task = generic.Hierarchy.Read(generic.Address).WriteDword(0x30, 0x17EA4F0).WriteWord(0x298, 0).WriteWord(0x29E, 40).WriteWord(0x29C, 4);
        frame = With(frame, generic.Hierarchy.Write(generic.Address, task));
        frame = NativeBattlePsychoBlast23Act.Append(frame, parent, NativeBattlePsychoBlast23Profile.ActPrimaryCallback, 0x17EE254, 0xD8, 0, selectActors, out _);
        frame = NativeBattlePsychoBlast23Act.Append(frame, parent, NativeBattlePsychoBlast23Profile.ActDelayedCallback, 0x17F01B0, 0x58, 1, selectActors, out _);
        NativeBattleEffectHierarchyAppend overlay = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(6, 0x8DDC30, 64, parent, selectActors);
        return With(frame, overlay.Hierarchy.Write(overlay.Address, overlay.Hierarchy.Read(overlay.Address).WriteDword(0x34, 0x16146C8).WriteWord(0x38, 39)));
    }

    private static NativeBattlePsychoBlast23Frame InitializeBasis(NativeBattlePsychoBlast23Frame frame, NativeBattleModulePoolTask task, NativeBattlePsychoBlast23Inputs inputs)
    {
        int actor = task.ReadByte(0x2C);
        int target = task.ReadByte(0x2D);
        NativeBattleGraphicsModel model = inputs.ReadModel(actor);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Psycho Blast requires its caster packed position.");
        short angle = model.Rotation14 ?? throw new NotSupportedException("Psycho Blast requires its caster angle.");
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        frame = inputs.Execute(frame, new NativeBattlePsychoBlast23Rotate(identity, 'Y', angle, null));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(identity, angle);
        NativeBattleVector first = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, -3584, -1024));
        NativeBattleVector second = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, -3584, -2424));
        NativeBattleEffectArena basis = WritePackedPosition(frame.State.Basis56, 0x20, transform, first);
        basis = WritePackedPosition(basis, 0x28, transform, second);
        frame = frame with { State = frame.State with { Basis56 = basis } };
        frame = inputs.Execute(frame, new NativeBattlePsychoBlast23TargetAnchor(target, 0xF0, 0));
        NativeBattleVector anchor = NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(target), inputs.ReadPoseSource(target), 0xF0, 0);
        basis = frame.State.Basis56;
        basis = WriteWord(basis, 0x30, anchor.X);
        basis = WriteWord(basis, 0x32, anchor.Y);
        basis = WriteWord(basis, 0x34, anchor.Z);
        NativeBattleVector direction = new(unchecked((short)(anchor.X - NativeBattlePsychoBlast23Act.ArenaWord(basis, 0x28))), unchecked((short)(anchor.Y - NativeBattlePsychoBlast23Act.ArenaWord(basis, 0x2A))), unchecked((short)(anchor.Z - NativeBattlePsychoBlast23Act.ArenaWord(basis, 0x2C))));
        frame = inputs.Execute(frame with { State = frame.State with { Basis56 = basis } }, new NativeBattlePsychoBlast23LookBasis(direction, new(0, 4096, 0)));
        NativeBattleActionMatrix rotation = NativeBattleDrawStock15Particles.LookBasis8DDA50(direction);
        basis = frame.State.Basis56;
        for (int index = 0; index < 9; index++)
        {
            basis = WriteWord(basis, index * 2, rotation.Rotation[index]);
        }
        return frame with { State = frame.State with { Basis56 = basis } };
    }

    private static NativeBattleEffectArena WritePackedPosition(NativeBattleEffectArena arena, int offset, NativeBattleModelTransform transform, NativeBattleVector delta)
    {
        arena = WriteWord(arena, offset, transform.Position28.X + delta.X);
        arena = WriteWord(arena, offset + 2, transform.Position28.Y + delta.Y);
        arena = WriteWord(arena, offset + 4, transform.Position28.Z + delta.Z);
        return WriteWord(arena, offset + 6, transform.Vertical34);
    }
    private static NativeBattleEffectArena WriteWord(NativeBattleEffectArena arena, int offset, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)value));
        return arena.Write(offset, bytes);
    }
    private static NativeBattlePsychoBlast23Frame With(NativeBattlePsychoBlast23Frame frame, NativeBattleEffectHierarchy hierarchy) => frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address) => hierarchy.Write(address, NativeBattlePsychoBlast23Act.Next(hierarchy.Read(address)));
    private static void Require(NativeBattlePsychoBlast23Frame frame)
    {
        frame.State.Validate();
        frame.Effect.Validate();
        if (frame.Effect.Binding.Effect != 23 || frame.Effect.Binding.Entry != NativeBattlePsychoBlast23Profile.Entry || frame.State.Nodes is null || frame.Effect.Parameters is null || frame.Effect.Pools.Length != 7 || frame.Effect.ReturnedDescriptor != 0 && frame.Effect.ReturnedDescriptor != NativeBattlePsychoBlast23Profile.Pools[0].Descriptor)
        {
            throw new InvalidDataException("Psycho Blast requires its executed controller state.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < 7; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = NativeBattlePsychoBlast23Profile.Pools[index];
            if (pool.DescriptorAddress != expected.Descriptor || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Psycho Blast pools contradict their physical owners.");
            }
        }
    }
}
