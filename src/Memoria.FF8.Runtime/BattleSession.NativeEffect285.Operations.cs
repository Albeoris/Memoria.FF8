using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect285Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect285Frame ExecuteNativeEffect285Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect285Frame frame, NativeBattleEffect285Operation operation, NativeBattleEffect285Resources resources)
    {
        switch (operation)
        {
            case NativeBattleEffect285UploadTim505E30 upload:
                int offset = NativeBattleWaterExecutionFields.ArenaOffset(upload.SourceAddress, resources.MainTim.Data.Length);
                NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, (frame.Memory.Arena ?? throw new NotSupportedException("Effect285 requires its initialized TIM allocation.")).Read(offset, resources.MainTim.Data.Length).ToImmutableArray());
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect285 requires its texture queue.")).EnqueueTim505E30(tim).State } };
            case NativeBattleEffect285Sound501330 sound:
                PublishNativeEffect285Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect285Frame(frame);
            case NativeBattleEffect285AllContacts506BA0 contacts:
                PublishNativeEffect285Frame(frame);
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(contacts.FirstContactIndex, contacts.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray() });
                graphics = graphics with { PendingContacts = pending };
                return ImportNativeEffect285Frame(frame);
            case NativeBattleEffect285CameraForce50A730:
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect285 requires its current camera.")).Apply50A730();
                return frame;
            case NativeBattleEffect285CameraPoint point:
                NativeCamera = ApplyNativeEffect285CameraPoint(NativeCamera ?? throw new NotSupportedException("Effect285 requires its current camera."), point);
                return frame;
            case NativeBattleEffect285CameraScalarFocal focal:
                NativeBattleCameraState focused = NativeCamera ?? throw new NotSupportedException("Effect285 requires its current camera.");
                NativeCamera = focused with { FocalLength = focal.Value, CurrentPose = focused.CurrentPose is { } focusedPose ? focusedPose with { FocalLength = focal.Value } : null };
                return frame;
            case NativeBattleEffect285CameraRoll roll:
                NativeBattleCameraState rolled = NativeCamera ?? throw new NotSupportedException("Effect285 requires its current camera.");
                NativeCamera = rolled with { Roll = roll.Value, CurrentPose = rolled.CurrentPose is { } rolledPose ? rolledPose with { Roll = roll.Value } : null };
                return frame;
            case NativeBattleEffect285ModelFlags flags:
                int actor = NativeEffect285Actor(graphics, flags.ModelAddress);
                graphics = graphics with { Models = graphics.Models.SetItem(actor, ApplyNativeEffect285ModelFlags(graphics.Models[actor], flags)) };
                return frame;
            case NativeBattleEffect285StageFlags flags:
                NativeBattleStageState visible = NativeStage ?? throw new NotSupportedException("Effect285 requires its current stage.");
                NativeStage = visible with { Groups = visible.Groups.Select(group => group with { Flags = unchecked((byte)((group.Flags & flags.AndMask) | flags.OrMask)) }).ToImmutableArray() };
                return frame;
            case NativeBattleEffect285Stage5713E0 color:
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect285 requires its current stage.");
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)color.Amount), ColorFlags = color.Color }).ToImmutableArray() };
                return frame;
            case NativeBattleEffect285Flash5712C0 flash:
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(unchecked((short)flash.Rise), unchecked((short)flash.Hold), unchecked((short)flash.Fall), flash.Intensity, pilotIdentity: flash.PilotIdentity) };
                return frame;
            case NativeBattleEffect285IoInitialize534110 initialize:
                return frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, NativeBattleWaterExecutionFields.ArenaOffset(initialize.Address, 5)) };
            case NativeBattleEffect285IoQueue5341D0 queue:
                return frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, queue.FileId, queue.Destination, queue.Type) };
            case NativeBattleEffect285IoAdvance534210:
                NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(frame.Memory);
                frame = frame with { Memory = advance.Memory };
                return advance.Request is { } request ? QueueNativeEffect285File(frame, resources, request) : frame;
            case NativeBattleEffect285DrawBatch draw when draw.CursorGlobal == CursorGlobal:
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect285 requires its current ordering table.");
                int cursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
                NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect285 requires its current packet arena.");
                NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, index =>
                {
                    (byte[] bytes, bool[] known) = arena.ReadSnapshot(index, 1);
                    return (bytes[0], known[0]);
                });
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return frame with { Memory = written.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
            default:
                throw new NotSupportedException($"Effect285 emitted unsupported service {operation.GetType().Name}.");
        }
    }
}
