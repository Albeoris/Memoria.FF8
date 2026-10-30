using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect174(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect174 requires its selected original effect.");
        NativeBattleEffect174Resources resources = effect.Binding.Effect174Resources ?? throw new NotSupportedException("Effect174 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect174 requires its current process memory.");
        NativeBattleEffect174Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect174 requires its current CRT stream."), memory.Effect174 ?? throw new NotSupportedException("Effect174 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect174 requires its current addressed memory.") };
        NativeBattleEffect174Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect174 requires its arithmetic constants."), _ => graphics.Flags, _ => nativeActionPackets, (_, actor) => graphics.Models[actor], (_, actor) => CaptureNativeEffect174Model(graphics.Models[actor]), (_, actor) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Effect174 requires its current world matrix."), null), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect174 requires its current projection."), _ => Camera().Roll, _ => Camera().FocalLength ?? throw new NotSupportedException("Effect174 requires its current scalar focal WORD."), ReadColor, Point502170, Anchor571400, ReadBones, WriteBones, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect174Controller.Initialize(frame, resources, inputs) : NativeBattleEffect174Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect174 requires its current camera.");

        void Publish(NativeBattleEffect174Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect174 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect174 lost its current addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect174Frame Import(NativeBattleEffect174Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect174 service lost its memory.");
            return current with { Memory = latest, State = latest.Effect174 ?? throw new InvalidDataException("Effect174 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect174 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect174 service lost its addressed memory.") };
        }

        NativeBattleEffect174PointStep Point502170(NativeBattleEffect174Frame current, int actor, byte selector, short fraction, NativeBattleEffectArena retained)
        {
            NativeFileStaging = current.Staging;
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect174 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, selector, fraction);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }

        uint ReadColor(NativeBattleEffect174Frame current, uint address)
        {
            NativeBattleStageColors colors = NativeStage?.ModelColors ?? throw new NotSupportedException("Effect174 requires its current stage model colors.");
            return address switch { 0xB8B7D8 => colors.Color40, 0xB8B9A8 => colors.Color44, _ => throw new NotSupportedException("Effect174 selected an unsupported current color owner.") };
        }

        NativeBattleEffect174PointStep Anchor571400(NativeBattleEffect174Frame current, int actor, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect174 requires its current actor transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleEffect174PointStep first = Point502170(current, actor, 0xF0, 0, retained);
            NativeBattleEffect174PointStep second = Point502170(first.Frame, actor, 0xF1, 0, retained);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Output8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Output8.Read(offset, 2))) / 2));
            return new(second.Frame, retained.Write(0, BitConverter.GetBytes(Average(0))).Write(2, BitConverter.GetBytes(Average(2))).Write(4, BitConverter.GetBytes(Average(4))).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        ImmutableArray<NativeBattlePackedMatrix32> ReadBones(NativeBattleEffect174Frame current, int actor)
        {
            NativeBattleBodyBoneMatrices bones = graphics.Models[actor].CurrentBodyBoneMatrices ?? throw new NotSupportedException("Effect174 requires its current primary bone publication.");
            bones.Validate();
            return bones.Matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
        }

        NativeBattleEffect174Frame WriteBones(NativeBattleEffect174Frame current, int actor, ImmutableArray<NativeBattlePackedMatrix32> packed)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleBodyBoneMatrices bones = model.CurrentBodyBoneMatrices ?? throw new NotSupportedException("Effect174 requires its current primary bone publication.");
            if (packed.IsDefault || packed.Length != bones.Matrices.Length)
            {
                throw new InvalidDataException("Effect174 matrix copy leaves its current primary bone publication.");
            }
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { CurrentBodyBoneMatrices = bones with { Matrices = packed.Select(matrix => matrix.ReadMatrix()).ToImmutableArray() } }) };
            return current;
        }

        NativeBattleEffect174Frame Execute(NativeBattleEffect174Frame current, NativeBattleEffect174Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect174UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect174Controller.ReadBytes(current, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect174 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect174RegisterCamera5099A0 registration:
                    if (registration.SourceAddress != NativeBattleEffect174Profile.CameraAddress)
                    {
                        throw new NotSupportedException("Effect174 selected an unsupported camera resource.");
                    }
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect174 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect174SetResourceFlags44 flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { ResourceFlags44 = flags.Value }) };
                    return current;
                case NativeBattleEffect174WriteModelBytes write:
                    graphics = graphics with { Models = graphics.Models.SetItem(write.Actor, ApplyNativeEffect174ModelBytes(graphics.Models[write.Actor], write)) };
                    return current;
                case NativeBattleEffect174StageGroupFlags group:
                    NativeBattleStageState currentStage = NativeStage ?? throw new NotSupportedException("Effect174 requires its current stage.");
                    NativeStage = currentStage with { Groups = currentStage.Groups.SetItem(group.Group, currentStage.Groups[group.Group] with { Flags = unchecked((byte)((currentStage.Groups[group.Group].Flags & group.AndMask) | group.OrMask)) }) };
                    return current;
                case NativeBattleEffect174DrawBody136 body:
                    (current, graphics) = DrawNativeEffect174Body(current, graphics, checked((byte)body.Actor), body.TemporaryAddress, body.PilotIdentity);
                    return current;
                case NativeBattleEffect174DrawModelHistory5120 history:
                    (current, graphics) = DrawNativeEffect174History(current, graphics, history.Actor, history.PilotIdentity);
                    return current;
                case NativeBattleEffect174Stage5713E0 intensity:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect174 requires its current stage.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity.Amount), ColorFlags = intensity.Color }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect174Sound501330 sound:
                    uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4));
                    Publish(current);
                    PublishNativeModuleSound(new(descriptor, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect174FirstContact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect174AppendCommonTextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => current = NativeBattleEffect174Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect174DrawBatch draw:
                    if (draw.CursorGlobal != NativeBattleEffect174Profile.CursorGlobal)
                    {
                        throw new NotSupportedException("Effect174 selected an unsupported private packet cursor.");
                    }
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect174 requires its current ordering table.");
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(draw.CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect174 requires its current COMMON arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect174 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private static NativeBattleEffectArena CaptureNativeEffect174Model(NativeBattleGraphicsModel model)
    {
        NativeBattleEffectArena bytes = NativeBattleRaldo19ModelMemory.Capture(model);
        if (model.Color40 is uint color40)
        {
            bytes = bytes.Write(0x28, BitConverter.GetBytes(color40));
        }
        if (model.Color44 is uint color44)
        {
            bytes = bytes.Write(0x2C, BitConverter.GetBytes(color44));
        }
        if (model.Alpha7 is byte alpha)
        {
            bytes = bytes.Write(7, new byte[] { alpha });
        }
        if (model.SequenceState is { } sequence)
        {
            bytes = bytes.Write(0x7C, BitConverter.GetBytes(sequence.MeshMask124));
        }
        return bytes;
    }

    internal static NativeBattleGraphicsModel ApplyNativeEffect174ModelBytes(NativeBattleGraphicsModel model, NativeBattleEffect174WriteModelBytes write)
    {
        if (write.Offset == 12 && write.Length == 2)
        {
            return model with { Rotation12 = BinaryPrimitives.ReadInt16LittleEndian(write.Data.Read(0, 2)) };
        }
        if (write.Offset == 12 && write.Length == 8)
        {
            model = NativeBattleRaldo19ModelMemory.Apply(model, 12, write.Data, 0, 4);
            return NativeBattleRaldo19ModelMemory.Apply(model, 16, write.Data, 4, 4);
        }
        if (write.Offset == 28 && write.Length is 6 or 8)
        {
            model = NativeBattleRaldo19ModelMemory.Apply(model, 28, write.Data, 0, 4);
            return NativeBattleRaldo19ModelMemory.Apply(model, 32, write.Data, 4, write.Length - 4);
        }
        if (write.Offset == 7 && write.Length == 1)
        {
            return model with { Alpha7 = write.Data.Read(0, 1)[0] };
        }
        if (write.Offset is 0x28 or 0x2C && write.Length == 4)
        {
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(write.Data.Read(0, 4));
            return write.Offset == 0x28 ? model with { Color40 = color } : model with { Color44 = color };
        }
        if (write.Offset == 0x2B && write.Length == 1)
        {
            uint color = model.Color40 ?? throw new NotSupportedException("Effect174 requires its current packed model color before its code-byte store.");
            return model with { Color40 = (color & 0xFFFFFF) | ((uint)write.Data.Read(0, 1)[0] << 24) };
        }
        if (write.Offset == 0x7C && write.Length == 4)
        {
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Effect174 requires its current object mask owner.");
            return model with { SequenceState = sequence with { MeshMask124 = BinaryPrimitives.ReadUInt32LittleEndian(write.Data.Read(0, 4)) } };
        }
        return NativeBattleRaldo19ModelMemory.Apply(model, write.Offset, write.Data, 0, write.Length);
    }
}
