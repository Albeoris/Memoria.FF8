using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect287Frame ExecuteNativeEffect287Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect287Frame frame, NativeBattleEffect287Operation operation, NativeBattleEffect287Resources resources)
    {
        switch (operation)
        {
            case NativeBattleEffect287UploadTim505E30 upload:
                NativeBattleEffectInitializerAsset asset = upload.SourceAddress switch
                {
                    FirstTimAddress => resources.MainTim,
                    SecondTimAddress => resources.SecondaryTim,
                    _ => throw new NotSupportedException("Effect287 selected an unsupported embedded texture.")
                };
                NativeBattleEffectHeapAllocation tim = new(asset.Name, asset.Data);
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect287 requires its texture queue.")).EnqueueTim505E30(tim).State } };
            case NativeBattleEffect287RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect287 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect287 requires its current camera.")).RegisterControlResource(textures.Animation);
                return frame with { Memory = frame.Memory with { Textures = textures } };
            case NativeBattleEffect287Sound501330 sound:
                PublishNativeEffect287Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect287Frame(frame);
            case NativeBattleEffect287Contacts506BA0 contacts:
                PublishNativeEffect287Frame(frame);
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(contacts.FirstContact, contacts.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray() });
                graphics = graphics with { PendingContacts = pending };
                return ImportNativeEffect287Frame(frame);
            case NativeBattleEffect287ModelFlags flags:
                int actor = NativeEffect287Actor(graphics, flags.ModelAddress);
                NativeBattleGraphicsModel current = graphics.Models[actor];
                ushort updated = flags.Width switch
                {
                    1 => unchecked((ushort)((current.Flags0 & 0xFF00) | (((current.Flags0 & flags.AndMask) | flags.OrMask) & 0xFF))),
                    2 => unchecked((ushort)((current.Flags0 & flags.AndMask) | flags.OrMask)),
                    _ => throw new InvalidDataException("Effect287 requires its exact model flags store width.")
                };
                graphics = graphics with { Models = graphics.Models.SetItem(actor, current with { Flags0 = updated }) };
                return frame;
            case NativeBattleEffect287Compose5095B0 compose:
                int composedActor = NativeEffect287Actor(graphics, compose.ModelAddress);
                NativeBattleGraphicsModel model = graphics.Models[composedActor];
                graphics = graphics with { Models = graphics.Models.SetItem(composedActor, NativeBattleBodyBoneMatrices.Compose5095B0(model, model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect287 requires its current model world matrix."))) };
                return frame;
            case NativeBattleEffect287Restore508C90 restore:
                int restoredActor = NativeEffect287Actor(graphics, restore.ModelAddress);
                NativeBattleGraphicsModel composed = graphics.Models[restoredActor];
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, restoredActor).PoseSource ?? throw new NotSupportedException("Effect287 requires its current physical skeleton.");
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect287 requires its current temporary owner."), 88);
                NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(composed, source, NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
                NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
                graphics = graphics with { Models = graphics.Models.SetItem(restoredActor, prepared.Model) };
                return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) };
            case NativeBattleEffect287Stage5713E0 color:
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect287 requires its current stage.");
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)color.Intensity), ColorFlags = unchecked((uint)color.Mode) }).ToImmutableArray() };
                return frame;
            case NativeBattleEffect287Initialize534110 initialize:
                return frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, NativeBattleWaterExecutionFields.ArenaOffset(initialize.Address, 5)) };
            case NativeBattleEffect287Queue5341D0 queue:
                return frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, queue.FileId, queue.Destination, queue.Type) };
            case NativeBattleEffect287Advance534210:
                NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(frame.Memory);
                frame = frame with { Memory = advance.Memory };
                return advance.Request is { } request ? QueueNativeEffect287File(frame, resources, request) : frame;
            case NativeBattleEffect287DrawBatch draw when draw.CursorGlobal == 0x1D8E054:
                NativeBattlePrimitiveFrame commonPrimitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect287 requires its current ordering table.");
                NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect287 requires its current common packet process.");
                int commonCursor = process.ByteCursor ?? throw new NotSupportedException("Effect287 requires its current common packet cursor.");
                NativeBattleGfGeometryBatch commonBatch = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, commonCursor, process.CommonPacketWriteCapacity, index =>
                {
                    (byte[] bytes, bool[] known) = process.ReadCommonPacketSnapshot(index, 1);
                    return (bytes[0], known[0]);
                });
                NativeBattleCommonPacketWrite commonWritten = NativeBattleCommonPacketArena.Append(process, commonPrimitive.Pending, commonBatch);
                graphics = graphics with { PrimitiveFrame = commonPrimitive with { Pending = commonWritten.Ordering } };
                return frame with { Memory = frame.Memory with { PrimitiveProcess = commonWritten.Process } };
            case NativeBattleEffect287DrawBatch draw when draw.CursorGlobal == CursorGlobal:
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect287 requires its current ordering table.");
                int cursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
                NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect287 requires its current packet arena.");
                NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, index =>
                {
                    (byte[] bytes, bool[] known) = arena.ReadSnapshot(index, 1);
                    return (bytes[0], known[0]);
                });
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return frame with { Memory = written.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
            default:
                throw new NotSupportedException($"Effect287 emitted unsupported service {operation.GetType().Name}.");
        }
    }
}
