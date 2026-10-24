using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using static Memoria.FF8.Runtime.NativeBattleEffect227Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect227Frame ExecuteNativeEffect227Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect227Frame frame, NativeBattleEffect227Operation operation, NativeBattleEffect227Resources resources)
    {
        switch (operation)
        {
            case NativeBattleEffect227UploadTim505E30 upload:
                int offset = NativeBattleWaterExecutionFields.ArenaOffset(upload.SourceAddress, resources.MainTim.Data.Length);
                NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, (frame.Memory.Arena ?? throw new NotSupportedException("Effect227 requires its initialized TIM allocation.")).Read(offset, resources.MainTim.Data.Length).ToImmutableArray());
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect227 requires its texture queue.")).EnqueueTim505E30(tim).State } };
            case NativeBattleEffect227RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect227 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect227 requires its current camera.")).RegisterControlResource(textures.Animation);
                return frame with { Memory = frame.Memory with { Textures = textures } };
            case NativeBattleEffect227ModelFlags flags when flags.Width is 1 or 2:
                int actorIndex = NativeEffect227Actor(graphics, flags.ModelAddress);
                NativeBattleGraphicsModel model = graphics.Models[actorIndex];
                int value = flags.Width == 1 ? (model.Flags0 & 0xFF00) | (((byte)model.Flags0 & (byte)flags.AndMask) | (byte)flags.OrMask) : (model.Flags0 & flags.AndMask) | flags.OrMask;
                graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, model with { Flags0 = unchecked((ushort)value) }) };
                return frame;
            case NativeBattleEffect227Sound501330 sound:
                PublishNativeEffect227Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect227Frame(frame);
            case NativeBattleEffect227Text4876F0 text:
                BattleCombatant actor = GraphicsActor(graphics, text.Actor);
                string message = (FindNativeOriginalGraphicsBinding(actor.Id) ?? throw new NotSupportedException("Effect227 requires its original actor text resource.")).Resource.ReadText91(text.Text);
                StopTurnAdvance();
                graphics = QueueNativeDescriptorMessage(graphics, new(Frame, "NativeText", actor.Id, null, text.Duration, message) { NativeMessageMode = 3 }, 0, 240);
                return frame;
            case NativeBattleEffect227VibrationStart4A29A0 vibration when vibration.SourceAddress == VibrationAddress:
                ImmutableArray<byte> bytes = resources.Vibration.Data;
                NativeVibrationBank bank = new(Convert.ToHexString(SHA256.HashData(bytes.AsSpan())), [BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(4))], bytes);
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect227 requires its retained vibration process.")).Enqueue(bank, vibration.Kind, vibration.Strength);
                NativeVibrationProcess = queued.State;
                return NativeBattleEffect227Controller.WriteBytes(frame, vibration.DestinationAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
            case NativeBattleEffect227VibrationStop4A2940 stop:
                NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect227 requires its retained vibration process.")).Stop4A2940(stop.Handle);
                return frame;
            case NativeBattleEffect227DrawBatch draw when draw.CursorGlobal == CursorGlobal:
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect227 requires its current ordering table.");
                int cursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
                NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect227 requires its current packet arena.");
                NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, index =>
                {
                    (byte[] bytes, bool[] known) = arena.ReadSnapshot(index, 1);
                    return (bytes[0], known[0]);
                });
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return frame with { Memory = written.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
            default:
                throw new NotSupportedException($"Effect227 emitted unsupported service {operation.GetType().Name}.");
        }
    }
}
