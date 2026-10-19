using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeTonberry90(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Tonberry requires its selected effect.");
        NativeBattleTonberry90Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Tonberry requires its retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Tonberry requires its retained CRT random state."));
        if (initialize)
        {
            frame = NativeBattleTonberry90Controller.Initialize762360(frame, nativeActionPackets);
        }
        else
        {
            NativeBattleTonberry90Resources resources = effect.Binding.Tonberry90Resources ?? throw new NotSupportedException("Native Tonberry requires its original programme, IO and vibration resources.");
            resources.Validate();
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Tonberry requires its current camera owner.");
            NativeBattleTonberry90DirectorServices directorServices = new(nativeActionPackets, StartVibration, StopVibration, (_, _, _, _) => throw new NotSupportedException("Native Tonberry creature initialization requires its complete selected actor contract."));
            NativeBattleTonberry90ProgrammeServices programmeServices = new(ReadAddress, PublishCamera, PlaySound);
            NativeBattleTonberry90Inputs inputs = new(nativeActionPackets, camera.CapturePackedView1D97778(), actor => graphics.Models[actor], ReadPoseSource, InitializeTarget, AdvanceLeaf, ApplyContact, Execute);
            frame = NativeBattleTonberry90Controller.Advance7624D0(frame, inputs);

            NativeBattlePoseSource ReadPoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Tonberry requires its selected current actor pose source.");

            NativeBattleTonberry90Frame InitializeTarget(NativeBattleTonberry90Frame current, uint address)
            {
                current = NativeBattleTonberry90Director.Initialize762BB0(current, address, actor => graphics.Models[actor], ReadPoseSource);
                return NativeBattleTonberry90Io.Initialize762760(current, resources.ReadStatic(NativeBattleTonberry90Io.ScriptAddress, 0x40).ToArray().ToImmutableArray());
            }

            NativeBattleTonberry90TaskStep AdvanceLeaf(NativeBattleTonberry90Frame current, uint address)
            {
                NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
                switch (task.Callback)
                {
                    case 0x762C60:
                        return NativeBattleTonberry90Director.Advance762C60(current, address, directorServices);
                    case 0x762DA0:
                        return NativeBattleTonberry90Programme.Advance762DA0(current, address, programmeServices);
                    case 0x762850:
                        NativeBattleTonberry90IoAdvance advanced = NativeBattleTonberry90Io.Advance762850(current, task);
                        current = advanced.Frame with { Effect = advanced.Frame.Effect with { Pools = new NativeBattleEffectHierarchy(advanced.Frame.Effect.Pools).Write(address, advanced.Task).Pools } };
                        if (advanced.Request is { } request)
                        {
                            NativeBattleEffectFileAsset file = resources.Files.SingleOrDefault(file => file.FileId == request.FileId) ?? throw new NotSupportedException($"Native Tonberry archive file {request.FileId} is not supplied.");
                            NativeBattleTonberry90IoQueued queued = NativeBattleTonberry90Io.CopyAndQueueFile(current, NativeFileIo ?? throw new NotSupportedException("Native Tonberry requires its asynchronous file scheduler."), file);
                            NativeFileIo = queued.FileIo;
                            current = queued.Frame;
                        }
                        return new(current, advanced.ReadyToComplete);
                    default:
                        throw new NotSupportedException($"Native Tonberry callback 0x{task.Callback:X8} at task 0x{address:X8}, signed phase {unchecked((sbyte)task.ReadByte(0x29))} requires its complete actor contract.");
                }
            }

            NativeBattleTonberry90VibrationStep StartVibration(NativeBattleTonberry90Frame current, uint source, int index, int strength)
            {
                if (source != resources.Vibration.Address)
                {
                    throw new NotSupportedException("Native Tonberry vibration source leaves its selected bank.");
                }
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Tonberry requires its vibration process.")).Enqueue(resources.Vibration.Bank, index, strength);
                NativeVibrationProcess = queued.State;
                return new(current, unchecked((short)queued.Handle));
            }

            NativeBattleTonberry90Frame StopVibration(NativeBattleTonberry90Frame current, short handle)
            {
                NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Native Tonberry requires its vibration process.")).Stop4A2940(handle);
                return current;
            }

            byte[] ReadAddress(NativeBattleTonberry90Frame current, uint address, int length)
            {
                NativeBattleTonberry90State shared = current.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry requires its current module owner.");
                if (address >= NativeBattleTonberry90State.GlobalAddress && (ulong)address + (uint)length <= (ulong)NativeBattleTonberry90State.GlobalAddress + NativeBattleTonberry90State.GlobalLength)
                {
                    return shared.Read(address, length);
                }
                if (shared.Io is { } io && address >= NativeBattleTonberry90Io.ScriptAddress && (ulong)address + (uint)length <= (ulong)NativeBattleTonberry90Io.ScriptAddress + 0x40)
                {
                    return io.Script.AsSpan().Slice(checked((int)(address - NativeBattleTonberry90Io.ScriptAddress)), length).ToArray();
                }
                return resources.ReadStatic(address, length).ToArray();
            }

            NativeBattleTonberry90Frame PublishCamera(NativeBattleTonberry90Frame current, NativeBattleMoogleCameraStep step)
            {
                NativeCamera = NativeBattleMoogleCamera.Publish733150(step, NativeCamera ?? throw new NotSupportedException("Native Tonberry camera publication requires its current camera owner."));
                return current;
            }

            NativeBattleTonberry90Frame PlaySound(NativeBattleTonberry90Frame current, uint descriptor, int flags, int pan)
            {
                uint sound = BinaryPrimitives.ReadUInt32LittleEndian(ReadAddress(current, descriptor, 4));
                PublishNativeModuleSound(new(sound, unchecked((uint)flags), null) { Pan = checked((byte)pan) });
                return current;
            }
        }
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleTonberry90Frame ApplyContact(NativeBattleTonberry90Frame current, int contact)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Tonberry contact lost its process memory.") };
        }

        NativeBattleTonberry90Frame Execute(NativeBattleTonberry90Frame current, NativeBattleChoco97Operation operation)
        {
            if (operation is not NativeBattleChoco97CommonTextureLoad load)
            {
                throw new NotSupportedException("Native Tonberry master operation requires its actual resource completion contract.");
            }
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleTonberry90Controller.WriteCompletionByte(current.Effect, address, value) });
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }
    }

    private void CompleteNativeTonberry90File(uint callback)
    {
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new NotSupportedException("Native Tonberry file completion requires its current graphics owner.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Tonberry file completion requires its selected effect.");
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Io.Complete(new(NativeEffectMemory ?? throw new NotSupportedException("Native Tonberry file completion requires its current effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Tonberry file completion requires its CRT owner.")), callback);
        NativeEffectMemory = frame.Memory;
    }
}
