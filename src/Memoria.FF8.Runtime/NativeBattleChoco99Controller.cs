using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco99Controller
{
    public const uint MainDescriptor = 0x2565C10;
    public const uint MainBank = 0x2565B48;
    private const uint ActionParameters = 0x1D99A78;

    public static NativeBattleChoco99Frame Initialize717D30(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleChoco99Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 99, Entry: 0x717D30, Initializer: 0x717D10 } || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Choco99 requires its actual entry and action parameters.");
        }
        NativeBattleChoco99State retained = memory.Choco99 ?? throw new NotSupportedException("Native Choco99 retained owners are missing.");
        retained.Validate();
        NativeBattleGfAnimationSource source = retained.ProgramSource ?? (retained.ProgramSourceKnownCold ? resources.ProgramSource : throw new NotSupportedException("Native Choco99 warm static program source is missing."));
        if (source.Address != 0x166DC58 || source.IsDefault || source.Length != 0x2884)
        {
            throw new InvalidDataException("Native Choco99 static program source contradicts its actual owner.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(0x256C480, 0x256C490, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(0x2565B18, 0x2565A40, 0x48, 3),
            NativeBattleEffectTaskPool.Initialize(0x256C1E0, 0x256B9F0, 0x2A4, 3),
            NativeBattleEffectTaskPool.Initialize(0x256B9D8, 0x2569A28, 0x548, 6),
            NativeBattleEffectTaskPool.Initialize(0x2565C20, 0x2565C30, 0xB0, 80),
            NativeBattleEffectTaskPool.Initialize(0x2569338, 0x2569348, 0x58, 20),
            NativeBattleEffectTaskPool.Initialize(0x256C470, 0x256C1F0, 0x40, 10),
            NativeBattleEffectTaskPool.Initialize(0x256D9D0, 0x256D750, 0x140, 2),
            NativeBattleEffectTaskPool.Initialize(0x256E1C0, 0x256D9F8, 0x30, 2)
        ]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x717ED0, 0x64);
        hierarchy = appended.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Choco99 initial contact pointer is unknown.");
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
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Choco99 TIM queue is unknown.");
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Choco99 loaded arena is unknown."), textureOffset);
            memory = memory with { Textures = textures };
        }
        retained = retained with { ProgramSource = source, ProgramSourceKnownCold = false, FreezePrimary = 0, FreezeSecondary = 0, NextAllocation = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)textureOffset + 0x2000) };
        return new(memory with { Choco99 = retained }, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = MainDescriptor }, random);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int index = Enumerable.Range(0, effect.Pools.Length).FirstOrDefault(slot => effect.Pools[slot].DescriptorAddress == MainDescriptor, -1);
        if (index < 0)
        {
            throw new NotSupportedException("Native Choco99 completion requires its actual main pool.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        uint delta = unchecked(address - pool.BankAddress);
        if (delta % pool.Stride != 0x62 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Choco99 completion pointer is not a root BYTE62.");
        }
        int slot = (int)(delta / pool.Stride);
        return effect with { Pools = effect.Pools.SetItem(index, pool.WriteSlot(slot, pool.Slots[slot].WriteByte(0x62, value))) };
    }

    public static NativeBattleChoco99Frame AppendProgram(NativeBattleChoco99Frame frame, uint parentAddress, NativeBattleChoco97AppendProgram operation, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Choco99 GF append lost its actual action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        int pool = Enumerable.Range(0, hierarchy.Pools.Length).Single(index => hierarchy.Pools[index].DescriptorAddress == 0x256B9D8);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, operation.Callback, 0x548, parentAddress, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, header, target));
        if (appended.Address == 0)
        {
            throw new NotSupportedException("Native Choco99 exhausted a mandatory GF allocation.");
        }
        NativeBattleModulePoolTask child = appended.Hierarchy.Read(appended.Address).WriteDword(0x74, operation.Source).WriteDword(0x78, unchecked((uint)(int)(short)operation.ClearBytes)).WriteWord(0x80, unchecked((ushort)operation.Arg5)).WriteWord(0x82, unchecked((ushort)operation.Arg6));
        return frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Write(appended.Address, child).Pools } };
    }

    public static NativeBattleChoco99Frame Advance717ED0(NativeBattleChoco99Frame frame, NativeBattleChoco99Inputs inputs, NativeBattleChoco99Resources resources)
    {
        frame.Effect.Validate();
        State().Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Choco99 lost its actual action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy.Validate();
        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte header, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, header, target);
        NativeBattleEffectHierarchyStep roots = hierarchy.Advance(0, (current, address) =>
        {
            hierarchy = current;
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback != 0x717ED0)
            {
                throw new NotSupportedException("Native Choco99 root callback changed its contract.");
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
                        AcceptNodes(NativeBattleChoco99Nodes.ClearStorage(NodeFrame(), textureOffset));
                        SetState(State() with { NextAllocation = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)textureOffset + 0x13760) });
                        Store(address, Next(hierarchy.Read(address)));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x63, 1)));
                    Append(1, 0x718130, 0x58, address);
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
                    throw new NotSupportedException("Native Choco99 root phase is unsupported.");
            }
            AcceptNodes(NativeBattleChoco99Nodes.BeginPass(NodeFrame(), inputs.View, frame.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Choco99 private cursor is missing.")));
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
                case 0x718130:
                    Target(address);
                    return Finish(address, true);
                case 0x718750:
                    Director(address);
                    return Finish(address, false);
                case 0x718310:
                    Io(address);
                    return Finish(address, false);
                case 0x71E8D0:
                    Model(address);
                    return Finish(address, false);
                case 0x718E70:
                    Nodes(address);
                    return Finish(address, true);
                case 0x7189B0:
                case 0x71F950:
                case 0x71FDC0:
                case 0x71FEC0:
                case 0x71FFB0:
                case 0x7201F0:
                case 0x720320:
                case 0x720410:
                case 0x720610:
                    Sync();
                    NativeBattleChoco99ProgramStep program = inputs.AdvanceProgram(frame, address, hierarchy.Read(address));
                    Accept(program.Frame);
                    Store(address, program.Task);
                    return Finish(address, false);
                case NativeBattleChoco99CameraTask.Callback:
                    Camera(address);
                    return Finish(address, false);
                case 0x71F310:
                case 0x721010:
                case 0x721100:
                case 0x721460:
                case 0x7215A0:
                case 0x71EE40:
                case 0x720500:
                case 0x720790:
                case 0x7208D0:
                case NativeBattleChoco99Particles.TrailCallback:
                case NativeBattleChoco99Particles.RadialCallback:
                    Sync();
                    NativeBattleChoco99TaskStep auxiliary = inputs.AdvanceAux(frame, address, hierarchy.Read(address));
                    Accept(auxiliary.Frame);
                    Store(address, auxiliary.Task);
                    return Finish(address, false);
                case 0x7211D0:
                case 0x721390:
                    Sync();
                    NativeBattleChoco99TaskStep secondary = inputs.AdvanceSecondary(frame, address, hierarchy.Read(address));
                    Accept(secondary.Frame);
                    Store(address, secondary.Task);
                    return Finish(address, false);
                case 0x71F880:
                    NativeBattleChoco99CameraYStep translation = NativeBattleChoco99Meteo.Advance71F880(task, resources.Meteo.CameraY);
                    Store(address, translation.Task);
                    if (translation.Y is short y)
                    {
                        Emit(new NativeBattleChoco99ViewTranslationY(y));
                    }
                    return Finish(address, false);
                case 0x8DDC30:
                    Sync();
                    NativeBattleChoco99OverlayStep overlay = inputs.AdvanceOverlay(frame, address);
                    Accept(overlay.Frame);
                    return overlay.Result;
                default:
                    throw new NotSupportedException($"Native Choco99 callback{task.Callback:X8} is unsupported.");
            }
        }

        void Target(uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(hierarchy.Read(address), Models(), inputs.ReadPoseSource);
            int actor = task.ReadByte(0x2D);
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattlePoseSource source = inputs.ReadPoseSource(actor);
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Choco99 target bounds require its current sequence.");
            NativeBattleDecodedPose pose = sequence.DecodedPose ?? source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Choco99 target pose is unknown."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Choco99 target frame is unknown."), sequence.PoseFlags109);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
            Store(address, task);
            switch (Stage(task))
            {
                case 0:
                    SetState(State() with { Context = NativeBattleChoco99Context.Initialize718670(inputs.ReadModel) });
                    ImmutableArray<byte> script = State().Io?.Script ?? (State().IoScriptKnownCold ? resources.Io.Script.Data : throw new NotSupportedException("Native Choco99 warm IO script is unknown."));
                    NativeBattleChoco97IoBanks banks = State().Io?.Banks ?? State().PendingIoBanks ?? throw new NotSupportedException("Native Choco99 external IO banks are unknown.");
                    NativeBattleChoco99IoInitialization initialized = NativeBattleChoco99Io.Initialize718200(script, State().NextAllocation ?? throw new NotSupportedException("Native Choco99 next allocation is unknown."), banks);
                    SetState(State() with { Io = initialized.State, IoScriptKnownCold = false, PendingIoBanks = null, NextAllocation = initialized.NextAddress });
                    Append(9, 0x718310, 0x30, address);
                    Append(2, NativeBattleChoco99Director.Callback, 0x48, address);
                    Store(address, Next(hierarchy.Read(address)));
                    break;
                case 1:
                    if (Context().Word(0x48) == 1)
                    {
                        int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        if ((uint)headerIndex >= inputs.Packets.Headers.Length)
                        {
                            throw new NotSupportedException("Native Choco99 contact header leaves its actual arena.");
                        }
                        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[headerIndex];
                        int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Native Choco99 contact pointer is unknown.");
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
                    throw new NotSupportedException("Native Choco99 target phase is unsupported.");
            }
        }

        void Io(uint address)
        {
            NativeBattleChoco99IoAdvance step = NativeBattleChoco99Io.Advance718310(hierarchy.Read(address), State().Io ?? throw new NotSupportedException("Native Choco99 IO has not been initialized."));
            SetState(State() with { Io = step.State });
            Store(address, step.Task);
            if (step.Request is { } request)
            {
                Emit(new NativeBattleChoco99FileRequest(request));
            }
        }

        void Camera(uint address)
        {
            NativeBattleChoco99CameraTaskStep step = NativeBattleChoco99CameraTask.Advance71DC40(hierarchy.Read(address), State().Camera, resources.Camera, unchecked((short)Context().Word(0x42)), ContextBytes(0, 8), ContextBytes(8, 8));
            Store(address, step.Task);
            SetState(State() with { Camera = step.Camera });
            foreach (NativeBattleMoogleCameraStep publication in step.Publications)
            {
                Emit(new NativeBattleChoco97CameraPublish(publication));
            }
        }

        void Director(uint address)
        {
            NativeBattleChoco97DirectorInputs services = new(() => BinaryPrimitives.ReadUInt16LittleEndian((State().Io ?? throw new NotSupportedException("Native Choco99 director IO is unknown.")).Control.AsSpan().Slice(0x166)), offset => BinaryPrimitives.ReadUInt32LittleEndian((State().Io ?? throw new NotSupportedException("Native Choco99 IO directory is unknown.")).Control.AsSpan().Slice(offset)), current =>
            {
                SetState(State() with { Context = current.Context });
                NativeBattleChoco99IoRelease release = NativeBattleChoco99Io.ReleaseWait(State().Io ?? throw new NotSupportedException("Native Choco99 IO wait owner is unknown."));
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
                        NativeBattleChoco99VibrationStep vibration = inputs.StartVibration(frame);
                        Accept(vibration.Frame);
                        SetState(State() with { Context = Context().WriteWord(0x52, unchecked((ushort)vibration.Handle)) });
                        break;
                    case NativeBattleChoco97AppendNeutral neutral:
                        uint child = Append(3, NativeBattleChoco99Nodes.Callback, 0x2A4, address);
                        Store(child, hierarchy.Read(child).WriteDword(0x30, neutral.Source).WriteWord(0x298, neutral.Delay).WriteWord(0x29C, neutral.Mode).WriteWord(0x29E, neutral.Duration));
                        break;
                    case NativeBattleChoco97AppendProgram program:
                        Sync();
                        Accept(AppendProgram(frame, address, program, inputs.Packets));
                        break;
                    case NativeBattleChoco97AppendAux auxiliary:
                        Append(5, auxiliary.Callback, auxiliary.Size, address);
                        break;
                    case NativeBattleChoco99AppendSecondary secondary:
                        Append(6, secondary.Callback, secondary.Size, address);
                        break;
                    case NativeBattleChoco99AppendSmall small:
                        child = Append(7, small.Callback, 0x40, address);
                        Store(child, hierarchy.Read(child).WriteDword(0x34, small.Pointer34).WriteWord(0x38, small.Value38));
                        break;
                    case NativeBattleChoco97AppendModel:
                        child = Append(8, 0x71E8D0, 0x140, address);
                        NativeBattleEffectOwnedModelStep model = NativeBattleChoco99Model.Initialize(hierarchy.Read(child), child, resources.Model);
                        Store(child, model.Task);
                        SetState(State() with { Models = State().Models.SetItem(child, model.State) });
                        break;
                    case NativeBattleChoco97LoadModelTim:
                        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Choco99 model TIM queue is unknown.");
                        (textures, _) = textures.EnqueueTim505E30(resources.Model.ModelTim);
                        frame = frame with { Memory = frame.Memory with { Textures = textures } };
                        break;
                    case NativeBattleChoco97SetFreeze freeze:
                        (uint primary, uint secondaryFreeze) = NativeBattleChoco99Director.SetFreeze71E840(State().FreezePrimary ?? throw new NotSupportedException("Native Choco99 primary freeze is unknown."), State().FreezeSecondary ?? throw new NotSupportedException("Native Choco99 secondary freeze is unknown."), freeze.Value);
                        SetState(State() with { FreezePrimary = primary, FreezeSecondary = secondaryFreeze });
                        break;
                    case NativeBattleChoco97StopVibration stop:
                        Emit(new NativeBattleChoco97VibrationStop(stop.Handle));
                        break;
                    default:
                        throw new NotSupportedException("Native Choco99 director operation is unsupported.");
                }
                return new(hierarchy.Read(address), Context());
            });
            NativeBattleChoco97DirectorStep step = NativeBattleChoco99Director.Advance718750(new(hierarchy.Read(address), Context()), services);
            Store(address, step.Frame.Task);
            SetState(State() with { Context = step.Frame.Context });
        }

        void Model(uint address)
        {
            NativeBattleEffectOwnedModelState owned = State().Models.TryGetValue(address, out NativeBattleEffectOwnedModelState? found) ? found ?? throw new NotSupportedException("Native Choco99 physical model decoder is null.") : throw new NotSupportedException("Native Choco99 physical model task has no retained decoder owner.");
            NativeBattleChoco97ModelInputs services = new(ContextBytes(0, 8), mode => NativeBattleChoco97CameraTask.ModeAtLeast730E80(Context().Word(0x40), unchecked((short)mode)), mode =>
            {
                (ushort requested, bool accepted) = NativeBattleChoco97CameraTask.RequestMode730EC0(Context().Word(0x40), Context().Word(0x44), unchecked((short)mode));
                if (accepted)
                {
                    SetState(State() with { Context = Context().WriteWord(0x44, requested) });
                }
                return accepted;
            }, (current, operation) =>
            {
                Store(address, current.Task);
                SetState(State() with { Models = State().Models.SetItem(address, current.State) });
                switch (operation)
                {
                    case NativeBattleChoco97ModelAppendAuxiliary append:
                        if (append.PoolAddress != 0x2565C20)
                        {
                            throw new NotSupportedException("Native Choco99 model child leaves its actual auxiliary pool.");
                        }
                        Append(5, append.Callback, append.TaskBytes, address);
                        break;
                    case NativeBattleChoco97ModelSound sound:
                        Emit(new NativeBattleChoco97Sound(sound.DescriptorAddress, sound.Flags, sound.Pan));
                        break;
                    default:
                        throw new NotSupportedException("Native Choco99 model operation is unsupported.");
                }
                return current with { Task = hierarchy.Read(address) };
            }, current =>
            {
                Store(address, current.Task);
                SetState(State() with { Models = State().Models.SetItem(address, current.State) });
                Sync();
                NativeBattleChoco99ModelDrawStep draw = inputs.DrawModel(frame, hierarchy.Read(address), current.State);
                Accept(draw.Frame);
                Store(address, draw.Task);
                return draw.Drawing;
            });
            NativeBattleChoco97ModelAdvance step = NativeBattleChoco99Model.Advance71E8D0(hierarchy.Read(address), owned, resources.Model, services);
            Store(address, step.Task);
            SetState(State() with { Models = State().Models.SetItem(address, step.State) });
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
                AcceptNodes(NativeBattleChoco99Nodes.InitializeContext(NodeFrame(), address, resource, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, origins));
                Sync();
                NativeBattleChoco99NodesStep initial = inputs.AdvanceNodes(frame, address);
                Accept(initial.Frame);
                Store(address, Next(hierarchy.Read(address)));
            }
            else if (phase == 1)
            {
                Sync();
                NativeBattleChoco99NodesStep advanced = inputs.AdvanceNodes(frame, address);
                Accept(advanced.Frame);
                if (advanced.Complete)
                {
                    task = hierarchy.Read(address);
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                }
            }
            else if (phase != 0 && phase != 2)
            {
                throw new NotSupportedException("Native Choco99 neutral phase is unsupported.");
            }
        }

        NativeBattleChoco99State State() => frame.Memory.Choco99 ?? throw new NotSupportedException("Native Choco99 retained owners are missing.");
        void SetState(NativeBattleChoco99State state) => frame = frame with { Memory = frame.Memory with { Choco99 = state } };
        NativeBattleChoco97Context Context() => State().Context ?? throw new NotSupportedException("Native Choco99 context is unknown.");
        ImmutableArray<byte> ContextBytes(int offset, int length) => Context().Data.AsSpan().Slice(offset, length).ToArray().ToImmutableArray();
        NativeBattleVector ContextVector(int offset) => new(unchecked((short)Context().Word(offset)), unchecked((short)Context().Word(offset + 2)), unchecked((short)Context().Word(offset + 4)));
        NativeBattleChoco99NodeFrame NodeFrame()
        {
            Sync();
            return new(frame.Memory, frame.Effect, State().Nodes, State().Io, frame.Random);
        }
        void AcceptNodes(NativeBattleChoco99NodeFrame nodes)
        {
            NativeBattleChoco99State retained = State() with { Nodes = nodes.State, Io = nodes.Io };
            frame = new(nodes.Memory with { Choco99 = retained }, nodes.Effect, nodes.Random);
            hierarchy = new(frame.Effect.Pools);
        }
        void Accept(NativeBattleChoco99Frame value)
        {
            frame = value;
            hierarchy = new(frame.Effect.Pools);
        }
        uint Append(int pool, uint callback, int requested, uint parent)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, requested, parent, Select);
            hierarchy = appended.Hierarchy;
            return appended.Address != 0 ? appended.Address : throw new NotSupportedException("Native Choco99 exhausted a mandatory physical task allocation.");
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

    private static int TextureOffset(NativeBattleOriginalEffectState effect) => effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x2565A30).ArenaOffset;
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
