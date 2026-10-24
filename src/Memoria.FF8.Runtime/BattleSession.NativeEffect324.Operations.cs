using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect324Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect324Frame ExecuteNativeEffect324Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect324Frame frame, NativeBattleEffect324Operation operation, NativeBattleEffect324Resources resources)
    {
        switch (operation)
        {
            case NativeBattleEffect324UploadTim505E30 upload:
                int offset = NativeBattleWaterExecutionFields.ArenaOffset(upload.SourceAddress, resources.MainTim.Data.Length);
                NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, (frame.Memory.Arena ?? throw new NotSupportedException("Effect324 requires its initialized TIM allocation.")).Read(offset, resources.MainTim.Data.Length).ToImmutableArray());
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect324 requires its texture queue.")).EnqueueTim505E30(tim).State } };
            case NativeBattleEffect324RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect324 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect324 requires its current camera.")).RegisterControlResource(textures.Animation);
                return frame with { Memory = frame.Memory with { Textures = textures } };
            case NativeBattleEffect324Sound5013A0 sound:
                PublishNativeEffect324Frame(frame);
                byte[] point = sound.Point8.Read(0, 6);
                PublishNativeModuleSound(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(4)), 0));
                return ImportNativeEffect324Frame(frame);
            case NativeBattleEffect324Contact506690 contact:
                PublishNativeEffect324Frame(frame);
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.OriginalIndex)] });
                graphics = graphics with { PendingContacts = pending };
                return ImportNativeEffect324Frame(frame);
            case NativeBattleEffect324VibrationStart4A29A0 vibration when vibration.SourceAddress == VibrationAddress && vibration.DestinationAddress == VibrationGlobal:
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect324 requires its current vibration process.")).Enqueue(resources.VibrationBank, vibration.Kind, vibration.Strength);
                NativeVibrationProcess = queued.State;
                return frame with { State = frame.State.WriteDword(vibration.DestinationAddress, unchecked((uint)queued.Handle)) };
            case NativeBattleEffect324VibrationStop4A2940 stop:
                NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect324 requires its current vibration process.")).Stop4A2940(stop.Handle);
                return frame;
            case NativeBattleEffect324WriteModelBytes write:
                int actor = NativeEffect324Actor(graphics, write.ModelAddress);
                graphics = graphics with { Models = graphics.Models.SetItem(actor, ApplyNativeEffect324ModelBytes(graphics.Models[actor], write)) };
                return frame;
            case NativeBattleEffect324WriteCurrentBoneMatrices32 write:
                int boneActor = NativeEffect324Actor(graphics, write.ModelAddress);
                NativeBattleGraphicsModel model = graphics.Models[boneActor];
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, boneActor).PoseSource ?? throw new NotSupportedException("Effect324 requires its current primary skeleton.");
                _ = NativeBattleBodyBoneMatrices.Require(model, source);
                if (write.Matrices.IsDefault || write.Matrices.Length != source.BoneCount)
                {
                    throw new InvalidDataException("Effect324 bone restore requires its complete current physical rig.");
                }
                NativeBattleBodyBoneMatrices bones = model.CurrentBodyBoneMatrices! with { Matrices = write.Matrices.Select(matrix => matrix.ReadMatrix()).ToImmutableArray() };
                bones.Validate();
                graphics = graphics with { Models = graphics.Models.SetItem(boneActor, model with { CurrentBodyBoneMatrices = bones }) };
                return frame;
            case NativeBattleEffect324DrawModel5977D0 body:
                var drawn = DrawNativePrimaryHistory76(frame.Memory, frame.Effect, frame.Staging, graphics, NativeEffect324Actor(graphics, body.ModelAddress));
                graphics = drawn.Graphics;
                return frame with { Memory = drawn.Memory, Staging = drawn.Staging };
            case NativeBattleEffect324DrawBatch draw when draw.CursorGlobal == CursorGlobal:
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect324 requires its current ordering table.");
                NativeBattleEffect324PacketWrite written = NativeBattleEffect324PacketArena.Append(frame.State, primitive.Pending, draw.Batch, draw.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return frame with { State = written.State };
            default:
                throw new NotSupportedException($"Effect324 emitted unsupported service {operation.GetType().Name}.");
        }
    }
}
