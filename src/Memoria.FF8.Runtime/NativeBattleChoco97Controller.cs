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
        return InitializeFamily(memory, effect, packets, NativeBattleChocoControllerResources.From(resources), random, false);
    }

    internal static NativeBattleChoco97Frame InitializeFamily(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleChocoControllerResources resources, NativeCrtRandomState random, bool choco98)
    {
        if ((!choco98 && effect.Binding is not { Effect: 97, Entry: 0x729A60, Initializer: 0x729A40 }) || (choco98 && effect.Binding is not { Effect: 98, Entry: 0x721860, Initializer: 0x721840 }) || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Choco requires its actual entry, initializer and action parameters.");
        }
        ValidateState(memory, choco98);
        NativeBattleChocoControllerState retained = NativeBattleChocoControllerState.Read(memory, choco98);
        uint mainDescriptor = choco98 ? NativeBattleChoco98Controller.MainDescriptor : MainDescriptor;
        uint mainBank = choco98 ? NativeBattleChoco98Controller.MainBank : MainBank;
        NativeBattleGfAnimationSource source = retained.ProgramSource ?? (retained.ProgramSourceKnownCold ? resources.ProgramSource : throw new NotSupportedException("Native Choco warmed static program source is missing."));
        if (source.Address != resources.ProgramSource.Address || source.Data.IsDefault || source.Data.Length != resources.ProgramSource.Data.Length || retained.MorphScratch.IsDefault || retained.MorphScratch.Length != (choco98 ? 0x168 : 0x3C0))
        {
            throw new InvalidDataException("Native Choco program and morph owners contradict their physical extents.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(mainDescriptor, mainBank, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(choco98 ? 0x2573AC8u : 0x2579D48u, choco98 ? 0x2573AD8u : 0x2579D58u, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(choco98 ? 0x256E440u : 0x2575350u, choco98 ? 0x256E368u : 0x2575278u, 0x48, 3),
            NativeBattleEffectTaskPool.Initialize(choco98 ? 0x2573AB8u : 0x2579D38u, choco98 ? 0x25732C8u : 0x2579548u, 0x2A4, 3),
            NativeBattleEffectTaskPool.Initialize(choco98 ? 0x25732B0u : 0x2579530u, choco98 ? 0x2571120u : 0x2578030u, (ushort)(choco98 ? 0x864 : 0x540), 4),
            NativeBattleEffectTaskPool.Initialize(choco98 ? 0x256E548u : 0x2575458u, choco98 ? 0x256E558u : 0x2575468u, 0x70, 100),
            NativeBattleEffectTaskPool.Initialize(choco98 ? 0x2574ED8u : 0x257B158u, choco98 ? 0x2574D98u : 0x257B018u, 0x140, 1),
            NativeBattleEffectTaskPool.Initialize(choco98 ? 0x25750D0u : 0x257B5A8u, choco98 ? 0x2574F00u : 0x257B180u, 0x30, 2)
        ]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, choco98 ? 0x7219D0u : 0x729BD0u, 0x64);
        hierarchy = appended.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Choco requires its actual initial contact pointer.");
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
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Choco requires its actual TIM queue owner.");
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Choco requires its loaded arena."), textureOffset);
            memory = memory with { Textures = textures };
        }
        retained = retained with { ProgramSource = source, ProgramSourceKnownCold = false, FreezePrimary = 0, FreezeSecondary = 0, NextAllocation = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)textureOffset + 0x2000) };
        return new(retained.Write(memory, choco98), effect with { Pools = hierarchy.Pools, ReturnedDescriptor = mainDescriptor }, random);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        return WriteCompletionByteFamily(effect, address, value, false);
    }

    internal static NativeBattleOriginalEffectState WriteCompletionByteFamily(NativeBattleOriginalEffectState effect, uint address, byte value, bool choco98)
    {
        uint mainDescriptor = choco98 ? NativeBattleChoco98Controller.MainDescriptor : MainDescriptor;
        int index = Enumerable.Range(0, effect.Pools.Length).FirstOrDefault(slot => effect.Pools[slot].DescriptorAddress == mainDescriptor, -1);
        if (index < 0)
        {
            throw new NotSupportedException("Native Choco completion requires its retained main pool.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        uint delta = unchecked(address - pool.BankAddress);
        if (delta % pool.Stride != 0x62 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new NotSupportedException("Native Choco completion pointer is not an actual root BYTE62.");
        }
        int slot = (int)(delta / pool.Stride);
        return effect with { Pools = effect.Pools.SetItem(index, pool.WriteSlot(slot, pool.Slots[slot].WriteByte(0x62, value))) };
    }

    public static NativeBattleChoco97Frame Advance729BD0(NativeBattleChoco97Frame frame, NativeBattleChoco97Inputs inputs, NativeBattleChoco97Resources resources)
    {
        return AdvanceFamily(frame, inputs, NativeBattleChocoControllerResources.From(resources), false);
    }

    internal static NativeBattleChoco97Frame AdvanceFamily(NativeBattleChoco97Frame frame, NativeBattleChoco97Inputs inputs, NativeBattleChocoControllerResources resources, bool choco98)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Choco lost its action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy.Validate();
        ValidateState(frame.Memory, choco98);
        uint mainDescriptor = choco98 ? NativeBattleChoco98Controller.MainDescriptor : MainDescriptor;
        NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target);
        NativeBattleEffectHierarchyStep roots = hierarchy.Advance(0, (current, address) =>
        {
            hierarchy = current;
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback != (choco98 ? 0x7219D0u : 0x729BD0u))
            {
                throw new NotSupportedException("Native Choco root callback changed its contract.");
            }
            int textureOffset = TextureOffset(frame.Effect);
            bool odd = (task.ReadWord(0x5C) & 1) != 0;
            SetShared(Shared() with { ViewCopy = MatrixBytes(inputs.View), PrivateBufferAliasOffset = textureOffset + (odd ? 0x1000 : 0x2000) });
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
                        if (choco98)
                        {
                            AcceptNodes98(NativeBattleChoco98Nodes.ClearStorage(NodeFrame98(), textureOffset));
                        }
                        else
                        {
                            AcceptNodes(NativeBattleChoco97Nodes.ClearStorage(NodeFrame(), textureOffset));
                        }
                        SetShared(Shared() with { NextAllocation = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)textureOffset + 0x13760) });
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x63, 1)));
                    Append(1, choco98 ? 0x721C20u : 0x729E20u, 0x58, address);
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
                    throw new NotSupportedException("Native Choco root phase is unsupported.");
            }
            int privateCursor = frame.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Choco private cursor is missing.");
            if (choco98)
            {
                AcceptNodes98(NativeBattleChoco98Nodes.BeginPass(NodeFrame98(), inputs.View, privateCursor));
            }
            else
            {
                AcceptNodes(NativeBattleChoco97Nodes.BeginPass(NodeFrame(), inputs.View, privateCursor));
            }
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
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = roots.Survivors == 0 ? 0u : mainDescriptor } };

        int Run(uint address)
        {
            uint callback = hierarchy.Read(address).Callback;
            bool admitted = choco98 ? callback is 0x721C20 or 0x722210 or 0x721DF0 or 0x728210 or 0x722920 or 0x722460 or 0x7294C0 or 0x7295D0 or 0x728C60 or 0x728DA0 or 0x7276F0 or 0x7292D0 or 0x7296D0 or 0x7297C0 or 0x728780 : callback is 0x729E20 or 0x72A410 or 0x729FF0 or 0x7306A0 or 0x72AB20 or 0x72A660 or 0x731840 or 0x731950 or 0x731100 or 0x72FBC0 or 0x731630 or 0x731A50 or 0x731B40 or 0x730C10;
            if (!admitted)
            {
                throw new NotSupportedException("Native Choco callback differs from its admitted effect profile.");
            }
            switch (callback)
            {
                case 0x729E20:
                case 0x721C20:
                    Target(address);
                    return Finish(address, true);
                case 0x72A410:
                case 0x722210:
                    Director(address);
                    return Finish(address, false);
                case 0x729FF0:
                case 0x721DF0:
                    AdvanceIo(address);
                    return Finish(address, false);
                case 0x7306A0:
                case 0x728210:
                    Model(address);
                    return Finish(address, false);
                case 0x72AB20:
                case 0x722920:
                    Nodes(address);
                    return Finish(address, true);
                case 0x72A660:
                case 0x722460:
                    InitialProgram(address);
                    return Finish(address, true);
                case 0x731840:
                case 0x7294C0:
                case 0x731950:
                case 0x7295D0:
                case 0x731100:
                case 0x728C60:
                case 0x728DA0:
                    Sync();
                    NativeBattleChoco97ProgramStep program = inputs.AdvanceProgram(frame, hierarchy.Read(address));
                    Accept(program.Frame);
                    Store(address, program.Task);
                    return Finish(address, false);
                case 0x72FBC0:
                case 0x7276F0:
                    AdvanceCamera(address);
                    return Finish(address, false);
                case 0x731630:
                case 0x7292D0:
                case 0x731A50:
                case 0x7296D0:
                case 0x731B40:
                case 0x7297C0:
                    NativeBattleChoco97StageFadeStep fade = NativeBattleChoco97StageFade.Advance(hierarchy.Read(address));
                    Store(address, fade.Task);
                    if (fade.Depth is not null || fade.ClearRgb)
                    {
                        Emit(new NativeBattleChoco97StageDepth(fade.Depth, fade.ClearRgb));
                    }
                    return Finish(address, false);
                case 0x730C10:
                case 0x728780:
                    Sync();
                    NativeBattleChoco97SpriteDrawStep sprite = inputs.AdvanceSprite(frame, hierarchy.Read(address));
                    Accept(sprite.Frame);
                    Store(address, sprite.Task);
                    return Finish(address, false);
                default:
                    throw new NotSupportedException($"Native Choco callback{callback:X8} is unsupported.");
            }
        }

        void Target(uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(hierarchy.Read(address), Models(), inputs.ReadPoseSource);
            int actor = task.ReadByte(0x2D);
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattlePoseSource source = inputs.ReadPoseSource(actor);
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Choco target bounds require its current sequence.");
            NativeBattleDecodedPose pose = sequence.DecodedPose ?? source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Choco target pose is unknown."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Choco target frame is unknown."), sequence.PoseFlags109);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
            Store(address, task);
            switch (Stage(task))
            {
                case 0:
                    SetShared(Shared() with { Context = NativeBattleChoco97Context.Initialize72A350(inputs.ReadModel) });
                    ImmutableArray<byte> script = Shared().Io?.Script ?? (Shared().IoScriptKnownCold ? resources.IoScript.Data : throw new NotSupportedException("Native Choco warmed mutable IO script is missing."));
                    NativeBattleChoco97IoBanks banks = Shared().Io?.Banks ?? Shared().PendingIoBanks ?? throw new NotSupportedException("Native Choco external IO physical owner is unknown.");
                    uint next = Shared().NextAllocation ?? throw new NotSupportedException("Native Choco next allocation is unknown.");
                    if (choco98)
                    {
                        NativeBattleChoco98IoInitialization initialized = NativeBattleChoco98Io.Initialize721CF0(script, next, banks);
                        SetShared(Shared() with { Io = NativeBattleChocoControllerIo.From(initialized.State), IoScriptKnownCold = false, PendingIoBanks = null, NextAllocation = initialized.NextAddress });
                    }
                    else
                    {
                        NativeBattleChoco97IoInitialization initialized = NativeBattleChoco97Io.Initialize729EF0(script, next, banks);
                        SetShared(Shared() with { Io = NativeBattleChocoControllerIo.From(initialized.State), IoScriptKnownCold = false, PendingIoBanks = null, NextAllocation = initialized.NextAddress });
                    }
                    Append(7, choco98 ? 0x721DF0u : 0x729FF0u, 0x30, address);
                    Append(2, choco98 ? 0x722210u : 0x72A410u, 0x48, address);
                    Store(address, Next(hierarchy.Read(address)));
                    break;
                case 1:
                    if (Context().Word(0x48) == 1)
                    {
                        int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        if ((uint)headerIndex >= inputs.Packets.Headers.Length)
                        {
                            throw new NotSupportedException("Native Choco contact header leaves its actual arena.");
                        }
                        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[headerIndex];
                        int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Native Choco contact pointer is missing.");
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
                    throw new NotSupportedException("Native Choco target phase is unsupported.");
            }
        }

        void AdvanceIo(uint address)
        {
            NativeBattleChocoControllerIo io = Shared().Io ?? throw new NotSupportedException("Native Choco IO has not been initialized.");
            if (choco98)
            {
                NativeBattleChoco98IoAdvance step = NativeBattleChoco98Io.Advance721DF0(hierarchy.Read(address), io.To98());
                SetShared(Shared() with { Io = NativeBattleChocoControllerIo.From(step.State) });
                Store(address, step.Task);
                if (step.Request is { } request)
                {
                    Emit(new NativeBattleChoco98FileRequest(request));
                }
            }
            else
            {
                NativeBattleChoco97IoAdvance step = NativeBattleChoco97Io.Advance729FF0(hierarchy.Read(address), io.To97());
                SetShared(Shared() with { Io = NativeBattleChocoControllerIo.From(step.State) });
                Store(address, step.Task);
                if (step.Request is { } request)
                {
                    Emit(new NativeBattleChoco97FileRequest(request));
                }
            }
        }

        bool ReleaseIoWait()
        {
            NativeBattleChocoControllerIo io = Shared().Io ?? throw new NotSupportedException("Native Choco IO wait owner is unknown.");
            if (choco98)
            {
                NativeBattleChoco98IoRelease release = NativeBattleChoco98Io.ReleaseWait(io.To98());
                SetShared(Shared() with { Io = NativeBattleChocoControllerIo.From(release.State) });
                return release.Released;
            }
            NativeBattleChoco97IoRelease prior = NativeBattleChoco97Io.ReleaseWait730590(io.To97());
            SetShared(Shared() with { Io = NativeBattleChocoControllerIo.From(prior.State) });
            return prior.Released;
        }

        void AdvanceCamera(uint address)
        {
            NativeBattleModulePoolTask task;
            NativeBattleMoogleCameraState camera;
            ImmutableArray<NativeBattleMoogleCameraStep> publications;
            if (choco98)
            {
                NativeBattleChoco98CameraTaskStep step = NativeBattleChoco98CameraTask.Advance7276F0(hierarchy.Read(address), Shared().Camera, resources.Camera98 ?? throw new NotSupportedException("Native Choco98 camera resources are missing."), unchecked((short)Context().Word(0x42)), ContextBytes(0, 8), ContextBytes(8, 8));
                task = step.Task;
                camera = step.Camera;
                publications = step.Publications;
            }
            else
            {
                NativeBattleChoco97CameraTaskStep step = NativeBattleChoco97CameraTask.Advance72FBC0(hierarchy.Read(address), Shared().Camera, resources.Camera97 ?? throw new NotSupportedException("Native Choco camera resources are missing."), unchecked((short)Context().Word(0x42)), ContextBytes(0, 8), ContextBytes(8, 8));
                task = step.Task;
                camera = step.Camera;
                publications = step.Publications;
            }
            Store(address, task);
            SetShared(Shared() with { Camera = camera });
            foreach (NativeBattleMoogleCameraStep publication in publications)
            {
                Emit(new NativeBattleChoco97CameraPublish(publication));
            }
        }

        void Director(uint address)
        {
            NativeBattleChoco97DirectorInputs directorInputs = new(() => (Shared().Io ?? throw new NotSupportedException("Native Choco director IO is unknown.")).WaitReleased, offset => BinaryPrimitives.ReadUInt32LittleEndian((Shared().Io ?? throw new NotSupportedException("Native Choco IO directory is unknown.")).Control.AsSpan().Slice(offset)), current =>
            {
                SetShared(Shared() with { Context = current.Context });
                return new(current, ReleaseIoWait());
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
                        uint child = Append(3, choco98 ? 0x722920u : 0x72AB20u, 0x2A4, address);
                        Store(child, hierarchy.Read(child).WriteDword(0x30, neutral.Source).WriteWord(0x298, neutral.Delay).WriteWord(0x29C, neutral.Mode).WriteWord(0x29E, neutral.Duration));
                        break;
                    case NativeBattleChoco97AppendProgram program:
                        child = Append(4, program.Callback, choco98 ? 0x864 : 0x540, address);
                        Store(child, hierarchy.Read(child).WriteDword(0x74, program.Source).WriteDword(0x78, unchecked((uint)(int)(short)program.ClearBytes)).WriteWord(0x80, unchecked((ushort)program.Arg5)).WriteWord(0x82, unchecked((ushort)program.Arg6)));
                        break;
                    case NativeBattleChoco97AppendAux auxiliary:
                        Append(5, auxiliary.Callback, auxiliary.Size, address);
                        break;
                    case NativeBattleChoco97AppendModel:
                        child = Append(6, choco98 ? 0x728210u : 0x7306A0u, 0x140, address);
                        NativeBattleEffectOwnedModelStep model = NativeBattleChoco97Model.Initialize(hierarchy.Read(child), child, resources.Model);
                        Store(child, model.Task);
                        SetShared(Shared() with { Model = model.State });
                        break;
                    case NativeBattleChoco97LoadModelTim:
                        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Choco model TIM queue is unknown.");
                        (textures, _) = textures.EnqueueTim505E30(resources.Model.ModelTim);
                        frame = frame with { Memory = frame.Memory with { Textures = textures } };
                        break;
                    case NativeBattleChoco97SetFreeze freeze:
                        (uint primary, uint secondary) = NativeBattleChoco97Director.SetFreeze730610(Shared().FreezePrimary ?? throw new NotSupportedException("Native Choco primary freeze is unknown."), Shared().FreezeSecondary ?? throw new NotSupportedException("Native Choco secondary freeze is unknown."), freeze.Value);
                        SetShared(Shared() with { FreezePrimary = primary, FreezeSecondary = secondary });
                        break;
                    case NativeBattleChoco97StopVibration stop:
                        Emit(new NativeBattleChoco97VibrationStop(stop.Handle));
                        break;
                    default:
                        throw new NotSupportedException("Native Choco director operation is unsupported.");
                }
                return new(hierarchy.Read(address), Context());
            });
            NativeBattleChoco97DirectorFrame directorFrame = new(hierarchy.Read(address), Context());
            NativeBattleChoco97DirectorStep step = choco98 ? NativeBattleChoco98Director.Advance722210(directorFrame, directorInputs) : NativeBattleChoco97Director.Advance72A410(directorFrame, directorInputs);
            Store(address, step.Frame.Task);
            SetShared(Shared() with { Context = step.Frame.Context });
        }

        void Model(uint address)
        {
            NativeBattleEffectOwnedModelState owned = Shared().Model ?? throw new NotSupportedException("Native Choco owned model has not been initialized.");
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
                        if (auxiliary.PoolAddress != (choco98 ? 0x256E548u : 0x2575458u))
                        {
                            throw new NotSupportedException("Native Choco model child leaves its physical auxiliary pool.");
                        }
                        Append(5, auxiliary.Callback, auxiliary.TaskBytes, address);
                        break;
                    case NativeBattleChoco97ModelSound sound:
                        Emit(new NativeBattleChoco97Sound(sound.DescriptorAddress, sound.Flags, sound.Pan));
                        break;
                    default:
                        throw new NotSupportedException("Native Choco model operation is unsupported.");
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
            NativeBattleChoco97ModelAdvance step = choco98 ? NativeBattleChoco98Model.Advance728210(hierarchy.Read(address), owned, resources.Model, modelInputs) : NativeBattleChoco97Model.Advance7306A0(hierarchy.Read(address), owned, resources.Model, modelInputs);
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
                if (choco98)
                {
                    AcceptNodes98(NativeBattleChoco98Nodes.InitializeContext(NodeFrame98(), address, resource, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, origins));
                }
                else
                {
                    AcceptNodes(NativeBattleChoco97Nodes.InitializeContext(NodeFrame(), address, resource, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, origins));
                }
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
                throw new NotSupportedException("Native Choco neutral phase is unsupported.");
            }
        }

        void InitialProgram(uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            int phase = Stage(task);
            if (phase == 0)
            {
                NativeBattleGfAnimationSource source = Shared().ProgramSource ?? throw new NotSupportedException("Native Choco static program source is missing.");
                task = choco98 ? NativeBattleChoco98ProgramDrawing.InitializeWorkspace7224C0(task, source, ContextBytes(0, 8)) : NativeBattleChoco97ProgramDrawing.InitializeWorkspace72A6C0(task, source, ContextBytes(0, 8));
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
                    throw new InvalidDataException("Native Choco initial program advance lost its actual remaining value.");
                }
                if (phase == 0)
                {
                    Store(address, task);
                    Emit(new NativeBattleChoco97Sound(choco98 ? 0x152AF34u : 0x152B5BCu, 0, 128));
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
                throw new NotSupportedException("Native Choco initial program phase is unsupported.");
            }
        }

        NativeBattleChocoControllerState Shared() => NativeBattleChocoControllerState.Read(frame.Memory, choco98);
        void SetShared(NativeBattleChocoControllerState value) => frame = frame with { Memory = value.Write(frame.Memory, choco98) };
        NativeBattleChoco97Context Context() => Shared().Context ?? throw new NotSupportedException("Native Choco target context has not been initialized.");
        ImmutableArray<byte> ContextBytes(int offset, int length) => Context().Data.AsSpan().Slice(offset, length).ToArray().ToImmutableArray();
        NativeBattleVector ContextVector(int offset) => new(unchecked((short)Context().Word(offset)), unchecked((short)Context().Word(offset + 2)), unchecked((short)Context().Word(offset + 4)));
        NativeBattleChoco97NodeFrame NodeFrame()
        {
            Sync();
            return new(frame.Memory, frame.Effect, Shared().Nodes, Shared().Io?.To97(), frame.Random);
        }
        NativeBattleChoco98NodeFrame NodeFrame98()
        {
            Sync();
            return new(frame.Memory, frame.Effect, Shared().Nodes, frame.Random);
        }
        void AcceptNodes(NativeBattleChoco97NodeFrame nodes)
        {
            NativeBattleChocoControllerState retained = Shared() with { Nodes = nodes.State, Io = nodes.Io is null ? null : NativeBattleChocoControllerIo.From(nodes.Io) };
            frame = new(retained.Write(nodes.Memory, choco98), nodes.Effect, nodes.Random);
            hierarchy = new(frame.Effect.Pools);
        }
        void AcceptNodes98(NativeBattleChoco98NodeFrame nodes)
        {
            NativeBattleChocoControllerState retained = Shared() with { Nodes = nodes.State };
            frame = new(retained.Write(nodes.Memory, choco98), nodes.Effect, nodes.Random);
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
            return appended.Address != 0 ? appended.Address : throw new NotSupportedException("Native Choco exhausted a required physical task pool.");
        }
        void Store(uint address, NativeBattleModulePoolTask task)
        {
            hierarchy = hierarchy.Write(address, task);
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

    private static int TextureOffset(NativeBattleOriginalEffectState effect) => effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == (effect.Binding.Effect == 98 ? 0x256E358u : 0x2575268u)).ArenaOffset;
    private static void ValidateState(NativeBattleEffectMemory memory, bool choco98)
    {
        if (choco98)
        {
            (memory.Choco98 ?? throw new NotSupportedException("Native Choco98 retained state is missing.")).Validate();
        }
        else
        {
            (memory.Choco97 ?? throw new NotSupportedException("Native Choco retained state is missing.")).Validate();
        }
    }
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
