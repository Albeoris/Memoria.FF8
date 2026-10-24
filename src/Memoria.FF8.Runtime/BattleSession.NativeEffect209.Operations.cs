using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect209Frame ExecuteNativeEffect209Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect209Frame current, NativeBattleEffect209Operation operation, NativeBattleEffect209Resources resources, NativeBattleEffect209Inputs inputs)
    {
        switch (operation)
        {
            case NativeBattleEffect209RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect209 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect209 requires its camera.")).RegisterControlResource(textures.Animation);
                return current with { Memory = current.Memory with { Textures = textures } };
            case NativeBattleEffect209UploadTim505E30 upload when upload.SourceAddress == TimAddress:
                return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect209 requires its texture queue.")).EnqueueTim505E30(resources.EmbeddedTim).State } };
            case NativeBattleEffect209ModelFlags flags:
                NativeBattleGraphicsModel model = graphics.Models[flags.Actor];
                ushort mask = flags.WordWidth ? flags.AndMask : (ushort)(0xFF00 | (flags.AndMask & 0xFF));
                ushort bits = flags.WordWidth ? flags.OrMask : (ushort)(flags.OrMask & 0xFF);
                graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, model with { Flags0 = (ushort)((model.Flags0 & mask) | bits) }) };
                return current;
            case NativeBattleEffect209QueueVariant50E3A0 request:
                NativeBattleStageVariant137Entry selected = ReadNativeEffect209Variant(resources);
                NativeBattleEffectFileAsset file = resources.ReadFile(selected.FileId);
                int destination = checked((int)(request.BufferAddress - 0x209FAB8));
                NativeBattleGuardianWorkBuffer work = current.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Effect209 requires its work-buffer destination.");
                NativeEffectIoResult = -1;
                QueueNativeFileIo482610(file.Data.Length, 0x508470);
                return current with { Memory = current.Memory with { GuardianWorkBuffer = work.Write(destination, file.Data.AsSpan()) } };
            case NativeBattleEffect209RebuildVariant50E3C0 rebuild:
                return RebuildNativeEffect209Stage(current, resources, rebuild.BufferAddress);
            case NativeBattleEffect209Stage5713E0 color:
                NativeBattleStageState colorStage = NativeStage ?? throw new NotSupportedException("Effect209 requires its current stage rows.");
                NativeStage = colorStage with { Groups = colorStage.Groups.Select(group => group with { ColorDepth = unchecked((short)color.Intensity), ColorFlags = color.Color }).ToImmutableArray() };
                return current;
            case NativeBattleEffect209SetStageFlags2 flags:
                NativeBattleStageState flagStage = NativeStage ?? throw new NotSupportedException("Effect209 requires its current stage rows.");
                NativeStage = flagStage with { Groups = flagStage.Groups.Select(group => group with { Flags = flags.Enabled ? (byte)(group.Flags | 2) : (byte)(group.Flags & ~2) }).ToImmutableArray() };
                return current;
            case NativeBattleEffect209PublishColor color:
                NativeBattleStageState globalStage = NativeStage ?? throw new NotSupportedException("Effect209 requires its current model colors.");
                NativeBattleStageColors globalColors = globalStage.ModelColors ?? throw new NotSupportedException("Effect209 requires its retained color DWORDs.");
                NativeStage = globalStage with { ModelColors = color.Address switch { 0xB8B7D8 => globalColors with { Color40 = color.Color }, 0xB8B9A8 => globalColors with { Color44 = color.Color }, _ => throw new NotSupportedException("Effect209 selected an unsupported global color address.") } };
                return current;
            case NativeBattleEffect209PublishNormalColor44 color:
                NativeBattleStageState normalStage = NativeStage ?? throw new NotSupportedException("Effect209 requires its current normal model color.");
                NativeStage = normalStage with { ModelColors = (normalStage.ModelColors ?? throw new NotSupportedException("Effect209 requires its retained color DWORDs.")) with { Color44 = color.Color } };
                graphics = graphics with { Models = graphics.Models.Select(model => (model.Flags0 & 2) != 0 ? model with { Color44 = color.Color } : model).ToImmutableArray() };
                return current;
            case NativeBattleEffect209Sound501330 sound:
                PublishNativeEffect209Frame(current);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect209Frame(current);
            case NativeBattleEffect209Vibration4A29A0 vibration when vibration.SourceAddress == VibrationAddress:
                ImmutableArray<byte> bytes = resources.Vibration.Data;
                NativeVibrationBank bank = new(Convert.ToHexString(SHA256.HashData(bytes.AsSpan())), [BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(4))], bytes);
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect209 requires its vibration process.")).Enqueue(bank, vibration.Mode, vibration.Magnitude);
                NativeVibrationProcess = queued.State;
                return NativeBattleEffect209Controller.WriteBytes(current, vibration.OutputAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
            case NativeBattleEffect209StopVibration4A2940 stop:
                NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect209 requires its vibration process.")).Stop4A2940(stop.Handle);
                return current;
            case NativeBattleEffect209Flash5712C0 flash:
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(unchecked((short)flash.Rise), unchecked((short)flash.Hold), unchecked((short)flash.Fall), flash.Color) };
                return current;
            case NativeBattleEffect209SetViewTranslationY translation:
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect209 requires its camera process.");
                NativeBattleCameraViewTranslationScratch scratch = camera.ViewTranslationScratch ?? throw new NotSupportedException("Effect209 requires its retained view-translation owner.");
                NativeCamera = camera with { ViewTranslationScratch = scratch with { Y = translation.Value } };
                return current;
            case NativeBattleEffect209SetProjectionH projection:
                NativeBattleCameraState projected = NativeCamera ?? throw new NotSupportedException("Effect209 requires its camera process.");
                NativeCamera = projected with { Projection = (projected.Projection ?? throw new NotSupportedException("Effect209 requires its current projection registers.")) with { FocalLength = projection.Value } };
                return current;
            case NativeBattleEffect209Contact506690 contact:
                PublishNativeEffect209Frame(current);
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                graphics = graphics with { PendingContacts = pending };
                return ImportNativeEffect209Frame(current);
            case NativeBattleEffect209CommonTextureLoad508630 load:
                (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => current = NativeBattleEffect209Controller.WriteCompletionByte(current, address, value));
                graphics = graphics with { Auxiliary = auxiliary };
                return current;
            case NativeBattleEffect209DrawBatch draw:
                return DeliverNativeEffect209Batch(ref graphics, current, draw);
            case NativeBattleEffect209DrawAddressedTemplate draw:
                return DeliverNativeEffect209Template(ref graphics, current, draw);
            case NativeBattleEffect209CaptureStage7D30:
                return CaptureNativeEffect209Stage(ref graphics, current, resources, inputs);
            default:
                throw new NotSupportedException($"Effect209 emitted unsupported service {operation.GetType().Name}.");
        }
    }
}
