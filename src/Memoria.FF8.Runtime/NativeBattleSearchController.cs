using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Search's physical hierarchy, independent model, retained dog state and ordered local controllers.</summary>
public static class NativeBattleSearchController
{
    public const uint MainDescriptor = 0x2594F70;
    public const uint MainBank = 0x2594EA8;
    public const uint ModelBank = 0x2598D78;
    public const uint ModelDescriptor = 0x2598ED0;
    private const uint ActionParameters = 0x1D99A78;

    public static NativeBattleSearchFrame Initialize755130(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleSearchResources resources, NativeCrtRandomState random)
    {
        if (effect.Binding.Entry != 0x755130 || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Search requires its original755130 entry and actual action parameters.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(0x25981A8, 0x25981B8, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(0x2594E78, 0x2594DA0, 0x48, 3),
            NativeBattleEffectTaskPool.Initialize(ModelDescriptor, ModelBank, 0x154, 1),
            NativeBattleEffectTaskPool.Initialize(0x2594F80, 0x2594F90, 0x90, 40),
            NativeBattleEffectTaskPool.Initialize(0x2596618, 0x2596628, 0x8C, 40),
            NativeBattleEffectTaskPool.Initialize(0x2597F00, 0x2597C08, 0x17C, 2),
            NativeBattleEffectTaskPool.Initialize(0x2598198, 0x2597F18, 0x40, 10)
        ]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x7552B0, 0x64);
        hierarchy = appended.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Search requires an original header/contact owner.");
        NativeBattleModulePoolTask root = hierarchy.Read(appended.Address).WriteDword(0xC, ActionParameters).WriteByte(0x2C, header.ReadByte(0)).WriteByte(0x2D, packets.ReadContact(contact).Primary.ActorIndex).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17));
        byte lastTarget = unchecked((byte)(root.ReadWord(0x5A) - 1));
        if (lastTarget < root.ReadWord(0x58))
        {
            lastTarget = unchecked((byte)root.ReadWord(0x58));
        }
        root = root.WriteByte(0x2F, lastTarget);
        hierarchy = hierarchy.Write(appended.Address, root);
        int textureOffset = TextureOffset(effect);
        _ = checked(textureOffset + 0x10000);
        if ((effect.Parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Search requires its actual animation/TIM load owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Search requires its loaded arena."), textureOffset);
            memory = memory with { Textures = textures };
        }
        NativeBattleSearchState retained = memory.Search ?? new(null, [], null, null, null, 0, 0);
        memory = memory with { Search = retained with { Global2598F18 = 0, Global2596610 = 0 } };
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = MainDescriptor }, random);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int index = Enumerable.Range(0, effect.Pools.Length).FirstOrDefault(slot => effect.Pools[slot].DescriptorAddress == MainDescriptor, -1);
        if (index < 0)
        {
            throw new NotSupportedException("Native Search completion has no retained root bank.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        uint delta = unchecked(address - pool.BankAddress);
        if (delta % pool.Stride != 0x62 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Search completion pointer is not a root BYTE62 owner.");
        }
        int slot = (int)(delta / pool.Stride);
        return effect with { Pools = effect.Pools.SetItem(index, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(0x62, value)) }) };
    }

    public static NativeBattleSearchFrame Advance7552B0(NativeBattleSearchFrame frame, NativeBattleSearchInputs inputs, NativeBattleSearchResources resources)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Search lost its action parameters.");
        NativeBattleEffectHierarchy state = new(frame.Effect.Pools);
        state.Validate();
        if (frame.Memory.Search is null)
        {
            throw new InvalidDataException("Native Search lost its explicitly initialized shared state.");
        }
        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep rootStep = state.Advance(0, (current, address) =>
        {
            state = current;
            NativeBattleModulePoolTask task = state.Read(address);
            Require(task, 0x7552B0);
            int baseOffset = TextureOffset(frame.Effect);
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = baseOffset + (odd ? 0 : 0x8000) }, Memory = frame.Memory with { Search = Shared() with { ViewCopy = MatrixBytes(inputs.View), PrivateAuxiliaryOffset = baseOffset + (odd ? 0x8000 : 0x10000) } } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(), inputs.ReadPoseSource);
            Store(address, task);
            switch (Stage(task))
            {
                case 0:
                case 1:
                case 5:
                    Store(address, Next(task));
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x63, 1)));
                    Append(1, 0x755480, 0x58, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        task = unchecked((sbyte)task.ReadByte(0x2A)) < Word(task, 0x58) ? task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, 3) : Next(task);
                        Store(address, task);
                    }
                    break;
                case 6:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        Store(address, Next(task));
                    }
                    break;
                case 7:
                    Store(address, Next(task));
                    Emit(new NativeBattleSearchCommonTextureLoad(baseOffset, address + 0x62));
                    break;
                case 8:
                    if (task.ReadByte(0x62) != 0)
                    {
                        Store(address, Next(task));
                    }
                    break;
                case 9:
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                    break;
                case 10:
                    break;
                default:
                    throw new NotSupportedException("Native Search root stage is unsupported.");
            }
            int survivors = 0;
            for (int pool = 1; pool < 8; pool++)
            {
                NativeBattleEffectHierarchyStep step = state.Advance(pool, (children, child) =>
                {
                    state = children;
                    int result = Run(child);
                    return new(state, result);
                });
                state = step.Hierarchy;
                survivors += step.Survivors;
            }
            task = state.Read(address).WriteWord(0x5E, unchecked((ushort)survivors));
            Store(address, Add(task, 0x5C, 1));
            int completed = Finish(address);
            return new(state, completed);
        });
        state = rootStep.Hierarchy;
        return frame with { Effect = frame.Effect with { Pools = state.Pools, ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : MainDescriptor } };

        int Run(uint address)
        {
            uint callback = state.Read(address).Callback;
            switch (callback)
            {
                case 0x755480:
                    Target(address);
                    break;
                case 0x7555B0:
                    Dog(address);
                    break;
                case 0x7557B0:
                    Model(address);
                    break;
                case 0x755C70:
                    Emitter(address);
                    break;
                case 0x7581D0:
                    Sprite(address);
                    break;
                case 0x755EE0:
                    Mesh(address);
                    break;
                case 0x758CB0:
                    Program(address);
                    break;
                default:
                    throw new NotSupportedException($"Native Search callback{callback:X8} is unsupported.");
            }
            return Finish(address);
        }

        void Target(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, Models(), inputs.ReadPoseSource);
            int actor = task.ReadByte(0x2D);
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattlePoseSource source = inputs.ReadPoseSource(actor);
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Search target bounds require the actual pose owner.");
            NativeBattleDecodedPose pose = sequence.DecodedPose ?? source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Search target bounds require its pose selector."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Search target bounds require its frame."), sequence.PoseFlags109);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
            Store(address, task);
            switch (Stage(task))
            {
                case 0:
                    byte[] dog = new byte[84];
                    ImmutableArray<byte> world = World(actor);
                    world.AsSpan().CopyTo(dog.AsSpan(8));
                    BinaryPrimitives.WriteUInt16LittleEndian(dog.AsSpan(0x4A), (ushort)((NativeWorldScriptedPathMath.NativeAngle(unchecked((short)(ReadWord(world, 0) - BinaryPrimitives.ReadInt16LittleEndian(dog))), ReadWord(world, 4)) - 2048) & 4095));
                    frame = frame with { Memory = frame.Memory with { Search = Shared() with { DogControl = dog.ToImmutableArray() } } };
                    Append(2, 0x7555B0, 0x48, address);
                    Store(address, Next(task));
                    break;
                case 1:
                    if (DogWord(0x48) == 1)
                    {
                        int headerIndex = parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A));
                        if ((uint)headerIndex >= inputs.Packets.Headers.Length)
                        {
                            throw new NotSupportedException("Native Search contact header leaves its physical arena.");
                        }
                        int contact = checked((inputs.Packets.Headers[headerIndex].FirstContactIndex ?? throw new NotSupportedException("Native Search lost its physical contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        Emit(new NativeBattleSearchContact(contact));
                        Store(address, Next(task));
                    }
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0 || DogWord(0x4C) == 1)
                    {
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                    uint root = task.ReadDword(0x10);
                    Store(root, state.Read(root).WriteByte(0x63, 0));
                    break;
                case 4:
                    break;
                default:
                    throw new NotSupportedException("Native Search target stage is unsupported.");
            }
        }

        void Dog(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            SetDog(0x46, DogWord(0x46) + 1);
            if (DogWord(0x40) != DogWord(0x42))
            {
                SetDog(0x40, DogWord(0x42));
                SetDog(0x46, 0);
                if (DogWord(0x40) == 1)
                {
                    uint child = Append(3, 0x7557B0, 0x154, address);
                    NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(state.Read(child), child, resources.ModelProfile.Model, 3, resources.ModelProfile.DefaultColor44);
                    Store(child, initialized.Task);
                    SetModel(initialized.State);
                }
            }
            if (DogWord(0x42) != DogWord(0x44))
            {
                SetDog(0x42, DogWord(0x44));
            }
            switch (Stage(task))
            {
                case 0:
                    Emit(new NativeBattleSearchUploadModelTim());
                    task = Next(task);
                    break;
                case 1:
                    if (DogWord(0x44) == DogWord(0x40) && DogWord(0x40) == 0)
                    {
                        SetDog(0x44, 1);
                        task = Next(task);
                    }
                    break;
                case 2:
                    if (DogWord(0x40) >= 1)
                    {
                        task = Next(task);
                    }
                    break;
                case 3:
                    if (DogWord(0x46) >= 155)
                    {
                        SetDog(0x48, 1);
                        task = Next(task.WriteWord(0x26, (ushort)(task.ReadWord(0x26) | 1)));
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new NotSupportedException("Native Search dog stage is unsupported.");
            }
            Store(address, task);
        }

        void Model(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            int actor = task.ReadByte(0x2D);
            switch (Stage(task))
            {
                case 0:
                    Emit(new NativeBattleSearchBindTargetResource(actor));
                    Emit(new NativeBattleSearchSelectActorSequence(actor, 1));
                    ImmutableArray<byte> world = World(actor);
                    task = WriteBytes(task, 0x148, world).WriteWord(0x148, unchecked((ushort)(ReadWord(world, 0) - 384))).WriteWord(0x14A, 0).WriteWord(0x14C, unchecked((ushort)(ReadWord(world, 4) - 1536)));
                    task = WriteBytes(task, 0x140, Slice(task, 0x148, 8));
                    task = Add(task, 0x144, 4096);
                    task = WriteBytes(task, 0x4C, Slice(task, 0x140, 8)).WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
                    task = Pose(task, address, 0, 0);
                    Emit(new NativeBattleSearchSound(resources.SoundAppearance, 0, 128));
                    Emit(new NativeBattleSearchVoice(resources.VoiceAppearance, 128, 1, 127));
                    task = Next(task);
                    break;
                case 1:
                    task = Pose(task, address, 0, 1);
                    task = Add(task, 0x150, 256);
                    if (Word(task, 0x150) >= 4096)
                    {
                        task = WriteBytes(task.WriteWord(0x150, 4096), 0x4C, Slice(task, 0x148, 8));
                        task = Pose(task, address, 1, 0);
                        Store(address, task);
                        uint emitter = Append(4, 0x755C70, 0x90, address);
                        Store(emitter, WriteBytes(state.Read(emitter), 0x1C, Slice(task, 0x4C, 8)).WriteDword(0x88, address + 0x30));
                        task = Next(task);
                    }
                    else
                    {
                        task = Interpolate(task);
                    }
                    break;
                case 2:
                    if (task.ReadWord(0x136) == 6)
                    {
                        Emit(new NativeBattleSearchSound(resources.SoundPose1, 1, 128));
                    }
                    bool completed = false;
                    task = Pose(task, address, 0, 2, value => completed = value);
                    if (completed)
                    {
                        task = Pose(task, address, 2, 0);
                        task = WriteBytes(WriteBytes(task, 0x140, Slice(task, 0x4C, 8)), 0x148, World(actor));
                        task = Add(task, 0x14C, -640).WriteWord(0x3E, 160).WriteWord(0x150, 0);
                        task = Next(task);
                    }
                    break;
                case 3:
                    task = Pose(task, address, 0, 1);
                    task = Add(task, 0x150, 683);
                    if (Word(task, 0x150) >= 4096)
                    {
                        task = WriteBytes(task.WriteWord(0x150, 4096), 0x4C, World(actor));
                        Emit(new NativeBattleSearchSelectActorSequence(actor, 2));
                        task = Pose(task, address, 3, 0).WriteWord(0x3E, 0);
                        task = Next(task);
                    }
                    else
                    {
                        task = Interpolate(task);
                    }
                    break;
                case 4:
                    bool poseCompleted = false;
                    task = Pose(task, address, 0, 2, value => poseCompleted = value);
                    if (poseCompleted)
                    {
                        task = Pose(task, address, 4, 0);
                        Emit(new NativeBattleSearchVoice(resources.VoiceDeparture, 128, 1, 127));
                        task = WriteBytes(WriteBytes(task, 0x140, Slice(task, 0x4C, 8)), 0x148, Slice(task, 0x4C, 8));
                        task = Add(task, 0x14C, 4096).WriteWord(0x150, 0);
                        task = Next(task);
                    }
                    if (task.ReadWord(0x136) == 11)
                    {
                        Store(address, task);
                        uint program = Append(6, 0x758CB0, 0x17C, address);
                        NativeBattleVector position = OwnedBone(address + 0x30, 24);
                        NativeBattleModulePoolTask child = Position(state.Read(program), 0x1C, position).WriteWord(0x1E, 0).WriteDword(0x74, resources.ProgramSource.Address).WriteDword(0x78, 0xE0);
                        Store(program, child);
                    }
                    break;
                case 5:
                    task = Pose(task, address, 0, 1);
                    task = Add(task, 0x150, 256);
                    if (Word(task, 0x150) >= 4096)
                    {
                        task = WriteBytes(task.WriteWord(0x150, 4096), 0x4C, Slice(task, 0x148, 8)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
                        task = Next(task);
                    }
                    else
                    {
                        task = Interpolate(task);
                    }
                    break;
                case 6:
                    break;
                default:
                    throw new NotSupportedException("Native Search model stage is unsupported.");
            }
            for (int axis = 0; axis < 3; axis++)
            {
                task = Add(task, 0x4C + axis * 2, Word(task, 0x124 + axis * 2));
            }
            Store(address, task);
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                Sync();
                NativeBattleSearchModelDrawStep drawn = inputs.DrawModel(frame, task, ModelState(address));
                frame = drawn.Frame;
                task = drawn.Task;
            }
            frame = frame with { Memory = frame.Memory with { Search = Shared() with { PublishedModelMatrix = Slice(task, 0x70, 32) } } };
            task = task.WriteDword(0x84, unchecked((uint)Word(task, 0x4C))).WriteDword(0x88, unchecked((uint)Word(task, 0x4E))).WriteDword(0x8C, unchecked((uint)Word(task, 0x50)));
            Store(address, task);
        }

        void Emitter(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (Stage(task) == 0)
            {
                if (Word(task, 0x24) >= 37)
                {
                    task = Next(task);
                }
            }
            else if (Stage(task) == 1)
            {
                for (int kind = 0; kind < 2; kind++)
                {
                    for (int member = 0; member < 2; member++)
                    {
                        uint child = Append(kind == 0 ? 4 : 5, kind == 0 ? 0x7581D0u : 0x755EE0u, kind == 0 ? 0x90 : 0x8C, address);
                        NativeBattleVector position = OwnedBone(task.ReadDword(0x88), member == 0 ? (byte)30 : (byte)33);
                        position = position with { X = unchecked((short)(position.X + (Rand() & 127) - 64)), Z = unchecked((short)(position.Z + (Rand() & 127) - 64)) };
                        NativeBattleModulePoolTask particle = Position(state.Read(child), 0x1C, position);
                        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), (short)(((Rand() & 511) - 256) & 4095));
                        if (kind == 0)
                        {
                            particle = particle.WriteWord(0x82, unchecked((ushort)-(Rand() % 96)));
                            NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, (short)(Rand() % 2560 + 512)));
                            particle = Position(particle, 0x78, velocity);
                        }
                        else
                        {
                            rotation = NativeBattleCuragaMatrices.RotateX8DD7E0(rotation, (short)((Rand() % 384 + 256) & 4095));
                            NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, (short)(Rand() % 2048 + 512)));
                            particle = Position(particle, 0x74, velocity);
                        }
                        Store(child, particle);
                    }
                }
                if (Word(task, 0x24) >= 50)
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
            }
            else if (Stage(task) != 2)
            {
                throw new NotSupportedException("Native Search emitter stage is unsupported.");
            }
            Store(address, task);
        }

        void Sprite(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (Stage(task) == 0)
            {
                task = Next(task.WriteDword(0x4C, resources.Sprite8.Address).WriteWord(0x56, unchecked((ushort)-64)).WriteWord(0x52, 7));
            }
            else if (Stage(task) == 1)
            {
                task = Add(task, 0x50, 1);
                if (Word(task, 0x50) > Word(task, 0x52))
                {
                    task = Next(task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                }
            }
            else if (Stage(task) != 2)
            {
                throw new NotSupportedException("Native Search sprite stage is unsupported.");
            }
            task = Add(task, 0x7A, Word(task, 0x82));
            task = Add(Add(task, 0x78, -Word(task, 0x78) / 8), 0x7C, -Word(task, 0x7C) / 8);
            for (int axis = 0; axis < 3; axis++)
            {
                task = Add(task, 0x1C + axis * 2, Word(task, 0x78 + axis * 2) / 16);
            }
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                Emit(new NativeBattleSearchSprite(task));
            }
            Store(address, task);
        }

        void Mesh(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (Stage(task) == 0)
            {
                task = Next(task.WriteDword(0x4C, resources.ParticleMesh.Address).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteWord(0x48, (ushort)(Rand() & 4095)));
            }
            else if (Stage(task) == 1)
            {
                task = Add(task, 0x48, 64);
                if (Word(task, 0x1E) >= 0)
                {
                    task = task.WriteWord(0x1E, 0);
                }
                if (Word(task, 0x24) >= 10)
                {
                    int scale = unchecked((int)task.ReadDword(0x38) - 1024);
                    if (scale <= 0)
                    {
                        scale = 16;
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    task = task.WriteDword(0x38, unchecked((uint)scale)).WriteDword(0x34, unchecked((uint)scale)).WriteDword(0x30, unchecked((uint)scale));
                }
            }
            else if (Stage(task) != 2)
            {
                throw new NotSupportedException("Native Search mesh stage is unsupported.");
            }
            task = Add(task, 0x74, -Word(task, 0x74) / 16);
            task = Add(task, 0x76, 128);
            task = Add(task, 0x76, -Word(task, 0x76) / 16);
            task = Add(task, 0x78, -Word(task, 0x78) / 16);
            for (int axis = 0; axis < 3; axis++)
            {
                task = Add(task, 0x1C + axis * 2, Word(task, 0x74 + axis * 2) / 16);
            }
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                Emit(new NativeBattleSearchMesh(task));
            }
            Store(address, task);
        }

        void Program(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            int phase = Stage(task);
            if (phase == 0 && Word(task, 0x24) >= 8)
            {
                task = NativeBattleSearchProgramDrawing.InitializeWorkspace7016B0(task, resources.ProgramSource).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x86, unchecked((ushort)-256));
                Sync();
                NativeBattleSearchProgramStep advanced = inputs.AdvanceProgram(frame, task);
                frame = advanced.Frame;
                task = Next(advanced.Task);
                Emit(new NativeBattleSearchSound(resources.SoundProgram, 1, 128));
            }
            else if (phase == 1)
            {
                Sync();
                NativeBattleSearchProgramStep advanced = inputs.AdvanceProgram(frame, task);
                frame = advanced.Frame;
                task = advanced.Task;
                if (advanced.Remaining == 0)
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
            }
            else if (phase != 0 && phase != 2)
            {
                throw new NotSupportedException("Native Search GF program stage is unsupported.");
            }
            Store(address, task);
        }

        NativeBattleModulePoolTask Pose(NativeBattleModulePoolTask task, uint address, short animation, int mode, Action<bool>? completed = null)
        {
            NativeBattleEffectOwnedModelState model = ModelState(address);
            NativeBattleEffectOwnedModelStep step = mode == 0 ? NativeBattleEffectOwnedModel.Select8DD190(task, model, resources.ModelProfile.Model, animation) : mode == 1 ? NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(task, model, resources.ModelProfile.Model) : NativeBattleEffectOwnedModel.AdvanceOnce8DD220(task, model, resources.ModelProfile.Model);
            SetModel(step.State);
            completed?.Invoke(step.Completed);
            return step.Task;
        }
        NativeBattleEffectOwnedModelState ModelState(uint address) => Shared().Models.Single(model => model.TaskAddress == address);
        void SetModel(NativeBattleEffectOwnedModelState model)
        {
            ImmutableArray<NativeBattleEffectOwnedModelState> models = Shared().Models;
            int index = -1;
            for (int slot = 0; slot < models.Length; slot++)
            {
                if (models[slot].TaskAddress == model.TaskAddress)
                {
                    index = slot;
                    break;
                }
            }
            frame = frame with { Memory = frame.Memory with { Search = Shared() with { Models = index < 0 ? models.Add(model) : models.SetItem(index, model) } } };
        }
        NativeBattleSearchState Shared() => frame.Memory.Search ?? throw new InvalidDataException("Native Search shared state is absent.");
        short DogWord(int offset) => ReadWord(Shared().DogControl ?? throw new InvalidDataException("Native Search dog state has not been cleared by its target producer."), offset);
        void SetDog(int offset, int value)
        {
            byte[] bytes = (Shared().DogControl ?? throw new InvalidDataException("Native Search dog state is absent.")).ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), unchecked((ushort)value));
            frame = frame with { Memory = frame.Memory with { Search = Shared() with { DogControl = bytes.ToImmutableArray() } } };
        }
        ImmutableArray<byte> World(int actor)
        {
            ImmutableArray<byte> bytes = inputs.ReadTargetWorld8(actor);
            if (bytes.IsDefault || bytes.Length != 8)
            {
                throw new InvalidDataException("Native Search requires the target's exact eight-byte world-position owner.");
            }
            return bytes;
        }
        uint Append(int pool, uint callback, int requested, uint parent)
        {
            NativeBattleEffectHierarchyAppend append = state.Append8DC540(pool, callback, requested, parent, Select);
            state = append.Hierarchy;
            return append.Address != 0 ? append.Address : throw new NotSupportedException("Native Search reached a task allocation without its required physical owner.");
        }
        void Store(uint address, NativeBattleModulePoolTask task)
        {
            NativeBattleModulePoolTask current = state.Read(address);
            state = state.Write(address, current with { Payload = task.Payload.SetItem(0x28 - 12, current.ReadByte(0x28)) });
        }
        int Finish(uint address)
        {
            NativeBattleModulePoolTask task = Add(state.Read(address), 0x24, 1);
            Store(address, task);
            if ((task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0)
            {
                state = state.Complete8DC530(address);
                return 2;
            }
            return 0;
        }
        void Sync() => frame = frame with { Effect = frame.Effect with { Pools = state.Pools } };
        void Emit(NativeBattleSearchOperation operation)
        {
            Sync();
            frame = inputs.Execute(frame, operation);
            state = new(frame.Effect.Pools);
        }
        NativeBattleVector OwnedBone(uint pointer, byte selector)
        {
            Sync();
            return inputs.ReadOwnedBone(frame, pointer, selector);
        }
        int Rand()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value;
        }
    }

    private static int TextureOffset(NativeBattleOriginalEffectState effect) => effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x2594D98).ArenaOffset;
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short ReadWord(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static int Stage(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, int offset, NativeBattleVector vector) => task.WriteWord(offset, unchecked((ushort)vector.X)).WriteWord(offset + 2, unchecked((ushort)vector.Y)).WriteWord(offset + 4, unchecked((ushort)vector.Z));
    private static ImmutableArray<byte> Slice(NativeBattleModulePoolTask task, int offset, int count) => task.Payload.AsSpan().Slice(offset - 12, count).ToArray().ToImmutableArray();
    private static NativeBattleModulePoolTask WriteBytes(NativeBattleModulePoolTask task, int offset, ImmutableArray<byte> bytes)
    {
        byte[] payload = task.Payload.ToArray();
        bytes.AsSpan().CopyTo(payload.AsSpan(offset - 12));
        return task with { Payload = payload.ToImmutableArray() };
    }
    private static ImmutableArray<byte> MatrixBytes(NativeBattleActionMatrix matrix)
    {
        matrix.Validate();
        byte[] bytes = new byte[32];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), matrix.Rotation[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), matrix.Z);
        return bytes.ToImmutableArray();
    }
    private static NativeBattleModulePoolTask Interpolate(NativeBattleModulePoolTask task)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            int start = Word(task, 0x140 + axis * 2);
            int end = Word(task, 0x148 + axis * 2);
            task = task.WriteWord(0x4C + axis * 2, unchecked((ushort)(start + unchecked((end - start) * Word(task, 0x150)) / 4096)));
        }
        return task;
    }
    private static void Require(NativeBattleModulePoolTask task, uint callback)
    {
        if (task.Callback != callback)
        {
            throw new NotSupportedException("Native Search root callback changed its contract.");
        }
    }
}
