using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect313Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect313Frame ExecuteNativeEffect313Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect313Frame frame, NativeBattleEffect313Operation operation, NativeBattleEffect313Resources resources)
    {
        switch (operation)
        {
            case NativeBattleEffect313UploadTim505E30 upload:
                NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, NativeBattleEffect313Controller.ReadBytes(frame, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect313 requires its texture queue.")).EnqueueTim505E30(tim).State } };
            case NativeBattleEffect313RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect313 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect313 requires its current camera.")).RegisterControlResource(textures.Animation);
                return frame with { Memory = frame.Memory with { Textures = textures } };
            case NativeBattleEffect313Sound501330 sound:
                PublishNativeEffect313Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect313Frame(frame);
            case NativeBattleEffect313Contact506690 contact:
                NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect313 requires its current action header.");
                NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C];
                int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect313 requires its current contact cursor.");
                if ((uint)contact.HeaderRelativeIndex >= header.ReadByte(16))
                {
                    throw new InvalidDataException("Effect313 contact leaves its current action header.");
                }
                PublishNativeEffect313Frame(frame);
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(checked(first + contact.HeaderRelativeIndex))] });
                graphics = graphics with { PendingContacts = pending };
                return ImportNativeEffect313Frame(frame);
            case NativeBattleEffect313WriteModelBytes write:
                int actor = NativeEffect313Actor(graphics, write.ModelAddress);
                graphics = graphics with { Models = graphics.Models.SetItem(actor, ApplyNativeEffect174ModelBytes(graphics.Models[actor], new(actor, write.Offset, write.Data, write.Length))) };
                return frame;
            case NativeBattleEffect313SelectSequence505C00 sequence:
                graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)NativeEffect313Actor(graphics, sequence.ModelAddress)), sequence.Sequence);
                return frame;
            case NativeBattleEffect313AppendTextureLoad508630 load:
                (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => frame = NativeBattleEffect313Controller.WriteCompletionByte(frame, address, value));
                graphics = graphics with { Auxiliary = auxiliary };
                return frame;
            case NativeBattleEffect313Stage5713E0 color:
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect313 requires its current stage.");
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)color.Intensity), ColorFlags = unchecked((uint)color.Mode) }).ToImmutableArray() };
                return frame;
            case NativeBattleEffect313DrawBatch draw when draw.CursorGlobal == CursorGlobal:
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect313 requires its current ordering table.");
                int cursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
                NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect313 requires its current packet arena.");
                NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, index =>
                {
                    (byte[] bytes, bool[] known) = arena.ReadSnapshot(index, 1);
                    return (bytes[0], known[0]);
                });
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return frame with { Memory = written.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
            default:
                throw new NotSupportedException($"Effect313 emitted unsupported service {operation.GetType().Name}.");
        }
    }
}
