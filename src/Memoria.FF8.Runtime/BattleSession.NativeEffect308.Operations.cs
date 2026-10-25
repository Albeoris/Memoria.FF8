using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect308Frame ExecuteNativeEffect308Operation(ref NativeBattleGraphicsState graphics, NativeBattleEffect308Frame frame, NativeBattleEffect308Operation operation, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        switch (operation)
        {
            case NativeBattleEffect308UploadTim505E30 upload:
                int offset = NativeBattleWaterExecutionFields.ArenaOffset(upload.SourceAddress, resources.MainTim.Data.Length);
                NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, (frame.Memory.Arena ?? throw new NotSupportedException("Effect308 requires its executed texture initializer.")).Read(offset, resources.MainTim.Data.Length).ToImmutableArray());
                return frame with { Memory = frame.Memory with { Textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect308 requires its texture queue.")).EnqueueTim505E30(tim).State } };
            case NativeBattleEffect308RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                NativeBattleEffectTextureState textures = (frame.Memory.Textures ?? throw new NotSupportedException("Effect308 requires its texture scheduler.")).Register5099A0(resources.Camera);
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect308 requires its current camera.")).RegisterControlResource(textures.Animation);
                return frame with { Memory = frame.Memory with { Textures = textures } };
            case NativeBattleEffect308ModelFlags flags:
                int actor = NativeEffect308Actor(graphics, flags.ModelAddress);
                NativeBattleGraphicsModel model = graphics.Models[actor];
                ushort updated = flags.Width switch
                {
                    1 => unchecked((ushort)((model.Flags0 & 0xFF00) | (((model.Flags0 & flags.AndMask) | flags.OrMask) & 0xFF))),
                    2 => unchecked((ushort)((model.Flags0 & flags.AndMask) | flags.OrMask)),
                    _ => throw new InvalidDataException("Effect308 requires its exact model flags store width.")
                };
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = updated }) };
                return frame;
            case NativeBattleEffect308WriteModelBytes write:
                int target = NativeEffect308Actor(graphics, write.ModelAddress);
                graphics = graphics with { Models = graphics.Models.SetItem(target, ApplyNativeEffect301ModelBytes(graphics.Models[target], write.Offset, write.Data, write.Length)) };
                return frame;
            case NativeBattleEffect308Music47E3C0 music:
                if ((ResolvedNativeFlags & 2) == 0)
                {
                    ushort channel = NativeMusicProcess?.ReturnChannel ?? throw new NotSupportedException("Effect308 requires its actual music return channel.");
                    NativeGlobalSoundProcess owner = NativeGlobalSoundProcess ?? throw new NotSupportedException("Effect308 requires its actual sound process.");
                    NativeMusicPlaybackState playback = owner.MusicPlayback ?? throw new NotSupportedException("Effect308 requires its current music playback.");
                    NativeGlobalSoundProcess = owner with { MusicPlayback = playback.FadeGain46BBC0(unchecked((uint)(short)channel), unchecked((byte)music.Duration), 0) };
                }
                return frame;
            case NativeBattleEffect308VibrationStart4A29A0 vibration when vibration.SourceAddress == VibrationAddress:
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect308 requires its vibration process.")).Enqueue(new NativeVibrationBank(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(resources.Vibration.Data.AsSpan())), [BinaryPrimitives.ReadInt32LittleEndian(resources.Vibration.Data.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(resources.Vibration.Data.AsSpan().Slice(4))], resources.Vibration.Data), vibration.Kind, vibration.Strength);
                NativeVibrationProcess = queued.State;
                return NativeBattleEffect308Controller.WriteBytes(frame, vibration.DestinationAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
            case NativeBattleEffect308VibrationStop4A2940 vibration:
                NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect308 requires its vibration process.")).Stop4A2940(vibration.Handle);
                return frame;
            case NativeBattleEffect308Sound501330 sound:
                PublishNativeEffect308Frame(frame);
                PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                return ImportNativeEffect308Frame(frame);
            case NativeBattleEffect308InitializeIo534110 initialize:
                return frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, initialize.ArenaOffset) };
            case NativeBattleEffect308QueueFile5341D0 queue:
                return frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, queue.FileId, queue.DestinationAddress, queue.Type) };
            case NativeBattleEffect308AdvanceIo534210:
                NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(frame.Memory);
                frame = frame with { Memory = advance.Memory };
                return advance.Request is { } request ? QueueNativeEffect308File(frame, resources, request) : frame;
            case NativeBattleEffect308DrawOffsetBody669070 body:
                return DrawNativeEffect308Body(ref graphics, frame, body, inputs);
            case NativeBattleEffect308SelectFragments667DB0 selection:
                return SelectNativeEffect308Fragments(ref graphics, frame, selection, resources, inputs);
            case NativeBattleEffect308ApplyOffsetPose662C00 pose:
                int poseActor = NativeEffect308Actor(graphics, pose.ModelAddress);
                NativeBattlePoseSource poseSource = NativeGraphicsPoseResource(graphics, poseActor).PoseSource ?? throw new NotSupportedException("Effect308 requires its current offset rig.");
                NativeBattleEffectArena offsets = NativeBattleEffect308Controller.ReadBytes(frame, pose.OffsetRowsAddress, checked(poseSource.BoneCount * 16));
                return PrepareNativeEffect308Pose(ref graphics, frame, poseActor, poseSource, offsets);
            case NativeBattleEffect308RestorePose508C90 restore:
                int restoreActor = NativeEffect308Actor(graphics, restore.ModelAddress);
                NativeBattlePoseSource restoreSource = NativeGraphicsPoseResource(graphics, restoreActor).PoseSource ?? throw new NotSupportedException("Effect308 requires its current restored rig.");
                return RestoreNativeEffect308Pose(ref graphics, frame, restoreActor, restoreSource);
            case NativeBattleEffect308Intensity5713E0 intensity:
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect308 requires its current stage groups.");
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity.Strength), ColorFlags = unchecked((uint)intensity.Kind) }).ToImmutableArray() };
                return frame;
            case NativeBattleEffect308White7040B0 white:
                return DeliverNativeEffect308Batch(ref graphics, frame, NativeBattleRinoa89FadeDrawing.Draw7040B0(255, 255, 255, white.Amount), CursorGlobal);
            case NativeBattleEffect308DrawBatch draw:
                return DeliverNativeEffect308Batch(ref graphics, frame, draw.Batch, draw.CursorGlobal, draw.Flags);
            default:
                throw new NotSupportedException($"Effect308 emitted unsupported service {operation.GetType().Name}.");
        }
    }

    private NativeBattleEffect308Frame DeliverNativeEffect308Batch(ref NativeBattleGraphicsState graphics, NativeBattleEffect308Frame frame, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags = 0)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect308 requires its current ordering table.");
        if (cursorGlobal == 0x1D8E054)
        {
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect308 requires its current common packet process.");
            int cursor = process.ByteCursor ?? throw new NotSupportedException("Effect308 requires its current common packet cursor.");
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
            throw new NotSupportedException("Effect308 packet delivery requires its actual cursor owner.");
        }
        int privateCursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect308 requires its current packet arena.");
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
