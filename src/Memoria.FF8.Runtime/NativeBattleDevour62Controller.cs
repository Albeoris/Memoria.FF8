using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDevour62Controller
{
    public static NativeBattleDevour62Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, NativeBattleDevour62Resources resources, NativeBattleDevour62State retained)
    {
        retained.Validate();
        effect.Validate();
        packets.Validate();
        resources.Validate();
        if (effect.Binding.Effect != 62 || effect.Binding.Entry != NativeBattleDevour62Profile.Entry)
        {
            throw new InvalidDataException("Devour requires its original module binding.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Devour requires its captured parameters.");
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleDevour62Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleDevour62Profile.RootCallback, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), parameters, packets));
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == NativeBattleDevour62Profile.InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Devour requires its executed initializer pointer.");
        int secondary = checked(source + NativeBattleDevour62Profile.DrawStep);
        NativeBattleDevour62State state = retained with { PrimaryDrawBase = secondary, AlternatePrimaryDrawBase = source, SecondaryDrawBase = checked(secondary + NativeBattleDevour62Profile.DrawStep), AlternateSecondaryDrawBase = secondary, AuxiliaryCursor = checked(secondary + NativeBattleDevour62Profile.DrawStep), Counter2601918 = 0, Counter2601770 = 0 };
        state.Validate();
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Devour requires its texture registration owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Devour requires its loaded arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = NativeBattleDevour62Profile.Pools[0].Descriptor }, random, state);
    }

    public static NativeBattleDevour62Frame Advance(NativeBattleDevour62Frame frame, NativeBattleDevour62Resources resources, NativeBattleDevour62Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        resources.Validate();
        NativeBattleDevour62Frame current = frame;
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleEffectHierarchyStep rootStep = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(0, Root);
        return current with { Effect = current.Effect with { Pools = rootStep.Hierarchy.Pools, ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : NativeBattleDevour62Profile.Pools[0].Descriptor } };

        NativeBattleEffectHierarchyCallback Root(NativeBattleEffectHierarchy hierarchy, uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            NativeBattleDevour62State state = current.State;
            current = current with { Memory = current.Memory with { SharedEffectView2793E58 = inputs.Camera }, State = state with { PrimaryDrawCursor = (odd ? state.AlternatePrimaryDrawBase : state.PrimaryDrawBase) ?? throw new NotSupportedException("Devour requires its actual primary bank."), SecondaryDrawCursor = (odd ? state.AlternateSecondaryDrawBase : state.SecondaryDrawBase) ?? throw new NotSupportedException("Devour requires its actual secondary bank.") } };
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
                        hierarchy = Next(hierarchy, address);
                    }
                    break;
                case 3:
                    hierarchy = hierarchy.Write(address, task.WriteByte(0x63, 1));
                    hierarchy = hierarchy.Append8DC540(1, NativeBattleDevour62Profile.TargetCallback, 88, address, Select).Hierarchy;
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
                    throw new InvalidDataException("Devour root signed phase leaves its eleven original handlers.");
            }
            hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x5E, 0));
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
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback == NativeBattleDevour62Profile.TargetCallback)
            {
                task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(), inputs.ReadPoseSource);
                NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
                NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Devour target requires its pose.");
                NativeBattlePoseSource pose = inputs.ReadPoseSource(task.ReadByte(0x2D));
                task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, pose, pose.Sample(sequence.Pose ?? throw new NotSupportedException("Devour target requires its selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Devour target requires its pose frame."), sequence.PoseFlags109));
                hierarchy = hierarchy.Write(address, task);
                Target();
            }
            else if (task.Callback == NativeBattleDevour62Profile.AuxiliaryCallback)
            {
                Auxiliary();
            }
            else if (task.Callback == NativeBattleDevour62Profile.QuadCallback)
            {
                Quads();
            }
            else if (task.Callback == NativeBattleDevour62Profile.ActorFadeCallback)
            {
                ActorFade();
            }
            else if (task.Callback == NativeBattleDevour62Profile.MusicCallback)
            {
                Music();
            }
            else
            {
                throw new NotSupportedException("Devour requires its original child callback.");
            }
            task = hierarchy.Read(address);
            hierarchy = hierarchy.Write(address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
            bool complete = Complete(hierarchy.Read(address));
            return new(complete ? hierarchy.Complete8DC530(address) : hierarchy, complete ? 2 : 0);

            void Execute(NativeBattleDevour62Operation operation)
            {
                current = inputs.Execute(With(current, hierarchy), operation);
                hierarchy = new(current.Effect.Pools);
                task = hierarchy.Read(address);
            }

            void Target()
            {
                switch (Phase(task))
                {
                    case 0:
                        hierarchy = hierarchy.Append8DC540(2, NativeBattleDevour62Profile.AuxiliaryCallback, 84, address, Select).Hierarchy;
                        hierarchy = Next(hierarchy, address);
                        break;
                    case 1:
                        if (Word(task, 0x24) == 75)
                        {
                            uint parent = task.ReadDword(0x10);
                            hierarchy = Next(hierarchy.Write(parent, hierarchy.Read(parent).WriteByte(0x63, 0)), address);
                        }
                        break;
                    case 2:
                        if (Word(task, 0x24) >= 83)
                        {
                            int first = Header(task).FirstContactIndex ?? throw new NotSupportedException("Devour requires its actual contact pointer.");
                            int contact = checked(first + unchecked((sbyte)task.ReadByte(0x2B)));
                            _ = inputs.Packets.ReadContact(contact);
                            Execute(new NativeBattleDevour62Contact(contact));
                            hierarchy = Done(hierarchy, address);
                        }
                        break;
                    case 3:
                        break;
                    default:
                        throw new InvalidDataException("Devour target phase leaves its original handlers.");
                }
            }

            void Auxiliary()
            {
                switch (Phase(task))
                {
                    case 0:
                        Execute(new NativeBattleDevour62UploadTim(0x2601900));
                        NativeCrtRandomState random = current.Random.Draw(out ushort value);
                        current = current with { Random = random };
                        short variant = unchecked((short)(value % 3));
                        hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x4E, unchecked((ushort)variant)));
                        Execute(new NativeBattleDevour62UploadTim(variant switch { 0 => 0x2601904, 1 => 0x26018F8, 2 => 0x26018FC, _ => throw new InvalidDataException("Devour CRT selector leaves its original three variants.") }));
                        hierarchy = Next(hierarchy, address);
                        break;
                    case 1:
                        foreach (uint callback in new uint[] { NativeBattleDevour62Profile.ActorFadeCallback, NativeBattleDevour62Profile.MusicCallback, NativeBattleDevour62Profile.QuadCallback })
                        {
                            hierarchy = hierarchy.Append8DC540(2, callback, 84, address, Select).Hierarchy;
                        }
                        hierarchy = Next(hierarchy, address);
                        break;
                    case 2:
                        if (Word(task, 0x24) >= 35)
                        {
                            int outcome = Math.Clamp((int)FirstOutcome(task), 0, 8);
                            Execute(new NativeBattleDevour62Sound(resources.Sound(outcome)));
                            hierarchy = Done(hierarchy, address);
                        }
                        break;
                    case 3:
                        break;
                    default:
                        throw new InvalidDataException("Devour auxiliary phase leaves its original handlers.");
                }
            }

            void Quads()
            {
                switch (Phase(task))
                {
                    case 0:
                        if (Word(task, 0x24) >= 7)
                        {
                            hierarchy = Next(hierarchy.Write(address, task.WriteWord(0x1C, 0).WriteWord(0x50, 128)), address);
                        }
                        break;
                    case 1:
                        hierarchy = hierarchy.Write(address, task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) - 4))));
                        Execute(new NativeBattleDevour62DrawQuads(address));
                        if (Word(task, 0x24) >= 75)
                        {
                            hierarchy = Next(hierarchy, address);
                        }
                        break;
                    case 2:
                        short brightness = unchecked((short)(task.ReadWord(0x50) - 16));
                        hierarchy = hierarchy.Write(address, task.WriteWord(0x50, unchecked((ushort)Math.Max(0, (int)brightness))));
                        if (brightness <= 0)
                        {
                            hierarchy = Done(hierarchy, address);
                        }
                        else
                        {
                            hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) - 4))));
                            Execute(new NativeBattleDevour62DrawQuads(address));
                        }
                        break;
                    case 3:
                        break;
                    default:
                        throw new InvalidDataException("Devour quad phase leaves its original handlers.");
                }
            }

            void ActorFade()
            {
                switch (Phase(task))
                {
                    case 0:
                        if (Word(task, 0x24) >= 10)
                        {
                            for (int actor = 0; actor < 7; actor++)
                            {
                                task = task.WriteDword(0x30 + 4 * actor, inputs.ReadModel(actor).Flags0);
                            }
                            hierarchy = Next(hierarchy.Write(address, task), address);
                        }
                        break;
                    case 1:
                        hierarchy = hierarchy.Write(address, task.WriteWord(0x4C, 4096));
                        Execute(new NativeBattleDevour62StageFade(4096, true));
                        Execute(new NativeBattleDevour62FadeActors(4096));
                        HideTarget();
                        hierarchy = Next(hierarchy, address);
                        break;
                    case 2:
                        if (Word(task, 0x24) > 83)
                        {
                            hierarchy = Next(hierarchy.Write(address, task.WriteWord(0x4C, 4096)), address);
                        }
                        break;
                    case 3:
                        short fade = unchecked((short)(task.ReadWord(0x4C) - 512));
                        fade = Math.Max((short)0, fade);
                        hierarchy = hierarchy.Write(address, task.WriteWord(0x4C, unchecked((ushort)fade)));
                        if (fade == 0)
                        {
                            hierarchy = Next(hierarchy, address);
                        }
                        Execute(new NativeBattleDevour62StageFade(fade, false));
                        Execute(new NativeBattleDevour62FadeActors(fade));
                        break;
                    case 4:
                        for (int actor = 0; actor < 7; actor++)
                        {
                            ushort flags = inputs.ReadModel(actor).Flags0;
                            if ((flags & 2) != 0)
                            {
                                ushort saved = unchecked((ushort)task.ReadDword(0x30 + 4 * actor));
                                Execute(new NativeBattleDevour62WriteActorFlags(actor, NativeBattleDevour62ActorFade.RestoreFlags(flags, saved)));
                            }
                        }
                        HideTarget();
                        hierarchy = Done(hierarchy, address);
                        break;
                    case 5:
                        break;
                    default:
                        throw new InvalidDataException("Devour actor fade phase leaves its original handlers.");
                }
            }

            void HideTarget()
            {
                if (FirstOutcome(task) != 8)
                {
                    int actor = task.ReadByte(0x2D);
                    Execute(new NativeBattleDevour62WriteActorFlags(actor, (ushort)(inputs.ReadModel(actor).Flags0 | 4)));
                }
            }

            void Music()
            {
                switch (Phase(task))
                {
                    case 0:
                        if (Word(task, 0x24) >= 10)
                        {
                            hierarchy = Next(hierarchy.Write(address, task.WriteWord(0x4C, 127)), address);
                        }
                        break;
                    case 1:
                        short gain = unchecked((short)(task.ReadWord(0x4C) - 8));
                        hierarchy = hierarchy.Write(address, task.WriteWord(0x4C, unchecked((ushort)Math.Max(0, (int)gain))));
                        if (gain <= 0)
                        {
                            hierarchy = Next(hierarchy, address);
                        }
                        Execute(new NativeBattleDevour62MusicGain(Math.Max(0, (int)gain)));
                        break;
                    case 2:
                        if (Word(task, 0x24) > 83)
                        {
                            hierarchy = Next(hierarchy, address);
                        }
                        break;
                    case 3:
                        short restored = unchecked((short)(task.ReadWord(0x4C) + 8));
                        hierarchy = hierarchy.Write(address, task.WriteWord(0x4C, unchecked((ushort)Math.Min(127, (int)restored))));
                        if (restored >= 127)
                        {
                            hierarchy = Done(hierarchy, address);
                        }
                        Execute(new NativeBattleDevour62MusicGain(Math.Min(127, (int)restored)));
                        break;
                    case 4:
                        break;
                    default:
                        throw new InvalidDataException("Devour music phase leaves its original handlers.");
                }
            }
        }
        NativeBattleActionHeaderSlot Header(NativeBattleModulePoolTask task)
        {
            int index = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
            if ((uint)index >= inputs.Packets.Headers.Length)
            {
                throw new InvalidDataException("Devour header selector exceeds its physical arena.");
            }
            return inputs.Packets.Headers[index];
        }
        short FirstOutcome(NativeBattleModulePoolTask task)
        {
            int header = parameters.HeaderIndex7C;
            if ((uint)header >= inputs.Packets.Headers.Length)
            {
                throw new InvalidDataException("Devour first captured header exceeds its physical arena.");
            }
            int first = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Devour requires its first physical contact.");
            return unchecked((short)inputs.Packets.ReadContact(first).Primary.Amount6);
        }
        ImmutableArray<NativeBattleGraphicsModel> Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToImmutableArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
    }

    private static NativeBattleDevour62Frame With(NativeBattleDevour62Frame frame, NativeBattleEffectHierarchy hierarchy) => frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static int Phase(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    private static bool Complete(NativeBattleModulePoolTask task) => (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
    private static NativeBattleEffectHierarchy Next(NativeBattleEffectHierarchy hierarchy, uint address) => hierarchy.Write(address, hierarchy.Read(address).WriteByte(0x29, unchecked((byte)(hierarchy.Read(address).ReadByte(0x29) + 1))));
    private static NativeBattleEffectHierarchy Done(NativeBattleEffectHierarchy hierarchy, uint address) => Next(hierarchy.Write(address, hierarchy.Read(address).WriteByte(0x26, (byte)(hierarchy.Read(address).ReadByte(0x26) | 1))), address);
    private static void Require(NativeBattleDevour62Frame frame)
    {
        frame.State.Validate();
        frame.Effect.Validate();
        if (frame.Effect.Binding.Effect != 62 || frame.Effect.Binding.Entry != NativeBattleDevour62Profile.Entry || frame.Effect.Parameters is null || frame.Effect.Pools.Length != 3 || frame.Effect.ReturnedDescriptor != 0 && frame.Effect.ReturnedDescriptor != NativeBattleDevour62Profile.Pools[0].Descriptor)
        {
            throw new InvalidDataException("Devour requires its executed controller state.");
        }
        new NativeBattleEffectHierarchy(frame.Effect.Pools).Validate();
        for (int index = 0; index < 3; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool expected = NativeBattleDevour62Profile.Pools[index];
            if (pool.DescriptorAddress != expected.Descriptor || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Devour pools contradict their physical owners.");
            }
        }
    }
}
