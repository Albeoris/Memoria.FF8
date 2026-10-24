using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect79(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect79 requires its selected effect.");
        NativeBattleEffect79Resources resources = effect.Binding.Effect79Resources ?? throw new NotSupportedException("Effect79 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect79 requires its process memory.");
        NativeBattleEffect79Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect79 requires its current CRT stream."), memory.Effect79 ?? throw new NotSupportedException("Effect79 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect79 requires its temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect79 requires its arithmetic constants.");
        NativeBattleStackBytes stack = NativeBattleEffect79Stack.Begin(graphics.Callback);
        NativeBattleEffect79Inputs inputs = new(colors, _ => nativeActionPackets, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, actor) => graphics.Models[actor], (_, actor) => Pose(actor), _ => Camera().Roll, ReadColor, Execute) { ParticleBasis = ParticleBasis };
        frame = initialize ? NativeBattleEffect79Controller.Initialize(frame, resources, inputs) : NativeBattleEffect79Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect79 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect79 requires its current projection.");
        NativeBattlePoseSource Pose(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect79 requires its current actor skeleton.");
        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Effect79 requires an actual physical model row.");
            }
            return checked((int)(delta / 156));
        }
        void Publish(NativeBattleEffect79Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect79 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect79 lost its temporary memory.");
            nativeCrtRandom = current.Random;
        }
        NativeBattleEffect79Frame Import(NativeBattleEffect79Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect79 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect79 ?? throw new InvalidDataException("Effect79 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect79 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect79 service lost its temporary memory.") };
        }
        uint ReadColor(NativeBattleEffect79Frame current, uint address)
        {
            NativeBattleStageColors modelColors = NativeStage?.ModelColors ?? throw new NotSupportedException("Effect79 requires its current model colors.");
            return address switch
            {
                0xB8B7D8 => modelColors.Color40,
                0xB8B9A8 => modelColors.Color44,
                _ => throw new InvalidDataException("Effect79 selected an unsupported model color owner.")
            };
        }
        NativeBattleEffect79Frame Deliver(NativeBattleEffect79Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect79 requires its current ordering table.");
            if (cursorGlobal == 0x1D8E054)
            {
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect79 requires its current ordinary packet process."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            uint pointer = current.State.ReadDword(cursorGlobal);
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(pointer, 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect79 requires its current COMMON bytes.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => arena.Read(offset, 1)[0]);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(0x20DFAB8u + (uint)written.Cursor)) };
        }
        NativeBattleEffect79ParticleBasisStep ParticleBasis(NativeBattleEffect79Frame current, uint taskAddress, ushort selector, bool useBoneBasis, short angle)
        {
            if (useBoneBasis)
            {
                int actor = NativeBattleEffect79Controller.Read(current, taskAddress).ReadByte(0x2C);
                byte[] bytes = new byte[32];
                bool[] known = new bool[32];
                stack.Bytes.CopyTo(bytes);
                stack.Known.CopyTo(known);
                NativeBattleBoneMatrixOutputStep queried = NativeBattleBoneMatrixOutput8DE8F0.Query(current.Memory, current.Staging!, graphics.Models[actor], Pose(actor), selector, 0, new(bytes.ToImmutableArray(), known.ToImmutableArray()));
                _ = NativeBattleEffect79Stack.ReadRotation(stack);
                return new(current with { Memory = queried.Memory, Staging = queried.Staging }, queried.Output);
            }
            NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            NativeBattleStatefulMatrixRotationStep yaw = NativeBattleStatefulMatrixRotation.Rotate(current.Memory, current.Staging!, matrix, 'Y', 2048);
            NativeBattleStatefulMatrixRotationStep pitch = NativeBattleStatefulMatrixRotation.Rotate(yaw.Memory, yaw.Staging, yaw.Matrix, 'X', angle);
            current = current with { Memory = pitch.Memory, Staging = pitch.Staging };
            current = Execute(current, new NativeBattleEffect79Point502170(taskAddress, selector));
            return new(current, pitch.Matrix);
        }
        NativeBattleEffect79Frame Execute(NativeBattleEffect79Frame current, NativeBattleEffect79Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect79ObserveBone bone:
                    NativeBattleBoneAnchorProducer observed = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[bone.Actor], Pose(bone.Actor), bone.Selector, 0);
                    return ObserveNativeEffect79BoneAnchor(current, observed);
                case NativeBattleEffect79UpdateCaster8DC740 caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleEffect79Controller.Read(current, caster.TaskAddress), current.Effect.Parameters!, graphics.Models, Pose, producer => current = ObserveNativeEffect79BoneAnchor(current, producer));
                    return NativeBattleEffect79Controller.Write(current, caster.TaskAddress, casterTask);
                case NativeBattleEffect79UpdateTarget8DC610 target:
                    NativeBattleModulePoolTask targetTask = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleEffect79Controller.Read(current, target.TaskAddress), graphics.Models, Pose, producer => current = ObserveNativeEffect79BoneAnchor(current, producer));
                    return NativeBattleEffect79Controller.Write(current, target.TaskAddress, targetTask);
                case NativeBattleEffect79UpdateBounds8DC870 bounds:
                    NativeBattleModulePoolTask boundsTask = NativeBattleEffect79Controller.Read(current, bounds.TaskAddress);
                    int boundsActor = boundsTask.ReadByte(0x2D);
                    NativeBattlePoseSource source = Pose(boundsActor);
                    boundsTask = NativeBattleEffectModelHelpers.UpdateBounds8DC870(boundsTask, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[boundsActor].SequenceState ?? throw new NotSupportedException("Effect79 requires its target pose."), source));
                    stack = NativeBattleEffect79Stack.ObserveTargetBounds(stack, SkeletonPointer(current, boundsActor), NativeBattleBodyBoneMatrices.Require(graphics.Models[boundsActor], source).Length);
                    return NativeBattleEffect79Controller.Write(current, bounds.TaskAddress, boundsTask);
                case NativeBattleEffect79ObserveBodyStack bodyStack:
                    stack = NativeBattleEffect79Stack.ObserveBody(stack, current.Effect.Pools[5], bodyStack.TaskAddress, NativeBattleEffect79Controller.Read(current, bodyStack.TaskAddress), bodyStack.DrewBody);
                    return current;
                case NativeBattleEffect79ObserveNodeStack nodeStack:
                    stack = NativeBattleEffect79Stack.ObserveNode(stack, nodeStack.Instruction, nodeStack.FirstDword);
                    return current;
                case NativeBattleEffect79Point502170 point:
                    int pointActor = NativeBattleEffect79Controller.Read(current, point.TaskAddress).ReadByte(0x2C);
                    NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[pointActor], Pose(pointActor), checked((byte)point.Selector), 0);
                    current = ObserveNativeEffect79BoneAnchor(current, producer);
                    NativeBattleVector vector = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
                    return NativeBattleEffect79Controller.Write(current, point.TaskAddress, NativeBattleEffect79Controller.Read(current, point.TaskAddress).WriteWord(0x1C, unchecked((ushort)vector.X)).WriteWord(0x1E, unchecked((ushort)vector.Y)).WriteWord(0x20, unchecked((ushort)vector.Z)));
                case NativeBattleEffect79UploadTim upload:
                    int timLength = upload.Address == current.State.ReadDword(InitializerGlobal) ? TextureLength : resources.ReadFile(400).Data.Length;
                    NativeBattleEffectHeapAllocation tim = new("effect79-current-tim", NativeBattleEffect79Controller.ReadBytes(current, upload.Address, timLength).Read(0, timLength).ToImmutableArray());
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Effect79 requires its texture scheduler.");
                    return current with { Memory = current.Memory with { Textures = textures.EnqueueTim505E30(tim).State } };
                case NativeBattleEffect79RegisterCamera camera:
                    NativeBattleEffectTextureState registered = (current.Memory.Textures ?? throw new NotSupportedException("Effect79 requires its texture scheduler.")).Register5099A0(camera.Camera);
                    NativeCamera = Camera().RegisterControlResource(registered.Animation);
                    return current with { Memory = current.Memory with { Textures = registered } };
                case NativeBattleEffect79Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(resources.Sounds[sound.SourceAddress], unchecked((uint)sound.Flags), null) { Pan = unchecked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleEffect79Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect79TextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionByteAddress, (address, value) => current = NativeBattleEffect79Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect79TargetFlags flags:
                    int flagActor = Actor(flags.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[flagActor];
                    ushort mask = flags.WordWidth ? flags.AndMask : (ushort)(0xFF00 | (byte)flags.AndMask);
                    graphics = graphics with { Models = graphics.Models.SetItem(flagActor, model with { Flags0 = (ushort)((model.Flags0 & mask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect79DeliverBatch packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                case NativeBattleEffect79DrawBody7BD020 draw:
                    (NativeBattleGraphicsState bodyGraphics, NativeBattleEffect79Frame bodyFrame) = DrawNativeEffect79Body(graphics, current, draw.ContextAddress, Deliver);
                    graphics = graphics with { Models = bodyGraphics.Models };
                    return bodyFrame;
                case NativeBattleEffect79ObserveCasterAnchor anchor:
                    return ObserveNativeEffect79Anchor(graphics, current, anchor.TaskAddress, anchor.ContextAddress);
                case NativeBattleEffect79ModelPatch patch:
                    NativeBattleGraphicsModel patched = graphics.Models[patch.Actor];
                    if (patch.Position is { } position)
                    {
                        patched = patched with { Transform = (patched.Transform ?? throw new NotSupportedException("Effect79 requires the current transform.")) with { Position28 = position } };
                    }
                    if (patch.WorldTranslation is { } translation)
                    {
                        patched = patched with { GeometryWorldMatrix = (patched.GeometryWorldMatrix ?? throw new NotSupportedException("Effect79 requires its current world matrix.")) with { X = translation.X, Y = translation.Y, Z = translation.Z } };
                    }
                    if (patch.ScalePointer is { } scalePointer)
                    {
                        patched = patched with { ScaleVectorAddress48 = scalePointer };
                    }
                    if (patch.Color40 is { } color)
                    {
                        patched = patched with { Color40 = color };
                    }
                    if (patch.ColorMode43 is { } colorMode)
                    {
                        patched = patched with { Color40 = ((patched.Color40 ?? throw new NotSupportedException("Effect79 requires current model color.")) & 0xFFFFFF) | ((uint)colorMode << 24) };
                    }
                    if (patch.Alpha7 is { } alpha)
                    {
                        patched = patched with { Alpha7 = alpha };
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(patch.Actor, patched) };
                    if (patch.Sequence is { } sequence)
                    {
                        graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)patch.Actor), sequence);
                    }
                    return current;
                case NativeBattleEffect79StageTransition transition:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect79 requires current stage groups.");
                    if (stage.Groups.Length != 4)
                    {
                        throw new InvalidDataException("Effect79 requires four physical stage groups.");
                    }
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)transition.Intensity), ColorFlags = transition.ClearRgb ? group.ColorFlags & 0xFF000000 : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect79QueueFile48D0A0 file:
                    stack = NativeBattleEffect79Stack.Invalidate(stack, 0x48D0A0, "Actual effect file queue");
                    NativeBattleEffectFileAsset data = resources.ReadFile(file.FileId);
                    current = NativeBattleEffect79Controller.WriteBytes(current, file.Destination, NativeBattleEffectArena.Clear().Write(0, data.Data.AsSpan()), data.Data.Length);
                    Publish(current);
                    QueueNativeFileIo482610(data.Data.Length, file.Callback);
                    return Import(current);
                case NativeBattleEffect79StageMusic501A20 music:
                    NativeMusicProcess = (NativeMusicProcess ?? throw new NotSupportedException("Effect79 requires current music process." )).StageLoad501A20(music.SourceAddress, (address, length) => NativeBattleEffect79Controller.ReadBytes(current, address, length).Read(0, length));
                    NativeBattleAuxiliaryState musicAuxiliary = graphics.Auxiliary.AppendMusicLoad501A20(music.CompletionByteAddress, (address, value) => current = NativeBattleEffect79Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = musicAuxiliary };
                    return current;
                case NativeBattleEffect79MusicGain501B10 gain:
                    NativeMusicProcess = (NativeMusicProcess ?? throw new NotSupportedException("Effect79 requires current music commands.")).QueueGain501B10(gain.Duration, gain.Gain);
                    return current;
                case NativeBattleEffect79MusicFade46BBC0 fade:
                    NativeGlobalSoundProcess soundOwner = NativeGlobalSoundProcess ?? throw new NotSupportedException("Effect79 requires its current sound process.");
                    NativeGlobalSoundProcess = soundOwner with { MusicPlayback = (soundOwner.MusicPlayback ?? throw new NotSupportedException("Effect79 requires its current playback.")).FadeGain46BBC0(unchecked((uint)fade.Channel), unchecked((byte)fade.Duration), unchecked((ushort)fade.Gain)) };
                    return current;
                case NativeBattleEffect79StartVibration4A29A0 vibration:
                    ImmutableArray<byte> vibrationBytes = resources.Vibration.Data;
                    NativeVibrationBank bank = new(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(vibrationBytes.AsSpan())), [BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan().Slice(4))], vibrationBytes);
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect79 requires retained vibration process.")).Enqueue(bank, vibration.Flags, vibration.Power);
                    NativeVibrationProcess = queued.State;
                    return NativeBattleEffect79Controller.WriteCompletionByte(NativeBattleEffect79Controller.WriteCompletionByte(current, vibration.HandleOutputAddress, unchecked((byte)queued.Handle)), vibration.HandleOutputAddress + 1, unchecked((byte)(queued.Handle >> 8)));
                case NativeBattleEffect79StopVibration4A2940 stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect79 requires retained vibration process.")).Stop4A2940(stop.Handle);
                    return current;
                case NativeBattleEffect79SoundBank501860:
                    throw new NotSupportedException("Effect79 selected script contains no sound-bank load; an imported stream requires its actual completion owner.");
                default:
                    throw new NotSupportedException("Effect79 emitted an unsupported native operation.");
            }
        }

        NativeBattleResourcePointer SkeletonPointer(NativeBattleEffect79Frame current, int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if (model.ActiveStagedResourceRow100 is uint rowAddress)
            {
                if (NativeBattleEffectTaskMemory.TryReadPointer(current.Effect.Pools, rowAddress, out NativeBattleResourcePointer? pointer))
                {
                    return pointer ?? throw new NotSupportedException("Effect79 stack observation requires its current staged skeleton reference.");
                }
                return (current.Staging ?? throw new NotSupportedException("Effect79 requires its current staged resource row.")).ReadResourcePointer(rowAddress) ?? throw new NotSupportedException("Effect79 stack observation requires its observed skeleton pointer.");
            }
            NativeBattleModelCpuResourceRow row = (NativeModelCpuResources ?? throw new NotSupportedException("Effect79 requires its current CPU skeleton owner.")).Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Effect79 requires its current physical resource row.")];
            return new(row.Require(12), null, row.Lengths.TryGetValue(12, out int length) ? length : null);
        }
    }

    private NativeBattleEffect79Frame ObserveNativeEffect79BoneAnchor(NativeBattleEffect79Frame frame, NativeBattleBoneAnchorProducer producer)
    {
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect79 bone observation requires its current temporary memory.");
        NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(frame.Memory, producer);
        return frame with { Memory = observed, Staging = NativeFileStaging };
    }
}
