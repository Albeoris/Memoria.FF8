using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect65(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect65 requires its selected effect.");
        NativeBattleEffect65Resources resources = effect.Binding.Effect65Resources ?? throw new NotSupportedException("Effect65 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect65 requires its current process memory.");
        NativeBattleEffect65Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect65 requires its current CRT stream."), memory.Effect65 ?? throw new NotSupportedException("Effect65 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect65 requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect65 requires its arithmetic constants.");
        NativeBattleEffect65Inputs inputs = new(colors, _ => nativeActionPackets, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect65 requires its current projection."), _ => Camera().Roll, (_, pointer) => graphics.Models[ActorNativeEffect65(pointer, graphics)], (_, pointer) => Position8(pointer), Anchor, WidePoint, WordPoint, PollIo, SelectIo, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect65Controller.Initialize(frame, resources, inputs) : NativeBattleEffect65Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect65 requires its current camera.");

        NativeBattleEffectArena Position8(uint pointer)
        {
            return NativeBattleEffectNodeContextMemory.Copy(NativeBattleRaldo19ModelMemory.Capture(graphics.Models[ActorNativeEffect65(pointer, graphics)]), 0x1C, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
        }

        void Publish(NativeBattleEffect65Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect65 = current.State };
            NativeFileStaging = current.Staging ?? throw new InvalidDataException("Effect65 lost its temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect65Frame Import(NativeBattleEffect65Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect65 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect65 ?? throw new InvalidDataException("Effect65 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect65 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect65 service lost its temporary memory.") };
        }

        NativeBattleEffect65PointOutputStep WidePoint(NativeBattleEffect65Frame current, NativeBattleEffectArena source, NativeBattleEffectArena output) => Point(current, source, output, true);
        NativeBattleEffect65PointOutputStep WordPoint(NativeBattleEffect65Frame current, NativeBattleEffectArena source, NativeBattleEffectArena output) => Point(current, source, output, false);

        NativeBattleEffect65PointOutputStep Point(NativeBattleEffect65Frame current, NativeBattleEffectArena source, NativeBattleEffectArena output, bool wide)
        {
            (NativeBattleGraphicsState pointGraphics, NativeBattleEffect65PointOutputStep step) = ReadNativeEffect65CasterPoint(graphics, current, source, output, wide);
            graphics = graphics with { Models = pointGraphics.Models };
            return step;
        }

        NativeBattleEffect65PointStep Anchor(NativeBattleEffect65Frame current, uint pointer)
        {
            int actor = ActorNativeEffect65(pointer, graphics);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource pose = PoseNativeEffect65(graphics, current, pointer);
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, pose, selector, 0));
                    current = current with { Memory = observed, Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            NativeBattleEffectArena point = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(anchor.X)).Write(2, BitConverter.GetBytes(anchor.Y)).Write(4, BitConverter.GetBytes(anchor.Z)).Write(6, BitConverter.GetBytes(anchor.W));
            return new(current, point);
        }

        NativeBattleEffect65Frame Deliver(NativeBattleEffect65Frame current, NativeBattleEffect65DeliverBatch packets)
        {
            packets = packets with { Batch = NativeBattleEffect65Presentation.FinalizeCapturedBatch(packets.Batch) };
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect65 requires its current ordering table.");
            if (packets.CursorGlobal == 0x1D8E054)
            {
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect65 requires its current ordinary packet process."), primitive.Pending, packets.Batch, packets.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            if (packets.CursorGlobal != CursorGlobal)
            {
                throw new InvalidDataException("Effect65 selected an unsupported packet cursor.");
            }
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(CursorGlobal), 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect65 requires its current COMMON arena.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(packets.Batch, cursor, NativeBattleEffectArena.Length, offset =>
            {
                (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                return (bytes[0], known[0]);
            });
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, packets.Flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(CursorGlobal, checked(ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEffect65Frame QueueFile(NativeBattleEffect65Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.ReadFile(request.FileId);
            int offset = NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length);
            current = current with { Memory = current.Memory with { Arena = (current.Memory.Arena ?? throw new NotSupportedException("Effect65 requires its current file destination.")).Write(offset, file.Data.AsSpan()) } };
            NativeBattlePilotVisualLifetimes.Allocated(current.Memory.Arena!, request.DestinationAddress, -1);
            QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
            return current;
        }

        NativeBattleEffect65IoStep PollIo(NativeBattleEffect65Frame current)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(current.Memory);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueFile(current, request) : current, poll.Busy);
        }

        NativeBattleEffect65IoCompletionStep SelectIo(NativeBattleEffect65Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueFile(current, request) : current, !poll.Busy);
        }

        NativeBattleEffect65Frame StorePosition(NativeBattleEffect65Frame current, uint pointer, NativeBattleEffectArena point, int length, bool world)
        {
            int actor = ActorNativeEffect65(pointer, graphics);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect65 requires its current actor transform.");
            NativeBattleVector position = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
            short padding = transform.Vertical34;
            if (length == 8)
            {
                padding = BinaryPrimitives.ReadInt16LittleEndian(point.Read(6, 2));
            }
            model = model with { Transform = transform with { Position28 = position, Vertical34 = padding } };
            if (world)
            {
                NativeBattleActionMatrix matrix = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect65 requires its current actor world matrix.");
                model = model with { GeometryWorldMatrix = matrix with { X = position.X, Y = position.Y, Z = position.Z } };
            }
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            return current;
        }

        NativeBattleEffect65Frame Execute(NativeBattleEffect65Frame current, NativeBattleEffect65Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect65UploadTim505E30 upload:
                    if (upload.SourceAddress != current.State.ReadDword(InitializerGlobal))
                    {
                        throw new InvalidDataException("Effect65 selected an unsupported current TIM.");
                    }
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect65Controller.ReadBytes(current, upload.SourceAddress, TextureLength).Read(0, TextureLength).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect65 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect65Vibration4A29A0 vibration:
                    if (vibration.SourceAddress != VibrationAddress)
                    {
                        throw new InvalidDataException("Effect65 selected an unsupported vibration source.");
                    }
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect65 requires its vibration process.")).Enqueue(resources.Vibration, vibration.Mode, vibration.Magnitude);
                    NativeVibrationProcess = queued.State;
                    return NativeBattleEffect65Controller.WriteBytes(current, vibration.OutputAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
                case NativeBattleEffect65StopVibration4A2940 stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect65 requires its vibration process.")).Stop4A2940(stop.Handle);
                    return current;
                case NativeBattleEffect65Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect65ModelFlags flags:
                    int flagActor = ActorNativeEffect65(flags.ModelAddress, graphics);
                    NativeBattleGraphicsModel flagged = graphics.Models[flagActor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flagActor, flagged with { Flags0 = (ushort)((flagged.Flags0 & flags.AndMask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect65WriteActorPoint8 packed:
                    return StorePosition(current, packed.ModelAddress, packed.Point8, 8, packed.PublishWorldXYZ);
                case NativeBattleEffect65WriteActorPositionXYZ position:
                    NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(position.Position.X)).Write(2, BitConverter.GetBytes(position.Position.Y)).Write(4, BitConverter.GetBytes(position.Position.Z));
                    return StorePosition(current, position.ModelAddress, xyz, 6, position.PublishWorldXYZ);
                case NativeBattleEffect65SetActorScalePointer30 scale:
                    int scaleActor = ActorNativeEffect65(scale.ModelAddress, graphics);
                    graphics = graphics with { Models = graphics.Models.SetItem(scaleActor, graphics.Models[scaleActor] with { ScaleVectorAddress48 = scale.Address }) };
                    return current;
                case NativeBattleEffect65SetSequence505C00 sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)ActorNativeEffect65(sequence.ModelAddress, graphics)), sequence.Sequence);
                    return current;
                case NativeBattleEffect65StageFlags stageFlags:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect65 requires its current stage rows.");
                    ImmutableArray<NativeBattleStageGroup> groups = stage.Groups;
                    IEnumerable<int> indices = stageFlags.Group is { } index ? [index] : Enumerable.Range(0, 4);
                    foreach (int groupIndex in indices)
                    {
                        NativeBattleStageGroup group = groups[groupIndex];
                        groups = groups.SetItem(groupIndex, group with { Flags = (byte)((group.Flags & stageFlags.AndMask) | stageFlags.OrMask) });
                    }
                    NativeStage = stage with { Groups = groups };
                    return current;
                case NativeBattleEffect65CameraForce50A730:
                    NativeCamera = Camera().Apply50A730();
                    return current;
                case NativeBattleEffect65CameraPoseXYZ pose:
                    NativeCamera = Camera().WriteEffectGlobals(new(pose.Eye, pose.Target, Camera().FocalLength, Camera().Roll));
                    return current;
                case NativeBattleEffect65CameraRoll roll:
                    NativeCamera = Camera().WriteEffectGlobals((Camera().CurrentPose ?? throw new NotSupportedException("Effect65 requires its current camera pose.")) with { FocalLength = Camera().FocalLength, Roll = roll.Roll });
                    return current;
                case NativeBattleEffect65CameraRestoreCaptured503300:
                    NativeCamera = NativeBattleEffect65CameraRestore.Restore(Camera());
                    return current;
                case NativeBattleEffect65PcRenderRequest47CF50 render:
                    return current with { Memory = current.Memory with { PcRenderRequest = (current.Memory.PcRenderRequest ?? throw new NotSupportedException("Effect65 requires its retained PC render request.")).Request47CF50(render.Mode, render.Red, render.Green, render.Blue, render.Alpha) } };
                case NativeBattleEffect65Contacts506BA0 contacts:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = nativeActionPackets.ReadHeaderContacts506BA0(contacts.HeaderIndex) });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect65IoInitialize534110 io:
                    return current with { Memory = NativeBattleEffectIo.Initialize534110(current.Memory, NativeBattleWaterExecutionFields.ArenaOffset(io.ControlAddress, 0x48)) };
                case NativeBattleEffect65IoQueue5341D0 file:
                    return current with { Memory = NativeBattleEffectIo.Queue5341D0(current.Memory, file.FileId, file.DestinationAddress, file.Type) };
                case NativeBattleEffect65IoAdvance534210:
                    NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(current.Memory);
                    current = current with { Memory = advance.Memory };
                    return advance.Request is { } request ? QueueFile(current, request) : current;
                case NativeBattleEffect65DeliverBatch packets:
                    return Deliver(current, packets);
                default:
                    throw new NotSupportedException("Effect65 emitted an unsupported native operation.");
            }
        }
    }
}
