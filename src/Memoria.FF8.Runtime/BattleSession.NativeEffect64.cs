using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect64(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect64 requires its selected effect.");
        NativeBattleEffect64Resources resources = effect.Binding.Effect64Resources ?? throw new NotSupportedException("Effect64 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect64 requires its current process memory.");
        NativeBattleEffect64Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect64 requires its current CRT stream."), memory.Effect64 ?? throw new NotSupportedException("Effect64 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect64 requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect64 requires its arithmetic constants.");
        NativeBattleEffect64Inputs inputs = new(colors, _ => Camera().CapturePackedView1D97778(), _ => Projection(), _ => Camera().Roll, (_, pointer) => graphics.Models[ActorNativeEffect64(pointer, graphics)], ReadBone, Execute);
        frame = initialize ? NativeBattleEffect64Controller.Initialize(frame, resources, inputs) : NativeBattleEffect64Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect64 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect64 requires its current projection.");
        void Publish(NativeBattleEffect64Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect64 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect64 lost its temporary memory.");
            nativeCrtRandom = current.Random;
        }
        NativeBattleEffect64Frame Import(NativeBattleEffect64Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect64 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect64 ?? throw new InvalidDataException("Effect64 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect64 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect64 service lost its temporary memory.") };
        }
        NativeBattleEffect64PointStep ReadBone(NativeBattleEffect64Frame current, uint pointer, int selector, int distance, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[ActorNativeEffect64(pointer, graphics)];
            NativeBattlePoseSource pose = PoseNativeEffect64(graphics, current, pointer);
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, pose, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging!;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            current = current with { Memory = observed, Staging = NativeFileStaging };
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }
        NativeBattleEffect64Frame Deliver(NativeBattleEffect64Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect64 requires its current ordering table.");
            if (cursorGlobal == 0x1D8E054)
            {
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect64 requires its current ordinary packet process."), primitive.Pending, batch, flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            if (cursorGlobal != CursorGlobal)
            {
                throw new InvalidDataException("Effect64 selected an unsupported packet cursor.");
            }
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(cursorGlobal), 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect64 requires its current COMMON arena.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, cursor, NativeBattleEffectArena.Length, offset =>
            {
                (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                return (bytes[0], known[0]);
            });
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(0x20DFAB8u + (uint)written.Cursor)) };
        }
        NativeBattleEffect64Frame Execute(NativeBattleEffect64Frame current, NativeBattleEffect64Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect64UploadTim505E30 upload:
                    if (upload.SourceAddress != current.State.ReadDword(InitializerGlobal))
                    {
                        throw new InvalidDataException("Effect64 selected an unsupported current TIM.");
                    }
                    NativeBattleEffectHeapAllocation tim = new("effect64-current-tim", NativeBattleEffect64Controller.ReadBytes(current, upload.SourceAddress, TextureLength).Read(0, TextureLength).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect64 requires its texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect64RegisterCamera camera:
                    NativeBattleEffectTextureState registered = (current.Memory.Textures ?? throw new NotSupportedException("Effect64 requires its texture scheduler.")).Register5099A0(camera.Camera);
                    NativeCamera = Camera().RegisterControlResource(registered.Animation);
                    return current with { Memory = current.Memory with { Textures = registered } };
                case NativeBattleEffect64ModelFlags flags:
                    int flagActor = ActorNativeEffect64(flags.ModelAddress, graphics);
                    NativeBattleGraphicsModel flagged = graphics.Models[flagActor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flagActor, flagged with { Flags0 = (ushort)((flagged.Flags0 & flags.AndMask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect64ModelMeshMask mask:
                    int maskActor = ActorNativeEffect64(mask.ModelAddress, graphics);
                    NativeBattleGraphicsModel masked = graphics.Models[maskActor];
                    graphics = graphics with { Models = graphics.Models.SetItem(maskActor, masked with { SequenceState = (masked.SequenceState ?? throw new NotSupportedException("Effect64 requires its current model mask.")) with { MeshMask124 = mask.Mask } }) };
                    return current;
                case NativeBattleEffect64Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect64Vibration4A29A0 vibration:
                    if (vibration.SourceAddress != VibrationAddress)
                    {
                        throw new InvalidDataException("Effect64 selected an unsupported vibration source.");
                    }
                    ImmutableArray<byte> vibrationBytes = resources.Vibration.Data;
                    NativeVibrationBank bank = new(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(vibrationBytes.AsSpan())), [BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan().Slice(4))], vibrationBytes);
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect64 requires its current vibration process.")).Enqueue(bank, vibration.Mode, vibration.Magnitude);
                    NativeVibrationProcess = queued.State;
                    return NativeBattleEffect64Controller.WriteBytes(current, vibration.OutputAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
                case NativeBattleEffect64StopVibration4A2940 stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect64 requires its current vibration process.")).Stop4A2940(stop.Handle);
                    return current;
                case NativeBattleEffect64ApplyOffsetPose662C00 pose:
                    (NativeBattleGraphicsState poseGraphics, NativeBattleEffect64Frame poseFrame) = ApplyNativeEffect64Pose(graphics, current, pose.ModelAddress, pose.OffsetRowsAddress);
                    graphics = graphics with { Models = poseGraphics.Models };
                    return poseFrame;
                case NativeBattleEffect64RestorePose508C90 restore:
                    (NativeBattleGraphicsState restoredGraphics, NativeBattleEffect64Frame restoredFrame) = RestoreNativeEffect64Pose(graphics, current, restore.ModelAddress);
                    graphics = graphics with { Models = restoredGraphics.Models };
                    return restoredFrame;
                case NativeBattleEffect64ClearGeometryBit15_6E5C90 cleanup:
                    return ClearNativeEffect64Geometry(graphics, current, cleanup.ModelAddress);
                case NativeBattleEffect64DrawBody6E56A0 body:
                    (NativeBattleGraphicsState bodyGraphics, NativeBattleEffect64Frame bodyFrame) = DrawNativeEffect64Body(graphics, current, body.ModelAddress, body.OffsetRowsAddress, Deliver);
                    graphics = graphics with { Models = bodyGraphics.Models };
                    return bodyFrame;
                case NativeBattleEffect64DeliverBatch packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                default:
                    throw new NotSupportedException("Effect64 emitted an unsupported native operation.");
            }
        }
    }
}
