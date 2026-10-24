using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect321Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect321Frame ExecuteNativeEffect321Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect321Frame frame, NativeBattleEffect321Operation operation, NativeBattleEffect321Resources resources)
    {
        switch (operation)
        {
            case NativeBattleEffect321UploadTim505E30 upload:
                int offset = NativeBattleWaterExecutionFields.ArenaOffset(upload.SourceAddress, resources.MainTim.Data.Length);
                NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, (frame.Memory.Arena ?? throw new NotSupportedException("Effect321 requires its initialized TIM allocation.")).Read(offset, resources.MainTim.Data.Length).ToImmutableArray());
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect321 requires its texture queue.")).EnqueueTim505E30(tim).State } };
            case NativeBattleEffect321RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect321 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect321 requires its current camera.")).RegisterControlResource(textures.Animation);
                return frame with { Memory = frame.Memory with { Textures = textures } };
            case NativeBattleEffect321WriteModelBytes write:
                int actor = NativeEffect321Actor(graphics, write.ModelAddress);
                NativeBattleGraphicsModel model = ApplyNativeEffect174ModelBytes(graphics.Models[actor], new(actor, write.Offset, write.Data, write.Length));
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                return frame;
            case NativeBattleEffect321PcRender47CF50 render:
                return frame with { Memory = frame.Memory with { PcRenderRequest = (frame.Memory.PcRenderRequest ?? throw new NotSupportedException("Effect321 requires its current PC render request.")).Request47CF50(render.Kind, render.Red, render.Green, render.Blue, render.Duration) } };
            case NativeBattleEffect321VibrationStart4A29A0 vibration when vibration.SourceAddress == VibrationAddress:
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect321 requires its vibration process.")).Enqueue(resources.VibrationBank, vibration.Kind, vibration.Strength);
                NativeVibrationProcess = queued.State;
                return frame with { State = frame.State.WriteDword(vibration.DestinationAddress, unchecked((uint)queued.Handle)) };
            case NativeBattleEffect321VibrationStop4A2940 vibration:
                NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect321 requires its vibration process.")).Stop4A2940(vibration.Handle);
                return frame;
            case NativeBattleEffect321Sound501330 sound:
                PublishNativeEffect321Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect321Frame(frame);
            case NativeBattleEffect321PositionalSound5013A0 sound:
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect321 sound requires its current camera.");
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect321 sound requires its actual temporary memory."), 12);
                NativeBattleEffect321SoundProjection projected = NativeBattleEffect321PositionalSound.Project5011B0(frame.Memory, camera.CapturePackedView1D97778(), camera.Projection ?? throw new NotSupportedException("Effect321 sound requires its current projection."), sound.Point8);
                NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, projected.Output12, 12);
                int pan = 96 * (BinaryPrimitives.ReadInt16LittleEndian(staging.Read(allocation.Address, 2)) - 160) / 320 + 128;
                frame = frame with { Memory = projected.Memory, Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 12) };
                PublishNativeEffect321Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = checked((byte)Math.Clamp(pan, 80, 175)) });
                return ImportNativeEffect321Frame(frame);
            case NativeBattleEffect321Contact506690 contact:
                PublishNativeEffect321Frame(frame);
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                int header = frame.Effect.Parameters?.HeaderIndex7C ?? throw new NotSupportedException("Effect321 requires its current action header.");
                int first = nativeActionPackets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect321 requires its current contact base.");
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(checked(first + contact.ContactIndex))] });
                graphics = graphics with { PendingContacts = pending };
                return ImportNativeEffect321Frame(frame);
            case NativeBattleEffect321AppendTextureLoad508630 load when load.SourceAddress == FirstPacketAddress:
                (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => frame = NativeBattleEffect321Controller.WriteCompletionByte(frame, address, value));
                graphics = graphics with { Auxiliary = auxiliary };
                return frame;
            case NativeBattleEffect321DrawBatch draw when draw.CursorGlobal == CursorGlobal:
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect321 requires its current ordering table.");
                NativeBattleEffect321PacketWrite written = NativeBattleEffect321PacketArena.Append(frame.State, primitive.Pending, draw.Batch, draw.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return frame with { State = written.State };
            default:
                throw new NotSupportedException($"Effect321 emitted unsupported service {operation.GetType().Name}.");
        }
    }
}
