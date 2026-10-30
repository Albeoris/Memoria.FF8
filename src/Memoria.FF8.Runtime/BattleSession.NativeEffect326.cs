using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect326Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeEffect326(NativeBattleGraphicsState graphics) => RunNativeEffect326(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeEffect326(NativeBattleGraphicsState graphics) => RunNativeEffect326(graphics, false);

    private NativeBattleGraphicsState RunNativeEffect326(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect326 requires its selected original effect.");
        NativeBattleEffect326Resources resources = effect.Binding.Effect326Resources ?? throw new NotSupportedException("Effect326 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect326 requires its current process memory.");
        NativeBattleEffect326Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect326 requires its current CRT stream."), memory.Effect326 ?? throw new NotSupportedException("Effect326 requires its retained storage; explicitly migrate a needed older snapshot.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect326 requires its addressed memory.") };
        int commonStart = frame.Memory.PrimitiveProcess?.ByteCursor ?? throw new NotSupportedException("Effect326 requires its actual callback COMMON cursor.");
        frame = SynchronizeCursor(frame);
        NativeBattleEffect326Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect326 requires its arithmetic constants."), _ => nativeActionPackets, _ => graphics.Flags, current => new(current, NativeEffectIoResult ?? throw new NotSupportedException("Effect326 requires its current signed I/O result.")), _ => Camera().CapturePackedView1D97778(), _ => Projection(), _ => Camera().Roll, _ => (Camera().CurrentPose ?? throw new NotSupportedException("Effect326 requires its current camera pose.")) with { FocalLength = Camera().FocalLength, Roll = Camera().Roll }, _ => Camera().EyePaddingF6, _ => Camera().TargetPaddingFE, (_, actor) => graphics.Models[actor], (_, actor) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Effect326 requires its current actor world matrix."), null), Center, Point, (_, handle) => (NativeVibrationProcess ?? throw new NotSupportedException("Effect326 requires its current vibration process.")).IsPlaying(handle), Execute)
        {
            CapturePresentation = true,
            EntryLifecycle = graphics.EntryLifecycle,
            ReadAnchor571400 = Anchor,
            ShadowSource = definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect326 requires its original shadow source."),
            PrimitiveCapabilities = definition.NativeGraphics?.PrimitiveCapabilities ?? throw new NotSupportedException("Effect326 requires its actual primitive capabilities."),
            ReadColor = (_, address) => address == 0xB8B7D8 ? (NativeStage?.ModelColors ?? throw new NotSupportedException("Effect326 requires its current stage model colors.")).Color40 : throw new NotSupportedException("Effect326 selected an unsupported color owner.")
        };
        frame = initialize ? Initialize(frame, resources, inputs) : Advance(frame, resources, inputs);
        int commonEnd = frame.Memory.PrimitiveProcess?.ByteCursor ?? throw new NotSupportedException("Effect326 lost its actual callback COMMON cursor.");
        frame = frame with { State = frame.State with { CommonPacketStart = commonStart, CommonPacketEnd = commonEnd } };
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect326 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect326 requires its current projection.");

        void Publish(NativeBattleEffect326Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect326 = current.State };
            nativeCrtRandom = current.Random;
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect326 lost its addressed memory.");
        }

        NativeBattleEffect326Frame Import(NativeBattleEffect326Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect326 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect326 ?? throw new InvalidDataException("Effect326 service lost its retained storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect326 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect326 service lost its addressed memory.") };
        }

        NativeBattleEffect326Frame SynchronizeCursor(NativeBattleEffect326Frame current)
        {
            int cursor = current.Memory.PrimitiveProcess?.ByteCursor ?? throw new NotSupportedException("Effect326 requires its actual COMMON packet cursor.");
            return current with { Staging = (current.Staging ?? throw new NotSupportedException("Effect326 requires its actual addressed cursor owner.")).Write(0x1D8E054, BitConverter.GetBytes(checked(NativeEffect326CommonBase(current.Memory.PrimitiveProcess!) + (uint)cursor))) };
        }

        NativeBattleEffectArena Center(NativeBattleEffect326Frame current, ushort mask, NativeBattleEffectArena retained)
        {
            NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Effect326 requires its actual selected target positions.")).ToArray());
            return retained.Write(0, BitConverter.GetBytes(center.X)).Write(2, BitConverter.GetBytes(center.Y)).Write(4, BitConverter.GetBytes(center.Z));
        }

        NativeBattleEffect326PointStep Point(NativeBattleEffect326Frame current, int actor, int selector, int fraction, NativeBattleEffectArena retained)
        {
            NativeFileStaging = current.Staging;
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect326 requires its actual actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), checked((short)fraction));
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }

        NativeBattleEffect326PointStep Anchor(NativeBattleEffect326Frame current, int actor, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect326 requires its current actor transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect326 requires its current actor skeleton.");
            NativeBattleVector first = Observe(0xF0);
            NativeBattleVector second = Observe(0xF1);
            return new(current, retained.Write(0, BitConverter.GetBytes(unchecked((short)((first.X + second.X) / 2)))).Write(2, BitConverter.GetBytes(unchecked((short)((first.Y + second.Y) / 2)))).Write(4, BitConverter.GetBytes(unchecked((short)((first.Z + second.Z) / 2)))).Write(6, BitConverter.GetBytes(transform.Angle36)));

            NativeBattleVector Observe(byte selector)
            {
                NativeFileStaging = current.Staging;
                NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0));
                current = current with { Memory = observed, Staging = NativeFileStaging };
                return new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value));
            }
        }

        NativeBattleEffect326Frame Execute(NativeBattleEffect326Frame current, NativeBattleEffect326Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect326Queue508480 queue:
                    NativeBattleEffectFileAsset file = resources.File(queue.FileId);
                    current = WriteBytes(current, queue.DestinationAddress, NativeBattleEffectArena.Clear().Write(0, file.Data.AsSpan()), file.Data.Length);
                    NativeEffectIoResult = -1;
                    QueueNativeFileIo482610(file.Data.Length, 0x508470);
                    return current;
                case NativeBattleEffect326UploadTim505E30 upload:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Effect326 requires its current texture queue.");
                    textures = upload.SourceAddress == TextureAddress ? textures.EnqueueTim505E30(resources.Texture).State : textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Effect326 requires its actual loaded TIM bytes."), NativeBattleWaterExecutionFields.ArenaOffset(upload.SourceAddress, 1)).State;
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect326UploadPixels505DF0 upload:
                    byte[] rectangle = upload.RectangleAddress == 0xE196B8 ? resources.ReadStatic(upload.RectangleAddress, 8) : throw new NotSupportedException("Effect326 selected an unsupported pixel rectangle.");
                    NativeBattleEffectTextureRectangle target = new(BinaryPrimitives.ReadInt16LittleEndian(rectangle.AsSpan(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(rectangle.AsSpan(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(rectangle.AsSpan(4, 2)), BinaryPrimitives.ReadInt16LittleEndian(rectangle.AsSpan(6, 2)));
                    NativeBattleEffectHeapAllocation pixels = new("current-effect-pixels", ReadBytes(current, upload.SourceAddress, checked(target.Width * target.Height * 2)).Read(0, checked(target.Width * target.Height * 2)).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect326 requires its texture queue.")).EnqueueRectangle505DF0(target, pixels) } };
                case NativeBattleEffect326PublishStream501860 stream:
                    int number = BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(current, stream.SourceAddress, 4).Read(0, 4));
                    current = current with { Memory = current.Memory with { SummonStream = (current.Memory.SummonStream ?? throw new NotSupportedException("Effect326 requires its current summon stream owner.")).Publish501860(stream.SourceAddress, number) } };
                    return WriteBytes(current, stream.CompletionAddress, NativeBattleEffectArena.Clear().Write(0, [1]), 1);
                case NativeBattleEffect326PlayStream5018C0 stream:
                    Publish(current);
                    PublishNativeModuleSound((current.Memory.SummonStream ?? throw new NotSupportedException("Effect326 requires its published stream number.")).Play5018C0(stream.Pan, stream.Volume));
                    return Import(current);
                case NativeBattleEffect326Sound501330 sound:
                    int index = checked((int)(sound.SourceAddress - SoundAddress) / 4);
                    if (sound.SourceAddress < SoundAddress || sound.SourceAddress != SoundAddress + index * 4 || (uint)index >= (uint)resources.Sounds.Length)
                    {
                        throw new NotSupportedException("Effect326 selected an unsupported sound descriptor.");
                    }
                    Publish(current);
                    PublishNativeModuleSound(new(resources.Sounds[index], sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect326Vibration4A29A0 vibration when vibration.SourceAddress == VibrationAddress:
                    ImmutableArray<byte> bytes = resources.Vibration.Data;
                    NativeVibrationBank bank = new(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.AsSpan())), [BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(4))], bytes);
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect326 requires its actual vibration process.")).Enqueue(bank, vibration.Mode, vibration.Magnitude);
                    NativeVibrationProcess = queued.State;
                    return WriteBytes(current, vibration.OutputAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
                case NativeBattleEffect326StopVibration4A2940 stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect326 requires its current vibration process.")).Stop4A2940(stop.Handle);
                    return current;
                case NativeBattleEffect326Stage5713E0 intensity:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect326 requires its current stage.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity.Intensity), ColorFlags = unchecked((uint)intensity.Selector) }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect326StageGroupFlags flags:
                    NativeBattleStageState flagStage = NativeStage ?? throw new NotSupportedException("Effect326 requires its current stage.");
                    NativeStage = flagStage with { Groups = flagStage.Groups.Select(group => group with { Flags = (byte)((group.Flags & flags.AndMask) | flags.OrMask) }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect326ModelFlags flags:
                    NativeBattleGraphicsModel model = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, model with { Flags0 = (ushort)((model.Flags0 & flags.AndMask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect326ModelColor color:
                    graphics = graphics with { Models = graphics.Models.SetItem(color.Actor, graphics.Models[color.Actor] with { Color40 = color.Value }) };
                    return current;
                case NativeBattleEffect326PublishCameraPose camera:
                    NativeCamera = Camera().WriteEffectGlobals(camera.Pose) with { EyePaddingF6 = camera.EyePadding, TargetPaddingFE = camera.TargetPadding };
                    return current;
                case NativeBattleEffect326Contacts506BA0:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = nativeActionPackets.ReadHeaderContacts506BA0(current.Effect.Parameters!.HeaderIndex7C) });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect326Flash5712C0 flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(unchecked((short)flash.Rise), unchecked((short)flash.Hold), unchecked((short)flash.Fall), checked((byte)flash.Intensity), flash.PilotIdentity) };
                    return current;
                case NativeBattleEffect326QueueTextureRelease508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => current = WriteBytes(current, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect326WriteModelBytes write:
                    graphics = graphics with { Models = graphics.Models.SetItem(write.Actor, ApplyNativeEffect326ModelBytes(graphics.Models[write.Actor], write)) };
                    return current;
                case NativeBattleEffect326ActorAnimation505C00 animation:
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)animation.Actor), animation.Animation);
                    return current;
                case NativeBattleEffect326DeliverBatch draw when draw.CursorGlobal == 0x1D8E054:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect326 requires its current ordering table.");
                    NativeBattleCommonPacketWrite written = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect326 requires its actual COMMON packet process."), primitive.Pending, draw.Batch, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return SynchronizeCursor(current with { Memory = current.Memory with { PrimitiveProcess = written.Process } });
                default:
                    throw new NotSupportedException($"Effect326 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private static uint NativeEffect326CommonBase(NativeBattlePrimitiveProcess process)
    {
        uint bank = process.PacketBank switch
        {
            "off_B6D070" => 0x1901018,
            "off_B6D094" => 0x199B018,
            _ => throw new NotSupportedException("Effect326 requires its actual COMMON packet bank.")
        };
        return checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity);
    }

    private static NativeBattleGraphicsModel ApplyNativeEffect326ModelBytes(NativeBattleGraphicsModel model, NativeBattleEffect326WriteModelBytes write)
    {
        if (write.Offset == 7 && write.Length == 1)
        {
            return model with { Alpha7 = write.Data.Read(0, 1)[0] };
        }
        if (write.Offset is 0x28 or 0x2C && write.Length == 4)
        {
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(write.Data.Read(0, 4));
            return write.Offset == 0x28 ? model with { Color40 = color } : model with { Color44 = color };
        }
        return NativeBattleRaldo19ModelMemory.Apply(model, write.Offset, write.Data, 0, write.Length);
    }
}
