using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect290Frame ExecuteNativeEffect290Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect290Frame frame, NativeBattleEffect290Operation operation, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        switch (operation)
        {
            case NativeBattleEffect290UploadTim505E30 upload:
                int offset = NativeBattleWaterExecutionFields.ArenaOffset(upload.SourceAddress, resources.MainTim.Data.Length);
                NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, (frame.Memory.Arena ?? throw new NotSupportedException("Effect290 requires its executed texture initializer.")).Read(offset, resources.MainTim.Data.Length).ToImmutableArray());
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect290 requires its texture queue.")).EnqueueTim505E30(tim).State } };
            case NativeBattleEffect290RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect290 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect290 requires its current camera.")).RegisterControlResource(textures.Animation);
                return frame with { Memory = frame.Memory with { Textures = textures } };
            case NativeBattleEffect290ModelFlags flags:
                int actor = NativeEffect290Actor(graphics, flags.ModelAddress);
                NativeBattleGraphicsModel model = graphics.Models[actor];
                ushort updated = flags.Width switch
                {
                    1 => unchecked((ushort)((model.Flags0 & 0xFF00) | (((model.Flags0 & flags.AndMask) | flags.OrMask) & 0xFF))),
                    2 => unchecked((ushort)((model.Flags0 & flags.AndMask) | flags.OrMask)),
                    _ => throw new InvalidDataException("Effect290 requires its exact model flags store width.")
                };
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = updated }) };
                return frame;
            case NativeBattleEffect290WriteModelBytes write:
                int target = NativeEffect290Actor(graphics, write.ModelAddress);
                graphics = graphics with { Models = graphics.Models.SetItem(target, ApplyNativeEffect301ModelBytes(graphics.Models[target], write.Offset, write.Data, write.Length)) };
                return frame;
            case NativeBattleEffect290Music47E3C0 music:
                if ((ResolvedNativeFlags & 2) == 0)
                {
                    ushort channel = NativeMusicProcess?.ReturnChannel ?? throw new NotSupportedException("Effect290 requires its actual music return channel.");
                    NativeGlobalSoundProcess owner = NativeGlobalSoundProcess ?? throw new NotSupportedException("Effect290 requires its actual sound process.");
                    NativeMusicPlaybackState playback = owner.MusicPlayback ?? throw new NotSupportedException("Effect290 requires its current music playback.");
                    NativeGlobalSoundProcess = owner with { MusicPlayback = playback.FadeGain46BBC0(unchecked((uint)(short)channel), unchecked((byte)music.Duration), 0) };
                }
                return frame;
            case NativeBattleEffect290VibrationStart4A29A0 vibration when vibration.SourceAddress == VibrationAddress:
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect290 requires its vibration process.")).Enqueue(resources.VibrationBank, vibration.Kind, vibration.Strength);
                NativeVibrationProcess = queued.State;
                return NativeBattleEffect290Controller.WriteBytes(frame, vibration.DestinationAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
            case NativeBattleEffect290VibrationStop4A2940 vibration:
                NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect290 requires its vibration process.")).Stop4A2940(vibration.Handle);
                return frame;
            case NativeBattleEffect290Sound501330 sound:
                PublishNativeEffect290Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect290Frame(frame);
            case NativeBattleEffect290InitializeIo534110 initialize:
                return frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, initialize.ArenaOffset) };
            case NativeBattleEffect290QueueFile5341D0 queue:
                return frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, queue.FileId, queue.DestinationAddress, queue.Type) };
            case NativeBattleEffect290AdvanceIo534210:
                NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(frame.Memory);
                frame = frame with { Memory = advance.Memory };
                return advance.Request is { } request ? QueueNativeEffect290File(frame, resources, request) : frame;
            case NativeBattleEffect290InitializeProgramme7016B0 programme:
                return NativeBattleEffect290Programme.Initialize(frame, programme.WorkspaceAddress, programme.SourceAddress, programme.ClearBytes, resources);
            case NativeBattleEffect290DrawBody676B40 body:
                return DrawNativeEffect290Body(ref graphics, frame, body, inputs);
            case NativeBattleEffect290SelectFragments675AA0 selection:
                return SelectNativeEffect290Fragments(ref graphics, frame, selection, resources, inputs);
            case NativeBattleEffect290BirthTarget676340 birth:
                return BirthNativeEffect290Target(ref graphics, frame, birth, resources, inputs);
            case NativeBattleEffect290DrawFragments676EF0 fragments:
                return NativeBattleEffect290Lower.DrawFragments(frame, fragments.MainAddress, resources, inputs);
            case NativeBattleEffect290DrawSmoke677690 smoke:
                return NativeBattleEffect290Lower.DrawSmoke(frame, smoke.MainAddress, resources, inputs);
            case NativeBattleEffect290AdvanceFragments6774C0 fragments:
                return NativeBattleEffect290Lower.AdvanceFragments(frame, fragments.MainAddress);
            case NativeBattleEffect290DrawBatch draw:
                return DeliverNativeEffect290Batch(ref graphics, frame, draw.Batch, draw.CursorGlobal, draw.Flags);
            default:
                throw new NotSupportedException($"Effect290 emitted unsupported service {operation.GetType().Name}.");
        }
    }

    private NativeBattleEffect290Frame DeliverNativeEffect290Batch(ref NativeBattleGraphicsState graphics, NativeBattleEffect290Frame frame, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags = 0)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect290 requires its current ordering table.");
        if (cursorGlobal == 0x1D8E054)
        {
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect290 requires its current common packet process.");
            int cursor = process.ByteCursor ?? throw new NotSupportedException("Effect290 requires its current common packet cursor.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, cursor, process.CommonPacketWriteCapacity, index =>
            {
                (byte[] bytes, bool[] known) = process.ReadCommonPacketSnapshot(index, 1);
                return (bytes[0], known[0]);
            });
            NativeBattleCommonPacketWrite delivered = NativeBattleCommonPacketArena.Append(process, primitive.Pending, resolved);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = delivered.Ordering } };
            return frame with { Memory = frame.Memory with { PrimitiveProcess = delivered.Process } };
        }
        if (cursorGlobal != CursorGlobal)
        {
            throw new NotSupportedException("Effect290 packet delivery requires its actual cursor owner.");
        }
        int privateCursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect290 requires its current packet arena.");
        NativeBattleGfGeometryBatch privateBatch = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, privateCursor, NativeBattleEffectArena.Length, index =>
        {
            (byte[] bytes, bool[] known) = arena.ReadSnapshot(index, 1);
            return (bytes[0], known[0]);
        });
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, privateCursor, primitive.Pending, privateBatch, flags);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        return frame with { Memory = written.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
    }
}
