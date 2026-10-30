using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect76(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect76 requires its selected original effect.");
        NativeBattleEffect76Resources resources = effect.Binding.Effect76Resources ?? throw new NotSupportedException("Effect76 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect76 requires its current process memory.");
        NativeBattleEffect76Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect76 requires its current CRT stream."), memory.Effect76 ?? throw new NotSupportedException("Effect76 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect76 requires its current addressed memory.") };
        NativeBattleEffect76Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect76 requires its arithmetic constants."), _ => graphics.Flags, current => new(current, (NativeEffectIoResult ?? throw new NotSupportedException("Effect76 requires its current signed IO result.")) < 0 ? -1 : unchecked((int)graphics.Flags)), _ => nativeActionPackets, (_, actor) => graphics.Models[actor], PoseSource, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect76 requires its current projection."), (_, actor) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Effect76 requires its current world matrix."), null), Position, ReadBone, _ => (Camera().CurrentPose ?? throw new NotSupportedException("Effect76 requires its current camera pose.")) with { FocalLength = Camera().FocalLength, Roll = Camera().Roll }, _ => Camera().EyePaddingF6, _ => Camera().TargetPaddingFE, (_, handle) => (NativeVibrationProcess ?? throw new NotSupportedException("Effect76 requires its current vibration process.")).IsPlaying(handle), (_, address) => address switch
        {
            0xB8B7D8 => Stage().ModelColors?.Color40 ?? throw new NotSupportedException("Effect76 requires its current model color."),
            0xB8B9A8 => Stage().ModelColors?.Color44 ?? throw new NotSupportedException("Effect76 requires its current model color."),
            _ => throw new NotSupportedException("Effect76 selected an unsupported current color address.")
        }, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect76Controller.Initialize(frame, resources, inputs) : NativeBattleEffect76Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect76 requires its current camera.");
        NativeBattleStageState Stage() => NativeStage ?? throw new NotSupportedException("Effect76 requires its current stage.");
        void Publish(NativeBattleEffect76Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect76 = current.State };
            NativeFileStaging = current.Staging;
            nativeCrtRandom = current.Random;
        }
        NativeBattleEffect76Frame Import(NativeBattleEffect76Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect76 service lost its current memory.");
            return current with { Memory = latest, State = latest.Effect76 ?? throw new InvalidDataException("Effect76 service lost its storage."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new InvalidDataException("Effect76 service lost its CRT stream.") };
        }
        NativeBattlePoseSource PoseSource(NativeBattleEffect76Frame current, int actor)
        {
            NativeFileStaging = current.Staging;
            return NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect76 requires its current physical skeleton.");
        }
        NativeBattleEffectArena Position(NativeBattleEffect76Frame current, int actor)
        {
            NativeBattleModelTransform transform = graphics.Models[actor].Transform ?? throw new NotSupportedException("Effect76 requires its current physical model point.");
            return NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Vertical34));
        }
        NativeBattleEffect76PointStep ReadBone(NativeBattleEffect76Frame current, int actor, byte selector, short distance, NativeBattleEffectArena retained)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(current, actor), selector, distance);
            NativeFileStaging = current.Staging;
            current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer), Staging = NativeFileStaging };
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }
        NativeBattleEffect76Frame Execute(NativeBattleEffect76Frame current, NativeBattleEffect76Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect76UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect76Controller.ReadBytes(current, upload.SourceAddress, TextureLength).Read(0, TextureLength).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect76 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect76SetMode4A8480 mode:
                    NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Effect76 requires its current UI flags.");
                    graphics = graphics with { UiServices = mode.Mode == 0 ? ui.SetFlags(0x200, 0x100) : ui.SetFlags(0x100, 0x200) };
                    return current;
                case NativeBattleEffect76ModelFlags flags:
                    NativeBattleGraphicsModel model = graphics.Models[flags.Actor];
                    ushort mask = flags.WordWidth ? flags.AndMask : (ushort)(0xFF00 | (byte)flags.AndMask);
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, model with { Flags0 = (ushort)((model.Flags0 & mask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect76ModelPointY pointY:
                    NativeBattleGraphicsModel positioned = graphics.Models[pointY.Actor];
                    NativeBattleModelTransform transform = positioned.Transform ?? throw new NotSupportedException("Effect76 requires its current model point.");
                    short y = pointY.Add ? unchecked((short)(transform.Position28.Y + pointY.Value)) : pointY.Value;
                    graphics = graphics with { Models = graphics.Models.SetItem(pointY.Actor, positioned with { Transform = transform with { Position28 = transform.Position28 with { Y = y } } }) };
                    return current;
                case NativeBattleEffect76Stage5713E0 intensity:
                    NativeBattleStageState stage = Stage();
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity.Intensity), ColorFlags = unchecked((uint)intensity.Color) }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect76MusicLoad501A20 music:
                    NativeMusicProcess = (NativeMusicProcess ?? throw new NotSupportedException("Effect76 requires its current music process.")).StageLoad501A20(music.SourceAddress, resources.ReadStatic);
                    NativeBattleAuxiliaryState musicAuxiliary = graphics.Auxiliary.AppendMusicLoad501A20(music.CompletionAddress, (address, value) => current = NativeBattleEffect76Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = musicAuxiliary };
                    return current;
                case NativeBattleEffect76MusicVolume501B10 gain:
                    NativeMusicProcess = (NativeMusicProcess ?? throw new NotSupportedException("Effect76 requires its current music commands.")).QueueGain501B10(unchecked((int)gain.Duration), gain.Gain);
                    return current;
                case NativeBattleEffect76QueueStage512AA0 queue:
                    Publish(current);
                    QueueNativeStage512AA0(queue.Index, queue.DestinationAddress);
                    return Import(current);
                case NativeBattleEffect76ReloadStage512AC0 reload:
                    Publish(current);
                    ReloadNativeStage512AC0(reload.Index, reload.SourceAddress);
                    return Import(current);
                case NativeBattleEffect76SyncModelColors61FC60 color:
                    NativeBattleStageState colorStage = Stage();
                    NativeStage = colorStage with { ModelColors = (colorStage.ModelColors ?? throw new NotSupportedException("Effect76 requires its current model color owner.")) with { Color44 = color.Color } };
                    graphics = graphics with { Models = graphics.Models.Select(item => (item.Flags0 & 2) != 0 ? item with { Color44 = color.Color } : item).ToImmutableArray() };
                    return current;
                case NativeBattleEffect76WaveRequest46CBB0 wave:
                    NativeBattleFileAudioRequest request = NativeBattleFileAudioRequest.Create46CBB0(NativeGlobalSoundProcess ?? throw new NotSupportedException("Effect76 requires its current sound process."), wave.Channel, wave.ResourceName, wave.Gain);
                    events.Add(new(Frame, "NativeFileAudio", null, null, 0, string.Empty) { NativeFileAudio = request });
                    return current;
                case NativeBattleEffect76Sequence505C00 sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)sequence.Actor), sequence.Sequence);
                    return current;
                case NativeBattleEffect76Text4876F0 text:
                    BattleCombatant actor = GraphicsActor(graphics, text.Actor);
                    string message = (FindNativeOriginalGraphicsBinding(actor.Id) ?? throw new NotSupportedException("Effect76 requires its original actor text resource.")).Resource.ReadText91(text.Index);
                    StopTurnAdvance();
                    graphics = QueueNativeDescriptorMessage(graphics, new(Frame, "NativeText", actor.Id, null, text.Duration, message) { NativeMessageMode = 3 }, 0, 240);
                    return current;
                case NativeBattleEffect76Sound501330 sound:
                    if (sound.SourceAddress != SoundAddress)
                    {
                        throw new NotSupportedException("Effect76 selected an unsupported sound descriptor.");
                    }
                    Publish(current);
                    PublishNativeModuleSound(new(resources.Sound, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect76Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect76Vibration4A29A0 vibration when vibration.SourceAddress == VibrationAddress:
                    ImmutableArray<byte> vibrationBytes = resources.Vibration.Data;
                    NativeVibrationBank bank = new(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(vibrationBytes.AsSpan())), [BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan().Slice(4))], vibrationBytes);
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect76 requires its current vibration process.")).Enqueue(bank, vibration.Mode, vibration.Magnitude);
                    NativeVibrationProcess = queued.State;
                    return NativeBattleEffect76Controller.WriteBytes(current, vibration.OutputAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
                case NativeBattleEffect76StopVibration4A2940 stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect76 requires its current vibration process.")).Stop4A2940(stop.Handle);
                    return current;
                case NativeBattleEffect76ForceCamera50A730:
                    NativeCamera = Camera().Apply50A730();
                    return current;
                case NativeBattleEffect76CaptureCameraRetirement:
                    NativeBattleCameraState camera = Camera();
                    NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Effect76 requires its current camera pose.");
                    NativeBattleCameraPose captured = camera.CapturedPose ?? throw new NotSupportedException("Effect76 requires its retained captured camera roll.");
                    NativeCamera = camera with { CapturedPose = captured with { Eye = pose.Eye, Target = pose.Target, FocalLength = camera.FocalLength ?? throw new NotSupportedException("Effect76 requires its current camera focal length.") }, CapturedEyePadding806 = camera.EyePaddingF6, CapturedTargetPadding80E = camera.TargetPaddingFE };
                    return current;
                case NativeBattleEffect76PublishCameraPose published:
                    NativeCamera = Camera().WriteEffectGlobals(published.Pose) with { EyePaddingF6 = published.EyePadding, TargetPaddingFE = published.TargetPadding };
                    return current;
                case NativeBattleEffect76GraphicsFlags graphicsFlags:
                    graphics = graphics with { Flags = (graphics.Flags & graphicsFlags.AndMask) | graphicsFlags.OrMask };
                    return current;
                case NativeBattleEffect76PcRender47CF50 render:
                    return current with { Memory = current.Memory with { PcRenderRequest = (current.Memory.PcRenderRequest ?? throw new NotSupportedException("Effect76 requires its current PC render request.")).Request47CF50(render.Mode, render.Red, render.Green, render.Blue, render.Depth) } };
                case NativeBattleEffect76Flash5712C0 flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(checked((short)flash.Rise), checked((short)flash.Hold), checked((short)flash.Fall), flash.Color) };
                    return current;
                case NativeBattleEffect76CommonTextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => current = NativeBattleEffect76Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect76DrawBatch draw:
                    draw = draw with { Batch = NativeBattleEffect76Presentation.FinalizeCapturedBatch(draw.Batch) };
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect76 requires its current ordering table.");
                    if (draw.CursorGlobal == 0x1D8E054)
                    {
                        NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect76 requires its ordinary packet process."), primitive.Pending, draw.Batch, draw.Flags);
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                        return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
                    }
                    if (draw.CursorGlobal != CursorGlobal)
                    {
                        throw new NotSupportedException("Effect76 selected an unsupported packet cursor.");
                    }
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect76 requires its current COMMON arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect76 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }
}
