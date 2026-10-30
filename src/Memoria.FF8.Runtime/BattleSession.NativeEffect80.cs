using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect80Frame ApplyNativeEffect80StageTransition(NativeBattleEffect80Frame frame, NativeBattleEffect80StageTransition transition)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect80 requires its current stage groups.");
        if (stage.Groups.IsDefault || stage.Groups.Length != 4 || (uint)transition.Index >= 4)
        {
            throw new InvalidDataException("Effect80 stage transition requires its actual four physical groups.");
        }
        NativeBattleStageGroup group = stage.Groups[transition.Index];
        group = group with { ColorDepth = unchecked((short)transition.Intensity), ColorFlags = transition.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags };
        NativeStage = stage with { Groups = stage.Groups.SetItem(transition.Index, group) };
        return frame;
    }

    private NativeBattleGraphicsState RunNativeEffect80(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect80 requires its selected effect.");
        NativeBattleEffect80Resources resources = effect.Binding.Effect80Resources ?? throw new NotSupportedException("Effect80 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect80 requires its process memory.");
        NativeBattleEffect80Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect80 requires its current CRT stream."), memory.Effect80 ?? throw new NotSupportedException("Effect80 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect80 requires its temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect80 requires its arithmetic constants.");
        NativeBattleEffect80Inputs inputs = new(colors, _ => nativeActionPackets, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, actor) => graphics.Models[actor], (_, actor) => Pose(actor), (_, actor) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]), 0x1C, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8), _ => Camera().Roll, ReadColor, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect80Controller.Initialize(frame, resources, inputs) : NativeBattleEffect80Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect80 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect80 requires its current projection.");
        NativeBattlePoseSource Pose(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect80 requires its current actor skeleton.");
        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Effect80 requires an actual physical model row.");
            }
            return checked((int)(delta / 156));
        }
        void Publish(NativeBattleEffect80Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect80 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect80 lost its temporary memory.");
            nativeCrtRandom = current.Random;
        }
        NativeBattleEffect80Frame Import(NativeBattleEffect80Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect80 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect80 ?? throw new InvalidDataException("Effect80 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect80 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect80 service lost its temporary memory.") };
        }
        uint ReadColor(NativeBattleEffect80Frame current, uint address)
        {
            NativeBattleStageColors modelColors = NativeStage?.ModelColors ?? throw new NotSupportedException("Effect80 requires its current model colors.");
            return address switch
            {
                0xB8B7D8 => modelColors.Color40,
                0xB8B9A8 => modelColors.Color44,
                _ => throw new InvalidDataException("Effect80 selected an unsupported model color owner.")
            };
        }
        NativeBattleEffect80Frame Deliver(NativeBattleEffect80Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags)
        {
            batch = NativeBattleEffect80Presentation.FinalizeCapturedBatch(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect80 requires its current ordering table.");
            if (cursorGlobal == 0x1D8E054)
            {
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect80 requires its current ordinary packet process."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            uint pointer = current.State.ReadDword(cursorGlobal);
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(pointer, 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect80 requires its current COMMON bytes.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, cursor, NativeBattleEffectArena.Length, offset =>
            {
                (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                return (bytes[0], known[0]);
            });
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(0x20DFAB8u + (uint)written.Cursor)) };
        }
        NativeBattleEffect80Frame Position(NativeBattleEffect80Frame current, uint address, NativeBattleVector position, bool publishWorld, short? vertical)
        {
            int actor = Actor(address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect80 requires its current model transform.");
            model = model with { Transform = transform with { Position28 = position, Vertical34 = vertical ?? transform.Vertical34 } };
            if (publishWorld)
            {
                model = model with { GeometryWorldMatrix = (model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect80 requires its current model world matrix.")) with { X = position.X, Y = position.Y, Z = position.Z } };
            }
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            return current;
        }
        NativeBattleEffect80Frame Execute(NativeBattleEffect80Frame current, NativeBattleEffect80Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect80ObserveBone bone:
                    NativeBattleBoneAnchorProducer observed = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[bone.Actor], Pose(bone.Actor), bone.Selector, 0);
                    return ObserveNativeEffect80BoneAnchor(current, observed);
                case NativeBattleEffect80UpdateCaster8DC740 caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleEffect80Controller.Read(current, caster.TaskAddress), current.Effect.Parameters!, graphics.Models, Pose, producer => current = ObserveNativeEffect80BoneAnchor(current, producer));
                    return NativeBattleEffect80Controller.Write(current, caster.TaskAddress, casterTask);
                case NativeBattleEffect80UpdateTarget8DC610 target:
                    NativeBattleModulePoolTask targetTask = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleEffect80Controller.Read(current, target.TaskAddress), graphics.Models, Pose, producer => current = ObserveNativeEffect80BoneAnchor(current, producer));
                    return NativeBattleEffect80Controller.Write(current, target.TaskAddress, targetTask);
                case NativeBattleEffect80UpdateBounds8DC870 bounds:
                    NativeBattleModulePoolTask boundsTask = NativeBattleEffect80Controller.Read(current, bounds.TaskAddress);
                    int boundsActor = boundsTask.ReadByte(0x2D);
                    NativeBattlePoseSource source = Pose(boundsActor);
                    boundsTask = NativeBattleEffectModelHelpers.UpdateBounds8DC870(boundsTask, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[boundsActor].SequenceState ?? throw new NotSupportedException("Effect80 requires its target pose."), source));
                    return NativeBattleEffect80Controller.Write(current, bounds.TaskAddress, boundsTask);
                case NativeBattleEffect80Point502170 point:
                    int pointActor = Actor(point.ModelAddress);
                    NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[pointActor], Pose(pointActor), checked((byte)point.Selector), 0);
                    current = ObserveNativeEffect80BoneAnchor(current, producer);
                    NativeBattleVector vector = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
                    NativeBattleEffectArena result = NativeBattleEffect80Controller.ReadBytes(current, point.OutputAddress, 8);
                    result = result.Write(0, BitConverter.GetBytes(vector.X)).Write(2, BitConverter.GetBytes(vector.Y)).Write(4, BitConverter.GetBytes(vector.Z));
                    return NativeBattleEffect80Controller.WriteBytes(current, point.OutputAddress, result, 8);
                case NativeBattleEffect80UploadTim upload:
                    if (upload.Address != current.State.ReadDword(InitializerGlobal))
                    {
                        throw new InvalidDataException("Effect80 selected an unsupported current TIM.");
                    }
                    NativeBattleEffectHeapAllocation tim = new("effect80-current-tim", NativeBattleEffect80Controller.ReadBytes(current, upload.Address, TextureLength).Read(0, TextureLength).ToImmutableArray());
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Effect80 requires its texture scheduler.");
                    return current with { Memory = current.Memory with { Textures = textures.EnqueueTim505E30(tim).State } };
                case NativeBattleEffect80RegisterCamera camera:
                    NativeBattleEffectTextureState registered = (current.Memory.Textures ?? throw new NotSupportedException("Effect80 requires its texture scheduler.")).Register5099A0(camera.Camera);
                    NativeCamera = Camera().RegisterControlResource(registered.Animation);
                    return current with { Memory = current.Memory with { Textures = registered } };
                case NativeBattleEffect80Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect80Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect80TextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionByteAddress, (address, value) => current = NativeBattleEffect80Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect80TargetFlags flags:
                    int flagActor = Actor(flags.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[flagActor];
                    if (flags.ByteOffset > 1 || flags.WordWidth && flags.ByteOffset != 0)
                    {
                        throw new InvalidDataException("Effect80 selected an invalid model flag width.");
                    }
                    int shift = flags.ByteOffset * 8;
                    ushort mask = flags.WordWidth ? flags.AndMask : (ushort)((0xFFFF ^ (0xFF << shift)) | ((byte)flags.AndMask << shift));
                    ushort bits = flags.WordWidth ? flags.OrMask : (ushort)((byte)flags.OrMask << shift);
                    graphics = graphics with { Models = graphics.Models.SetItem(flagActor, model with { Flags0 = (ushort)((model.Flags0 & mask) | bits) }) };
                    return current;
                case NativeBattleEffect80DeliverBatch packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                case NativeBattleEffect80DrawBody7AFEC0 draw:
                    (NativeBattleGraphicsState bodyGraphics, NativeBattleEffect80Frame bodyFrame) = DrawNativeEffect80Body(graphics, current, draw.ContextAddress, Deliver, draw.PilotIdentity);
                    graphics = graphics with { Models = bodyGraphics.Models };
                    return bodyFrame;
                case NativeBattleEffect80BodyAnchor7B4750 anchor:
                    return ObserveNativeEffect80Anchor(graphics, current, anchor.TaskAddress, anchor.ContextAddress);
                case NativeBattleEffect80StageTransition transition:
                    return ApplyNativeEffect80StageTransition(current, transition);
                case NativeBattleEffect80SetSequence505C00 sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)Actor(sequence.ModelAddress)), sequence.Sequence);
                    return current;
                case NativeBattleEffect80CopyActorPoint copy:
                    NativeBattleModelTransform sourceTransform = graphics.Models[Actor(copy.SourceModel)].Transform ?? throw new NotSupportedException("Effect80 requires its actual source position.");
                    return Position(current, copy.DestinationModel, sourceTransform.Position28 with { Y = unchecked((short)(sourceTransform.Position28.Y + copy.YOffset)) }, copy.PublishWorld, sourceTransform.Vertical34);
                case NativeBattleEffect80SetActorPosition position:
                    return Position(current, position.ModelAddress, position.Position, position.PublishWorld, null);
                case NativeBattleEffect80Vibration4A29A0 vibration:
                    if (vibration.SourceAddress != VibrationAddress)
                    {
                        throw new InvalidDataException("Effect80 selected an unsupported vibration source.");
                    }
                    ImmutableArray<byte> vibrationBytes = resources.Vibration.Data;
                    NativeVibrationBank bank = new(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(vibrationBytes.AsSpan())), [BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan().Slice(4))], vibrationBytes);
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect80 requires its current vibration process.")).Enqueue(bank, vibration.Mode, vibration.Magnitude);
                    NativeVibrationProcess = queued.State;
                    return NativeBattleEffect80Controller.WriteBytes(current, vibration.OutputAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)queued.Handle))), 2);
                case NativeBattleEffect80StopVibration4A2940 stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect80 requires its current vibration process.")).Stop4A2940(stop.Handle);
                    return current;
                default:
                    throw new NotSupportedException("Effect80 emitted an unsupported native operation.");
            }
        }
    }
}
