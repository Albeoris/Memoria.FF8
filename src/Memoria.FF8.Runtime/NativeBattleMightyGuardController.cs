using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleMightyGuardController
{
    public static NativeBattleMightyGuardFrame Initialize(NativeBattleMightyGuardFrame frame, NativeBattleMightyGuardResources resources, NativeBattleActionPacketsState packets)
    {
        Require(frame, resources);
        packets.Validate();
        NativeBattleMightyGuardProfile profile = frame.Profile;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Mighty Guard requires its captured action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.MainCallback, 100);
        hierarchy = root.Hierarchy;
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address);
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, 0x1D99A78, unchecked((sbyte)task.ReadByte(0x2A)), unchecked((sbyte)task.ReadByte(0x2B)));
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        byte targets = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = unchecked((byte)(targets - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        hierarchy = hierarchy.Write(root.Address, task.WriteDword(0x0C, 0x1D99A78).WriteByte(0x29, 0).WriteByte(0x2C, actors.Actor).WriteByte(0x2D, actors.Target).WriteWord(0x5A, targets).WriteWord(0x58, groups).WriteByte(0x2F, maximum));
        int source = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Mighty Guard requires its executed texture initializer pointer.");
        if (source < 0 || source > NativeBattleEffectArena.Length - 2 * profile.DrawBankBytes)
        {
            throw new InvalidDataException("Mighty Guard's working banks leave the effect arena.");
        }
        NativeBattleEffectMemory memory = frame.Memory;
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Mighty Guard requires its retained texture owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Mighty Guard requires its loaded texture arena."), source);
            memory = memory with { Textures = textures };
        }
        return frame with { Memory = memory, Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = profile.Pools[0].Descriptor }, State = frame.State with { SourceArenaOffset = source, AuxiliaryCursor = checked(source + 2 * profile.DrawBankBytes), EntryCounter = 0 } };
    }

    public static NativeBattleMightyGuardFrame Advance(NativeBattleMightyGuardFrame frame, NativeBattleMightyGuardResources resources, NativeBattleMightyGuardInputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        Require(frame, resources);
        NativeBattleMightyGuardProfile profile = frame.Profile;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Mighty Guard lost its captured action parameters.");
        if (frame.Effect.ReturnedDescriptor != profile.Pools[0].Descriptor || frame.Effect.Pools.Length != profile.Pools.Length)
        {
            throw new InvalidDataException("Mighty Guard requires its executed original hierarchy.");
        }
        for (int index = 0; index < profile.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = profile.Pools[index];
            if (pool.DescriptorAddress != expected.Descriptor || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Mighty Guard's physical task pool differs from its original module.");
            }
        }
        NativeBattleMightyGuardFrame current = frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyStep roots = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != profile.MainCallback)
            {
                throw new InvalidDataException("Mighty Guard's root pool contains another callback.");
            }
            int source = current.State.SourceArenaOffset ?? throw new NotSupportedException("Mighty Guard requires its initialized working source.");
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            NativeBattlePackedMatrix32 camera = inputs.PackedCamera ?? NativeBattlePackedMatrix32.FromMatrix(inputs.Camera, inputs.CopiedCameraPadding);
            current = current with { Memory = current.Memory with { SharedEffectView2793E58 = camera }, State = current.State with { PrimaryDrawCursor = checked(source + (odd ? 0 : profile.DrawBankBytes)), SecondaryDrawCursor = checked(source + (odd ? profile.DrawBankBytes : 2 * profile.DrawBankBytes)) } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(), inputs.ReadPoseSource);
            state = state.Write(address, task);
            switch (Phase(task))
            {
                case 0:
                case 1:
                case 7:
                case 8:
                    state = Next(state, address);
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        int first = current.State.AuxiliaryCursor ?? throw new NotSupportedException("Mighty Guard requires its current auxiliary cursor.");
                        int second = checked(first + profile.EmitterBytes);
                        int end = checked(second + profile.GeometryBytes);
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Mighty Guard requires its actual node arena.");
                        arena = arena.Write(first, new byte[profile.EmitterBytes]).Write(second, new byte[profile.GeometryBytes]);
                        current = current with { Memory = current.Memory with { Arena = arena }, State = current.State with { Emitters = first, Geometry = second, AuxiliaryCursor = end, EmitterScan = 0, GeometryScan = 0, EmitterReset = 0, GeometryReset = 0 } };
                        state = Add(state, 3, profile.FadeInCallback, 64, address).Hierarchy;
                        if (profile.CasterDatAddress is { } casterDat)
                        {
                            Emit(ref state, new NativeBattleMightyGuardBindCaster(task.ReadByte(0x2C), profile.SavedCasterBank!.Value, casterDat, 1));
                        }
                        state = Next(state, address);
                    }
                    break;
                case 3:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = Add(state, 1, profile.TargetCallback, 88, address).Hierarchy;
                    state = Next(state, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        state = state.Write(address, AdvanceTargetIteration(task, profile));
                    }
                    break;
                case 5:
                    task = task.WriteWord(0x60, unchecked((ushort)(task.ReadWord(0x60) - 1)));
                    state = state.Write(address, task);
                    if (unchecked((short)task.ReadWord(0x60)) <= 0)
                    {
                        state = Add(state, 3, profile.FadeOutCallback, 64, address).Hierarchy;
                        state = Next(state, address);
                    }
                    break;
                case 6:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        state = Next(state, address);
                    }
                    break;
                case 9:
                    state = Done(state, address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("Mighty Guard's root leaves its eleven native phases.");
            }
            state = state.Write(address, state.Read(address).WriteWord(0x5E, 0));
            current = current with { State = current.State with { PassEmitters = 0, PassGeometry = 0 } };
            int survivors = 0;
            for (int pool = 1; pool < profile.Pools.Length; pool++)
            {
                NativeBattleEffectHierarchyStep children = state.Advance(pool, Child);
                state = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
                state = state.Write(address, state.Read(address).WriteWord(0x5E, unchecked((ushort)survivors)));
            }
            state = state.Write(address, state.Read(address).WriteWord(0x5C, unchecked((ushort)(state.Read(address).ReadWord(0x5C) + 1))));
            return Finish(state, address);
        });
        return current with { Effect = current.Effect with { Pools = roots.Hierarchy.Pools, ReturnedDescriptor = roots.Survivors == 0 ? 0u : profile.Pools[0].Descriptor } };

        NativeBattleEffectHierarchyCallback Child(NativeBattleEffectHierarchy state, uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback == profile.TargetCallback)
            {
                task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(), inputs.ReadPoseSource);
                int actor = task.ReadByte(0x2D);
                NativeBattlePoseSource source = inputs.ReadPoseSource(actor);
                NativeBattleSequenceState pose = inputs.ReadModel(actor).SequenceState ?? throw new NotSupportedException("Mighty Guard requires its target's current pose.");
                task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(pose.Pose ?? throw new NotSupportedException("Mighty Guard's target pose is unknown."), pose.PoseFrame114 ?? throw new NotSupportedException("Mighty Guard's target pose frame is unknown."), pose.PoseFlags109));
                state = state.Write(address, task);
                switch (Phase(task))
                {
                    case 0:
                        NativeBattleEffectHierarchyAppend append = Add(state, 2, profile.GenericCallback, 676, address);
                        state = append.Hierarchy.Write(append.Address, append.Hierarchy.Read(append.Address).WriteDword(0x30, profile.SourceAddress).WriteWord(0x298, profile.GenericDelay).WriteWord(0x29C, 0).WriteWord(0x29E, 45));
                        state = Next(state, address);
                        break;
                    case 1:
                        if (task.ReadWord(0x24) == 20)
                        {
                            uint root = task.ReadDword(0x10);
                            state = state.Write(root, state.Read(root).WriteByte(0x63, 0));
                            state = Next(state, address);
                        }
                        break;
                    case 2:
                        if (unchecked((short)task.ReadWord(0x24)) >= profile.ContactAge)
                        {
                            int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                            int first = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Mighty Guard requires the selected contact buffer.");
                            Emit(ref state, new NativeBattleMightyGuardContact(checked(first + unchecked((sbyte)task.ReadByte(0x2B)))));
                            state = Done(state, address);
                        }
                        break;
                    case 3:
                        break;
                    default:
                        throw new InvalidDataException("Mighty Guard's target leaves its four native phases.");
                }
                if (unchecked((short)state.Read(address).ReadWord(0x24)) == profile.SoundAge)
                {
                    Emit(ref state, new NativeBattleMightyGuardSound(new(resources.Sound, 0, null) { Pan = 128 }));
                }
            }
            else if (task.Callback == profile.GenericCallback)
            {
                switch (Phase(task))
                {
                    case 0:
                        if (unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
                        {
                            Publish(state);
                            current = InitializeContext(current, address, resources, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource);
                            state = new(current.Effect.Pools);
                            _ = Context(ref state, address);
                            state = Next(state, address);
                        }
                        break;
                    case 1:
                        if (Context(ref state, address))
                        {
                            state = Done(state, address);
                        }
                        break;
                    case 2:
                        break;
                    default:
                        throw new InvalidDataException("Mighty Guard's generic task leaves its three native phases.");
                }
            }
            else if (task.Callback == profile.FadeInCallback || task.Callback == profile.FadeOutCallback)
            {
                bool fadeIn = task.Callback == profile.FadeInCallback;
                switch (Phase(task))
                {
                    case 0:
                        state = state.Write(address, task.WriteWord(0x1C, fadeIn ? (ushort)0 : (ushort)1024));
                        if (fadeIn)
                        {
                            Emit(ref state, new NativeBattleMightyGuardStage(0, true));
                        }
                        state = Next(state, address);
                        break;
                    case 1:
                        short depth = unchecked((short)(task.ReadWord(0x1C) + (fadeIn ? 256 : -256)));
                        bool done = fadeIn ? depth >= 1024 : depth <= 0;
                        depth = fadeIn ? Math.Min(depth, (short)1024) : Math.Max(depth, (short)0);
                        state = state.Write(address, task.WriteWord(0x1C, unchecked((ushort)depth)));
                        if (done)
                        {
                            state = Done(state, address);
                        }
                        Emit(ref state, new NativeBattleMightyGuardStage(depth, false));
                        break;
                    case 2:
                        break;
                    default:
                        throw new InvalidDataException("Mighty Guard's stage task leaves its three native phases.");
                }
            }
            else
            {
                throw new NotSupportedException("Mighty Guard's child pool contains an unsupported callback.");
            }
            return Finish(state, address);
        }

        bool Context(ref NativeBattleEffectHierarchy state, uint address)
        {
            Publish(state);
            current = current with { State = current.State with { ContextTask = address } };
            var step = NativeBattleEffectNodeLifecycle.AdvanceContext(() => new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address), task =>
            {
                NativeBattleEffectHierarchy latest = new(current.Effect.Pools);
                current = current with { Effect = current.Effect with { Pools = latest.Write(address, task).Pools } };
            }, () => current = inputs.AdvanceNodes(current, address));
            current = current with { State = current.State with { PassEmitters = unchecked((ushort)(current.State.PassEmitters + step.EmitterCount)), PassGeometry = unchecked((ushort)(current.State.PassGeometry + step.GeometryCount)) } };
            state = new(current.Effect.Pools);
            return step.Complete;
        }

        NativeBattleEffectHierarchyAppend Add(NativeBattleEffectHierarchy state, int pool, uint callback, int size, uint parent)
        {
            NativeBattleEffectHierarchyAppend result = state.Append8DC540(pool, callback, size, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            if (result.Address == 0)
            {
                throw new NotSupportedException("Mighty Guard dereferences its exhausted mandatory task allocation.");
            }
            return result;
        }

        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        void Publish(NativeBattleEffectHierarchy state) => current = current with { Effect = current.Effect with { Pools = state.Pools } };
        void Emit(ref NativeBattleEffectHierarchy state, NativeBattleMightyGuardOperation operation)
        {
            Publish(state);
            current = inputs.Execute(current, operation);
            state = new(current.Effect.Pools);
        }
    }

    public static NativeBattleModulePoolTask AdvanceTargetIteration(NativeBattleModulePoolTask task, NativeBattleMightyGuardProfile profile)
    {
        if (task.ReadByte(0x63) != 0)
        {
            return task;
        }
        if (unchecked((sbyte)task.ReadByte(0x2B)) < unchecked((short)task.ReadWord(0x5A)) - 1)
        {
            return task.WriteByte(0x2B, unchecked((byte)(task.ReadByte(0x2B) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1)));
        }
        return task.WriteWord(0x60, profile.RootWait).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    }

    public static NativeBattleMightyGuardFrame InitializeContext(NativeBattleMightyGuardFrame frame, uint address, NativeBattleMightyGuardResources resources, NativeBattleActionPacketsState packets, Func<int, NativeBattleGraphicsModel> readModel, Func<int, NativeBattlePoseSource> readPoseSource)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        using NativeBattleNodeInitialization<NativeBattleMightyGuardState> initialization = frame.State.PrepareSource(resources);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != frame.Profile.GenericCallback || task.ReadDword(0x30) != frame.Profile.SourceAddress)
        {
            throw new InvalidDataException("Mighty Guard's node context belongs to another module.");
        }
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Mighty Guard requires its action context."), packets, readModel, readPoseSource, frame.Profile.SourceAddress, singleSelectedTarget: true);
        if (frame.Profile.ScalesToTarget)
        {
            short scale = TargetScale(readModel(task.ReadByte(0x2D)).GeometryBounds52 ?? throw new NotSupportedException("Enemy Mighty Guard requires its target's actual native bounds."));
            task = task.WriteDword(0x34 + 0x214, unchecked((uint)(int)scale)).WriteDword(0x34 + 0x218, unchecked((uint)(int)scale)).WriteDword(0x34 + 0x21C, unchecked((uint)(int)scale));
        }
        NativeBattleEffectHierarchy initialized = hierarchy.Write(address, task);
        NativeBattleMightyGuardState owner = initialization.Publish() with { ContextTask = address };
        return NativeBattleTaskOperation.CompleteStandalone(operation, frame with { State = owner, Effect = frame.Effect with { Pools = initialized.Pools } });
    }

    public static short TargetScale(NativeBattleEffectLocalBounds bounds)
    {
        short height = unchecked((short)(bounds.Maximum.Y - bounds.Minimum.Y));
        return Math.Clamp(unchecked((short)(height * 3)), (short)1024, (short)16384);
    }

    private static int Phase(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy state, uint address)
    {
        NativeBattleModulePoolTask task = state.Read(address);
        return state.Write(address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }
    private static NativeBattleEffectHierarchy Done(NativeBattleEffectHierarchy state, uint address)
    {
        NativeBattleModulePoolTask task = state.Read(address);
        return Next(state.Write(address, task.WriteByte(0x26, unchecked((byte)(task.ReadByte(0x26) | 1)))), address);
    }
    private static NativeBattleEffectHierarchyCallback Finish(NativeBattleEffectHierarchy state, uint address)
    {
        NativeBattleModulePoolTask task = state.Read(address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        state = state.Write(address, task);
        bool done = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        return new(done ? state.Complete8DC530(address) : state, done ? 2 : 0);
    }
    private static void Require(NativeBattleMightyGuardFrame frame, NativeBattleMightyGuardResources resources)
    {
        frame.State.Validate();
        resources.Validate();
        frame.Effect.Validate();
        if (resources.Effect != frame.Profile.Effect || frame.State.Effect != frame.Profile.Effect || frame.Effect.Binding.Entry != frame.Profile.Entry || frame.Effect.Binding.Initializer != frame.Profile.Initializer)
        {
            throw new InvalidDataException("Mighty Guard's state and resources must match its original module entry.");
        }
    }
}
