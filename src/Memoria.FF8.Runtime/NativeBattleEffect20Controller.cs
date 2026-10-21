using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect20Controller
{
    public static NativeBattleEffect20Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, NativeBattleEffect20Resources resources, NativeBattleEffect20State retained)
    {
        retained.Validate();
        effect.Validate();
        packets.Validate();
        resources.Validate();
        if (effect.Binding.Effect != 20 || effect.Binding.Entry != NativeBattleEffect20Profile.Entry)
        {
            throw new InvalidDataException("Effect20 requires its original module binding.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Effect20 requires its actual captured parameters.");
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleEffect20Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleEffect20Profile.RootCallback, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), parameters, packets));
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == NativeBattleEffect20Profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect20 requires its executed initializer resource pointer.");
        NativeBattleEffect20State state = retained with { SourceArenaOffset = source, AuxiliaryCursor = checked(source + 2 * NativeBattleEffect20Profile.DrawStep), Counter27268F8 = 0, Counter2722960 = 0 };
        state.Validate();
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Effect20 requires its actual texture registration owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Effect20 requires its loaded arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x2721680 }, random, state);
    }

    public static NativeBattleEffect20Frame Advance(NativeBattleEffect20Frame frame, NativeBattleEffect20Resources resources, NativeBattleEffect20Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        resources.Validate();
        NativeBattleEffect20Frame current = frame;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleEffectHierarchyStep rootStep = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(0, Root);
        return current with { Effect = current.Effect with { Pools = rootStep.Hierarchy.Pools, ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : 0x2721680u } };

        NativeBattleEffectHierarchyCallback Root(NativeBattleEffectHierarchy hierarchy, uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            int source = current.State.SourceArenaOffset ?? throw new NotSupportedException("Effect20 requires its actual source bank.");
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            current = current with { Memory = current.Memory with { SharedEffectView2793E58 = inputs.Camera }, State = current.State with { PrimaryDrawCursor = checked(source + (odd ? 0 : NativeBattleEffect20Profile.DrawStep)), SecondaryDrawCursor = checked(source + (odd ? NativeBattleEffect20Profile.DrawStep : 2 * NativeBattleEffect20Profile.DrawStep)) } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(), inputs.ReadPoseSource);
            hierarchy = hierarchy.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                case 1:
                case 7:
                case 8:
                    hierarchy = Next(hierarchy, address);
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        hierarchy = hierarchy.Append8DC540(4, NativeBattleEffect20Profile.FadeInCallback, 64, address, Select).Hierarchy;
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 3:
                    hierarchy = hierarchy.Write(address, task.WriteByte(0x63, 1));
                    hierarchy = hierarchy.Append8DC540(1, NativeBattleEffect20Profile.TargetCallback, 88, address, Select).Hierarchy;
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
                            hierarchy = hierarchy.Write(address, task.WriteWord(0x60, 15));
                            hierarchy = Next(hierarchy, address);
                        }
                    }
                    break;
                case 5:
                    task = task.WriteWord(0x60, unchecked((ushort)(task.ReadWord(0x60) - 1)));
                    hierarchy = hierarchy.Write(address, task);
                    if (Word(task, 0x60) <= 0)
                    {
                        hierarchy = hierarchy.Append8DC540(4, NativeBattleEffect20Profile.FadeOutCallback, 64, address, Select).Hierarchy;
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 6:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 9:
                    hierarchy = hierarchy.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    hierarchy = Next(hierarchy, address);
                    break;
                case 10:
                    break;
                default:
                    throw new InvalidDataException("Effect20 signed root phase leaves its eleven original handlers.");
            }
            hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x5E, 0));
            int survivors = 0;
            for (int pool = 1; pool < 5; pool++)
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
            NativeBattleEffect20LeafStep step = hierarchy.Read(address).Callback switch
            {
                NativeBattleEffect20Profile.TargetCallback => Target(address),
                NativeBattleEffect20Profile.MainCallback => Main(address),
                NativeBattleEffect20Profile.SpriteCallback => NativeBattleEffect20Particles.AdvanceSprite(current, address, inputs, Select),
                NativeBattleEffect20Profile.ParticleCallback => NativeBattleEffect20Particles.AdvanceParticle(current, address, inputs),
                NativeBattleEffect20Profile.FadeInCallback => Fade(address, true),
                NativeBattleEffect20Profile.FadeOutCallback => Fade(address, false),
                0x8DDC30 => inputs.AdvanceOverlay(current, address),
                _ => throw new NotSupportedException("Effect20 child pool requires its original callback.")
            };
            current = step.Frame;
            return new(new(current.Effect.Pools), step.Result);
        }

        NativeBattleEffect20LeafStep Target(uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(hierarchy.Read(address), Models(), inputs.ReadPoseSource);
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Effect20 target requires its actual pose.");
            NativeBattlePoseSource pose = inputs.ReadPoseSource(task.ReadByte(0x2D));
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, pose, pose.Sample(sequence.Pose ?? throw new NotSupportedException("Effect20 target requires its selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Effect20 target requires its current pose frame."), sequence.PoseFlags109));
            hierarchy = hierarchy.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    hierarchy = hierarchy.Append8DC540(2, NativeBattleEffect20Profile.MainCallback, 120, address, Select).Hierarchy;
                    hierarchy = Next(hierarchy, address);
                    break;
                case 1:
                    if (task.ReadWord(0x24) == 30)
                    {
                        uint root = task.ReadDword(0x10);
                        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x63, 0));
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 2:
                    if (Word(task, 0x24) >= 30)
                    {
                        int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        int contact = checked((inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect20 requires its actual contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        current = inputs.Execute(With(current, hierarchy), new NativeBattleEffect20Contact(contact));
                        hierarchy = new(current.Effect.Pools);
                        hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteByte(0x26, (byte)(hierarchy.Read(address).ReadByte(0x26) | 1)));
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Effect20 target signed phase leaves its four original handlers.");
            }
            current = With(current, hierarchy);
            if (task.ReadWord(0x24) == 0)
            {
                current = inputs.Execute(current, new NativeBattleEffect20Sound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 }));
            }
            return NativeBattleEffect20Particles.Finish(current, address);
        }

        NativeBattleEffect20LeafStep Main(uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    current = inputs.Execute(current, new NativeBattleEffect20VibrationEnqueue(address));
                    hierarchy = new(current.Effect.Pools);
                    task = hierarchy.Read(address);
                    int actor = task.ReadByte(0x2C);
                    NativeBattleModelTransform transform = inputs.ReadModel(actor).Transform ?? throw new NotSupportedException("Effect20 main effect requires its caster's actual packed position.");
                    task = task.WriteDword(0x1C, unchecked((ushort)transform.Position28.X) | (uint)unchecked((ushort)transform.Position28.Y) << 16).WriteDword(0x20, unchecked((ushort)transform.Position28.Z) | (uint)unchecked((ushort)transform.Vertical34) << 16);
                    task = task.WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) - 4416)));
                    current = inputs.Execute(With(current, hierarchy.Write(address, task)), new NativeBattleEffect20ClearActorFlags124Bit2(actor));
                    current = inputs.Execute(current, new NativeBattleEffect20BodyDraw(address));
                    hierarchy = Next(new(current.Effect.Pools), address);
                    break;
                case 1:
                    if (Word(task, 0x24) >= 34)
                    {
                        NativeBattleEffectHierarchyAppend overlay = hierarchy.Append8DC540(4, 0x8DDC30, 64, address, Select);
                        hierarchy = overlay.Hierarchy.Write(overlay.Address, overlay.Hierarchy.Read(overlay.Address).WriteDword(0x34, 0x1629F14).WriteWord(0x38, 1));
                        task = hierarchy.Read(address).WriteWord(0x60, 288).WriteWord(0x62, unchecked((ushort)-320)).WriteWord(0x64, 32);
                        current = inputs.Execute(With(current, hierarchy.Write(address, task)), new NativeBattleEffect20RingSequence(0x1D97668, 12));
                        hierarchy = new(current.Effect.Pools);
                        hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x70, 24));
                        hierarchy = Next(hierarchy, address);
                    }
                    current = inputs.Execute(With(current, hierarchy), new NativeBattleEffect20BodyDraw(address));
                    hierarchy = new(current.Effect.Pools);
                    break;
                case 2:
                    task = task.WriteWord(0x62, unchecked((ushort)(task.ReadWord(0x62) + 40))).WriteWord(0x68, (ushort)((task.ReadWord(0x68) - 64) & 0xFFF)).WriteWord(0x6C, (ushort)((task.ReadWord(0x6C) + 48) & 0xFFF));
                    task = task.WriteWord(0x58, unchecked((ushort)(task.ReadWord(0x58) + task.ReadWord(0x60)))).WriteWord(0x5A, unchecked((ushort)(task.ReadWord(0x5A) + task.ReadWord(0x62)))).WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + task.ReadWord(0x64)))).WriteWord(0x70, unchecked((ushort)(task.ReadWord(0x70) - 1)));
                    hierarchy = hierarchy.Write(address, task);
                    if (Word(task, 0x70) <= 0)
                    {
                        current = inputs.Execute(With(current, hierarchy), new NativeBattleEffect20Activate(6));
                        current = inputs.Execute(current, new NativeBattleEffect20VibrationStop(Word(task, 0x74)));
                        hierarchy = new(current.Effect.Pools);
                        hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteByte(0x26, (byte)(hierarchy.Read(address).ReadByte(0x26) | 1)));
                        hierarchy = Next(hierarchy, address);
                    }
                    current = inputs.Execute(With(current, hierarchy), new NativeBattleEffect20BodyDraw(address));
                    hierarchy = new(current.Effect.Pools);
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Effect20 main effect signed phase leaves its four original handlers.");
            }
            current = Emit(With(current, hierarchy), address);
            return NativeBattleEffect20Particles.Finish(current, address);
        }

        NativeBattleEffect20Frame Emit(NativeBattleEffect20Frame value, uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(value.Effect.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            int index = Word(task, 0x72);
            if ((uint)index >= resources.EmissionAges.Length)
            {
                throw new InvalidDataException("Effect20 emission index leaves its original age table.");
            }
            if (resources.EmissionAges[index] != Word(task, 0x24))
            {
                return value;
            }
            hierarchy = hierarchy.Write(address, task.WriteWord(0x72, unchecked((ushort)(index + 1))));
            NativeCrtRandomState random = value.Random;
            for (int child = 0; child < 4; child++)
            {
                random = random.Draw(out ushort offsetIndex);
                NativeBattleVector offset = resources.EmissionOffsets[offsetIndex % 12];
                NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(2, NativeBattleEffect20Profile.SpriteCallback, 120, address, Select);
                hierarchy = append.Hierarchy;
                NativeBattleModulePoolTask parent = hierarchy.Read(address);
                NativeBattleModulePoolTask sprite = hierarchy.Read(append.Address).WriteDword(0x1C, parent.ReadDword(0x1C)).WriteDword(0x20, parent.ReadDword(0x20));
                sprite = sprite.WriteWord(0x1C, unchecked((ushort)(sprite.ReadWord(0x1C) + offset.X))).WriteWord(0x1E, unchecked((ushort)(sprite.ReadWord(0x1E) + offset.Y))).WriteWord(0x20, unchecked((ushort)(sprite.ReadWord(0x20) + offset.Z)));
                for (int axis = 0; axis < 3; axis++)
                {
                    random = random.Draw(out ushort jitter);
                    int field = 0x1C + axis * 2;
                    sprite = sprite.WriteWord(field, unchecked((ushort)(sprite.ReadWord(field) + (jitter & 255) - 128)));
                }
                if ((child & 1) != 0)
                {
                    sprite = sprite.WriteWord(0x1C, unchecked((ushort)-Word(sprite, 0x1C)));
                }
                hierarchy = hierarchy.Write(append.Address, sprite);
            }
            return With(value, hierarchy) with { Random = random };
        }

        NativeBattleEffect20LeafStep Fade(uint address, bool fadeIn)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    task = NextTask(task.WriteWord(0x1C, fadeIn ? (ushort)0 : (ushort)1024));
                    current = With(current, hierarchy.Write(address, task));
                    if (fadeIn)
                    {
                        current = inputs.Execute(current, new NativeBattleEffect20StageInitialize());
                    }
                    break;
                case 1:
                    short value = unchecked((short)(Word(task, 0x1C) + (fadeIn ? 256 : -256)));
                    bool done = fadeIn ? value >= 1024 : value <= 0;
                    task = task.WriteWord(0x1C, unchecked((ushort)(done ? fadeIn ? 1024 : 0 : value)));
                    if (done)
                    {
                        task = NextTask(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    current = inputs.Execute(With(current, hierarchy.Write(address, task)), new NativeBattleEffect20StageFade(Word(task, 0x1C)));
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("Effect20 stage fade signed phase leaves its three original handlers.");
            }
            return NativeBattleEffect20Particles.Finish(current, address);
        }

        ImmutableArray<NativeBattleGraphicsModel> Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToImmutableArray();
        NativeBattleEffectTaskActors Select(uint parameter, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, parameter, group, target);
    }

    private static NativeBattleEffect20Frame With(NativeBattleEffect20Frame frame, NativeBattleEffectHierarchy hierarchy) => frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static bool Complete(NativeBattleModulePoolTask task) => (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
    private static NativeBattleModulePoolTask NextTask(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address) => hierarchy.Write(address, NextTask(hierarchy.Read(address)));

    private static void Require(NativeBattleEffect20Frame frame)
    {
        frame.State.Validate();
        frame.Effect.Validate();
        if (frame.Effect.Binding.Effect != 20 || frame.Effect.Binding.Entry != NativeBattleEffect20Profile.Entry || frame.State.SourceArenaOffset is null || frame.Effect.Parameters is null || frame.Effect.Pools.Length != 5 || frame.Effect.ReturnedDescriptor is not (0 or 0x2721680))
        {
            throw new InvalidDataException("Effect20 requires its executed original controller.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < 5; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = NativeBattleEffect20Profile.Pools[index];
            if (pool.DescriptorAddress != expected.Descriptor || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect20 pools contradict their original physical owners.");
            }
        }
    }
}
