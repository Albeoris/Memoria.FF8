using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Moogle96's physical hierarchy, independent model, retained dog state and neutral-node owners.</summary>
public static class NativeBattleMoogle96Controller
{
    public const uint MainDescriptor = 0x257B920;
    public const uint MainBank = 0x257B858;
    public const uint ModelBank = 0x257F538;
    public const uint ModelDescriptor = 0x257F678;
    private const uint ActionParameters = 0x1D99A78;

    public static NativeBattleMoogle96Frame Initialize731DE0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleMoogle96Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 96, Entry: 0x731DE0, Initializer: 0x731DC0 } || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Moogle96 requires its original731DE0 entry and actual action parameters.");
        }
        NativeBattleMoogle96State retained = memory.Moogle96 ?? throw new NotSupportedException("Native Moogle requires its explicitly initialized cold/import state owner.");
        NativeBattleGfAnimationSource program = retained.ProgramSource ?? (retained.ProgramSourceKnownCold ? resources.ProgramSource : throw new NotSupportedException("Native Moogle's warmed mutable GF source is missing."));
        if (program.Address != resources.ProgramSource.Address || program.IsDefault || program.Length != resources.ProgramSource.Length)
        {
            throw new InvalidDataException("Native Moogle mutable GF source contradicts its actual original owner.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(0x257E268, 0x257E278, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(0x257B828, 0x257B750, 0x48, 3),
            NativeBattleEffectTaskPool.Initialize(0x257E258, 0x257DA68, 0x2A4, 3),
            NativeBattleEffectTaskPool.Initialize(0x257DA50, 0x257D018, 0x51C, 2),
            NativeBattleEffectTaskPool.Initialize(0x257B930, 0x257B940, 0x70, 50),
            NativeBattleEffectTaskPool.Initialize(0x257CF28, 0x257CF38, 0x70, 2),
            NativeBattleEffectTaskPool.Initialize(ModelDescriptor, ModelBank, 0x140, 1),
            NativeBattleEffectTaskPool.Initialize(0x257F708, 0x257F6A0, 0x30, 2)
        ]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x731F70, 0x64);
        hierarchy = appended.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Moogle96 requires an original header/contact owner.");
        NativeBattleModulePoolTask root = hierarchy.Read(appended.Address).WriteDword(0xC, ActionParameters).WriteByte(0x2C, header.ReadByte(0)).WriteByte(0x2D, packets.ReadContact(contact).Primary.ActorIndex).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17));
        byte lastTarget = unchecked((byte)(root.ReadWord(0x5A) - 1));
        if (lastTarget < root.ReadWord(0x58))
        {
            lastTarget = unchecked((byte)root.ReadWord(0x58));
        }
        root = root.WriteByte(0x2F, lastTarget);
        hierarchy = hierarchy.Write(appended.Address, root);
        int textureOffset = TextureOffset(effect);
        uint next = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)textureOffset + 0x2000);
        if ((effect.Parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Moogle96 requires its actual animation/TIM load owner.");
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Moogle96 requires its loaded arena."), textureOffset);
            memory = memory with { Textures = textures };
        }
        program = NativeBattleOwnedGfAnimationSource.Own(program);
        memory = memory with { Moogle96 = retained with { ProgramSource = program, ProgramSourceKnownCold = false, Global257F724 = 0, Global257CF24 = 0, NextAllocation = next } };
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = MainDescriptor }, random);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int index = Enumerable.Range(0, effect.Pools.Length).FirstOrDefault(slot => effect.Pools[slot].DescriptorAddress == MainDescriptor, -1);
        if (index < 0)
        {
            throw new NotSupportedException("Native Moogle96 completion has no retained root bank.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        uint delta = unchecked(address - pool.BankAddress);
        if (delta % pool.Stride != 0x62 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Moogle96 completion pointer is not a root BYTE62 owner.");
        }
        int slot = (int)(delta / pool.Stride);
        return effect with { Pools = effect.Pools.SetItem(index, pool.WriteSlot(slot, pool.Slots[slot].WriteByte(0x62, value))) };
    }

    public static NativeBattleMoogle96Frame Advance731F70(NativeBattleMoogle96Frame frame, NativeBattleMoogle96Inputs inputs, NativeBattleMoogle96Resources resources)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Moogle96 lost its action parameters.");
        NativeBattleEffectHierarchy state = new(frame.Effect.Pools);
        state.Validate();
        if (frame.Memory.Moogle96 is null)
        {
            throw new InvalidDataException("Native Moogle96 lost its explicitly initialized shared state.");
        }
        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep rootStep = state.Advance(0, (current, address) =>
        {
            state = current;
            NativeBattleModulePoolTask task = state.Read(address);
            Require(task, 0x731F70);
            int baseOffset = TextureOffset(frame.Effect);
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = baseOffset + (odd ? 0 : 0x1000) }, Memory = frame.Memory with { Moogle96 = Shared() with { ViewCopy = MatrixBytes(inputs.View), PrivateBufferAliasOffset = baseOffset + (odd ? 0x1000 : 0x2000) } } };
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
                        NativeBattleMoogle96NodeFrame cleared = NativeBattleMoogle96Nodes.ClearStorage(new(frame.Memory, frame.Effect, Shared().Nodes, frame.Random), baseOffset);
                        frame = frame with { Memory = cleared.Memory with { Moogle96 = Shared() with { Nodes = cleared.State, NextAllocation = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)baseOffset + 0x13760) } } };
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x63, 1)));
                    Append(1, 0x7321D0, 0x58, address);
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
                    Emit(new NativeBattleMoogle96CommonTextureLoad(baseOffset, address + 0x62));
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
                    throw new NotSupportedException("Native Moogle96 root stage is unsupported.");
            }
            NativeBattleMoogle96NodeFrame pass = NativeBattleMoogle96Nodes.BeginPass(new(frame.Memory, frame.Effect, Shared().Nodes, frame.Random), inputs.View, frame.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Moogle96 lost its actual private cursor."));
            frame = frame with { Memory = frame.Memory with { Moogle96 = Shared() with { Nodes = pass.State } } };
            Store(address, state.Read(address).WriteWord(0x5E, 0));
            int survivors = 0;
            foreach (int pool in new[] { 1, 2, 8, 7, 3, 4, 5, 6 })
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
                case 0x7321D0:
                    Target(address);
                    break;
                case 0x7327B0:
                    Dog(address);
                    break;
                case 0x739610:
                    Model(address);
                    break;
                case 0x734840:
                    Nodes(address);
                    break;
                case 0x733FC0:
                    Program(address);
                    break;
                case 0x732A00:
                    Camera(address);
                    break;
                case 0x733570:
                case 0x733BE0:
                    StageFade(address);
                    break;
                case 0x733810:
                case 0x733CE0:
                    EnemyFade(address);
                    break;
                case 0x733690:
                    Music(address);
                    break;
                case 0x732390:
                    NativeBattleMoogle96IoAdvance io = NativeBattleMoogle96Io.Advance732390(state.Read(address), Shared().Io ?? throw new InvalidDataException("Native Moogle IO owner is missing."));
                    frame = frame with { Memory = frame.Memory with { Moogle96 = Shared() with { Io = io.State } } };
                    Store(address, io.Task);
                    if (io.Request is { } request)
                    {
                        Emit(new NativeBattleMoogle96FileRequest(request));
                    }
                    if (io.ReadyToComplete)
                    {
                        state = state.Complete8DC530(address);
                        return 2;
                    }
                    return 0;
                default:
                    throw new NotSupportedException($"Native Moogle96 callback{callback:X8} is unsupported.");
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
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Moogle96 target bounds require the actual pose owner.");
            NativeBattleDecodedPose pose = sequence.DecodedPose ?? source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Moogle96 target bounds require its pose selector."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Moogle96 target bounds require its frame."), sequence.PoseFlags109);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
            Store(address, task);
            switch (Stage(task))
            {
                case 0:
                    byte[] dog = new byte[84];
                    BinaryPrimitives.WriteInt16LittleEndian(dog.AsSpan(2), -256);
                    int[] enemies = Enumerable.Range(3, 4).Where(index => (inputs.ReadModel(index).Flags0 & 2) != 0).ToArray();
                    if (enemies.Length != 0)
                    {
                        NativeBattleVector[] positions = enemies.Select(index => (inputs.ReadModel(index).Transform ?? throw new NotSupportedException("Native Moogle centroid requires actual enemy positions.")).Position28).ToArray();
                        BinaryPrimitives.WriteInt16LittleEndian(dog.AsSpan(8), unchecked((short)((positions.Min(position => (int)position.X) + positions.Max(position => (int)position.X)) / 2)));
                        BinaryPrimitives.WriteInt16LittleEndian(dog.AsSpan(12), unchecked((short)((positions.Min(position => (int)position.Z) + positions.Max(position => (int)position.Z)) / 2)));
                    }
                    frame = frame with { Memory = frame.Memory with { Moogle96 = Shared() with { DogControl = dog.ToImmutableArray() } } };
                    ImmutableArray<byte> script = Shared().Io?.Script ?? (Shared().IoScriptKnownCold ? resources.IoScript.Data : throw new NotSupportedException("Native Moogle's warmed mutable IO script is missing."));
                    NativeBattleMoogle96IoInitialization initializedIo = NativeBattleMoogle96Io.Initialize7322A0(script, Shared().NextAllocation ?? throw new InvalidDataException("Native Moogle next allocation is unknown."));
                    frame = frame with { Memory = frame.Memory with { Moogle96 = Shared() with { Io = initializedIo.State, IoScriptKnownCold = false, NextAllocation = initializedIo.NextAddress } } };
                    Append(8, 0x732390, 0x30, address);
                    Append(2, 0x7327B0, 0x48, address);
                    Store(address, Next(task));
                    break;
                case 1:
                    if (DogWord(0x48) == 1)
                    {
                        int headerIndex = parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A));
                        if ((uint)headerIndex >= inputs.Packets.Headers.Length)
                        {
                            throw new NotSupportedException("Native Moogle96 contact header leaves its physical arena.");
                        }
                        NativeBattleActionHeaderSlot selectedHeader = inputs.Packets.Headers[headerIndex];
                        int firstContact = selectedHeader.FirstContactIndex ?? throw new NotSupportedException("Native Moogle lost its physical contact pointer.");
                        for (int contact = 0; contact < selectedHeader.ReadByte(16); contact++)
                        {
                            Emit(new NativeBattleMoogle96Contact(checked(firstContact + contact)));
                        }
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
                    throw new NotSupportedException("Native Moogle96 target stage is unsupported.");
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
                if (DogWord(0x40) == 2)
                {
                    uint child = Append(3, 0x734840, 0x2A4, address);
                    Store(child, state.Read(child).WriteDword(0x30, 0x169227C).WriteWord(0x298, 0).WriteWord(0x29C, 2).WriteWord(0x29E, 45));
                    child = Append(4, 0x733FC0, 0x51C, address);
                    Store(child, state.Read(child).WriteDword(0x74, CurrentProgram().Address).WriteDword(0x78, 0x488).WriteWord(0x80, 0).WriteWord(0x82, 0));
                    child = Append(7, 0x739610, 0x140, address);
                    NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(state.Read(child), child, resources.Model, 3, resources.DefaultColor44);
                    Store(child, initialized.Task);
                    SetModel(initialized.State);
                    child = Append(3, 0x734840, 0x2A4, address);
                    Store(child, state.Read(child).WriteDword(0x30, 0x1696DE8).WriteWord(0x298, 110).WriteWord(0x29C, 2).WriteWord(0x29E, 45));
                }
            }
            if (DogWord(0x42) != DogWord(0x44))
            {
                SetDog(0x42, DogWord(0x44));
            }
            switch (Stage(task))
            {
                case 0:
                    if (RequestMode(1))
                    {
                        task = Next(task);
                    }
                    break;
                case 1:
                    if (DogWord(0x40) >= 1)
                    {
                        Emit(new NativeBattleMoogle96Sound(resources.SoundAppearance, 0, 128));
                        Append(5, 0x732A00, 0x70, address);
                        Append(5, 0x733570, 0x70, address);
                        Append(5, 0x733690, 0x70, address);
                        Sync();
                        NativeBattleMoogle96VibrationStep vibration = inputs.StartVibration(frame);
                        frame = vibration.Frame;
                        state = new(frame.Effect.Pools);
                        SetDog(0x52, vibration.Handle);
                        task = Next(task.WriteWord(0x44, 12));
                    }
                    break;
                case 2:
                    task = Add(task, 0x44, -1);
                    if (Word(task, 0x44) <= 0)
                    {
                        Append(5, 0x733810, 0x70, address);
                        task = Next(task.WriteWord(0x44, task.ReadWord(0x24)));
                    }
                    break;
                case 3:
                    NativeBattleMoogle96IoRelease release = NativeBattleMoogle96Io.ReleaseWait733A80(Shared().Io ?? throw new InvalidDataException("Native Moogle IO owner is missing."));
                    frame = frame with { Memory = frame.Memory with { Moogle96 = Shared() with { Io = release.State } } };
                    if (release.Released)
                    {
                        task = Next(task);
                    }
                    break;
                case 4:
                    if ((Shared().Io ?? throw new InvalidDataException("Native Moogle IO owner is missing.")).WaitReleased == 0)
                    {
                        task = Next(task);
                    }
                    break;
                case 5:
                    if (Word(task, 0x24) - Word(task, 0x44) >= 20)
                    {
                        task = Next(task);
                    }
                    break;
                case 6:
                    if (RequestMode(2))
                    {
                        task = Next(task);
                    }
                    break;
                case 7:
                    if (DogWord(0x40) >= 2)
                    {
                        task = Next(task.WriteWord(0x44, 144));
                    }
                    break;
                case 8:
                    task = Add(task, 0x44, -1);
                    if (Word(task, 0x44) <= 0)
                    {
                        task = Next(task);
                    }
                    break;
                case 9:
                    if (RequestMode(3))
                    {
                        task = Next(task);
                    }
                    break;
                case 10:
                    if (DogWord(0x40) >= 3)
                    {
                        Append(5, 0x733BE0, 0x70, address);
                        Append(5, 0x733CE0, 0x70, address);
                        task = Next(task.WriteWord(0x44, 30));
                    }
                    break;
                case 11:
                    task = Add(task, 0x44, -1);
                    if (Word(task, 0x44) <= 0)
                    {
                        task = Next(task);
                    }
                    break;
                case 12:
                    if (RequestMode(4))
                    {
                        Emit(new NativeBattleMoogle96StopVibration(DogWord(0x52)));
                        SetDog(0x48, 1);
                        task = Next(task);
                    }
                    break;
                case 13:
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    break;
                case 14:
                    break;
                default:
                    throw new NotSupportedException("Native Moogle director phase is unsupported.");
            }
            Store(address, task);
            Emit(new NativeBattleMoogle96DirectorModeE1());
        }

        void Model(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            switch (Stage(task))
            {
                case 0:
                    task = task.WriteDword(0x4C, DogDword(0)).WriteDword(0x50, DogDword(4)).WriteDword(0x60, checked(address + 0x114)).WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
                    task = TextureProgram(Pose(task, address, 0, 0), 0);
                    task = Next(task);
                    Emit(new NativeBattleMoogle96Sound(resources.SoundLater, 1, 128));
                    break;
                case 1:
                    if (task.ReadWord(0x24) == 20)
                    {
                        task = TextureProgram(task, 1);
                    }
                    bool completed = false;
                    task = Pose(task, address, 0, 2, value => completed = value);
                    if (completed)
                    {
                        task = Next(Pose(task, address, 1, 0));
                    }
                    break;
                case 2:
                    completed = false;
                    task = Pose(task, address, 0, 2, value => completed = value);
                    if (completed)
                    {
                        task = Next(Pose(task, address, 1, 0));
                    }
                    break;
                case 3:
                    completed = false;
                    task = Pose(task, address, 0, 2, value => completed = value);
                    if (completed)
                    {
                        task = Next(TextureProgram(Pose(task, address, 2, 0), 2));
                    }
                    break;
                case 4:
                    task = Pose(task, address, 0, 1);
                    if (Word(task, 0x136) >= 31)
                    {
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                    }
                    break;
                case 5:
                    break;
                default:
                    throw new NotSupportedException("Native Moogle model phase is unsupported.");
            }
            Store(address, task);
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                Sync();
                NativeBattleMoogle96ModelDrawStep draw = inputs.DrawModel(frame, task, ModelState(address));
                frame = draw.Frame;
                state = new(frame.Effect.Pools);
                task = draw.Task;
                int texture = TextureIndex(task.ReadDword(0x110));
                int section = resources.Model.Section(3);
                NativeBattleMoogle96TextureTable table = new(checked(resources.Model.Address + (uint)section), resources.Model.Bytes.AsSpan().Slice(section).ToArray().ToImmutableArray());
                NativeBattleMoogle96TextureProgram? source = texture < 0 ? null : new(TextureAddress(texture), resources.TextureSequences[texture].Data);
                NativeBattleMoogle96TextureStep motion = NativeBattleMoogle96TextureMotion.Advance739890(task, source, table);
                task = motion.Task;
                foreach (NativeBattleTextureCopy copy in motion.Copies)
                {
                    Emit(new NativeBattleMoogle96TextureCopy(copy));
                }
            }
            Store(address, task);
        }

        NativeBattleModulePoolTask TextureProgram(NativeBattleModulePoolTask task, int index) => task.WriteDword(0x110, TextureAddress(index)).WriteWord(0x12E, index == 0 ? (ushort)1 : (ushort)0).WriteWord(0x12C, 0).WriteWord(0x130, 0).WriteWord(0x132, 0);
        uint TextureAddress(int index) => index switch { 0 => 0x1532FC8, 1 => 0x1532FD8, 2 => 0x1532FE8, _ => throw new InvalidDataException("Native Moogle texture program index is invalid.") };
        int TextureIndex(uint pointer) => pointer switch { 0 => -1, 0x1532FC8 => 0, 0x1532FD8 => 1, 0x1532FE8 => 2, _ => throw new InvalidDataException("Native Moogle texture program pointer is outside its actual sources.") };

        void Nodes(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            int phase = Stage(task);
            if (phase == 0 && Word(task, 0x24) >= Word(task, 0x298))
            {
                Sync();
                NativeBattleMoogle96NodeFrame initialized = NativeBattleMoogle96Nodes.InitializeContext(new(frame.Memory, frame.Effect, Shared().Nodes, frame.Random), address, new(task.ReadDword(0x30), task.ReadDword(0x30) == 0x169227C ? resources.NodesA.Data : resources.NodesB.Data), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, new(DogVector(0), DogVector(8)));
                frame = frame with { Memory = initialized.Memory with { Moogle96 = Shared() with { Nodes = initialized.State } }, Effect = initialized.Effect, Random = initialized.Random };
                state = new(frame.Effect.Pools);
                task = Next(state.Read(address));
                Store(address, task);
            }
            if (phase == 1 || phase == 0 && Stage(task) == 1)
            {
                Sync();
                NativeBattleMoogle96NodesStep advanced = inputs.AdvanceNodes(frame, address);
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
                throw new NotSupportedException("Native Moogle96 neutral context stage is unsupported.");
            }
        }

        void Program(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            int phase = Stage(task);
            if (phase == 0)
            {
                task = NativeBattleMoogle96ProgramDrawing.InitializeWorkspace734400(task, CurrentProgram(), DogBytes(0, 8));
            }
            if (phase is 0 or 1)
            {
                Store(address, task);
                Sync();
                NativeBattleMoogle96ProgramStep draw = inputs.AdvanceProgram(frame, task);
                frame = draw.Frame;
                state = new(frame.Effect.Pools);
                task = draw.Task;
                if (phase == 0)
                {
                    task = Next(task);
                }
                else if (draw.Remaining == 0)
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
            }
            else if (phase != 2)
            {
                throw new NotSupportedException("Native Moogle GF phase is unsupported.");
            }
            frame = frame with { Memory = frame.Memory with { Moogle96 = Shared() with { ProgramSource = NativeBattleMoogle96ProgramDrawing.MutateUv7340A0(CurrentProgram()) } } };
            Store(address, task);
        }

        void Camera(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            int phase = Stage(task);
            if (phase == 0)
            {
                CameraInitialize(0, 8, NativeBattleMoogleCamera.Reset732AF0());
                task = Next(task);
            }
            else if (phase == 1)
            {
                CameraAdvance();
                if (DogWord(0x42) >= 2)
                {
                    CameraInitialize(1, 0);
                    task = Next(task);
                }
            }
            else if (phase is >= 2 and <= 11)
            {
                if (CameraAdvance())
                {
                    CameraInitialize(phase, phase == 11 ? 8 : 0);
                    task = Next(task);
                }
            }
            else if (phase == 12)
            {
                if (CameraAdvance())
                {
                    task = Next(task);
                }
            }
            else if (phase == 13)
            {
                if (DogWord(0x42) >= 4)
                {
                    task = Next(task);
                }
            }
            else if (phase == 14)
            {
                task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
            }
            else if (phase != 15)
            {
                throw new NotSupportedException("Native Moogle camera phase is unsupported.");
            }
            Store(address, task);
        }
        void CameraInitialize(int source, int anchor, NativeBattleMoogleCameraState? reset = null)
        {
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(reset ?? Shared().Camera ?? throw new InvalidDataException("Native Moogle retained camera is missing."), resources.Camera.Sources[source], DogBytes(anchor, 8), 0);
            PublishCamera(step);
        }
        bool CameraAdvance()
        {
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Advance732E00(Shared().Camera ?? throw new InvalidDataException("Native Moogle retained camera is missing."));
            PublishCamera(step);
            return step.Complete;
        }
        void PublishCamera(NativeBattleMoogleCameraStep step)
        {
            frame = frame with { Memory = frame.Memory with { Moogle96 = Shared() with { Camera = step.State } } };
            Emit(new NativeBattleMoogle96CameraPublish(step));
        }

        void StageFade(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            bool fadeIn = task.Callback == 0x733570;
            switch (Stage(task))
            {
                case 0:
                    task = Next(task.WriteWord(0x1C, fadeIn ? (ushort)0 : (ushort)4096));
                    Emit(new NativeBattleMoogle96StageFade(fadeIn ? (short?)0 : null, fadeIn, false, !fadeIn));
                    break;
                case 1:
                    task = Add(task, 0x1C, fadeIn ? 256 : -256);
                    bool complete = fadeIn ? Word(task, 0x1C) >= 4096 : Word(task, 0x1C) <= 0;
                    if (complete)
                    {
                        task = Next(task.WriteWord(0x1C, fadeIn ? (ushort)4096 : (ushort)0).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    Emit(new NativeBattleMoogle96StageFade(Word(task, 0x1C), false, complete && fadeIn, false));
                    break;
                case 2:
                    break;
                default:
                    throw new NotSupportedException("Native Moogle stage fade phase is unsupported.");
            }
            Store(address, task);
        }

        void EnemyFade(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            bool fadeIn = task.Callback == 0x733810;
            switch (Stage(task))
            {
                case 0:
                    if (!fadeIn)
                    {
                        foreach (int actor in Enumerable.Range(3, 4).Where(index => (inputs.ReadModel(index).Flags0 & 2) != 0))
                        {
                            ushort flags = inputs.ReadModel(actor).Flags0;
                            Emit(new NativeBattleMoogle96EnemyFlags(actor, unchecked((ushort)((flags & 0xFFFBu) | (DogDword(0x2C + 4 * (actor - 3)) & 4u)))));
                        }
                    }
                    task = Next(task.WriteWord(0x1C, fadeIn ? (ushort)0 : (ushort)4096));
                    break;
                case 1:
                    task = Add(task, 0x1C, fadeIn ? 256 : -256);
                    bool complete = fadeIn ? Word(task, 0x1C) >= 4096 : Word(task, 0x1C) <= 0;
                    if (complete)
                    {
                        task = Next(task.WriteWord(0x1C, fadeIn ? (ushort)4096 : (ushort)0).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                        if (fadeIn)
                        {
                            foreach (int actor in Enumerable.Range(3, 4).Where(index => (inputs.ReadModel(index).Flags0 & 2) != 0))
                            {
                                ushort flags = inputs.ReadModel(actor).Flags0;
                                SetDogDword(0x2C + 4 * (actor - 3), flags);
                                Emit(new NativeBattleMoogle96EnemyFlags(actor, (ushort)(flags | 4)));
                            }
                        }
                    }
                    ApplyEnemyFade(Word(task, 0x1C));
                    break;
                case 2:
                    break;
                default:
                    throw new NotSupportedException("Native Moogle enemy fade phase is unsupported.");
            }
            Store(address, task);
        }
        void ApplyEnemyFade(short progress)
        {
            foreach (int actor in Enumerable.Range(3, 4).Where(index => (inputs.ReadModel(index).Flags0 & 2) != 0))
            {
                uint source = inputs.TransitionColor;
                uint color = source & 0xFFFFFF;
                byte alpha = 0;
                if (progress != 0)
                {
                    color = 0x02000000;
                    for (int channel = 0; channel < 3; channel++)
                    {
                        int value = (byte)(source >> (channel * 8));
                        color |= (uint)unchecked((byte)(value - unchecked(value * progress) / 4096)) << (channel * 8);
                    }
                    alpha = (byte)(unchecked((byte)(255 - unchecked(255 * progress) / 4096)) >> 2);
                }
                Emit(new NativeBattleMoogle96EnemyFade(actor, (ushort)(inputs.ReadModel(actor).Flags0 | 0x800), color, alpha));
            }
        }

        void Music(uint address)
        {
            NativeBattleModulePoolTask task = state.Read(address);
            switch (Stage(task))
            {
                case 0:
                    if (DogWord(0x40) >= 2)
                    {
                        task = Next(task.WriteWord(0x1C, 127));
                    }
                    break;
                case 1:
                    task = Add(task, 0x1C, -8);
                    if (Word(task, 0x1C) <= 0)
                    {
                        task = Next(task.WriteWord(0x1C, 0));
                    }
                    Emit(new NativeBattleMoogle96MusicGain(Word(task, 0x1C)));
                    break;
                case 2:
                    if (DogWord(0x40) >= 4)
                    {
                        task = Next(task);
                    }
                    break;
                case 3:
                    task = Add(task, 0x1C, 8);
                    if (Word(task, 0x1C) >= 127)
                    {
                        task = Next(task.WriteWord(0x1C, 127).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    Emit(new NativeBattleMoogle96MusicGain(Word(task, 0x1C)));
                    break;
                case 4:
                    break;
                default:
                    throw new NotSupportedException("Native Moogle music phase is unsupported.");
            }
            Store(address, task);
        }
        bool RequestMode(short requested)
        {
            if (DogWord(0x44) != DogWord(0x40) || DogWord(0x40) != requested - 1)
            {
                return false;
            }
            SetDog(0x44, requested);
            return true;
        }
        NativeBattleGfAnimationSource CurrentProgram() => Shared().ProgramSource ?? throw new NotSupportedException("Native Moogle's warmed mutable GF source is missing.");
        ImmutableArray<byte> DogBytes(int offset, int count) => (Shared().DogControl ?? throw new InvalidDataException("Native Moogle dog owner is missing.")).AsSpan().Slice(offset, count).ToArray().ToImmutableArray();
        uint DogDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(DogBytes(offset, 4).AsSpan());
        NativeBattleVector DogVector(int offset) => new(DogWord(offset), DogWord(offset + 2), DogWord(offset + 4));
        void SetDogDword(int offset, uint value)
        {
            byte[] data = (Shared().DogControl ?? throw new InvalidDataException("Native Moogle dog owner is missing.")).ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
            frame = frame with { Memory = frame.Memory with { Moogle96 = Shared() with { DogControl = data.ToImmutableArray() } } };
        }

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
            frame = frame with { Memory = frame.Memory with { Moogle96 = Shared() with { Models = index < 0 ? models.Add(model) : models.SetItem(index, model) } } };
        }
        NativeBattleMoogle96State Shared() => frame.Memory.Moogle96 ?? throw new InvalidDataException("Native Moogle96 shared state is absent.");
        short DogWord(int offset) => ReadWord(Shared().DogControl ?? throw new InvalidDataException("Native Moogle96 dog state has not been cleared by its target producer."), offset);
        void SetDog(int offset, int value)
        {
            byte[] bytes = (Shared().DogControl ?? throw new InvalidDataException("Native Moogle96 dog state is absent.")).ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), unchecked((ushort)value));
            frame = frame with { Memory = frame.Memory with { Moogle96 = Shared() with { DogControl = bytes.ToImmutableArray() } } };
        }
        uint Append(int pool, uint callback, int requested, uint parent)
        {
            NativeBattleEffectHierarchyAppend append = state.Append8DC540(pool, callback, requested, parent, Select);
            state = append.Hierarchy;
            return append.Address != 0 ? append.Address : throw new NotSupportedException("Native Moogle96 reached a task allocation without its required physical owner.");
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
        void Emit(NativeBattleMoogle96Operation operation)
        {
            Sync();
            frame = inputs.Execute(frame, operation);
            state = new(frame.Effect.Pools);
        }

    }

    private static int TextureOffset(NativeBattleOriginalEffectState effect) => effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x257B740).ArenaOffset;
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
            throw new NotSupportedException("Native Moogle96 root callback changed its contract.");
        }
    }
}
