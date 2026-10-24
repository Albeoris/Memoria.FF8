using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect213Frame ExecuteNativeEffect213Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect213Frame frame, NativeBattleEffect213Operation operation, NativeBattleEffect213Resources resources)
    {
        switch (operation)
        {
            case NativeBattleEffect213UploadTim505E30 upload:
                int offset = NativeBattleWaterExecutionFields.ArenaOffset(upload.SourceAddress, resources.MainTim.Data.Length);
                NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, (frame.Memory.Arena ?? throw new NotSupportedException("Effect213 requires its current initialized TIM allocation.")).Read(offset, resources.MainTim.Data.Length).ToImmutableArray());
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect213 requires its texture queue.")).EnqueueTim505E30(tim).State } };
            case NativeBattleEffect213RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect213 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect213 requires its current camera.")).RegisterControlResource(textures.Animation);
                return frame with { Memory = frame.Memory with { Textures = textures } };
            case NativeBattleEffect213Sound501330 sound:
                PublishNativeEffect213Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect213Frame(frame);
            case NativeBattleEffect213PositionalSound5013A0 sound:
                PublishNativeEffect213Frame(frame);
                byte[] point = sound.Point8.Read(0, 8);
                PublishNativeModuleSound(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(6))));
                return ImportNativeEffect213Frame(frame);
            case NativeBattleEffect213Contact506690 contact:
                PublishNativeEffect213Frame(frame);
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.OriginalIndex)] });
                graphics = graphics with { PendingContacts = pending };
                return ImportNativeEffect213Frame(frame);
            case NativeBattleEffect213DrawBatch draw when draw.CursorGlobal == CursorGlobal:
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect213 requires its current ordering table.");
                int cursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
                NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect213 requires its current packet arena.");
                NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, index =>
                {
                    (byte[] bytes, bool[] known) = arena.ReadSnapshot(index, 1);
                    return (bytes[0], known[0]);
                });
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return frame with { Memory = written.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
            default:
                throw new NotSupportedException($"Effect213 emitted unsupported service {operation.GetType().Name}.");
        }
    }
}
