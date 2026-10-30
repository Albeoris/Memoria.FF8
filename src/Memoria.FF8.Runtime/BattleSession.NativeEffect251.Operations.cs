using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect251Frame ExecuteNativeEffect251Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect251Frame frame, NativeBattleEffect251Operation operation, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        switch (operation)
        {
            case NativeBattleEffect251UploadTim505E30 upload when upload.SourceAddress == EmbeddedTimAddress:
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect251 requires its current texture queue.")).EnqueueTim505E30(resources.EmbeddedTim).State } };
            case NativeBattleEffect251RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect251 requires its current texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect251 requires its current camera.")).RegisterControlResource(textures.Animation);
                return frame with { Memory = frame.Memory with { Textures = textures } };
            case NativeBattleEffect251WriteModelBytes write:
                int writtenActor = NativeEffect301Actor(graphics, write.ModelAddress);
                graphics = graphics with { Models = graphics.Models.SetItem(writtenActor, ApplyNativeEffect301ModelBytes(graphics.Models[writtenActor], write.Offset, write.Data, write.Length)) };
                return frame;
            case NativeBattleEffect251ModelFlags flags:
                int actor = NativeEffect301Actor(graphics, flags.ModelAddress);
                NativeBattleGraphicsModel model = graphics.Models[actor];
                ushort updated = flags.Width switch
                {
                    1 => unchecked((ushort)((model.Flags0 & 0xFF00) | (((model.Flags0 & flags.AndMask) | flags.OrMask) & 0xFF))),
                    2 => unchecked((ushort)((model.Flags0 & flags.AndMask) | flags.OrMask)),
                    _ => throw new InvalidDataException("Effect251 requires its actual model flag store width.")
                };
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = updated }) };
                return frame;
            case NativeBattleEffect251InitializeIo534110 initialize:
                return frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, NativeBattleWaterExecutionFields.ArenaOffset(initialize.Address, 5)) };
            case NativeBattleEffect251QueueFile5341D0 queue:
                return frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, queue.FileId, queue.DestinationAddress, queue.Type) };
            case NativeBattleEffect251AdvanceIo534210:
                NativeBattleEffectIoAdvance advanced = NativeBattleEffectIo.Advance534210(frame.Memory);
                frame = frame with { Memory = advanced.Memory };
                return advanced.Request is { } request ? QueueNativeEffect251File(frame, resources, request) : frame;
            case NativeBattleEffect251Sound501330 sound:
                PublishNativeEffect251Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect251Frame(frame);
            case NativeBattleEffect251Intensity5713E0 intensity:
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect251 requires its current stage groups.");
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity.Strength), ColorFlags = unchecked((uint)intensity.Kind) }).ToImmutableArray() };
                return frame;
            case NativeBattleEffect251SetModelAnimation505C00 animation:
                graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)NativeEffect301Actor(graphics, animation.ModelAddress)), animation.Mode);
                return frame;
            case NativeBattleEffect251Contact506690 contact:
                long relative = (long)contact.TargetArgumentAddress - 0x1D28344;
                if (relative < 0 || relative % 24 != 0 || relative / 24 >= nativeActionPackets.ContactCount)
                {
                    throw new InvalidDataException("Effect251 contact argument leaves its current physical contact records.");
                }
                NativeBattleContactPacket selected = nativeActionPackets.ReadContact(checked((int)(relative / 24)));
                PublishNativeEffect251Frame(frame);
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [selected] });
                graphics = graphics with { PendingContacts = pending };
                return ImportNativeEffect251Frame(frame);
            case NativeBattleEffect251DrawTarget6A2F60 body:
                PublishNativeEffect251Frame(frame);
                int target = NativeEffect301Actor(graphics, body.ModelAddress);
                NativeBattleActionMatrix world = graphics.Models[target].GeometryWorldMatrix ?? throw new NotSupportedException("Effect251 transformed body requires its actual current world matrix.");
                NativeBattleActionMatrix matrix = NativeBattleDuel212Body.Matrix6A2F60(world, inputs.ReadView(frame).ReadMatrix(), NativeBattleEffect251Controller.Vector(body.Angles8), NativeBattleEffect251Controller.Vector(body.Pivot8));
                graphics = DrawNativeEffectModelContext92(graphics, target, matrix, NativeBattleEffect251Controller.Vector(body.Pivot8), inputs.ReadProjection(frame), -47, 4, pilotIdentity: body.PilotIdentity);
                return ImportNativeEffect251Frame(frame);
            case NativeBattleEffect251DrawBatch draw:
                return DeliverNativeEffect251Batch(ref graphics, frame, draw);
            default:
                throw new NotSupportedException($"Effect251 emitted unsupported service {operation.GetType().Name}.");
        }
    }

    private NativeBattleEffect251Frame QueueNativeEffect251File(NativeBattleEffect251Frame frame, NativeBattleEffect251Resources resources, NativeBattleEffectFileRequest request)
    {
        NativeBattleEffectFileAsset file = resources.Files.TryGetValue(request.FileId, out NativeBattleEffectFileAsset? selected) ? selected : throw new NotSupportedException("Effect251 selected an unsupported original completion file.");
        int offset = NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect251 requires its current physical file destination.");
        frame = frame with { Memory = frame.Memory with { Arena = arena.Write(offset, file.Data.AsSpan()) } };
        QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
        return frame;
    }

    private static NativeBattleEffect251Frame DeliverNativeEffect251Batch(ref NativeBattleGraphicsState graphics, NativeBattleEffect251Frame frame, NativeBattleEffect251DrawBatch draw)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect251 requires its current ordering table.");
        if (draw.CursorGlobal == 0x1D8E054)
        {
            NativeBattleCommonPacketWrite common = NativeBattleCommonPacketDelivery.Deliver(frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect251 requires its ordinary packet process."), primitive.Pending, draw.Batch, draw.Flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = common.Ordering } };
            return frame with { Memory = frame.Memory with { PrimitiveProcess = common.Process } };
        }
        if (draw.CursorGlobal != CursorGlobal)
        {
            throw new NotSupportedException("Effect251 packet delivery requires its actual output cursor owner.");
        }
        uint buffer = frame.State.ReadDword(ArenaGlobal);
        NativeBattleModulePoolTask root = NativeBattleEffect251Controller.Read(frame, RootBank);
        uint startAddress = checked(buffer + (uint)(root.ReadByte(15) == 0 ? FirstPacketOffset : SecondPacketOffset));
        int start = NativeBattleWaterExecutionFields.ArenaOffset(startAddress, 0x10000);
        int end = checked(start + 0x10000);
        int cursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
        if (cursor < start || cursor > end || draw.Batch.AllocationBytes < 0 || draw.Batch.AllocationBytes > end - cursor || draw.Batch.Attempts.Any(attempt => attempt.ByteOffsetRelative < 0 || (long)cursor + attempt.ByteOffsetRelative + attempt.PacketLength > end))
        {
            throw new NotSupportedException("Effect251 packet writes exceed its current physical output bank.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect251 requires its actual packet arena.");
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, end, index =>
        {
            (byte[] bytes, bool[] known) = arena.ReadSnapshot(index, 1);
            return (bytes[0], known[0]);
        });
        NativeBattleEffectPacketWrite delivered = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, resolved, draw.Flags);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = delivered.Ordering } };
        return frame with { Memory = delivered.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)delivered.Cursor)) };
    }
}
