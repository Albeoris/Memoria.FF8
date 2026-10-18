using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Recover93's physical hierarchy, independent model, retained dog state and neutral-node owners.</summary>
public static class NativeBattleRecover93Controller
{
    public const uint MainDescriptor = 0x2592728;
    public const uint MainBank = 0x2592660;
    public const uint ModelBank = 0x2594C10;
    public const uint ModelDescriptor = 0x2594D50;
    private const uint ActionParameters = 0x1D99A78;

    public static NativeBattleRecover93Frame Initialize74E390(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleRecover93Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding.Entry != 0x74E390 || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Recover93 requires its original74E390 entry and actual action parameters.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(0x2594038, 0x2594048, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(0x2592630, 0x2592558, 0x48, 3),
            NativeBattleEffectTaskPool.Initialize(ModelDescriptor, ModelBank, 0x140, 1),
            NativeBattleEffectTaskPool.Initialize(0x2593D98, 0x2593AF0, 0x2A4, 1),
            NativeBattleEffectTaskPool.Initialize(0x2592738, 0x2592748, 0xB0, 20),
            NativeBattleEffectTaskPool.Initialize(0x2593510, 0x2593520, 0x58, 8),
            NativeBattleEffectTaskPool.Initialize(0x2593AD8, 0x25937E0, 0x17C, 2),
            NativeBattleEffectTaskPool.Initialize(0x2594028, 0x2593DA8, 0x40, 10)
        ]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x74E520, 0x64);
        hierarchy = appended.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Recover93 requires an original header/contact owner.");
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
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Recover93 requires its actual animation/TIM load owner.");
            textures = textures.Register5099A0(resources.Animation);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Recover93 requires its loaded arena."), textureOffset);
            memory = memory with { Textures = textures };
        }
        NativeBattleRecover93State retained = memory.Recover93 ?? new(null, [], null, null, null, 0, 0, NativeBattleRecover93NodeState.FromColdProcess());
        memory = memory with { Recover93 = retained with { Global2594D88 = 0, Global259350C = 0 } };
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = MainDescriptor }, random);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int index = Enumerable.Range(0, effect.Pools.Length).FirstOrDefault(slot => effect.Pools[slot].DescriptorAddress == MainDescriptor, -1);
        if (index < 0)
        {
            throw new NotSupportedException("Native Recover93 completion has no retained root bank.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        uint delta = unchecked(address - pool.BankAddress);
        if (delta % pool.Stride != 0x62 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Recover93 completion pointer is not a root BYTE62 owner.");
        }
        int slot = (int)(delta / pool.Stride);
        return effect with { Pools = effect.Pools.SetItem(index, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(0x62, value)) }) };
    }

    public static NativeBattleRecover93Frame Advance74E520(NativeBattleRecover93Frame frame, NativeBattleRecover93Inputs inputs, NativeBattleRecover93Resources resources)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Recover93 lost its action parameters.");
        NativeBattleEffectHierarchy state = new(frame.Effect.Pools);
        state.Validate();
        if (frame.Memory.Recover93 is null)
        {
            throw new InvalidDataException("Native Recover93 lost its explicitly initialized shared state.");
        }
        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep rootStep = state.Advance(0, (current, address) =>
        {
            state = current;
            NativeBattleModulePoolTask task = state.Read(address);
            Require(task, 0x74E520);
            int baseOffset = TextureOffset(frame.Effect);
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = baseOffset + (odd ? 0 : 0x8000) }, Memory = frame.Memory with { Recover93 = Shared() with { ViewCopy = MatrixBytes(inputs.View), PrivateAuxiliaryOffset = baseOffset + (odd ? 0x8000 : 0x10000) } } };
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
                        NativeBattleRecover93NodeFrame cleared = NativeBattleRecover93Nodes.ClearStorage(new(frame.Memory, frame.Effect, Shared().Nodes, frame.Random), baseOffset);
                        frame = frame with { Memory = cleared.Memory with { Recover93 = Shared() with { Nodes = cleared.State } } };
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x63, 1)));
                    Append(1, 0x74E780, 0x58, address);
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
                    Emit(new NativeBattleRecover93CommonTextureLoad(baseOffset, address + 0x62));
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
                    throw new NotSupportedException("Native Recover93 root stage is unsupported.");
            }
            NativeBattleRecover93NodeFrame pass = NativeBattleRecover93Nodes.BeginPass(new(frame.Memory, frame.Effect, Shared().Nodes, frame.Random), inputs.View, frame.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Recover93 lost its actual private cursor."));
            frame = frame with { Memory = frame.Memory with { Recover93 = Shared() with { Nodes = pass.State } } };
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
                case 0x74E780:
                    Target(address);
                    break;
                case 0x74E8B0:
                    Dog(address);
                    break;
                case 0x7544E0:
                    Model(address);
                    break;
                case 0x74EB70:
                    Nodes(address);
                    break;
                case 0x754E60:
                    StageFade(address);
                    break;
                case 0x754950:
                    Program(address);
                    break;
                default:
                    throw new NotSupportedException($"Native Recover93 callback{callback:X8} is unsupported.");
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
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Recover93 target bounds require the actual pose owner.");
            NativeBattleDecodedPose pose = sequence.DecodedPose ?? source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Recover93 target bounds require its pose selector."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Recover93 target bounds require its frame."), sequence.PoseFlags109);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
            Store(address, task);
            switch (Stage(task))
            {
                case 0:
                    byte[] dog = new byte[84];
                    ImmutableArray<byte> world = World(actor);
                    world.AsSpan().CopyTo(dog.AsSpan(8));
                    BinaryPrimitives.WriteUInt16LittleEndian(dog.AsSpan(0x4A), (ushort)((NativeWorldScriptedPathMath.NativeAngle(unchecked((short)(ReadWord(world, 0) - BinaryPrimitives.ReadInt16LittleEndian(dog))), ReadWord(world, 4)) - 2048) & 4095));
                    frame = frame with { Memory = frame.Memory with { Recover93 = Shared() with { DogControl = dog.ToImmutableArray() } } };
                    Append(2, 0x74E8B0, 0x48, address);
                    Store(address, Next(task));
                    break;
                case 1:
                    if (DogWord(0x48) == 1)
                    {
                        int headerIndex = parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A));
                        if ((uint)headerIndex >= inputs.Packets.Headers.Length)
                        {
                            throw new NotSupportedException("Native Recover93 contact header leaves its physical arena.");
                        }
                        int contact = checked((inputs.Packets.Headers[headerIndex].FirstContactIndex ?? throw new NotSupportedException("Native Recover93 lost its physical contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        Emit(new NativeBattleRecover93Contact(contact));
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
                    throw new NotSupportedException("Native Recover93 target stage is unsupported.");
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
                    uint child = Append(3, 0x7544E0, 0x140, address);
                    NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(state.Read(child), child, resources.Model, 3, resources.DefaultColor44);
                    Store(child, initialized.Task);
                    SetModel(initialized.State);
                    child = Append(4, 0x74EB70, 0x2A4, address);
                    Store(child, state.Read(child).WriteDword(0x30, NativeBattleRecover93Nodes.SourceAddress).WriteWord(0x298, 44).WriteWord(0x29C, 0).WriteWord(0x29E, 30));
                    Append(5, 0x754E60, 0xB0, address);
                }
            }
            if (DogWord(0x42) != DogWord(0x44))
            {
                SetDog(0x42, DogWord(0x44));
            }
            switch (Stage(task))
            {
                case 0:
                    Emit(new NativeBattleRecover93UploadModelTim());
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
                    if (DogWord(0x46) >= 44)
                    {
                        Emit(new NativeBattleRecover93Sound(resources.SoundDogTime44, 0, 128));
                        task = Next(task);
                    }
                    break;
                case 4:
                    if (DogWord(0x46) >= 75)
                    {
                        SetDog(0x48, 1);
                        task = Next(task.WriteWord(0x26, (ushort)(task.ReadWord(0x26) | 1)));
                    }
                    break;
                case 5:
                    break;
                default:
                    throw new NotSupportedException("Native Recover93 dog stage is unsupported.");
            }
            Store(address, task);
        }

        void Model(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            switch (Stage(task))
            {
                case 0:
                    NativeBattleVector target = Bone(task.ReadByte(0x2D), 241);
                    task = Position(task, 0x4C, target).WriteWord(0x4C, unchecked((ushort)(target.X - 1024))).WriteWord(0x4E, 0).WriteWord(0x50, unchecked((ushort)(target.Z + 4608))).WriteWord(0x128, unchecked((ushort)-256)).WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
                    task = Next(Pose(task, address, 0, 0));
                    Emit(new NativeBattleRecover93Sound(resources.SoundAppearance, 0, 128));
                    Emit(new NativeBattleRecover93Voice(resources.VoiceAppearance, 128, 1, 127));
                    break;
                case 1:
                    task = Pose(task, address, 0, 1);
                    if (Word(task, 0x136) >= 16)
                    {
                        task = Next(Pose(task.WriteWord(0x128, 0), address, 1, 0));
                    }
                    break;
                case 2:
                    bool completed = false;
                    task = Pose(task, address, 0, 2, value => completed = value);
                    if (completed)
                    {
                        task = Next(Pose(task, address, 2, 0).WriteWord(0x128, 256));
                        Emit(new NativeBattleRecover93Voice(resources.VoiceDeparture, 128, 1, 127));
                    }
                    if (task.ReadWord(0x136) == 19)
                    {
                        Store(address, task);
                        uint child = Append(7, 0x754950, 0x17C, address);
                        NativeBattleVector position = OwnedBone(address + 0x30, 24);
                        Store(child, Position(state.Read(child), 0x1C, position).WriteDword(0x74, resources.ProgramSource.Address).WriteDword(0x78, 0xE0).WriteWord(0x80, 0).WriteWord(0x82, 0));
                    }
                    break;
                case 3:
                    task = Pose(task, address, 0, 1);
                    if (Word(task, 0x136) >= 16)
                    {
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new NotSupportedException("Native Recover93 model stage is unsupported.");
            }
            for (int axis = 0; axis < 3; axis++)
            {
                task = Add(task, 0x4C + axis * 2, Word(task, 0x124 + axis * 2));
            }
            Store(address, task);
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                Sync();
                NativeBattleRecover93ModelDrawStep drawn = inputs.DrawModel(frame, task, ModelState(address));
                frame = drawn.Frame;
                state = new(frame.Effect.Pools);
                task = drawn.Task;
            }
            frame = frame with { Memory = frame.Memory with { Recover93 = Shared() with { PublishedModelMatrix = Slice(task, 0x70, 32) } } };
            task = task.WriteDword(0x84, unchecked((uint)Word(task, 0x4C))).WriteDword(0x88, unchecked((uint)Word(task, 0x4E))).WriteDword(0x8C, unchecked((uint)Word(task, 0x50)));
            Store(address, task);
        }

        void Nodes(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            int phase = Stage(task);
            if (phase == 0 && Word(task, 0x24) >= Word(task, 0x298))
            {
                Sync();
                NativeBattleRecover93NodeFrame initialized = NativeBattleRecover93Nodes.Initialize74EC00(new(frame.Memory, frame.Effect, Shared().Nodes, frame.Random), address, resources.Nodes, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource);
                frame = frame with { Memory = initialized.Memory with { Recover93 = Shared() with { Nodes = initialized.State } }, Effect = initialized.Effect, Random = initialized.Random };
                state = new(frame.Effect.Pools);
                task = Next(state.Read(address));
                Store(address, task);
            }
            if (phase == 1 || phase == 0 && Stage(task) == 1)
            {
                Sync();
                NativeBattleRecover93NodesStep advanced = inputs.AdvanceNodes(frame, address);
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
                throw new NotSupportedException("Native Recover93 neutral context stage is unsupported.");
            }
        }

        void StageFade(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            switch (Stage(task))
            {
                case 0:
                    if (Word(task, 0x24) >= 35)
                    {
                        task = Next(task.WriteWord(0x1C, 0));
                        Emit(new NativeBattleRecover93StageDepth(0));
                        Emit(new NativeBattleRecover93StageClearRgb());
                    }
                    break;
                case 1:
                    task = Add(task, 0x1C, 256);
                    if (Word(task, 0x1C) >= 1536)
                    {
                        task = Next(task.WriteWord(0x1C, 1536));
                    }
                    Emit(new NativeBattleRecover93StageDepth(Word(task, 0x1C)));
                    break;
                case 2:
                    if (Word(task, 0x24) >= 70)
                    {
                        task = Next(task);
                    }
                    break;
                case 3:
                    task = Add(task, 0x1C, -256);
                    if (Word(task, 0x1C) <= 0)
                    {
                        task = Next(task.WriteWord(0x1C, 0).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    Emit(new NativeBattleRecover93StageDepth(Word(task, 0x1C)));
                    break;
                case 4:
                    break;
                default:
                    throw new NotSupportedException("Native Recover93 stage fade is unsupported.");
            }
            Store(address, task);
        }

        void Program(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            int phase = Stage(task);
            if (phase == 0)
            {
                task = NativeBattleSearchProgramDrawing.InitializeWorkspace7016B0(task, resources.ProgramSource).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x86, unchecked((ushort)-256));
                NativeBattleVector target = Bone(task.ReadByte(0x2D), 241);
                task = Position(task, 0x174, target).WriteWord(0x68, unchecked((ushort)((target.X - Word(task, 0x1C)) / 8))).WriteWord(0x6A, unchecked((ushort)-8)).WriteWord(0x6C, unchecked((ushort)((target.Z - Word(task, 0x20)) / 8)));
            }
            else if (phase == 1)
            {
                task = Add(task, 0x6A, 13);
                for (int axis = 0; axis < 3; axis++)
                {
                    task = Add(task, 0x1C + axis * 2, Word(task, 0x68 + axis * 2));
                }
            }
            if (phase is 0 or 1)
            {
                Store(address, task);
                Sync();
                NativeBattleRecover93ProgramStep advanced = inputs.AdvanceProgram(frame, task);
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
            else if (phase != 2)
            {
                throw new NotSupportedException("Native Recover93 GF program stage is unsupported.");
            }
            Store(address, task);
        }

        NativeBattleVector Bone(int actor, byte selector) => NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(actor), inputs.ReadPoseSource(actor), selector, 0);

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
            frame = frame with { Memory = frame.Memory with { Recover93 = Shared() with { Models = index < 0 ? models.Add(model) : models.SetItem(index, model) } } };
        }
        NativeBattleRecover93State Shared() => frame.Memory.Recover93 ?? throw new InvalidDataException("Native Recover93 shared state is absent.");
        short DogWord(int offset) => ReadWord(Shared().DogControl ?? throw new InvalidDataException("Native Recover93 dog state has not been cleared by its target producer."), offset);
        void SetDog(int offset, int value)
        {
            byte[] bytes = (Shared().DogControl ?? throw new InvalidDataException("Native Recover93 dog state is absent.")).ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), unchecked((ushort)value));
            frame = frame with { Memory = frame.Memory with { Recover93 = Shared() with { DogControl = bytes.ToImmutableArray() } } };
        }
        ImmutableArray<byte> World(int actor)
        {
            ImmutableArray<byte> bytes = inputs.ReadTargetWorld8(actor);
            if (bytes.IsDefault || bytes.Length != 8)
            {
                throw new InvalidDataException("Native Recover93 requires the target's exact eight-byte world-position owner.");
            }
            return bytes;
        }
        uint Append(int pool, uint callback, int requested, uint parent)
        {
            NativeBattleEffectHierarchyAppend append = state.Append8DC540(pool, callback, requested, parent, Select);
            state = append.Hierarchy;
            return append.Address != 0 ? append.Address : throw new NotSupportedException("Native Recover93 reached a task allocation without its required physical owner.");
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
        void Emit(NativeBattleRecover93Operation operation)
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

    }

    private static int TextureOffset(NativeBattleOriginalEffectState effect) => effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x2592548).ArenaOffset;
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short ReadWord(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static int Stage(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, int offset, NativeBattleVector vector) => task.WriteWord(offset, unchecked((ushort)vector.X)).WriteWord(offset + 2, unchecked((ushort)vector.Y)).WriteWord(offset + 4, unchecked((ushort)vector.Z));
    private static ImmutableArray<byte> Slice(NativeBattleModulePoolTask task, int offset, int count) => task.Payload.AsSpan().Slice(offset - 12, count).ToArray().ToImmutableArray();
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
            throw new NotSupportedException("Native Recover93 root callback changed its contract.");
        }
    }
}
