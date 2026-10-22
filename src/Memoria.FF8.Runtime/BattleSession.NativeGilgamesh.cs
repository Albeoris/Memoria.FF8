using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeGilgamesh(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Gilgamesh requires its selected original effect.");
        NativeBattleGilgameshEffectResources resources = effect.Binding.GilgameshResources ?? throw new NotSupportedException("Native Gilgamesh requires its selected original resource profile.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Gilgamesh requires its current effect memory.");
        NativeBattleScriptedGraphicsMemory scripted = memory.ScriptedGraphics ?? throw new NotSupportedException("Native Gilgamesh requires its current depth-cue registers.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Gilgamesh requires its current geometry color registers.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Gilgamesh requires its current camera projection.");
        NativeBattleGilgameshFrame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Native Gilgamesh requires its retained CRT random owner.")) { Staging = NativeFileStaging };
        NativeBattleGilgameshInputs inputs = new(graphics.Flags, nativeActionPackets, projection, scripted.Dqa, scripted.Dqb, colors, ReadAnchor571400, () => NativeEffectIoResult ?? throw new NotSupportedException("Native Gilgamesh requires its actual signed IO result."), () => NativeCamera?.CurrentPose ?? throw new NotSupportedException("Native Gilgamesh requires its current camera globals."), Execute)
        {
            ReadProjection = () => NativeCamera?.Projection ?? throw new NotSupportedException("Gilgamesh requires its current native projection."),
            ReadFocalShadow = () => NativeCamera?.FocalLength ?? throw new NotSupportedException("Gilgamesh requires its retained signed focal shadow.")
        };
        frame = initialize ? NativeBattleGilgameshController.Initialize58DCF0(frame, nativeActionPackets, graphics.Models) : NativeBattleGilgameshController.Advance596B70(frame, resources, inputs);
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleModuleAnchor ReadAnchor571400(byte actor)
        {
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            NativeBattleModelTransform transform = graphics.Models[actor].Transform ?? throw new NotSupportedException("Native571400 requires its actual actor transform WORD.");
            return anchor with { W = transform.Angle36 };
        }

        NativeBattleGilgameshFrame Execute(NativeBattleGilgameshFrame current, NativeBattleGilgameshOperation operation)
        {
            NativeEffectMemory = current.Memory;
            if (current.Staging is { } staging)
            {
                NativeFileStaging = staging;
            }
            NativeBattleGilgameshFrame executed = ExecuteCore(current, operation);
            return executed with { Staging = NativeFileStaging };
        }

        NativeBattleGilgameshFrame ExecuteCore(NativeBattleGilgameshFrame current, NativeBattleGilgameshOperation operation)
        {
            switch (operation)
            {
                case NativeBattleGilgameshMasamuneAnchor anchor:
                    NativeBattleModuleAnchor packed = ReadAnchor571400(anchor.Actor);
                    return UpdateNativeMasamuneChild(current, anchor.Address, task => task.WriteWord(0x10, unchecked((ushort)packed.X)).WriteWord(0x12, unchecked((ushort)packed.Y)).WriteWord(0x14, unchecked((ushort)packed.Z)).WriteWord(0x16, unchecked((ushort)packed.W)));
                case NativeBattleGilgameshMasamuneProjectFlare projectedFlare:
                    NativeBattleModulePoolTask child = ReadNativeMasamuneChild(current, projectedFlare.Address);
                    NativeBattleCameraState currentCamera = NativeCamera ?? throw new NotSupportedException("Masamune requires its current camera owner.");
                    NativeBattleGilgameshMasamuneProjectionStep projected = NativeBattleGilgameshMasamuneProjection.Project(current.Memory, child, currentCamera.Projection ?? throw new NotSupportedException("Masamune requires its actual inherited projection."), currentCamera.CanonicalViewPadding is ushort padding ? unchecked((short)padding) : null);
                    return UpdateNativeMasamuneChild(current with { Memory = projected.Memory }, projectedFlare.Address, _ => projected.Task);
                case NativeBattleGilgameshMasamuneProjectionH focal:
                    NativeBattleCameraState focalCamera = NativeCamera ?? throw new NotSupportedException("Masamune requires its current projection owner.");
                    NativeCamera = focalCamera with { Projection = (focalCamera.Projection ?? throw new NotSupportedException("Masamune requires its actual GTE projection H.")) with { FocalLength = focal.Value } };
                    return current;
                case NativeBattleGilgameshPackets packets:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Gilgamesh requires its initialized ordering table.");
                    NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Gilgamesh requires its current common packet owner."), primitive.Pending, packets.Batch);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
                case NativeBattleGilgameshPacketCursorAdvance advance:
                    NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native sword trail requires its current common packet cursor.");
                    int cursor = checked((process.ByteCursor ?? throw new NotSupportedException("Native sword trail requires its allocated packet bank.")) + advance.Bytes);
                    if (advance.Bytes < 0 || cursor > process.CommonPacketWriteCapacity)
                    {
                        throw new InvalidDataException("Native sword-trail cursor publication leaves its actual packet allocation.");
                    }
                    return current with { Memory = current.Memory with { PrimitiveProcess = process with { ByteCursor = cursor } } };
                case NativeBattleGilgameshSprites sprites:
                    graphics = AppendNativeModulePacketBatch(graphics, sprites.Batch, null, null);
                    return current with { Memory = NativeEffectMemory ?? throw new NotSupportedException("Native Gilgamesh sprite submission lost its current memory.") };
                case NativeBattleGilgameshContacts contacts:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(contacts.First, contacts.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray() });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new NotSupportedException("Native Gilgamesh contact publication lost its current memory.") };
                case NativeBattleGilgameshCameraPublish camera:
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Gilgamesh requires its current camera globals.")).WriteEffectGlobals(camera.Step.Pose);
                    return current with { Memory = current.Memory with { GuardianCameraProcess = camera.Step.Task.Process } };
                case NativeBattleGilgameshTextureLoad texture:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(NativeBattleEffectNodeMemory.ArenaAddress + 0xE000, texture.CompletionAddress, (address, value) => current = current with { Effect = WriteNativeGilgameshCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleGilgameshPcRequest request:
                    NativeBattlePcRenderRequest pcRequest = current.Memory.PcRenderRequest ?? throw new NotSupportedException("Native47CF50 requires its retained PC render request owner.");
                    return current with { Memory = current.Memory with { PcRenderRequest = pcRequest.Request47CF50(request.Mode, 255, 255, 255, 63) } };
                case NativeBattleGilgameshService service:
                    return ExecuteService(current, service.Operation);
                default:
                    throw new NotSupportedException("Native Gilgamesh operation requires its original service contract.");
            }
        }

        NativeBattleGilgameshFrame ExecuteService(NativeBattleGilgameshFrame current, NativeBattleGilgameshDirectorOperation operation)
        {
            switch (operation.Service)
            {
                case NativeBattleGilgameshDirectorService.BeginWave:
                    NativeBattleSummonStreamState stream = current.Memory.SummonStream ?? throw new NotSupportedException("Native Gilgamesh requires its retained stream number.");
                    uint source;
                    int number;
                    if (operation.UseWorkBuffer)
                    {
                        NativeBattleGuardianWorkBuffer work = current.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Native Gilgamesh requires its actual current work buffer.");
                        source = 0x209FAB8;
                        number = BinaryPrimitives.ReadInt32LittleEndian(work.Read(0, 4));
                    }
                    else
                    {
                        if (operation.First != NativeBattleGilgameshProfile.ForEffect(effect.Binding.Effect).InitialStreamAddress)
                        {
                            throw new NotSupportedException("Native Gilgamesh initial stream requires its selected original source.");
                        }
                        source = NativeBattleGilgameshProfile.ForEffect(effect.Binding.Effect).InitialStreamAddress;
                        number = resources.InitialStreamNumber;
                    }
                    return current with { Memory = current.Memory with { SummonStream = stream.Publish501860(source, number) } };
                case NativeBattleGilgameshDirectorService.StartVibration:
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Gilgamesh requires its retained vibration process.")).Enqueue(resources.Director.Vibration, operation.First, operation.Second);
                    NativeVibrationProcess = queued.State;
                    NativeBattleGilgameshProcess shared = current.Memory.Gilgamesh ?? throw new NotSupportedException("Native Gilgamesh requires its retained vibration handle owner.");
                    return current with { Memory = current.Memory with { Gilgamesh = shared with { VibrationHandle = queued.Handle } } };
                case NativeBattleGilgameshDirectorService.StopVibrationIfActive:
                    int handle = current.Memory.Gilgamesh?.VibrationHandle ?? throw new NotSupportedException("Native Gilgamesh requires its actual retained vibration handle.");
                    NativeVibrationProcessState vibration = NativeVibrationProcess ?? throw new NotSupportedException("Native Gilgamesh requires its retained vibration process.");
                    if (vibration.IsPlaying(handle))
                    {
                        NativeVibrationProcess = vibration.Stop4A2940(handle);
                    }
                    return current;
                case NativeBattleGilgameshDirectorService.Sound:
                    uint descriptor = operation.First switch
                    {
                        0xCD0378 => resources.Director.SoundInitial,
                        0xCD037C => resources.Director.SoundMiddle,
                        0xCD0380 => resources.Director.SoundLate,
                        0xCD0388 or 0xCD038C or 0xCD0390 => resources.Director.SoundClosing,
                        _ => throw new NotSupportedException("Native Gilgamesh sound requires its original selected DWORD source.")
                    };
                    PublishNativeModuleSound(new(descriptor, unchecked((uint)operation.Second), null) { RawPan = unchecked((uint)operation.Third) });
                    return current;
                case NativeBattleGilgameshDirectorService.Volume:
                    PublishNativeModuleSound((current.Memory.SummonStream ?? throw new NotSupportedException("Native Gilgamesh requires its retained stream number.")).Play5018C0(unchecked((uint)operation.First), unchecked((uint)operation.Third)));
                    return current;
                case NativeBattleGilgameshDirectorService.File:
                    NativeBattleArchiveFile file = resources.Files.Files.Single(item => item.FileId == operation.First);
                    NativeBattleEffectMemory loaded = operation.UseWorkBuffer
                        ? current.Memory with { GuardianWorkBuffer = (current.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Native Gilgamesh file IO requires its current work buffer.")).Write(0, file.Bytes.AsSpan()) }
                        : current.Memory with { Arena = (current.Memory.Arena ?? throw new NotSupportedException("Native Gilgamesh file IO requires its current arena.")).Write(operation.Second, file.Bytes.AsSpan()) };
                    NativeEffectIoResult = -1;
                    QueueNativeFileIo482610(file.Bytes.Length, 0x508470);
                    return current with { Memory = loaded };
                case NativeBattleGilgameshDirectorService.Upload:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Gilgamesh upload requires its retained texture queue.");
                    (textures, _) = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Native Gilgamesh upload requires its actual arena."), operation.First);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleGilgameshDirectorService.QueueColor:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(unchecked((short)operation.First), unchecked((short)operation.Second), unchecked((short)operation.Third), unchecked((short)operation.Fourth)) };
                    return current;
                case NativeBattleGilgameshDirectorService.Title:
                    graphics = PublishNativeOriginalEffectTitle(graphics, graphics.ActionGraphics ?? throw new NotSupportedException("Native50A750 requires its current action title context."));
                    return current;
                case NativeBattleGilgameshDirectorService.Stage:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its current four stage groups.");
                    stage.Validate();
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)operation.First), ColorFlags = unchecked((uint)operation.Second) }).ToImmutableArray() };
                    return current;
                case NativeBattleGilgameshDirectorService.Initializer:
                    if (operation.First is not (0x5904D0 or 0x5904F0))
                    {
                        throw new NotSupportedException("Native Gilgamesh service initializer requires its proved stage flag operation.");
                    }
                    NativeBattleStageState flagStage = NativeStage ?? throw new NotSupportedException("Native Gilgamesh requires its four actual stage flag owners.");
                    flagStage.Validate();
                    NativeStage = flagStage with { Groups = flagStage.Groups.Select(group => group with { Flags = operation.First == 0x5904D0 ? (byte)(group.Flags | 2) : (byte)(group.Flags & ~2) }).ToImmutableArray() };
                    return current;
                default:
                    throw new NotSupportedException("Native Gilgamesh director service requires its exact original implementation.");
            }
        }
    }

    private static NativeBattleOriginalEffectState WriteNativeGilgameshCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        uint delta = unchecked(address - 0x21FFD68);
        if (effect.Binding.Entry is not (0x58D930 or 0x58DB10 or 0x58DCF0) || delta >= 100 * 0x24 || delta % 0x24 != 0x0E)
        {
            throw new NotSupportedException("Native Gilgamesh texture completion requires its actual child-task BYTE owner.");
        }
        int index = -1;
        for (int current = 0; current < effect.Pools.Length; current++)
        {
            if (effect.Pools[current].DescriptorAddress == 0x21FFD58)
            {
                index = current;
                break;
            }
        }
        if (index < 0)
        {
            throw new NotSupportedException("Native Gilgamesh texture completion lost its physical raw task bank.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        if (pool.BankAddress != 0x21FFD68 || pool.Stride != 0x24 || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native Gilgamesh texture completion changed its physical child bank.");
        }
        int slot = (int)(delta / 0x24);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        task = task.WriteWord(0x0E, (ushort)((task.ReadWord(0x0E) & 0xFF00) | value));
        return effect with { Pools = effect.Pools.SetItem(index, pool with { Slots = pool.Slots.SetItem(slot, task) }) };
    }

    private bool TryWriteNativeGilgameshCompletion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x21FFD68);
        if (delta >= 100 * 0x24 || delta % 0x24 != 0x0E)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x58D930 or 0x58DB10 or 0x58DCF0 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = WriteNativeGilgameshCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState current = graphics.OriginalEffect is { Binding.Entry: 0x58D930 or 0x58DB10 or 0x58DCF0 } selected ? selected : throw new NotSupportedException("Native Gilgamesh completion lost its current raw task owner.");
            updated = graphics with { OriginalEffect = WriteNativeGilgameshCompletionByte(current, address, value) };
        }
        return true;
    }
}
