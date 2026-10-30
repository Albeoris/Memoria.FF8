using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect66(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect66 requires its selected original effect.");
        effect = effect with { PresentationPackets = [] };
        NativeBattleEffect66Resources resources = effect.Binding.Effect66Resources ?? throw new NotSupportedException("Effect66 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect66 requires its current process memory.");
        NativeBattleEffect66Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect66 requires its current CRT stream."), memory.Effect66 ?? throw new NotSupportedException("Effect66 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect66 requires its current temporary memory.") };
        NativeBattleEffect66Inputs inputs = null!;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect66 requires its arithmetic constants."), _ => nativeActionPackets, _ => graphics.Flags, current => new(current, (NativeEffectIoResult ?? throw new NotSupportedException("Effect66 requires its signed current IO result.")) < 0 ? -1 : unchecked((int)graphics.Flags)), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect66 requires its current projection."), _ => Camera().Roll, _ => (Camera().CurrentPose ?? throw new NotSupportedException("Effect66 requires its current camera pose.")) with { FocalLength = Camera().FocalLength, Roll = Camera().Roll }, (_, actor) => graphics.Models[actor], PoseSource, (_, actor) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Effect66 requires its current world matrix."), null), ReadBone, (_, handle) => (NativeVibrationProcess ?? throw new NotSupportedException("Effect66 requires its vibration process.")).IsPlaying(handle), Execute);
        frame = initialize ? NativeBattleEffect66Controller.Initialize(frame, resources, inputs) : NativeBattleEffect66Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect66 requires its current camera.");
        void Publish(NativeBattleEffect66Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect66 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect66 lost its temporary memory.");
            nativeCrtRandom = current.Random;
        }
        NativeBattleEffect66Frame Import(NativeBattleEffect66Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect66 service lost its current memory.");
            return current with { Memory = latest, State = latest.Effect66 ?? throw new InvalidDataException("Effect66 service lost its storage."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new InvalidDataException("Effect66 service lost its CRT stream.") };
        }
        NativeBattlePoseSource PoseSource(NativeBattleEffect66Frame current, int actor)
        {
            NativeFileStaging = current.Staging;
            return NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect66 requires its current physical skeleton.");
        }
        NativeBattleEffect66PointStep ReadBone(NativeBattleEffect66Frame current, int actor, int bone, int distance, NativeBattleEffectArena retained)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(current, actor), checked((byte)bone), checked((short)distance));
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            current = current with { Memory = observed, Staging = NativeFileStaging };
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }
        NativeBattleEffect66Frame Execute(NativeBattleEffect66Frame current, NativeBattleEffect66Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect66ForceCamera50A730:
                    NativeCamera = Camera().Apply50A730();
                    return current;
                case NativeBattleEffect66ModelMask7C mask:
                    NativeBattleGraphicsModel model = graphics.Models[mask.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(mask.Actor, model with { SequenceState = (model.SequenceState ?? throw new NotSupportedException("Effect66 requires its current object mask.")) with { MeshMask124 = mask.Mask } }) };
                    return current;
                case NativeBattleEffect66ModelFlags flags:
                    NativeBattleGraphicsModel flagged = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, flagged with { Flags0 = (ushort)((flagged.Flags0 & flags.AndMask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect66StageGroupFlags flags:
                    NativeBattleStageState stageOwner = NativeStage ?? throw new NotSupportedException("Effect66 requires its current stage groups.");
                    NativeBattleStageGroup stageGroup = stageOwner.Groups[1];
                    NativeStage = stageOwner with { Groups = stageOwner.Groups.SetItem(1, stageGroup with { Flags = (byte)((stageGroup.Flags & flags.AndMask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect66SetMode4A8480 mode:
                    NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Effect66 requires its current UI flags.");
                    graphics = graphics with { UiServices = mode.Mode == 0 ? ui.SetFlags(0x200, 0x100) : ui.SetFlags(0x100, 0x200) };
                    return current;
                case NativeBattleEffect66Flags flags:
                    graphics = graphics with { Flags = (graphics.Flags & flags.AndMask) | flags.OrMask };
                    return current;
                case NativeBattleEffect66Sequence505C00 sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)sequence.Actor), sequence.Sequence);
                    return current;
                case NativeBattleEffect66Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect66Vibration4A29A0 vibration when vibration.SourceAddress == VibrationAddress:
                    ImmutableArray<byte> vibrationBytes = resources.Vibration.Data;
                    NativeVibrationBank vibrationBank = new(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(vibrationBytes.AsSpan())), [BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan().Slice(4))], vibrationBytes);
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect66 requires its current vibration process.")).Enqueue(vibrationBank, vibration.Mode, vibration.Magnitude);
                    NativeVibrationProcess = queued.State;
                    return NativeBattleEffect66Controller.WriteBytes(current, vibration.OutputAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
                case NativeBattleEffect66UploadTim505E30 upload:
                    int length = upload.SourceAddress == EmbeddedTimAddress ? EmbeddedTimLength : upload.SourceAddress == current.State.ReadDword(InitializerGlobal) ? TextureLength : throw new NotSupportedException("Effect66 selected an unsupported current TIM source.");
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", upload.SourceAddress == EmbeddedTimAddress ? resources.EmbeddedTim.Data : NativeBattleEffect66Controller.ReadBytes(current, upload.SourceAddress, length).Read(0, length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect66 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect66PublishCameraPose camera:
                    NativeCamera = Camera().WriteEffectGlobals(camera.Pose);
                    return current;
                case NativeBattleEffect66MusicFade46BBC0 fade:
                    NativeGlobalSoundProcess soundOwner = NativeGlobalSoundProcess ?? throw new NotSupportedException("Effect66 requires its current sound process.");
                    NativeGlobalSoundProcess = soundOwner with { MusicPlayback = (soundOwner.MusicPlayback ?? throw new NotSupportedException("Effect66 requires its current music playback.")).FadeGain46BBC0(fade.Channel, fade.Duration, unchecked((ushort)fade.Gain)) };
                    return current;
                case NativeBattleEffect66MusicStop46B800 stop:
                    NativeGlobalSoundProcess stopOwner = NativeGlobalSoundProcess ?? throw new NotSupportedException("Effect66 requires its current sound process.");
                    NativeGlobalSoundProcess = stopOwner with { MusicPlayback = (stopOwner.MusicPlayback ?? throw new NotSupportedException("Effect66 requires its current music playback.")).Stop46B800(stop.Channel) };
                    return current;
                case NativeBattleEffect66Text4876F0 text:
                    BattleCombatant actor = GraphicsActor(graphics, text.Actor);
                    string message = (FindNativeOriginalGraphicsBinding(actor.Id) ?? throw new NotSupportedException("Effect66 requires its original actor text resource.")).Resource.ReadText91(text.Text);
                    StopTurnAdvance();
                    graphics = QueueNativeDescriptorMessage(graphics, new(Frame, "NativeText", actor.Id, null, text.Duration, message) { NativeMessageMode = 3 }, 0, 240);
                    return current;
                case NativeBattleEffect66Flash5712C0 flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(checked((short)flash.Rise), checked((short)flash.Hold), checked((short)flash.Fall), checked((short)flash.Intensity)) };
                    return current;
                case NativeBattleEffect66Stage5713E0 stage:
                    NativeBattleStageState retainedStage = NativeStage ?? throw new NotSupportedException("Effect66 requires its current stage groups.");
                    NativeStage = retainedStage with { Groups = retainedStage.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Intensity), ColorFlags = unchecked((uint)stage.Selector) }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect66Rectangle571250 rectangle:
                    Publish(current);
                    NativeBattleModulePacketBatch rectangleBatch = NativeBattleFlashPackets.Emit571250(rectangle.Red, rectangle.Green, rectangle.Blue);
                    if (rectangle.PilotIdentity is { } rectangleIdentity)
                    {
                        rectangleBatch = rectangleBatch with { Primitives = rectangleBatch.Primitives.Select(item => item with { Primitive = item.Primitive with { PilotGeometry = item.Primitive.PilotGeometry! with { Identity = rectangleIdentity, SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } } }).ToImmutableArray() };
                    }
                    NativeBattlePrimitiveProcess rectangleProcess = current.Memory.PrimitiveProcess!;
                    current = current with { Effect = current.Effect with { PresentationPackets = current.Effect.PresentationPackets.AddRange(rectangleBatch.Primitives.Select(item => new NativeBattlePrimitiveReference(rectangleProcess.PacketBank!, rectangleProcess.PacketBuffer, checked(rectangleProcess.ByteCursor!.Value + item.ByteOffset)))) } };
                    graphics = AppendNativeModulePacketBatch(graphics, rectangleBatch, null, null);
                    return Import(current);
                case NativeBattleEffect66PcRender47CF50 render:
                    return current with { Memory = current.Memory with { PcRenderRequest = (current.Memory.PcRenderRequest ?? throw new NotSupportedException("Effect66 requires its current PC render request.")).Request47CF50(render.Mode, render.Red, render.Green, render.Blue, render.Alpha) } };
                case NativeBattleEffect66Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect66StopVibration4A2940 stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect66 requires its current vibration process.")).Stop4A2940(stop.Handle);
                    return current;
                case NativeBattleEffect66SoundGain46BEC0 gain:
                    NativeBattleSoundGainRequest request = NativeBattleSoundGainRequest.Create46BEC0(NativeGlobalSoundProcess ?? throw new NotSupportedException("Effect66 requires its current sound process."), gain.Duration, gain.Gain);
                    events.Add(new(Frame, "NativeSoundGain", null, null, 0, string.Empty) { NativeSoundGain = request });
                    return current;
                case NativeBattleEffect66CaptureCameraRetirement:
                    NativeCamera = NativeBattleEffect66CameraCapture.Capture(Camera());
                    return current;
                case NativeBattleEffect66StopSound46B450 stop when stop.Voice == -1 && stop.Mode == 0:
                    events.Add(new(Frame, "NativeSoundStopAll", null, null, 0, string.Empty) { NativeStopAllSounds = true });
                    return current;
                case NativeBattleEffect66DrawBody176 body:
                    (NativeBattleGraphicsState bodyGraphics, NativeBattleEffect66Frame bodyFrame) = DrawNativeEffect66Body(graphics, current, body.TemporaryAddress, resources, inputs, body.PilotIdentity);
                    graphics = graphics with { Models = bodyGraphics.Models };
                    return bodyFrame;
                case NativeBattleEffect66DrawStageBody44 stage:
                    (NativeBattleGraphicsState stageGraphics, NativeBattleEffect66Frame stageFrame) = DrawNativeEffect66Stage(graphics, current, stage.TemporaryAddress, stage.PilotIdentity);
                    graphics = stageGraphics;
                    return stageFrame;
                case NativeBattleEffect66DeliverBatch packets:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect66 requires its current ordering table.");
                    if (packets.CursorGlobal == 0x1D8E054)
                    {
                        NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect66 requires its current ordinary packet process.");
                        current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, packets.Batch) } };
                        NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(process, primitive.Pending, packets.Batch, packets.Flags);
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                        return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
                    }
                    if (packets.CursorGlobal != CursorGlobal)
                    {
                        throw new NotSupportedException("Effect66 selected an unsupported packet cursor.");
                    }
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect66 requires its current COMMON arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(packets.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, cursor, resolved) } };
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, packets.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect66 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }
}
