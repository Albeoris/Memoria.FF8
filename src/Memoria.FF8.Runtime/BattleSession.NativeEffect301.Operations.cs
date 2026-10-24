using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect301Frame ExecuteNativeEffect301Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect301Frame frame, NativeBattleEffect301Operation operation, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        switch (operation)
        {
            case NativeBattleEffect301UploadTim505E30 upload:
                int offset = NativeBattleWaterExecutionFields.ArenaOffset(upload.SourceAddress, resources.MainTim.Data.Length);
                NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, (frame.Memory.Arena ?? throw new NotSupportedException("Effect301 requires its initialized TIM allocation.")).Read(offset, resources.MainTim.Data.Length).ToImmutableArray());
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect301 requires its texture queue.")).EnqueueTim505E30(tim).State } };
            case NativeBattleEffect301RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect301 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect301 requires its current camera.")).RegisterControlResource(textures.Animation);
                return frame with { Memory = frame.Memory with { Textures = textures } };
            case NativeBattleEffect301ApplyCamera50A730:
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect301 requires its current camera.")).Apply50A730();
                return frame;
            case NativeBattleEffect301ModelFlags flags:
                int target = NativeEffect301Actor(graphics, flags.ModelAddress);
                NativeBattleGraphicsModel current = graphics.Models[target];
                ushort updated = flags.Width switch
                {
                    1 => unchecked((ushort)((current.Flags0 & 0xFF00) | (((current.Flags0 & flags.AndMask) | flags.OrMask) & 0xFF))),
                    2 => unchecked((ushort)((current.Flags0 & flags.AndMask) | flags.OrMask)),
                    _ => throw new InvalidDataException("Effect301 requires its exact model flags store width.")
                };
                graphics = graphics with { Models = graphics.Models.SetItem(target, current with { Flags0 = updated }) };
                return frame;
            case NativeBattleEffect301WriteModelBytes write:
                int actor = NativeEffect301Actor(graphics, write.ModelAddress);
                graphics = graphics with { Models = graphics.Models.SetItem(actor, ApplyNativeEffect301ModelBytes(graphics.Models[actor], write.Offset, write.Data, write.Length)) };
                return frame;
            case NativeBattleEffect301VibrationStart4A29A0 vibration when vibration.SourceAddress == VibrationAddress:
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect301 requires its vibration process.")).Enqueue(resources.VibrationBank, vibration.Kind, vibration.Strength);
                NativeVibrationProcess = queued.State;
                return NativeBattleEffect301Controller.WriteBytes(frame, vibration.DestinationAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
            case NativeBattleEffect301VibrationStop4A2940 vibration:
                NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect301 requires its vibration process.")).Stop4A2940(vibration.Handle);
                return frame;
            case NativeBattleEffect301Sound501330 sound:
                PublishNativeEffect301Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect301Frame(frame);
            case NativeBattleEffect301DrawBody66E300 body:
                return DrawNativeEffect301Body(ref graphics, frame, body, resources, inputs);
            case NativeBattleEffect301DrawBatch draw when draw.CursorGlobal == CursorGlobal:
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect301 requires its current ordering table.");
                int cursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
                NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect301 requires its current packet arena.");
                NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, index =>
                {
                    (byte[] bytes, bool[] known) = arena.ReadSnapshot(index, 1);
                    return (bytes[0], known[0]);
                });
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return frame with { Memory = written.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
            default:
                throw new NotSupportedException($"Effect301 emitted unsupported service {operation.GetType().Name}.");
        }
    }
}
