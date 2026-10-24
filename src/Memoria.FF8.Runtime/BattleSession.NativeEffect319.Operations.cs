using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect319Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect319Frame ExecuteNativeEffect319Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect319Frame frame, NativeBattleEffect319Operation operation, NativeBattleEffect319Resources resources)
    {
        switch (operation)
        {
            case NativeBattleEffect319UploadTim505E30 upload:
                int offset = NativeBattleWaterExecutionFields.ArenaOffset(upload.SourceAddress, resources.MainTim.Data.Length);
                NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, (frame.Memory.Arena ?? throw new NotSupportedException("Effect319 requires its initialized TIM allocation.")).Read(offset, resources.MainTim.Data.Length).ToImmutableArray());
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect319 requires its texture queue.")).EnqueueTim505E30(tim).State } };
            case NativeBattleEffect319RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect319 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect319 requires its current camera.")).RegisterControlResource(textures.Animation);
                return frame with { Memory = frame.Memory with { Textures = textures } };
            case NativeBattleEffect319MeshMask mask:
                int actorIndex = NativeEffect319Actor(graphics, mask.ModelAddress);
                NativeBattleGraphicsModel model = graphics.Models[actorIndex];
                NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Effect319 requires its current mesh-mask owner.");
                graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, model with { SequenceState = sequence with { MeshMask124 = (sequence.MeshMask124 & mask.AndMask) | mask.OrMask } }) };
                return frame;
            case NativeBattleEffect319ScaleBone56BEF0 scale:
                int caster = NativeEffect319Actor(graphics, frame.State.ReadDword(CasterGlobal));
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, caster).PoseSource ?? throw new NotSupportedException("Effect319 requires its current bone-cache source.");
                _ = NativeBattleBodyBoneMatrices.Require(graphics.Models[caster], source);
                int bone = NativeBattleEffect319BoneCache.ResolveIndex(NativeEffect319SkeletonAddress(graphics, frame, caster), source.BoneCount, scale.BoneRecordAddress);
                graphics = graphics with { Models = graphics.Models.SetItem(caster, NativeBattleEffect319BoneCache.ScaleCurrent56BEF0(graphics.Models[caster], bone, scale.XY, scale.Z)) };
                return frame;
            case NativeBattleEffect319Sound501330 sound:
                PublishNativeEffect319Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect319Frame(frame);
            case NativeBattleEffect319Contact506690 contact:
                PublishNativeEffect319Frame(frame);
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.OriginalIndex)] });
                graphics = graphics with { PendingContacts = pending };
                return ImportNativeEffect319Frame(frame);
            case NativeBattleEffect319Initialize534110 initialize:
                return frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, NativeBattleWaterExecutionFields.ArenaOffset(initialize.Address, 5)) };
            case NativeBattleEffect319Queue5341D0 queue:
                return frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, queue.FileId, queue.Destination, queue.Type) };
            case NativeBattleEffect319Advance534210:
                NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(frame.Memory);
                frame = frame with { Memory = advance.Memory };
                return advance.Request is { } request ? QueueNativeEffect319File(frame, resources, request) : frame;
            case NativeBattleEffect319DrawBatch draw when draw.CursorGlobal == CursorGlobal:
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect319 requires its current ordering table.");
                int cursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
                NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect319 requires its current packet arena.");
                NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, index =>
                {
                    (byte[] bytes, bool[] known) = arena.ReadSnapshot(index, 1);
                    return (bytes[0], known[0]);
                });
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return frame with { Memory = written.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
            default:
                throw new NotSupportedException($"Effect319 emitted unsupported service {operation.GetType().Name}.");
        }
    }
}
