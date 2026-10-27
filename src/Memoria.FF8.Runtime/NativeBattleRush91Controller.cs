using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Rush91's physical hierarchy, independent model, retained dog state and neutral-node owners.</summary>
public static class NativeBattleRush91Controller
{
    public const uint MainDescriptor = 0x2599108;
    public const uint MainBank = 0x2599040;
    public const uint ModelBank = 0x259E768;
    public const uint ModelDescriptor = 0x259E8C0;
    private const uint ActionParameters = 0x1D99A78;

    public static NativeBattleRush91Frame Initialize759370(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleRush91Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 91, Entry: 0x759370, Initializer: 0x759350 } || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Rush91 requires its original759370 entry and actual action parameters.");
        }
        if (memory.Rush91MorphScratch is not { IsDefault: false, Length: 0x5A0 } || memory.FullscreenOverlay is null || memory.FullscreenOverlayPause2795914 is null)
        {
            throw new NotSupportedException("Native Rush requires its retained morph and fixed fullscreen packet owners from genuine cold initialization.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(0x259DB90, 0x259DBA0, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(0x2599010, 0x2598F38, 0x48, 3),
            NativeBattleEffectTaskPool.Initialize(ModelDescriptor, ModelBank, 0x158, 1),
            NativeBattleEffectTaskPool.Initialize(0x259D8F0, 0x259D648, 0x2A4, 1),
            NativeBattleEffectTaskPool.Initialize(0x2599118, 0x2599128, 0x90, 80),
            NativeBattleEffectTaskPool.Initialize(0x259BE30, 0x259BE40, 0x8C, 40),
            NativeBattleEffectTaskPool.Initialize(0x259D630, 0x259D420, 0x108, 2),
            NativeBattleEffectTaskPool.Initialize(0x259DB80, 0x259D900, 0x40, 10)
        ]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x759510, 0x64);
        hierarchy = appended.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Rush91 requires an original header/contact owner.");
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
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Rush91 requires its actual animation/TIM load owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Rush91 requires its loaded arena."), textureOffset);
            memory = memory with { Textures = textures };
        }
        NativeBattleRush91State retained = memory.Rush91 ?? new(null, [], null, null, null, 0, 0, NativeBattleRecover93NodeState.FromColdProcess());
        memory = memory with { Rush91 = retained with { Global259EE98 = 0, Global259BE2C = 0 } };
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = MainDescriptor }, random);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int index = Enumerable.Range(0, effect.Pools.Length).FirstOrDefault(slot => effect.Pools[slot].DescriptorAddress == MainDescriptor, -1);
        if (index < 0)
        {
            throw new NotSupportedException("Native Rush91 completion has no retained root bank.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        uint delta = unchecked(address - pool.BankAddress);
        if (delta % pool.Stride != 0x62 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Rush91 completion pointer is not a root BYTE62 owner.");
        }
        int slot = (int)(delta / pool.Stride);
        return effect with { Pools = effect.Pools.SetItem(index, pool.WriteSlot(slot, pool.Slots[slot].WriteByte(0x62, value))) };
    }

    public static NativeBattleRush91Frame Advance759510(NativeBattleRush91Frame frame, NativeBattleRush91Inputs inputs, NativeBattleRush91Resources resources)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Rush91 lost its action parameters.");
        NativeBattleEffectHierarchy state = new(frame.Effect.Pools);
        state.Validate();
        if (frame.Memory.Rush91 is null)
        {
            throw new InvalidDataException("Native Rush91 lost its explicitly initialized shared state.");
        }
        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep rootStep = state.Advance(0, (current, address) =>
        {
            state = current;
            NativeBattleModulePoolTask task = state.Read(address);
            Require(task, 0x759510);
            int baseOffset = TextureOffset(frame.Effect);
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = baseOffset + (odd ? 0 : 0x8000) }, Memory = frame.Memory with { Rush91 = Shared() with { ViewCopy = MatrixBytes(inputs.View), PrivateAuxiliaryOffset = baseOffset + (odd ? 0x8000 : 0x10000) } } };
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
                        NativeBattleRecover93NodeFrame cleared = NativeBattleRush91Nodes.ClearStorage(new(frame.Memory, frame.Effect, Shared().Nodes, frame.Random), baseOffset);
                        frame = frame with { Memory = cleared.Memory with { Rush91 = Shared() with { Nodes = cleared.State } } };
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x63, 1)));
                    Append(1, 0x759770, 0x58, address);
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
                    Emit(new NativeBattleRush91CommonTextureLoad(baseOffset, address + 0x62));
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
                    throw new NotSupportedException("Native Rush91 root stage is unsupported.");
            }
            NativeBattleRecover93NodeFrame pass = NativeBattleRush91Nodes.BeginPass(new(frame.Memory, frame.Effect, Shared().Nodes, frame.Random), inputs.View, frame.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Rush91 lost its actual private cursor."));
            frame = frame with { Memory = frame.Memory with { Rush91 = Shared() with { Nodes = pass.State } } };
            Store(address, state.Read(address).WriteWord(0x5E, 0));
            int survivors = 0;
            for (int pool = 1; pool < 9; pool++)
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
                case 0x759770:
                    Target(address);
                    break;
                case 0x7598C0:
                    Dog(address);
                    break;
                case 0x75E910:
                    Model(address);
                    break;
                case 0x759B40:
                    Nodes(address);
                    break;
                case 0x7611B0:
                    Sprite(address);
                    break;
                case 0x75EEC0:
                    Mesh(address);
                    break;
                case 0x8DDEC0:
                    Sync();
                    NativeBattleRush91OverlayStep overlay = inputs.AdvanceOverlay(frame, address, inputs.OverlayPause2795914);
                    frame = overlay.Frame;
                    state = new(frame.Effect.Pools);
                    return overlay.Result;
                case 0x761B00:
                    Program(address);
                    break;
                default:
                    throw new NotSupportedException($"Native Rush91 callback{callback:X8} is unsupported.");
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
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Rush91 target bounds require the actual pose owner.");
            NativeBattleDecodedPose pose = sequence.DecodedPose ?? source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Rush91 target bounds require its pose selector."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Rush91 target bounds require its frame."), sequence.PoseFlags109);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
            Store(address, task);
            switch (Stage(task))
            {
                case 0:
                    byte[] dog = new byte[84];
                    NativeBattleVector target = Bone(actor, 241);
                    BinaryPrimitives.WriteInt16LittleEndian(dog.AsSpan(8), target.X);
                    BinaryPrimitives.WriteInt16LittleEndian(dog.AsSpan(10), Math.Min(target.Y, (short)-768));
                    BinaryPrimitives.WriteInt16LittleEndian(dog.AsSpan(12), target.Z);
                    BinaryPrimitives.WriteUInt16LittleEndian(dog.AsSpan(0x4A), (ushort)((NativeWorldScriptedPathMath.NativeAngle(unchecked((short)(target.X - BinaryPrimitives.ReadInt16LittleEndian(dog))), target.Z) - 2048) & 4095));
                    frame = frame with { Memory = frame.Memory with { Rush91 = Shared() with { DogControl = dog.ToImmutableArray() } } };
                    Append(2, 0x7598C0, 0x48, address);
                    Store(address, Next(task));
                    break;
                case 1:
                    if (DogWord(0x48) == 1)
                    {
                        int headerIndex = parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A));
                        if ((uint)headerIndex >= inputs.Packets.Headers.Length)
                        {
                            throw new NotSupportedException("Native Rush91 contact header leaves its physical arena.");
                        }
                        int contact = checked((inputs.Packets.Headers[headerIndex].FirstContactIndex ?? throw new NotSupportedException("Native Rush91 lost its physical contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        Emit(new NativeBattleRush91Contact(contact));
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
                    throw new NotSupportedException("Native Rush91 target stage is unsupported.");
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
                    uint child = Append(3, 0x75E910, 0x158, address);
                    NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(state.Read(child), child, resources.Model, 3, resources.DefaultColor44);
                    Store(child, initialized.Task);
                    SetModel(initialized.State);
                }
                else if (DogWord(0x40) == 2)
                {
                    uint child = Append(8, NativeBattleFullscreenOverlay.Callback, 0x40, address);
                    Store(child, state.Read(child).WriteDword(0x34, resources.OverlayProgram.Address).WriteWord(0x38, 0));
                    child = Append(4, 0x759B40, 0x2A4, address);
                    Store(child, state.Read(child).WriteDword(0x30, 0x16CB3FC).WriteWord(0x298, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 15));
                }
            }
            if (DogWord(0x42) != DogWord(0x44))
            {
                SetDog(0x42, DogWord(0x44));
            }
            switch (Stage(task))
            {
                case 0:
                    Emit(new NativeBattleRush91UploadModelTim());
                    task = Next(task);
                    break;
                case 1:
                    if (RequestMode(1))
                    {
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
                    if (DogWord(0x40) >= 2)
                    {
                        task = Next(task.WriteWord(0x26, (ushort)(task.ReadWord(0x26) | 1)));
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new NotSupportedException("Native Rush91 dog stage is unsupported.");
            }
            Store(address, task);
        }

        void Model(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            switch (Stage(task))
            {
                case 0:
                    task = task.WriteDword(0x4C, DogDword(8)).WriteDword(0x50, DogDword(12)).WriteWord(0x4E, 0).WriteWord(0x128, unchecked((ushort)-256));
                    int approach = unchecked(Scale(task) * 2000) >> 12;
                    task = Add(task, 0x50, unchecked(approach + 9216)).WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
                    task = Next(Pose(task, address, 0, 0));
                    Emit(new NativeBattleRush91Sound(resources.SoundAppearance, 0, 128));
                    break;
                case 1:
                    task = Pose(task, address, 0, 1);
                    if (Word(task, 0x136) >= 12)
                    {
                        Store(address, task);
                        uint child = Append(7, 0x761B00, 0x108, address);
                        Store(child, state.Read(child).WriteDword(0x74, resources.ProgramSource.Address).WriteDword(0x78, 0x70).WriteWord(0x80, 0).WriteWord(0x82, 0).WriteDword(0x104, address + 0x30));
                        task = Next(task);
                    }
                    break;
                case 2:
                    task = Pose(task, address, 0, 1);
                    if (Word(task, 0x136) >= 24)
                    {
                        task = Next(Pose(task, address, 1, 0));
                    }
                    break;
                case 3:
                    bool completed = false;
                    task = Pose(task, address, 0, 2, value => completed = value);
                    if (completed)
                    {
                        task = Next(Pose(task, address, 2, 0));
                    }
                    break;
                case 4:
                    task = Height(task);
                    completed = false;
                    task = Pose(task, address, 0, 2, value => completed = value);
                    if (completed)
                    {
                        task = Next(Pose(Add(task, 0x50, -512), address, 3, 0));
                    }
                    break;
                case 5:
                    task = Height(Pose(task, address, 0, 1));
                    if (Word(task, 0x136) >= 0)
                    {
                        task = Next(task);
                    }
                    break;
                case 6:
                    task = Height(Pose(task, address, 0, 1));
                    if (RequestMode(2))
                    {
                        SetDog(0x48, 1);
                        task = Next(task);
                    }
                    break;
                case 7:
                    task = Height(Pose(task, address, 0, 1));
                    if (DogWord(0x40) >= 2)
                    {
                        task = Next(task.WriteWord(0x152, 8).WriteWord(0x134, 15).WriteWord(0x50, unchecked((ushort)(DogWord(12) - (unchecked(Scale(task) * 2000) >> 12)))).WriteWord(0x128, unchecked((ushort)(Word(task, 0x128) / 2))));
                    }
                    break;
                case 8:
                    task = Add(Pose(task, address, 0, 1), 0x134, -1);
                    if (Word(task, 0x134) <= 0)
                    {
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                    }
                    break;
                case 9:
                    break;
                default:
                    throw new NotSupportedException("Native Rush model phase is unsupported.");
            }
            task = Add(task, 0x126, Word(task, 0x152));
            task = Add(Add(task, 0x4E, Word(task, 0x126)), 0x50, Word(task, 0x128));
            if (Word(task, 0x4E) > 0)
            {
                task = task.WriteWord(0x4E, 0);
            }
            Store(address, task);
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                Sync();
                NativeBattleRush91ModelDrawStep drawn = inputs.DrawModel(frame, task, ModelState(address));
                frame = drawn.Frame;
                state = new(frame.Effect.Pools);
                task = drawn.Task;
            }
            Store(address, task);
            byte pose = task.ReadByte(0x9C);
            byte poseFrame = task.ReadByte(0xA2);
            if (pose == 0)
            {
                if (poseFrame == 1)
                {
                    EmitParticles(address, 2);
                }
                else if (poseFrame == 2)
                {
                    EmitParticles(address, 1);
                    EmitParticles(address, 3);
                }
                else if (poseFrame == 4)
                {
                    EmitParticles(address, 0);
                }
            }
            else if (pose == 1)
            {
                if (poseFrame == 1)
                {
                    EmitParticles(address, 2);
                    EmitParticles(address, 1);
                }
                else if (poseFrame == 2)
                {
                    EmitParticles(address, 3);
                    EmitParticles(address, 0);
                }
            }
            frame = frame with { Memory = frame.Memory with { Rush91 = Shared() with { PublishedModelMatrix = Slice(task, 0x70, 32) } } };
            task = task.WriteDword(0x84, unchecked((uint)Word(task, 0x4C))).WriteDword(0x88, unchecked((uint)Word(task, 0x4E))).WriteDword(0x8C, unchecked((uint)Word(task, 0x50)));
            Store(address, task);
        }

        int Scale(NativeBattleModulePoolTask task) => (inputs.ReadModel(task.ReadByte(0x2D)).Transform ?? throw new NotSupportedException("Native Rush requires the actual target scale WORD38.")).Scale38;
        NativeBattleModulePoolTask Height(NativeBattleModulePoolTask task)
        {
            int index = Word(task, 0x154);
            if (resources.HeightProgram.Data.IsDefault || resources.HeightProgram.Data.Length != 16 || (uint)index >= 8)
            {
                throw new InvalidDataException("Native Rush height index leaves its actual eight-WORD source.");
            }
            short height = ReadWord(resources.HeightProgram.Data, index * 2);
            task = task.WriteWord(0x150, unchecked((ushort)height));
            if (height != 4096)
            {
                task = Add(task, 0x154, 1);
            }
            return task.WriteWord(0x4E, unchecked((ushort)(unchecked(DogWord(10) * height) / 4096)));
        }

        void EmitParticles(uint modelAddress, int type)
        {
            byte selector = type switch { 0 => 30, 1 => 33, 2 => 28, 3 => 29, _ => throw new InvalidDataException("Native Rush particle type is unsupported.") };
            for (int member = 0; member < 4; member++)
            {
                uint child = Append(5, 0x7611B0, 0x90, modelAddress);
                NativeBattleVector position = OwnedBone(modelAddress + 0x30, selector);
                NativeBattleModulePoolTask particle = Position(state.Read(child), 0x1C, position);
                particle = Add(particle, 0x1C, (Rand() & 255) - 128);
                particle = Add(particle, 0x20, (Rand() & 255) - 128).WriteWord(0x1E, 0);
                NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), (short)(((Rand() & 1023) - 512) & 4095));
                particle = particle.WriteWord(0x82, unchecked((ushort)-(Rand() % 96)));
                NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, (short)((Rand() & 4095) + 512)));
                Store(child, Position(particle, 0x78, velocity));
            }
            for (int member = 0; member < 2; member++)
            {
                uint child = Append(6, 0x75EEC0, 0x8C, modelAddress);
                NativeBattleModulePoolTask particle = Position(state.Read(child), 0x1C, OwnedBone(modelAddress + 0x30, selector)).WriteWord(0x1E, 0);
                NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), (short)(((Rand() & 511) - 256) & 4095));
                rotation = NativeBattleCuragaMatrices.RotateX8DD7E0(rotation, (short)((Rand() % 384 + 256) & 4095));
                Store(child, Position(particle, 0x74, NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, (short)(Rand() % 2048 + 512)))));
            }
        }

        void Nodes(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            int phase = Stage(task);
            if (phase == 0 && Word(task, 0x24) >= Word(task, 0x298))
            {
                Sync();
                NativeBattleRecover93NodeFrame initialized = NativeBattleRush91Nodes.InitializeContext(new(frame.Memory, frame.Effect, Shared().Nodes, frame.Random), address, new(resources.Nodes.Data), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource);
                frame = frame with { Memory = initialized.Memory with { Rush91 = Shared() with { Nodes = initialized.State } }, Effect = initialized.Effect, Random = initialized.Random };
                state = new(frame.Effect.Pools);
                task = Next(state.Read(address));
                Store(address, task);
            }
            if (phase == 1 || phase == 0 && Stage(task) == 1)
            {
                Sync();
                NativeBattleRush91NodesStep advanced = inputs.AdvanceNodes(frame, address);
                frame = advanced.Frame;
                state = new(frame.Effect.Pools);
                task = state.Read(address);
                if (advanced.Complete)
                {
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                }
            }
            else if (phase != 0 && phase != 2)
            {
                throw new NotSupportedException("Native Rush91 neutral context stage is unsupported.");
            }
        }

        void Sprite(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (Stage(task) == 0)
            {
                task = Next(task.WriteDword(0x4C, resources.Sprite12.Address).WriteWord(0x56, unchecked((ushort)-64)).WriteWord(0x52, 7));
            }
            else if (Stage(task) == 1)
            {
                task = Add(task, 0x50, 1);
                if (Word(task, 0x50) > Word(task, 0x52))
                {
                    task = Next(task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                }
                task = Add(task, 0x7A, Word(task, 0x82));
                task = Add(Add(task, 0x78, -Word(task, 0x78) / 8), 0x7C, -Word(task, 0x7C) / 8);
                for (int axis = 0; axis < 3; axis++)
                {
                    task = Add(task, 0x1C + axis * 2, Word(task, 0x78 + axis * 2) / 16);
                }
            }
            else if (Stage(task) != 2)
            {
                throw new NotSupportedException("Native Rush sprite phase is unsupported.");
            }
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                Emit(new NativeBattleRush91Sprite(task));
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
                task = Add(task, 0x76, 128);
                for (int axis = 0; axis < 3; axis++)
                {
                    int offset = 0x74 + axis * 2;
                    task = Add(task, offset, -Word(task, offset) / 16);
                    task = Add(task, 0x1C + axis * 2, Word(task, offset) / 16);
                }
            }
            else if (Stage(task) != 2)
            {
                throw new NotSupportedException("Native Rush mesh phase is unsupported.");
            }
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                Emit(new NativeBattleRush91Mesh(task));
            }
            Store(address, task);
        }

        void Program(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            int phase = Stage(task);
            if (phase == 0)
            {
                task = NativeBattleRush91ProgramDrawing.InitializeWorkspace761B60(task, resources.ProgramSource).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x86, unchecked((ushort)-256));
            }
            if (phase is 0 or 1)
            {
                task = Position(task, 0x1C, OwnedBone(task.ReadDword(0x104), 241));
                task = Add(task, 0x20, -256);
                if (phase == 1 && DogWord(0x40) >= 2)
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
                else
                {
                    Store(address, task);
                    Sync();
                    NativeBattleRush91ProgramStep advanced = inputs.AdvanceProgram(frame, task);
                    frame = advanced.Frame;
                    state = new(frame.Effect.Pools);
                    task = advanced.Task;
                    if (phase == 0)
                    {
                        task = Next(task);
                    }
                    else if (advanced.Remaining == 0)
                    {
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                }
            }
            else if (phase != 2)
            {
                throw new NotSupportedException("Native Rush GF phase is unsupported.");
            }
            Store(address, task);
        }

        NativeBattleVector Bone(int actor, byte selector) => NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(actor), inputs.ReadPoseSource(actor), selector, 0);
        bool RequestMode(short requested)
        {
            if (DogWord(0x44) != DogWord(0x40) || DogWord(0x40) != requested - 1)
            {
                return false;
            }
            SetDog(0x44, requested);
            return true;
        }
        uint DogDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian((Shared().DogControl ?? throw new InvalidDataException("Native Rush dog owner is absent.")).AsSpan().Slice(offset));

        NativeBattleModulePoolTask Pose(NativeBattleModulePoolTask task, uint address, short animation, int mode, Action<bool>? completed = null)
        {
            NativeBattleEffectOwnedModelState model = ModelState(address);
            NativeBattleEffectOwnedModelStep step = mode == 0 ? NativeBattleEffectOwnedModel.Select8DD190(task, model, resources.Model, animation) : mode == 1 ? NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(task, model, resources.Model) : NativeBattleEffectOwnedModel.AdvanceOnce8DD220(task, model, resources.Model);
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
            frame = frame with { Memory = frame.Memory with { Rush91 = Shared() with { Models = index < 0 ? models.Add(model) : models.SetItem(index, model) } } };
        }
        NativeBattleRush91State Shared() => frame.Memory.Rush91 ?? throw new InvalidDataException("Native Rush91 shared state is absent.");
        short DogWord(int offset) => ReadWord(Shared().DogControl ?? throw new InvalidDataException("Native Rush91 dog state has not been cleared by its target producer."), offset);
        void SetDog(int offset, int value)
        {
            byte[] bytes = (Shared().DogControl ?? throw new InvalidDataException("Native Rush91 dog state is absent.")).ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), unchecked((ushort)value));
            frame = frame with { Memory = frame.Memory with { Rush91 = Shared() with { DogControl = bytes.ToImmutableArray() } } };
        }
        uint Append(int pool, uint callback, int requested, uint parent)
        {
            NativeBattleEffectHierarchyAppend append = state.Append8DC540(pool, callback, requested, parent, Select);
            state = append.Hierarchy;
            return append.Address != 0 ? append.Address : throw new NotSupportedException("Native Rush91 reached a task allocation without its required physical owner.");
        }
        void Store(uint address, NativeBattleModulePoolTask task)
        {
            state = state.Write(address, task);
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
        void Emit(NativeBattleRush91Operation operation)
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
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }

    }

    private static int TextureOffset(NativeBattleOriginalEffectState effect) => effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x2598F28).ArenaOffset;
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short ReadWord(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static int Stage(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, int offset, NativeBattleVector vector) => task.WriteWord(offset, unchecked((ushort)vector.X)).WriteWord(offset + 2, unchecked((ushort)vector.Y)).WriteWord(offset + 4, unchecked((ushort)vector.Z));
    private static ImmutableArray<byte> Slice(NativeBattleModulePoolTask task, int offset, int count) => task.ReadRawPayloadSpan().Slice(offset - 12, count).ToArray().ToImmutableArray();
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
    private static void Require(NativeBattleModulePoolTask task, uint callback)
    {
        if (task.Callback != callback)
        {
            throw new NotSupportedException("Native Rush91 root callback changed its contract.");
        }
    }
}
