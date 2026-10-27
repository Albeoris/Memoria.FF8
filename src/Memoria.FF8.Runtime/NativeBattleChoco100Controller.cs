using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco100Controller
{
    public const uint MainDescriptor = 0x255ECA8;
    public const uint MainBank = 0x255EBE0;
    private const uint ActionParameters = 0x1D99A78;

    public static NativeBattleChoco100Frame Initialize70D390(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleChoco100Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 100, Entry: 0x70D390, Initializer: 0x70D370 } || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Choco100 requires its actual entry and action parameters.");
        }
        NativeBattleChoco100State retained = memory.Choco100 ?? throw new NotSupportedException("Native Choco100 retained owners are missing.");
        retained.Validate();
        NativeBattleGfAnimationSource source = retained.ProgramSource ?? (retained.ProgramSourceKnownCold ? resources.ProgramSource : throw new NotSupportedException("Native Choco100 warm static program source is missing."));
        if (source.Address != 0x1663154 || source.IsDefault || source.Length != 0x2884)
        {
            throw new InvalidDataException("Native Choco100 static program source contradicts its actual owner.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(0x2564168, 0x2564178, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(0x255EBB0, 0x255EAD8, 0x48, 3),
            NativeBattleEffectTaskPool.Initialize(0x2563EC8, 0x2563438, 0x2A4, 4),
            NativeBattleEffectTaskPool.Initialize(0x2563420, 0x2561F20, 0x540, 4),
            NativeBattleEffectTaskPool.Initialize(0x255ECB8, 0x255ECC8, 0x70, 100),
            NativeBattleEffectTaskPool.Initialize(0x2561890, 0x25618A0, 0xD0, 8),
            NativeBattleEffectTaskPool.Initialize(0x2564158, 0x2563ED8, 0x40, 10),
            NativeBattleEffectTaskPool.Initialize(0x25656B8, 0x2565438, 0x140, 2),
            NativeBattleEffectTaskPool.Initialize(0x2565868, 0x25656E0, 0x30, 2)
        ]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x70EB00, 0x64);
        hierarchy = appended.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Choco100 initial contact pointer is unknown.");
        NativeBattleModulePoolTask root = hierarchy.Read(appended.Address).WriteDword(0xC, ActionParameters).WriteByte(0x2C, header.ReadByte(0)).WriteByte(0x2D, packets.ReadContact(contact).Primary.ActorIndex).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17));
        byte last = unchecked((byte)(root.ReadWord(0x5A) - 1));
        if (last < root.ReadWord(0x58))
        {
            last = unchecked((byte)root.ReadWord(0x58));
        }
        hierarchy = hierarchy.Write(appended.Address, root.WriteByte(0x2F, last));
        int textureOffset = TextureOffset(effect);
        if ((effect.Parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Choco100 TIM queue is unknown.");
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Choco100 loaded arena is unknown."), textureOffset);
            memory = memory with { Textures = textures };
        }
        retained = retained with { ProgramSource = source, ProgramSourceKnownCold = false, FreezePrimary = 0, FreezeSecondary = 0, NextAllocation = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)textureOffset + 0x2000) };
        return new(memory with { Choco100 = retained }, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = MainDescriptor }, random);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int index = Enumerable.Range(0, effect.Pools.Length).FirstOrDefault(slot => effect.Pools[slot].DescriptorAddress == MainDescriptor, -1);
        if (index < 0)
        {
            throw new NotSupportedException("Native Choco100 completion requires its actual main pool.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        uint delta = unchecked(address - pool.BankAddress);
        if (delta % pool.Stride != 0x62 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Choco100 completion pointer is not a root BYTE62.");
        }
        int slot = (int)(delta / pool.Stride);
        return effect with { Pools = effect.Pools.SetItem(index, pool.WriteSlot(slot, pool.Slots[slot].WriteByte(0x62, value))) };
    }

    public static NativeBattleChoco100Frame AppendProgram(NativeBattleChoco100Frame frame, uint parentAddress, NativeBattleChoco97AppendProgram operation, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Choco100 GF append lost its actual action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        int pool = Enumerable.Range(0, hierarchy.Pools.Length).Single(index => hierarchy.Pools[index].DescriptorAddress == 0x2563420);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, operation.Callback, 0x540, parentAddress, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, header, target));
        if (appended.Address == 0)
        {
            throw new NotSupportedException("Native Choco100 exhausted a mandatory GF allocation.");
        }
        NativeBattleModulePoolTask child = appended.Hierarchy.Read(appended.Address).WriteDword(0x74, operation.Source).WriteDword(0x78, unchecked((uint)(int)(short)operation.ClearBytes)).WriteWord(0x80, unchecked((ushort)operation.Arg5)).WriteWord(0x82, unchecked((ushort)operation.Arg6));
        return frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Write(appended.Address, child).Pools } };
    }

    public static void RequireLoadedModel498(NativeBattleChoco97IoBanks? banks, NativeBattleEffectOwnedModelResource model)
    {
        if (model.Address != 0x19E8678 || model.Bytes.IsDefault || model.Bytes.Length != 0x5090)
        {
            throw new InvalidDataException("Native Choco100 loaded model requires the actual file498 byte owner.");
        }
        if (banks is null)
        {
            throw new NotSupportedException("Native Choco100 loaded model requires its completed file498 IO owner.");
        }
        byte[] current = banks.Read(model.Address, model.Bytes.Length);
        if (!current.AsSpan().SequenceEqual(model.Bytes.AsSpan()))
        {
            throw new InvalidDataException("Native Choco100 loaded model IO bytes contradict its authenticated resource.");
        }
    }

    public static NativeBattleChoco100Frame Advance70EB00(NativeBattleChoco100Frame frame, NativeBattleChoco100Inputs inputs, NativeBattleChoco100Resources resources)
    {
        frame.Effect.Validate();
        State().Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Choco100 lost its actual action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy.Validate();
        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte header, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, header, target);
        NativeBattleEffectHierarchyStep roots = hierarchy.Advance(0, (current, address) =>
        {
            hierarchy = current;
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback != 0x70EB00)
            {
                throw new NotSupportedException("Native Choco100 root callback changed its contract.");
            }
            int textureOffset = TextureOffset(frame.Effect);
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            SetState(State() with { ViewCopy = MatrixBytes(inputs.View), PrivateBufferAliasOffset = textureOffset + (odd ? 0x1000 : 0x2000) });
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = textureOffset + (odd ? 0 : 0x1000) } };
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
                        AcceptNodes(NativeBattleChoco100Nodes.ClearStorage(NodeFrame(), textureOffset));
                        SetState(State() with { NextAllocation = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)textureOffset + 0x13760) });
                        Store(address, Next(hierarchy.Read(address)));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x63, 1)));
                    Append(1, 0x70ED60, 0x58, address);
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
                    throw new NotSupportedException("Native Choco100 root phase is unsupported.");
            }
            AcceptNodes(NativeBattleChoco100Nodes.BeginPass(NodeFrame(), inputs.View, frame.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Choco100 private cursor is missing.")));
            Store(address, hierarchy.Read(address).WriteWord(0x5E, 0));
            int survivors = 0;
            foreach (int pool in new[] { 1, 2, 9, 8, 3, 4, 5, 6, 7 })
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
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            switch (task.Callback)
            {
                case 0x70ED60:
                    Target(address);
                    return Finish(address, true);
                case 0x70F370:
                    Director(address);
                    return Finish(address, false);
                case 0x70EF30:
                    Io(address);
                    return Finish(address, false);
                case 0x715540:
                case 0x7175F0:
                    Model(address);
                    return Finish(address, false);
                case 0x70FAF0:
                    Nodes(address);
                    return Finish(address, true);
                case 0x70F5F0:
                case 0x7163D0:
                case 0x716840:
                case 0x716950:
                    Sync();
                    NativeBattleChoco100ProgramStep program = inputs.AdvanceProgram(frame, address, hierarchy.Read(address));
                    Accept(program.Frame);
                    Store(address, program.Task);
                    return Finish(address, false);
                case NativeBattleChoco100CameraTask.Callback:
                    Camera(address);
                    return Finish(address, false);
                case 0x715AB0:
                case 0x716A60:
                case 0x716B50:
                    Sync();
                    NativeBattleChoco100TaskStep auxiliary = inputs.AdvanceAux(frame, address, hierarchy.Read(address));
                    Accept(auxiliary.Frame);
                    Store(address, auxiliary.Task);
                    return Finish(address, false);
                case 0x716C20:
                    Sync();
                    NativeBattleChoco100TaskStep secondary = inputs.AdvanceSecondary(frame, address, hierarchy.Read(address));
                    Accept(secondary.Frame);
                    Store(address, secondary.Task);
                    return Finish(address, false);
                case 0x716300:
                    Sync();
                    NativeBattleChoco100TaskStep small = inputs.AdvanceSmall(frame, address, hierarchy.Read(address));
                    Accept(small.Frame);
                    Store(address, small.Task);
                    return Finish(address, false);
                default:
                    throw new NotSupportedException($"Native Choco100 callback{task.Callback:X8} is unsupported.");
            }
        }

        void Target(uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(hierarchy.Read(address), Models(), inputs.ReadPoseSource);
            int actor = task.ReadByte(0x2D);
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattlePoseSource source = inputs.ReadPoseSource(actor);
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Choco100 target bounds require its current sequence.");
            NativeBattleDecodedPose pose = sequence.DecodedPose ?? source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Choco100 target pose is unknown."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Choco100 target frame is unknown."), sequence.PoseFlags109);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
            Store(address, task);
            switch (Stage(task))
            {
                case 0:
                    SetState(State() with { Context = NativeBattleChoco100Context.Initialize70F290(inputs.ReadModel) });
                    ImmutableArray<byte> script = State().Io?.Script ?? (State().IoScriptKnownCold ? resources.Io.Script.Data : throw new NotSupportedException("Native Choco100 warm IO script is unknown."));
                    NativeBattleChoco97IoBanks banks = State().Io?.Banks ?? State().PendingIoBanks ?? throw new NotSupportedException("Native Choco100 external IO banks are unknown.");
                    NativeBattleChoco100IoInitialization initialized = NativeBattleChoco100Io.Initialize70EE30(script, State().NextAllocation ?? throw new NotSupportedException("Native Choco100 next allocation is unknown."), banks);
                    SetState(State() with { Io = initialized.State, IoScriptKnownCold = false, PendingIoBanks = null, NextAllocation = initialized.NextAddress });
                    Append(9, 0x70EF30, 0x30, address);
                    Append(2, NativeBattleChoco100Director.Callback, 0x48, address);
                    Store(address, Next(hierarchy.Read(address)));
                    break;
                case 1:
                    if (Context().Word(0x48) == 1)
                    {
                        int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        if ((uint)headerIndex >= inputs.Packets.Headers.Length)
                        {
                            throw new NotSupportedException("Native Choco100 contact header leaves its actual arena.");
                        }
                        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[headerIndex];
                        int count = header.ReadByte(16);
                        if (count > 0)
                        {
                            int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Native Choco100 contact pointer is unknown.");
                            for (int contact = 0; contact < count; contact++)
                            {
                                Emit(new NativeBattleChoco97Contact(checked(firstContact + contact)));
                            }
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
                    throw new NotSupportedException("Native Choco100 target phase is unsupported.");
            }
        }

        void Io(uint address)
        {
            NativeBattleChoco100IoAdvance step = NativeBattleChoco100Io.Advance70EF30(hierarchy.Read(address), State().Io ?? throw new NotSupportedException("Native Choco100 IO has not been initialized."));
            SetState(State() with { Io = step.State });
            Store(address, step.Task);
            if (step.Request is { } request)
            {
                Emit(new NativeBattleChoco100FileRequest(request));
            }
        }

        void Camera(uint address)
        {
            NativeBattleChoco100CameraTaskStep step = NativeBattleChoco100CameraTask.Advance7148C0(hierarchy.Read(address), State().Camera, resources.Camera, Context());
            Store(address, step.Task);
            SetState(State() with { Camera = step.Camera, Context = step.Context });
            foreach (NativeBattleChoco97Operation operation in step.Operations)
            {
                Emit(operation);
            }
        }

        void Director(uint address)
        {
            NativeBattleChoco97DirectorInputs services = new(() => BinaryPrimitives.ReadUInt16LittleEndian((State().Io ?? throw new NotSupportedException("Native Choco100 director IO is unknown.")).Control.AsSpan().Slice(0x166)), offset => BinaryPrimitives.ReadUInt32LittleEndian((State().Io ?? throw new NotSupportedException("Native Choco100 IO directory is unknown.")).Control.AsSpan().Slice(offset)), current =>
            {
                SetState(State() with { Context = current.Context });
                NativeBattleChoco100IoRelease release = NativeBattleChoco100Io.ReleaseWait(State().Io ?? throw new NotSupportedException("Native Choco100 IO wait owner is unknown."));
                SetState(State() with { Io = release.State });
                return new(current, release.Released);
            }, (current, operation) =>
            {
                Store(address, current.Task);
                SetState(State() with { Context = current.Context });
                switch (operation)
                {
                    case NativeBattleChoco97StartVibration:
                        Sync();
                        NativeBattleChoco100VibrationStep vibration = inputs.StartVibration(frame);
                        Accept(vibration.Frame);
                        SetState(State() with { Context = Context().WriteWord(0x52, unchecked((ushort)vibration.Handle)) });
                        break;
                    case NativeBattleChoco97AppendNeutral neutral:
                        uint child = Append(3, NativeBattleChoco100Nodes.Callback, 0x2A4, address);
                        Store(child, hierarchy.Read(child).WriteDword(0x30, neutral.Source).WriteWord(0x298, neutral.Delay).WriteWord(0x29C, neutral.Mode).WriteWord(0x29E, neutral.Duration));
                        break;
                    case NativeBattleChoco97AppendProgram program:
                        Sync();
                        Accept(AppendProgram(frame, address, program, inputs.Packets));
                        break;
                    case NativeBattleChoco97AppendAux auxiliary:
                        Append(5, auxiliary.Callback, auxiliary.Size, address);
                        break;
                    case NativeBattleChoco100AppendContactSecondary secondary:
                        child = Append(6, NativeBattleChoco100Director.ContactSecondaryCallback, NativeBattleChoco100Director.ContactSecondaryTaskBytes, address);
                        Store(child, hierarchy.Read(child).WriteByte(0x2D, secondary.Actor));
                        break;
                    case NativeBattleChoco99AppendSmall small:
                        child = Append(7, small.Callback, 0x40, address);
                        Store(child, hierarchy.Read(child).WriteDword(0x34, small.Pointer34).WriteWord(0x38, small.Value38));
                        break;
                    case NativeBattleChoco97AppendModel:
                        child = Append(8, 0x715540, 0x140, address);
                        NativeBattleEffectOwnedModelStep model = NativeBattleChoco100Model.Initialize(hierarchy.Read(child), child, resources.Model);
                        Store(child, model.Task);
                        SetState(State() with { Models = State().Models.SetItem(child, model.State) });
                        break;
                    case NativeBattleChoco100AppendLoadedModel loaded:
                        if (loaded.ResourceAddress != resources.LoadedModel.Model.Address || loaded.InitializationArgument != 0x3C)
                        {
                            throw new NotSupportedException("Native Choco100 loaded model requires its actual current resource and initialization operand.");
                        }
                        RequireLoadedModel498(State().Io?.Banks, resources.LoadedModel.Model);
                        child = Append(8, NativeBattleChoco100Director.LoadedModelCallback, NativeBattleChoco100Director.LoadedModelTaskBytes, address);
                        NativeBattleEffectOwnedModelStep loadedModel = NativeBattleChoco100LoadedModel.Initialize(hierarchy.Read(child), child, resources.LoadedModel);
                        Store(child, loadedModel.Task);
                        SetState(State() with { Models = State().Models.SetItem(child, loadedModel.State) });
                        break;
                    case NativeBattleChoco100DirectorSound sound:
                        Emit(new NativeBattleChoco97Sound(sound.Descriptor, sound.Flags, sound.Pan));
                        break;
                    case NativeBattleChoco97LoadModelTim:
                        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Choco100 model TIM queue is unknown.");
                        (textures, _) = textures.EnqueueTim505E30(resources.Model.ModelTim);
                        frame = frame with { Memory = frame.Memory with { Textures = textures } };
                        break;
                    case NativeBattleChoco97SetFreeze freeze:
                        (uint primary, uint secondaryFreeze) = NativeBattleChoco100Director.SetFreeze7154B0(State().FreezePrimary ?? throw new NotSupportedException("Native Choco100 primary freeze is unknown."), State().FreezeSecondary ?? throw new NotSupportedException("Native Choco100 secondary freeze is unknown."), freeze.Value);
                        SetState(State() with { FreezePrimary = primary, FreezeSecondary = secondaryFreeze });
                        break;
                    case NativeBattleChoco97StopVibration stop:
                        Emit(new NativeBattleChoco97VibrationStop(stop.Handle));
                        break;
                    default:
                        throw new NotSupportedException("Native Choco100 director operation is unsupported.");
                }
                return new(hierarchy.Read(address), Context());
            });
            NativeBattleChoco97DirectorStep step = NativeBattleChoco100Director.Advance70F370(new(hierarchy.Read(address), Context()), new(services, current => ReadSelectedContacts(current.Task)));
            Store(address, step.Frame.Task);
            SetState(State() with { Context = step.Frame.Context });
        }

        void Model(uint address)
        {
            NativeBattleEffectOwnedModelState owned = State().Models.TryGetValue(address, out NativeBattleEffectOwnedModelState? found) ? found ?? throw new NotSupportedException("Native Choco100 model decoder is null.") : throw new NotSupportedException("Native Choco100 physical model task has no retained decoder owner.");
            NativeBattleChoco97ModelAdvance step;
            if (hierarchy.Read(address).Callback == NativeBattleChoco100LoadedModel.Callback)
            {
                step = NativeBattleChoco100LoadedModel.Advance7175F0(hierarchy.Read(address), owned, resources.LoadedModel, new(ContextBytes(8, 8), AtLeast, Request, Draw));
            }
            else
            {
                NativeBattleChoco97ModelInputs services = new(ContextBytes(0, 8), AtLeast, Request, (current, operation) =>
                {
                    Store(address, current.Task);
                    SetState(State() with { Models = State().Models.SetItem(address, current.State) });
                    switch (operation)
                    {
                        case NativeBattleChoco97ModelAppendAuxiliary append:
                            if (append.PoolAddress != 0x255ECB8)
                            {
                                throw new NotSupportedException("Native Choco100 static model child leaves its actual auxiliary pool.");
                            }
                            Append(5, append.Callback, append.TaskBytes, address);
                            break;
                        case NativeBattleChoco97ModelSound sound:
                            Emit(new NativeBattleChoco97Sound(sound.DescriptorAddress, sound.Flags, sound.Pan));
                            break;
                        default:
                            throw new NotSupportedException("Native Choco100 static model operation is unsupported.");
                    }
                    return current with { Task = hierarchy.Read(address) };
                }, Draw);
                step = NativeBattleChoco100Model.Advance715540(hierarchy.Read(address), owned, resources.Model, services);
            }
            Store(address, step.Task);
            SetState(State() with { Models = State().Models.SetItem(address, step.State) });
            foreach (NativeBattleTextureCopy copy in step.TextureCopies)
            {
                Emit(new NativeBattleChoco97TextureCopy(copy));
            }

            bool AtLeast(int mode) => NativeBattleChoco97CameraTask.ModeAtLeast730E80(Context().Word(0x40), unchecked((short)mode));
            bool Request(int mode)
            {
                (ushort requested, bool accepted) = NativeBattleChoco97CameraTask.RequestMode730EC0(Context().Word(0x40), Context().Word(0x44), unchecked((short)mode));
                if (accepted)
                {
                    SetState(State() with { Context = Context().WriteWord(0x44, requested) });
                }
                return accepted;
            }
            NativeBattleEffectOwnedModelDraw Draw(NativeBattleEffectOwnedModelStep current)
            {
                Store(address, current.Task);
                SetState(State() with { Models = State().Models.SetItem(address, current.State) });
                Sync();
                NativeBattleChoco100ModelDrawStep drawing = inputs.DrawModel(frame, hierarchy.Read(address), current.State);
                Accept(drawing.Frame);
                Store(address, drawing.Task);
                return drawing.Drawing;
            }
        }

        ImmutableArray<NativeBattleContactPacket> ReadSelectedContacts(NativeBattleModulePoolTask task)
        {
            int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
            if ((uint)headerIndex >= inputs.Packets.Headers.Length)
            {
                throw new NotSupportedException("Native Choco100 selected contact header leaves its actual arena.");
            }
            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[headerIndex];
            int count = header.ReadByte(16);
            if (count == 0)
            {
                return [];
            }
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Choco100 selected contact pointer is unknown.");
            return Enumerable.Range(0, count).Select(index => inputs.Packets.ReadContact(checked(first + index))).ToImmutableArray();
        }

        void Nodes(uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            int phase = Stage(task);
            if (phase == 0 && Word(task, 0x24) >= Word(task, 0x298))
            {
                NativeBattleChoco97NodeResource resource = resources.Nodes.Single(resource => resource.Address == task.ReadDword(0x30));
                NativeBattleEffectNodeDogOrigins origins = new(ContextVector(0), ContextVector(8));
                AcceptNodes(NativeBattleChoco100Nodes.InitializeContext(NodeFrame(), address, resource, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, origins));
                Sync();
                NativeBattleChoco100NodesStep initial = inputs.AdvanceNodes(frame, address);
                Accept(initial.Frame);
                Store(address, Next(hierarchy.Read(address)));
            }
            else if (phase == 1)
            {
                Sync();
                NativeBattleChoco100NodesStep advanced = inputs.AdvanceNodes(frame, address);
                Accept(advanced.Frame);
                if (advanced.Complete)
                {
                    task = hierarchy.Read(address);
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                }
            }
            else if (phase != 0 && phase != 2)
            {
                throw new NotSupportedException("Native Choco100 neutral phase is unsupported.");
            }
        }

        NativeBattleChoco100State State() => frame.Memory.Choco100 ?? throw new NotSupportedException("Native Choco100 retained owners are missing.");
        void SetState(NativeBattleChoco100State state) => frame = frame with { Memory = frame.Memory with { Choco100 = state } };
        NativeBattleChoco97Context Context() => State().Context ?? throw new NotSupportedException("Native Choco100 context is unknown.");
        ImmutableArray<byte> ContextBytes(int offset, int length) => Context().Data.AsSpan().Slice(offset, length).ToArray().ToImmutableArray();
        NativeBattleVector ContextVector(int offset) => new(unchecked((short)Context().Word(offset)), unchecked((short)Context().Word(offset + 2)), unchecked((short)Context().Word(offset + 4)));
        NativeBattleChoco100NodeFrame NodeFrame()
        {
            Sync();
            return new(frame.Memory, frame.Effect, State().Nodes, State().Io, frame.Random);
        }
        void AcceptNodes(NativeBattleChoco100NodeFrame nodes)
        {
            NativeBattleChoco100State retained = State() with { Nodes = nodes.State, Io = nodes.Io };
            frame = new(nodes.Memory with { Choco100 = retained }, nodes.Effect, nodes.Random);
            hierarchy = new(frame.Effect.Pools);
        }
        void Accept(NativeBattleChoco100Frame value)
        {
            frame = value;
            hierarchy = new(frame.Effect.Pools);
        }
        uint Append(int pool, uint callback, int requested, uint parent)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, requested, parent, Select);
            hierarchy = appended.Hierarchy;
            return appended.Address != 0 ? appended.Address : throw new NotSupportedException("Native Choco100 exhausted a mandatory physical task allocation.");
        }
        void Store(uint address, NativeBattleModulePoolTask task)
        {
            hierarchy = hierarchy.Write(address, task);
        }
        int Finish(uint address, bool increment)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (increment)
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

    private static int TextureOffset(NativeBattleOriginalEffectState effect) => effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x255EAC8).ArenaOffset;
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
