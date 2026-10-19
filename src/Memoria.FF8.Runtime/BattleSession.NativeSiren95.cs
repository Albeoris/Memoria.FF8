using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeSiren95(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Siren requires its selected effect.");
        NativeBattleSiren95Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Siren requires its retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Siren requires its retained CRT random state."));
        if (initialize)
        {
            frame = NativeBattleSiren95Controller.Initialize739D80(frame);
            frame = NativeBattleSiren95Controller.Initialize739DA0(frame, nativeActionPackets);
        }
        else
        {
            NativeBattleSiren95Resources resources = effect.Binding.Siren95Resources ?? throw new NotSupportedException("Native Siren requires its original programme and IO resources.");
            resources.Validate();
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Siren requires its current camera owner.");
            NativeBattleSiren95DirectorServices director = new(nativeActionPackets, StartVibration, StopVibration, SpawnStage, PlaySound, actor => graphics.Models[actor], WriteModel);
            frame = NativeBattleSiren95Controller.Advance739F40(frame, new(nativeActionPackets, camera.CapturePackedView1D97778(), actor => graphics.Models[actor], ReadPoseSource, InitializeTarget, AdvanceLeaf, ApplyContact, Execute));

            NativeBattlePoseSource ReadPoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Siren requires its selected actor pose source.");

            NativeBattleSiren95Frame InitializeTarget(NativeBattleSiren95Frame current, uint address)
            {
                current = NativeBattleSiren95Director.Initialize73A6E0(current, actor => graphics.Models[actor]);
                return NativeBattleSiren95Io.Initialize73A270(current, resources.IoScript);
            }

            NativeBattleSiren95TaskStep AdvanceLeaf(NativeBattleSiren95Frame current, uint address)
            {
                NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
                switch (task.Callback)
                {
                    case 0x73A7B0:
                        return NativeBattleSiren95Director.Advance73A7B0(current, address, director);
                    case 0x73A9E0:
                        return NativeBattleSiren95Programme.Advance73A9E0(current, address, new(ReadProgramAddress, PublishCamera));
                    case 0x73A380:
                        NativeBattleSiren95IoAdvance advanced = NativeBattleSiren95Io.Advance73A380(current, task);
                        current = advanced.Frame with { Effect = advanced.Frame.Effect with { Pools = new NativeBattleEffectHierarchy(advanced.Frame.Effect.Pools).Write(address, advanced.Task).Pools } };
                        if (advanced.Request is { } request)
                        {
                            NativeBattleEffectFileAsset file = resources.Files.SingleOrDefault(file => file.FileId == request.FileId) ?? throw new NotSupportedException($"Native Siren archive file {request.FileId} is not supplied.");
                            NativeBattleSiren95IoQueued queued = NativeBattleSiren95Io.CopyAndQueueFile(current, NativeFileIo ?? throw new NotSupportedException("Native Siren requires its asynchronous file scheduler."), file);
                            NativeFileIo = queued.FileIo;
                            current = queued.Frame;
                        }
                        return new(current, advanced.ReadyToComplete);
                    default:
                        throw new NotSupportedException($"Native Siren callback 0x{task.Callback:X8} at task 0x{address:X8}, signed phase {unchecked((sbyte)task.ReadByte(0x29))} requires its complete actor contract.");
                }
            }

            byte[] ReadProgramAddress(NativeBattleSiren95Frame current, uint address, int length)
            {
                if (address >= NativeBattleSiren95State.GlobalAddress && (ulong)address + (uint)length <= (ulong)NativeBattleSiren95State.GlobalAddress + NativeBattleSiren95State.GlobalLength)
                {
                    return (current.Memory.Siren95 ?? throw new NotSupportedException("Native Siren camera requires its raw module owner.")).Read(address, length);
                }
                NativeBattleEffectHeapAllocation data = resources.CameraPrograms ?? throw new NotSupportedException("Native Siren requires its original camera programmes.");
                long offset = (long)address - 0x1533A98;
                if (data.Data.IsDefault || offset < 0 || length < 0 || offset > data.Data.Length - length)
                {
                    throw new NotSupportedException("Native Siren camera read leaves its original resource span.");
                }
                return data.Data.AsSpan().Slice((int)offset, length).ToArray();
            }

            NativeBattleSiren95Frame PublishCamera(NativeBattleSiren95Frame current, NativeBattleMoogleCameraStep step)
            {
                NativeCamera = NativeBattleMoogleCamera.Publish733150(step, NativeCamera ?? throw new NotSupportedException("Native Siren camera publication requires its current camera owner."));
                return current;
            }

            NativeBattleSiren95VibrationStep StartVibration(NativeBattleSiren95Frame current, uint source, int index, int strength)
            {
                NativeBattleMoogle96VibrationResource vibration = resources.Vibration ?? throw new NotSupportedException("Native Siren requires its original vibration bank.");
                if (source != vibration.Address)
                {
                    throw new NotSupportedException("Native Siren vibration address leaves its supplied bank.");
                }
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Siren requires its vibration process.")).Enqueue(vibration.Bank, index, strength);
                NativeVibrationProcess = queued.State;
                return new(current, unchecked((short)queued.Handle));
            }

            NativeBattleSiren95Frame StopVibration(NativeBattleSiren95Frame current, short handle)
            {
                NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Native Siren requires its vibration process.")).Stop4A2940(handle);
                return current;
            }

            NativeBattleSiren95Frame PlaySound(NativeBattleSiren95Frame current, uint source, int flags, int pan)
            {
                NativeBattleEffectHeapAllocation bytes = resources.SoundDescriptors ?? throw new NotSupportedException("Native Siren requires its original sound descriptors.");
                long offset = (long)source - 0x1533014;
                if (bytes.Data.IsDefault || offset < 0 || offset > bytes.Data.Length - 4)
                {
                    throw new NotSupportedException("Native Siren sound descriptor leaves its supplied bank.");
                }
                uint sound = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Data.AsSpan().Slice((int)offset, 4));
                PublishNativeModuleSound(new(sound, unchecked((uint)flags), null) { Pan = checked((byte)pan) });
                return current;
            }

            NativeBattleSiren95Frame WriteModel(NativeBattleSiren95Frame current, int actor, NativeBattleGraphicsModel model)
            {
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                return current;
            }

            NativeBattleSiren95Frame SpawnStage(NativeBattleSiren95Frame current, uint address)
            {
                NativeBattleSiren95State shared = current.Memory.Siren95 ?? throw new NotSupportedException("Native Siren requires its retained director owner.");
                uint data = shared.DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren director pointer is unknown.");
                throw new NotSupportedException($"Native Siren stage {unchecked((short)shared.ReadWord(data + 0x40))} requires its complete actor spawning contract.");
            }
        }
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleSiren95Frame ApplyContact(NativeBattleSiren95Frame current, int contact)
        {
            NativeEffectMemory = current.Memory;
            var pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Siren contact lost its process memory.") };
        }

        NativeBattleSiren95Frame Execute(NativeBattleSiren95Frame current, NativeBattleChoco97Operation operation)
        {
            if (operation is not NativeBattleChoco97CommonTextureLoad load)
            {
                throw new NotSupportedException("Native Siren master operation requires its actual resource completion contract.");
            }
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleSiren95Controller.WriteCompletionByte(current.Effect, address, value) });
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }
    }

    private void CompleteNativeSiren95File(uint callback)
    {
        NativeBattleOriginalEffectState effect = NativeGraphics?.OriginalEffect ?? throw new NotSupportedException("Native Siren file completion requires its selected effect.");
        NativeBattleSiren95Frame frame = NativeBattleSiren95Io.Complete(new(NativeEffectMemory ?? throw new NotSupportedException("Native Siren file completion requires its current memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Siren file completion requires its CRT owner.")), callback);
        NativeEffectMemory = frame.Memory;
    }
}
