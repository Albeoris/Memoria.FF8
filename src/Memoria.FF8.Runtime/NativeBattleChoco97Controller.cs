using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco97Controller
{
    public const uint MainDescriptor = 0x2575448;
    public const uint MainBank = 0x2575380;
    private const uint ActionParameters = 0x1D99A78;

    public static NativeBattleChoco97Frame Initialize729A60(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleChoco97Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 97, Entry: 0x729A60, Initializer: 0x729A40 } || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Choco97 requires its actual entry, initializer and action parameters.");
        }
        NativeBattleChoco97State retained = memory.Choco97 ?? throw new NotSupportedException("Native Choco97 requires its explicit cold/imported retained state.");
        retained.Validate();
        NativeBattleGfAnimationSource source = retained.ProgramSource ?? (retained.ProgramSourceKnownCold ? resources.ProgramSource : throw new NotSupportedException("Native Choco97 warmed static program source is missing."));
        if (source.Address != resources.ProgramSource.Address || source.Data.IsDefault || source.Data.Length != resources.ProgramSource.Data.Length || retained.MorphScratch.IsDefault || retained.MorphScratch.Length != 0x3C0)
        {
            throw new InvalidDataException("Native Choco97 program and morph owners contradict their physical extents.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(0x2579D48, 0x2579D58, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(0x2575350, 0x2575278, 0x48, 3),
            NativeBattleEffectTaskPool.Initialize(0x2579D38, 0x2579548, 0x2A4, 3),
            NativeBattleEffectTaskPool.Initialize(0x2579530, 0x2578030, 0x540, 4),
            NativeBattleEffectTaskPool.Initialize(0x2575458, 0x2575468, 0x70, 100),
            NativeBattleEffectTaskPool.Initialize(0x257B158, 0x257B018, 0x140, 1),
            NativeBattleEffectTaskPool.Initialize(0x257B5A8, 0x257B180, 0x30, 2)
        ]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x729BD0, 0x64);
        hierarchy = appended.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Choco97 requires its actual initial contact pointer.");
        NativeBattleModulePoolTask root = hierarchy.Read(appended.Address).WriteDword(0xC, ActionParameters).WriteByte(0x2C, header.ReadByte(0)).WriteByte(0x2D, packets.ReadContact(contact).Primary.ActorIndex).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17));
        byte lastTarget = unchecked((byte)(root.ReadWord(0x5A) - 1));
        if (lastTarget < root.ReadWord(0x58))
        {
            lastTarget = unchecked((byte)root.ReadWord(0x58));
        }
        hierarchy = hierarchy.Write(appended.Address, root.WriteByte(0x2F, lastTarget));
        int textureOffset = TextureOffset(effect);
        if ((effect.Parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Choco97 requires its actual TIM queue owner.");
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Choco97 requires its loaded arena."), textureOffset);
            memory = memory with { Textures = textures };
        }
        retained = retained with { ProgramSource = source, ProgramSourceKnownCold = false, FreezePrimary = 0, FreezeSecondary = 0, NextAllocation = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)textureOffset + 0x2000) };
        return new(memory with { Choco97 = retained }, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = MainDescriptor }, random);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int index = Enumerable.Range(0, effect.Pools.Length).FirstOrDefault(slot => effect.Pools[slot].DescriptorAddress == MainDescriptor, -1);
        if (index < 0)
        {
            throw new NotSupportedException("Native Choco97 completion requires its retained main pool.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        uint delta = unchecked(address - pool.BankAddress);
        if (delta % pool.Stride != 0x62 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Choco97 completion pointer is not an actual root BYTE62.");
        }
        int slot = (int)(delta / pool.Stride);
        return effect with { Pools = effect.Pools.SetItem(index, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(0x62, value)) }) };
    }

    public static NativeBattleChoco97Frame Advance729BD0(NativeBattleChoco97Frame frame, NativeBattleChoco97Inputs inputs, NativeBattleChoco97Resources resources)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Choco97 lost its action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy.Validate();
        Shared().Validate();
        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep roots = hierarchy.Advance(0, (current, address) =>
        {
            hierarchy = current;
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback != 0x729BD0)
            {
                throw new NotSupportedException("Native Choco97 root callback changed its contract.");
            }
            int textureOffset = TextureOffset(frame.Effect);
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = textureOffset + (odd ? 0 : 0x1000) }, Memory = frame.Memory with { Choco97 = Shared() with { ViewCopy = MatrixBytes(inputs.View), PrivateBufferAliasOffset = textureOffset + (odd ? 0x1000 : 0x2000) } } };
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
                        NativeBattleChoco97NodeFrame cleared = NativeBattleChoco97Nodes.ClearStorage(NodeFrame(), textureOffset);
                        AcceptNodes(cleared);
                        SetShared(Shared() with { NextAllocation = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)textureOffset + 0x13760) });
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x63, 1)));
                    Append(1, 0x729E20, 0x58, address);
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        Store(address, unchecked((sbyte)task.ReadByte(0x2A)) < Word(task, 0x58) ? task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, 3) : Next(task));
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
                    Emit(new NativeBattleChoco97CommonTextureLoad(textureOffset, address + 0x62));
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
                    throw new NotSupportedException("Native Choco97 root phase is unsupported.");
            }
            NativeBattleChoco97NodeFrame pass = NativeBattleChoco97Nodes.BeginPass(NodeFrame(), inputs.View, frame.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Choco97 private cursor is missing."));
            AcceptNodes(pass);
            Store(address, hierarchy.Read(address).WriteWord(0x5E, 0));
            int survivors = 0;
            foreach (int pool in new[] { 1, 2, 7, 6, 3, 4, 5 })
            {
                NativeBattleEffectHierarchyStep children = hierarchy.Advance(pool, (active, child) =>
                {
                    hierarchy = active;
                    int completed = Run(child);
                    return new(hierarchy, completed);
                });
                hierarchy = children.Hierarchy;
                survivors = unchecked(survivors + children.Survivors);
            }
            Store(address, Add(hierarchy.Read(address).WriteWord(0x5E, unchecked((ushort)survivors)), 0x5C, 1));
            int completion = Finish(address, true);
            return new(hierarchy, completion);
        });
        hierarchy = roots.Hierarchy;
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = roots.Survivors == 0 ? 0u : MainDescriptor } };

        int Run(uint address)
        {
            uint callback = hierarchy.Read(address).Callback;
            switch (callback)
            {
                case 0x729E20:
                    Target(address);
                    return Finish(address, true);
                case 0x72A410:
                    Director(address);
                    return Finish(address, false);
                case 0x729FF0:
                    NativeBattleChoco97IoAdvance io = NativeBattleChoco97Io.Advance729FF0(hierarchy.Read(address), Shared().Io ?? throw new NotSupportedException("Native Choco97 IO has not been initialized."));
                    SetShared(Shared() with { Io = io.State });
                    Store(address, io.Task);
                    if (io.Request is { } request)
                    {
                        Emit(new NativeBattleChoco97FileRequest(request));
                    }
                    return Finish(address, false);
                case 0x7306A0:
                    Model(address);
                    return Finish(address, false);
                case 0x72AB20:
                    Nodes(address);
                    return Finish(address, true);
                case 0x72A660:
                    InitialProgram(address);
                    return Finish(address, true);
                case 0x731840:
                case 0x731950:
                case 0x731100:
                    Sync();
                    NativeBattleChoco97ProgramStep program = inputs.AdvanceProgram(frame, hierarchy.Read(address));
                    Accept(program.Frame);
                    Store(address, program.Task);
                    return Finish(address, false);
                case 0x72FBC0:
                    NativeBattleChoco97CameraTaskStep camera = NativeBattleChoco97CameraTask.Advance72FBC0(hierarchy.Read(address), Shared().Camera, resources.Camera, unchecked((short)Context().Word(0x42)), ContextBytes(0, 8), ContextBytes(8, 8));
                    SetShared(Shared() with { Camera = camera.Camera });
                    Store(address, camera.Task);
                    foreach (NativeBattleMoogleCameraStep publication in camera.Publications)
                    {
                        Emit(new NativeBattleChoco97CameraPublish(publication));
                    }
                    return Finish(address, false);
                case 0x731630:
                case 0x731A50:
                case 0x731B40:
                    NativeBattleChoco97StageFadeStep fade = NativeBattleChoco97StageFade.Advance(hierarchy.Read(address));
                    Store(address, fade.Task);
                    if (fade.Depth is not null || fade.ClearRgb)
                    {
                        Emit(new NativeBattleChoco97StageDepth(fade.Depth, fade.ClearRgb));
                    }
                    return Finish(address, false);
                case 0x730C10:
                    Sync();
                    NativeBattleChoco97SpriteDrawStep sprite = inputs.AdvanceSprite(frame, hierarchy.Read(address));
                    Accept(sprite.Frame);
                    Store(address, sprite.Task);
                    return Finish(address, false);
                default:
                    throw new NotSupportedException($"Native Choco97 callback{callback:X8} is unsupported.");
            }
        }

        void Target(uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(hierarchy.Read(address), Models(), inputs.ReadPoseSource);
            int actor = task.ReadByte(0x2D);
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattlePoseSource source = inputs.ReadPoseSource(actor);
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Choco97 target bounds require its current sequence.");
            NativeBattleDecodedPose pose = sequence.DecodedPose ?? source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Choco97 target pose is unknown."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Choco97 target frame is unknown."), sequence.PoseFlags109);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
            Store(address, task);
            switch (Stage(task))
            {
                case 0:
                    byte[] context = new byte[0x54];
                    BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(4), 2048);
                    int[] enemies = Enumerable.Range(3, 4).Where(index => (inputs.ReadModel(index).Flags0 & 2) != 0).ToArray();
                    if (enemies.Length != 0)
                    {
                        NativeBattleVector[] positions = enemies.Select(index => (inputs.ReadModel(index).Transform ?? throw new NotSupportedException("Native Choco97 centroid requires its active enemy positions.")).Position28).ToArray();
                        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(8), unchecked((short)((positions.Min(position => (int)position.X) + positions.Max(position => (int)position.X)) / 2)));
                        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(12), unchecked((short)((positions.Min(position => (int)position.Z) + positions.Max(position => (int)position.Z)) / 2)));
                    }
                    SetShared(Shared() with { Context = new(context.ToImmutableArray()) });
                    ImmutableArray<byte> script = Shared().Io?.Script ?? (Shared().IoScriptKnownCold ? resources.Io.Script.Data : throw new NotSupportedException("Native Choco97 warmed mutable IO script is missing."));
                    NativeBattleChoco97IoBanks banks = Shared().Io?.Banks ?? Shared().PendingIoBanks ?? throw new NotSupportedException("Native Choco97 external IO physical owner is unknown.");
                    NativeBattleChoco97IoInitialization initialized = NativeBattleChoco97Io.Initialize729EF0(script, Shared().NextAllocation ?? throw new NotSupportedException("Native Choco97 next allocation is unknown."), banks);
                    SetShared(Shared() with { Io = initialized.State, IoScriptKnownCold = false, PendingIoBanks = null, NextAllocation = initialized.NextAddress });
                    Append(7, 0x729FF0, 0x30, address);
                    Append(2, 0x72A410, 0x48, address);
                    Store(address, Next(hierarchy.Read(address)));
                    break;
                case 1:
                    if (Context().Word(0x48) == 1)
                    {
                        int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        if ((uint)headerIndex >= inputs.Packets.Headers.Length)
                        {
                            throw new NotSupportedException("Native Choco97 contact header leaves its actual arena.");
                        }
                        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[headerIndex];
                        int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Native Choco97 contact pointer is missing.");
                        for (int contact = 0; contact < header.ReadByte(16); contact++)
                        {
                            Emit(new NativeBattleChoco97Contact(checked(firstContact + contact)));
                        }
                        Store(address, Next(hierarchy.Read(address)));
                    }
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0 || Context().Word(0x4C) == 1)
                    {
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                    uint root = task.ReadDword(0x10);
                    Store(root, hierarchy.Read(root).WriteByte(0x63, 0));
                    break;
                case 4:
                    break;
                default:
                    throw new NotSupportedException("Native Choco97 target phase is unsupported.");
            }
        }

        void Director(uint address)
        {
            NativeBattleChoco97DirectorInputs directorInputs = new(() => (Shared().Io ?? throw new NotSupportedException("Native Choco97 director IO is unknown.")).WaitReleased, offset => BinaryPrimitives.ReadUInt32LittleEndian((Shared().Io ?? throw new NotSupportedException("Native Choco97 IO directory is unknown.")).Control.AsSpan().Slice(offset)), current =>
            {
                SetShared(Shared() with { Context = current.Context });
                NativeBattleChoco97IoRelease release = NativeBattleChoco97Io.ReleaseWait730590(Shared().Io ?? throw new NotSupportedException("Native Choco97 IO wait owner is unknown."));
                SetShared(Shared() with { Io = release.State });
                return new(current, release.Released);
            }, (current, operation) =>
            {
                Store(address, current.Task);
                SetShared(Shared() with { Context = current.Context });
                switch (operation)
                {
                    case NativeBattleChoco97StartVibration:
                        Sync();
                        NativeBattleChoco97VibrationStep vibration = inputs.StartVibration(frame);
                        Accept(vibration.Frame);
                        SetShared(Shared() with { Context = Context().WriteWord(0x52, unchecked((ushort)vibration.Handle)) });
                        break;
                    case NativeBattleChoco97AppendNeutral neutral:
                        uint child = Append(3, 0x72AB20, 0x2A4, address);
                        Store(child, hierarchy.Read(child).WriteDword(0x30, neutral.Source).WriteWord(0x298, neutral.Delay).WriteWord(0x29C, neutral.Mode).WriteWord(0x29E, neutral.Duration));
                        break;
                    case NativeBattleChoco97AppendProgram program:
                        child = Append(4, program.Callback, 0x540, address);
                        Store(child, hierarchy.Read(child).WriteDword(0x74, program.Source).WriteDword(0x78, unchecked((uint)(int)(short)program.ClearBytes)).WriteWord(0x80, unchecked((ushort)program.Arg5)).WriteWord(0x82, unchecked((ushort)program.Arg6)));
                        break;
                    case NativeBattleChoco97AppendAux auxiliary:
                        Append(5, auxiliary.Callback, auxiliary.Size, address);
                        break;
                    case NativeBattleChoco97AppendModel:
                        child = Append(6, 0x7306A0, 0x140, address);
                        NativeBattleEffectOwnedModelStep model = NativeBattleChoco97Model.Initialize(hierarchy.Read(child), child, resources.Model);
                        Store(child, model.Task);
                        SetShared(Shared() with { Model = model.State });
                        break;
                    case NativeBattleChoco97LoadModelTim:
                        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Choco97 model TIM queue is unknown.");
                        (textures, _) = textures.EnqueueTim505E30(resources.Model.ModelTim);
                        frame = frame with { Memory = frame.Memory with { Textures = textures } };
                        break;
                    case NativeBattleChoco97SetFreeze freeze:
                        (uint primary, uint secondary) = NativeBattleChoco97Director.SetFreeze730610(Shared().FreezePrimary ?? throw new NotSupportedException("Native Choco97 primary freeze is unknown."), Shared().FreezeSecondary ?? throw new NotSupportedException("Native Choco97 secondary freeze is unknown."), freeze.Value);
                        SetShared(Shared() with { FreezePrimary = primary, FreezeSecondary = secondary });
                        break;
                    case NativeBattleChoco97StopVibration stop:
                        Emit(new NativeBattleChoco97VibrationStop(stop.Handle));
                        break;
                    default:
                        throw new NotSupportedException("Native Choco97 director operation is unsupported.");
                }
                return new(hierarchy.Read(address), Context());
            });
            NativeBattleChoco97DirectorStep step = NativeBattleChoco97Director.Advance72A410(new(hierarchy.Read(address), Context()), directorInputs);
            Store(address, step.Frame.Task);
            SetShared(Shared() with { Context = step.Frame.Context });
        }

        void Model(uint address)
        {
            NativeBattleEffectOwnedModelState owned = Shared().Model ?? throw new NotSupportedException("Native Choco97 owned model has not been initialized.");
            NativeBattleChoco97ModelInputs modelInputs = new(ContextBytes(0, 8), mode => NativeBattleChoco97CameraTask.ModeAtLeast730E80(Context().Word(0x40), unchecked((short)mode)), mode =>
            {
                (ushort requested, bool accepted) = NativeBattleChoco97CameraTask.RequestMode730EC0(Context().Word(0x40), Context().Word(0x44), unchecked((short)mode));
                if (accepted)
                {
                    SetShared(Shared() with { Context = Context().WriteWord(0x44, requested) });
                }
                return accepted;
            }, (current, operation) =>
            {
                Store(address, current.Task);
                SetShared(Shared() with { Model = current.State });
                switch (operation)
                {
                    case NativeBattleChoco97ModelAppendAuxiliary auxiliary:
                        if (auxiliary.PoolAddress != 0x2575458)
                        {
                            throw new NotSupportedException("Native Choco97 model child leaves its physical auxiliary pool.");
                        }
                        Append(5, auxiliary.Callback, auxiliary.TaskBytes, address);
                        break;
                    case NativeBattleChoco97ModelSound sound:
                        Emit(new NativeBattleChoco97Sound(sound.DescriptorAddress, sound.Flags, sound.Pan));
                        break;
                    default:
                        throw new NotSupportedException("Native Choco97 model operation is unsupported.");
                }
                return current with { Task = hierarchy.Read(address) };
            }, current =>
            {
                Store(address, current.Task);
                SetShared(Shared() with { Model = current.State });
                Sync();
                NativeBattleChoco97ModelDrawStep draw = inputs.DrawModel(frame, hierarchy.Read(address), current.State);
                Accept(draw.Frame);
                Store(address, draw.Task);
                return draw.Drawing;
            });
            NativeBattleChoco97ModelAdvance step = NativeBattleChoco97Model.Advance7306A0(hierarchy.Read(address), owned, resources.Model, modelInputs);
            Store(address, step.Task);
            SetShared(Shared() with { Model = step.State });
            foreach (NativeBattleTextureCopy copy in step.TextureCopies)
            {
                Emit(new NativeBattleChoco97TextureCopy(copy));
            }
        }

        void Nodes(uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            int phase = Stage(task);
            if (phase == 0 && Word(task, 0x24) >= Word(task, 0x298))
            {
                NativeBattleChoco97NodeResource resource = resources.Nodes.Single(resource => resource.Address == task.ReadDword(0x30));
                NativeBattleEffectNodeDogOrigins origins = new(ContextVector(0), ContextVector(8));
                NativeBattleChoco97NodeFrame initialized = NativeBattleChoco97Nodes.InitializeContext(NodeFrame(), address, resource, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, origins);
                AcceptNodes(initialized);
                Sync();
                NativeBattleChoco97NodesStep initial = inputs.AdvanceNodes(frame, address);
                Accept(initial.Frame);
                Store(address, Next(hierarchy.Read(address)));
            }
            else if (phase == 1)
            {
                Sync();
                NativeBattleChoco97NodesStep advanced = inputs.AdvanceNodes(frame, address);
                Accept(advanced.Frame);
                if (advanced.Complete)
                {
                    task = hierarchy.Read(address);
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                }
            }
            else if (phase != 0 && phase != 2)
            {
                throw new NotSupportedException("Native Choco97 neutral phase is unsupported.");
            }
        }

        void InitialProgram(uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            int phase = Stage(task);
            if (phase == 0)
            {
                task = NativeBattleChoco97ProgramDrawing.InitializeWorkspace72A6C0(task, Shared().ProgramSource ?? throw new NotSupportedException("Native Choco97 static program source is missing."), ContextBytes(0, 8));
            }
            if (phase is 0 or 1)
            {
                Store(address, task);
                Sync();
                NativeBattleChoco97ProgramStep draw = inputs.AdvanceProgram(frame, task);
                Accept(draw.Frame);
                task = draw.Task;
                if (draw.Remaining is null)
                {
                    throw new InvalidDataException("Native Choco97 initial program advance lost its actual remaining value.");
                }
                if (phase == 0)
                {
                    Store(address, task);
                    Emit(new NativeBattleChoco97Sound(0x152B5BC, 0, 128));
                    task = Next(hierarchy.Read(address));
                }
                else if (draw.Remaining == 0)
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
                Store(address, task);
            }
            else if (phase != 2)
            {
                throw new NotSupportedException("Native Choco97 initial program phase is unsupported.");
            }
        }

        NativeBattleChoco97State Shared() => frame.Memory.Choco97 ?? throw new NotSupportedException("Native Choco97 retained state is missing.");
        void SetShared(NativeBattleChoco97State value) => frame = frame with { Memory = frame.Memory with { Choco97 = value } };
        NativeBattleChoco97Context Context() => Shared().Context ?? throw new NotSupportedException("Native Choco97 target context has not been initialized.");
        ImmutableArray<byte> ContextBytes(int offset, int length) => Context().Data.AsSpan().Slice(offset, length).ToArray().ToImmutableArray();
        NativeBattleVector ContextVector(int offset) => new(unchecked((short)Context().Word(offset)), unchecked((short)Context().Word(offset + 2)), unchecked((short)Context().Word(offset + 4)));
        NativeBattleChoco97NodeFrame NodeFrame()
        {
            Sync();
            return new(frame.Memory, frame.Effect, Shared().Nodes, Shared().Io, frame.Random);
        }
        void AcceptNodes(NativeBattleChoco97NodeFrame nodes)
        {
            NativeBattleChoco97State retained = Shared() with { Nodes = nodes.State, Io = nodes.Io };
            frame = new(nodes.Memory with { Choco97 = retained }, nodes.Effect, nodes.Random);
            hierarchy = new(frame.Effect.Pools);
        }
        void Accept(NativeBattleChoco97Frame value)
        {
            frame = value;
            hierarchy = new(frame.Effect.Pools);
        }
        uint Append(int pool, uint callback, int requested, uint parent)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, requested, parent, Select);
            hierarchy = appended.Hierarchy;
            return appended.Address != 0 ? appended.Address : throw new NotSupportedException("Native Choco97 exhausted a required physical task pool.");
        }
        void Store(uint address, NativeBattleModulePoolTask task)
        {
            NativeBattleModulePoolTask current = hierarchy.Read(address);
            hierarchy = hierarchy.Write(address, current with { Payload = task.Payload.SetItem(0x28 - 12, current.ReadByte(0x28)) });
        }
        int Finish(uint address, bool incrementCounter)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (incrementCounter)
            {
                task = Add(task, 0x24, 1);
                Store(address, task);
            }
            if ((task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0)
            {
                hierarchy = hierarchy.Complete8DC530(address);
                return 2;
            }
            return 0;
        }
        void Sync() => frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        void Emit(NativeBattleChoco97Operation operation)
        {
            Sync();
            Accept(inputs.Execute(frame, operation));
        }
    }

    private static int TextureOffset(NativeBattleOriginalEffectState effect) => effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x2575268).ArenaOffset;
    private static int Stage(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
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
}
