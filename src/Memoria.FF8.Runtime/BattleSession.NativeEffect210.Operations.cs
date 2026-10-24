using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect210Frame ExecuteNativeEffect210Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect210Frame current, NativeBattleEffect210Operation operation, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        switch (operation)
        {
            case NativeBattleEffect210UploadTim505E30 upload when upload.SourceAddress == TimAddress:
                return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect210 requires its texture queue.")).EnqueueTim505E30(resources.EmbeddedTim).State } };
            case NativeBattleEffect210RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect210 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect210 requires its camera process.")).RegisterControlResource(textures.Animation);
                return current with { Memory = current.Memory with { Textures = textures } };
            case NativeBattleEffect210WriteModelBytes write:
                graphics = graphics with { Models = graphics.Models.SetItem(write.Actor, NativeBattleRaldo19ModelMemory.Apply(graphics.Models[write.Actor], write.Offset, write.Data, 0, write.Length)) };
                return current;
            case NativeBattleEffect210StageFlags flags:
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect210 requires its current stage rows.");
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { Flags = (byte)((group.Flags & flags.AndMask) | flags.OrMask) }).ToImmutableArray() };
                return current;
            case NativeBattleEffect210Stage5713E0 brightness:
                NativeBattleStageState colored = NativeStage ?? throw new NotSupportedException("Effect210 requires its current stage rows.");
                NativeStage = colored with { Groups = colored.Groups.Select(group => group with { ColorDepth = unchecked((short)brightness.Amount), ColorFlags = brightness.Color }).ToImmutableArray() };
                return current;
            case NativeBattleEffect210Sound501330 sound:
                PublishNativeEffect210Frame(current);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect210Frame(current);
            case NativeBattleEffect210AllContacts506BA0 contacts:
                PublishNativeEffect210Frame(current);
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(contacts.FirstContactIndex, contacts.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray() });
                graphics = graphics with { PendingContacts = pending };
                return ImportNativeEffect210Frame(current);
            case NativeBattleEffect210IoInitialize534110 initialize:
                return current with { Memory = NativeBattleEffectIo.Initialize534110(current.Memory, NativeBattleWaterExecutionFields.ArenaOffset(initialize.ControlAddress, 5)) };
            case NativeBattleEffect210IoQueue5341D0 queue:
                return current with { Memory = NativeBattleEffectIo.Queue5341D0(current.Memory, queue.FileId, queue.DestinationAddress, queue.Type) };
            case NativeBattleEffect210IoAdvance534210:
                NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(current.Memory);
                current = current with { Memory = advance.Memory };
                return advance.Request is { } request ? QueueNativeEffect210File(current, resources, request) : current;
            case NativeBattleEffect210DrawBatch draw:
                return DeliverNativeEffect210Batch(ref graphics, current, draw);
            case NativeBattleEffect210Capture9440 capture:
                return CaptureNativeEffect210Stage(ref graphics, current, capture);
            case NativeBattleEffect210Bounds9800 bounds:
                return ReadNativeEffect210Bounds(ref graphics, current, bounds);
            case NativeBattleEffect210DrawModel9960 body:
                return DrawNativeEffect210Body(ref graphics, current, body);
            case NativeBattleEffect210CaptureEcho8200 captureEcho:
                return CaptureNativeEffect210Echo(graphics, current, captureEcho);
            case NativeBattleEffect210DrawEcho7EE0 echo:
                return DrawNativeEffect210Echo(ref graphics, current, echo);
            default:
                throw new NotSupportedException($"Effect210 emitted unsupported service {operation.GetType().Name}.");
        }
    }

    private static NativeBattleEffect210Frame DeliverNativeEffect210Batch(ref NativeBattleGraphicsState graphics, NativeBattleEffect210Frame frame, NativeBattleEffect210DrawBatch draw)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect210 requires its current ordering table.");
        if (draw.CursorGlobal == 0x1D8E054)
        {
            NativeBattleCommonPacketWrite common = NativeBattleCommonPacketDelivery.Deliver(frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect210 requires its ordinary packet process."), primitive.Pending, draw.Batch, draw.Flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = common.Ordering } };
            return frame with { Memory = frame.Memory with { PrimitiveProcess = common.Process } };
        }
        if (draw.CursorGlobal is not (CursorGlobal or SecondCursorGlobal))
        {
            throw new NotSupportedException("Effect210 selected an unsupported packet cursor.");
        }
        int cursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(draw.CursorGlobal), 0);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect210 requires its current packet arena.");
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
        {
            (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
            return (bytes[0], known[0]);
        });
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, resolved, draw.Flags);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        return frame with { Memory = written.Memory, State = frame.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
    }
}
