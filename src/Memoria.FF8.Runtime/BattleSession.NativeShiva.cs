using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeShiva185(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Shiva requires its retained effect process.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Shiva requires its actual selected module.");
        NativeBattleShivaResources resources = effect.Binding.ShivaResources ?? throw new NotSupportedException("Native Shiva requires its original camera, audio, streamed files and geometry resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Shiva requires its published camera projection.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Shiva requires its original geometry interpolation constants.");
        NativeBattleScriptedGraphicsMemory registers = memory.ScriptedGraphics ?? throw new NotSupportedException("Native Shiva requires its current GTE control owners.");
        NativeBattleShivaFrame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Native Shiva requires its retained CRT random state."));
        NativeBattleShivaInputs inputs = new(graphics.Flags, projection, colors, registers.Zsf3 ?? throw new NotSupportedException("Native Shiva requires its initialized current ZSF3."), registers.Zsf4 ?? throw new NotSupportedException("Native Shiva requires its initialized current ZSF4."), registers.Dqa, registers.Dqb, () => NativeEffectIoResult ?? throw new NotSupportedException("Native Shiva requires its current signed IO result."), () => NativeCamera?.CurrentPose ?? throw new NotSupportedException("Native Shiva requires its current camera pose."), Execute);
        frame = initialize ? NativeBattleShivaExecution.Initialize5C0D50(frame, nativeActionPackets, graphics.Models) : NativeBattleShivaExecution.Advance5C7F50(frame, resources, inputs);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleShivaFrame Execute(NativeBattleShivaFrame current, NativeBattleShivaOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleShivaCameraPublish camera:
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Shiva requires its current camera globals.")).WriteEffectGlobals(camera.Step.Pose);
                    return current with { Memory = current.Memory with { GuardianCameraProcess = camera.Step.Task.Process } };
                case NativeBattleShivaTextureLoad texture:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(0x20DFAB8, texture.CompletionAddress, (address, value) => current = current with { Effect = WriteNativeShivaCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleShivaPackets packets:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Shiva requires its initialized ordering table.");
                    if (packets.EffectArena)
                    {
                        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.DrawCursor ?? throw new NotSupportedException("Native Shiva requires its current physical draw cursor."), primitive.Pending, packets.Batch);
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                        return current with { Memory = written.Memory, DrawCursor = written.Cursor };
                    }
                    NativeBattleCommonPacketWrite common = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Shiva requires its common primitive process."), primitive.Pending, packets.Batch);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = common.Ordering } };
                    return current with { Memory = current.Memory with { PrimitiveProcess = common.Process } };
                case NativeBattleShivaSprites sprites:
                    graphics = AppendNativeModulePacketBatch(graphics, sprites.Batch, null, null);
                    return current with { Memory = NativeEffectMemory ?? throw new NotSupportedException("Native Shiva sprites lost their actual primitive process.") };
                case NativeBattleShivaTextureCopyOperation copy:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Shiva texture copying requires its current texture queue.");
                    return current with { Memory = current.Memory with { Textures = textures.EnqueueCopy505EB0(copy.Copy.SourceX, copy.Copy.SourceY, copy.Copy.Width, copy.Copy.Height, copy.Copy.DestinationX, copy.Copy.DestinationY) } };
                case NativeBattleShivaModelService { Operation: NativeBattleShivaModelOverlay overlay }:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(checked((short)overlay.First), checked((short)overlay.Second), checked((short)overlay.Third), checked((short)overlay.Fourth)) };
                    return current;
                case NativeBattleShivaService service:
                    return ExecuteService(current, service.Operation);
                default:
                    throw new NotSupportedException("Native Shiva operation requires its actual service contract.");
            }
        }

        NativeBattleShivaFrame ExecuteService(NativeBattleShivaFrame current, NativeBattleShivaDirectorOperation operation)
        {
            NativeBattleShivaProcess shared = current.Memory.Shiva ?? throw new NotSupportedException("Native Shiva service requires its retained process.");
            switch (operation.Service)
            {
                case NativeBattleShivaDirectorService.BeginWave:
                    uint source;
                    int number;
                    if (operation.UseWorkBuffer)
                    {
                        source = 0x209FAB8;
                        number = BinaryPrimitives.ReadInt32LittleEndian((current.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Native Shiva audio requires its actual auxiliary work buffer.")).Read(0, 4));
                    }
                    else
                    {
                        if (operation.First != 0xE9AD20)
                        {
                            throw new NotSupportedException("Native Shiva initial audio requires its original selector address.");
                        }
                        source = 0xE9AD20;
                        number = unchecked((int)resources.InitialAudioSelector);
                    }
                    NativeBattleSummonStreamState stream = current.Memory.SummonStream ?? throw new NotSupportedException("Native Shiva requires its retained summon stream number.");
                    //5018AF publishes the caller's completion byte synchronously before returning.
                    return current with { Memory = current.Memory with { SummonStream = stream.Publish501860(source, number), Shiva = shared with { SoundReady = 1 } } };
                case NativeBattleShivaDirectorService.StartVibration:
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Shiva requires its retained vibration process.")).Enqueue(resources.Vibration, operation.Second, operation.Third);
                    NativeVibrationProcess = queued.State;
                    return current with { Memory = current.Memory with { Shiva = shared with { VibrationHandle = queued.Handle } } };
                case NativeBattleShivaDirectorService.StopVibrationIfActive:
                    NativeVibrationProcessState vibration = NativeVibrationProcess ?? throw new NotSupportedException("Native Shiva requires its current vibration process.");
                    if (vibration.IsPlaying(shared.VibrationHandle))
                    {
                        NativeVibrationProcess = vibration.Stop4A2940(shared.VibrationHandle);
                    }
                    return current;
                case NativeBattleShivaDirectorService.Sound:
                    int soundIndex = operation.First switch
                    {
                        0xD32488 => 0,
                        0xD3248C => 1,
                        0xD32490 => 2,
                        0xD32494 => 3,
                        _ => throw new NotSupportedException("Native Shiva sound requires its actual descriptor DWORD.")
                    };
                    PublishNativeModuleSound(new(resources.Sounds[soundIndex], 0, null) { Pan = checked((byte)operation.Third) });
                    return current;
                case NativeBattleShivaDirectorService.Volume:
                    PublishNativeModuleSound((current.Memory.SummonStream ?? throw new NotSupportedException("Native Shiva requires its retained summon stream number.")).Play5018C0(unchecked((uint)operation.First), unchecked((uint)operation.Third)));
                    return current;
                case NativeBattleShivaDirectorService.File:
                    NativeBattleEffectFileAsset file = resources.Files.Single(item => item.FileId == operation.First);
                    NativeBattleEffectMemory loaded = current.Memory.DeliverBattleFile(file, operation.Second, operation.UseWorkBuffer);
                    NativeEffectIoResult = -1;
                    QueueNativeFileIo482610(file.Data.Length, 0x508470);
                    return current with { Memory = loaded };
                case NativeBattleShivaDirectorService.Upload:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Shiva requires its current texture queue.");
                    (textures, _) = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Native Shiva upload requires its loaded TIM arena."), operation.First);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleShivaDirectorService.Stage:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Shiva requires its current stage groups.");
                    stage.Validate();
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)operation.First), ColorFlags = unchecked((uint)operation.Second) }).ToImmutableArray() };
                    return current;
                case NativeBattleShivaDirectorService.Contacts:
                    NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C];
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Shiva requires its actual contact pointer.");
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(first, header.ReadByte(0x10)).Select(nativeActionPackets.ReadContact).ToImmutableArray() });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Shiva contact lost its current process memory.") };
                default:
                    throw new NotSupportedException("Native Shiva service requires its original reached implementation.");
            }
        }
    }

    private static NativeBattleOriginalEffectState WriteNativeShivaCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        uint delta = unchecked(address - 0x22BC1A8);
        if (effect.Binding.Entry != 0x5C0D50 || delta >= 100 * 0x24 || delta % 0x24 != 0x0E || effect.Pools.Length != 2)
        {
            throw new NotSupportedException("Native Shiva completion requires its actual physical child-task byte.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[1];
        if (pool.DescriptorAddress != 0x22BC198 || pool.BankAddress != 0x22BC1A8 || pool.Stride != 0x24 || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native Shiva completion changed its child-bank layout.");
        }
        int slot = checked((int)(delta / 0x24));
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteByte(0x0E, value);
        return effect with { Pools = effect.Pools.SetItem(1, pool.WriteSlot(slot, task)) };
    }

    private bool TryWriteNativeShivaCompletion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x22BC1A8);
        if (delta >= 100 * 0x24 || delta % 0x24 != 0x0E)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5C0D50 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = WriteNativeShivaCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x5C0D50 } selected ? selected : throw new NotSupportedException("Native Shiva completion lost its current task bank.");
            updated = graphics with { OriginalEffect = WriteNativeShivaCompletionByte(effect, address, value) };
        }
        return true;
    }
}
