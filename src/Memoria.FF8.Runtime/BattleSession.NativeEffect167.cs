using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect167(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = (graphics.OriginalEffect ?? throw new NotSupportedException("Effect167 requires its selected original effect.")) with { PresentationPackets = [] };
        NativeBattleEffect167Resources resources = effect.Binding.Effect167Resources ?? throw new NotSupportedException("Effect167 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect167 requires its current process memory.");
        NativeBattleEffect167Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect167 requires its current CRT stream."), memory.Effect167 ?? throw new NotSupportedException("Effect167 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect167 requires its current addressed memory.") };
        NativeBattleEffect167Inputs inputs = null!;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect167 requires its arithmetic constants."), _ => graphics.Flags, _ => nativeActionPackets, (_, actor) => graphics.Models[actor], (_, actor) => NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]), ReadBoneMatrix, (_, actor) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Effect167 requires its current model world matrix."), null), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect167 requires its current projection."), _ => Camera().FocalLength ?? throw new NotSupportedException("Effect167 requires its current scalar focal WORD."), _ => Camera().Roll, Point502170, LocalPoint, Execute);
        inputs = inputs with
        {
            CapturePresentation = true,
            ReadColor = (_, address) => address == 0xB8B7D8 ? (NativeStage?.ModelColors ?? throw new NotSupportedException("Effect167 requires its current stage model colors.")).Color40 : throw new NotSupportedException("Effect167 selected an unsupported current color owner.")
        };
        frame = initialize ? NativeBattleEffect167Controller.Initialize(frame, resources, inputs) : NativeBattleEffect167Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect167 requires its current camera.");

        void Publish(NativeBattleEffect167Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect167 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect167 lost its current addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect167Frame Import(NativeBattleEffect167Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect167 service lost its memory.");
            return current with { Memory = latest, State = latest.Effect167 ?? throw new InvalidDataException("Effect167 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect167 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect167 service lost its addressed memory.") };
        }

        NativeBattlePackedMatrix32 ReadBoneMatrix(NativeBattleEffect167Frame current, int actor, int selector)
        {
            NativeFileStaging = current.Staging;
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect167 requires its current skeleton.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], source);
            if ((uint)selector >= (uint)bones.Length)
            {
                throw new InvalidDataException("Effect167 selected a bone outside its current physical skeleton.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(bones[selector], null);
        }

        NativeBattleEffect167PointStep Point502170(NativeBattleEffect167Frame current, int actor, byte selector, short fraction, NativeBattleEffectArena retained)
        {
            NativeFileStaging = current.Staging;
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect167 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, selector, fraction);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }

        NativeBattleEffect167PointStep LocalPoint(NativeBattleEffect167Frame current, int actor, int selector, int fraction, NativeBattleEffectArena retained)
        {
            NativeFileStaging = current.Staging;
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect167 requires its current local skeleton.");
            NativeBattleEffect167LocalBonePointStep step = NativeBattleEffect167LocalBonePoint.Query(current.Memory, current.Staging!, model, source!, selector, fraction, retained);
            return new(current with { Memory = step.Memory, Staging = step.Staging }, step.Output8);
        }

        NativeBattleEffect167Frame Execute(NativeBattleEffect167Frame current, NativeBattleEffect167Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect167ForceCamera50A730:
                    NativeCamera = Camera().Apply50A730();
                    return current;
                case NativeBattleEffect167SetProjectionH projection:
                    NativeCamera = Camera() with { Projection = (Camera().Projection ?? throw new NotSupportedException("Effect167 requires its current projection.")) with { FocalLength = projection.Value } };
                    return current;
                case NativeBattleEffect167UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect167Controller.ReadBytes(current, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect167 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect167RegisterCamera5099A0 registration:
                    if (registration.SourceAddress != NativeBattleEffect167Profile.CameraAddress)
                    {
                        throw new NotSupportedException("Effect167 selected an unsupported camera resource.");
                    }
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect167 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect167Flash5712C0 flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(checked((short)flash.Rise), checked((short)flash.Hold), checked((short)flash.Fall), flash.Color) };
                    return current;
                case NativeBattleEffect167Sound501330 sound:
                    if (sound.SourceAddress != NativeBattleEffect167Profile.SoundAddress)
                    {
                        throw new NotSupportedException("Effect167 selected an unsupported sound descriptor.");
                    }
                    Publish(current);
                    PublishNativeModuleSound(new(resources.Sound, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect167StageGroupFlags flags:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect167 requires its current stage.");
                    NativeBattleStageGroup group = stage.Groups[flags.Group];
                    NativeStage = stage with { Groups = stage.Groups.SetItem(flags.Group, group with { Flags = (byte)((group.Flags & flags.AndMask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect167WriteModelBytes write:
                    graphics = graphics with { Models = graphics.Models.SetItem(write.Actor, ApplyNativeEffect167ModelBytes(graphics.Models[write.Actor], write)) };
                    return current;
                case NativeBattleEffect167ResourceFlags44 flags:
                    ushort retainedFlags = NativeCastingResourceFlags(graphics, checked((byte)flags.Actor));
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { ResourceFlags44 = (ushort)((retainedFlags & flags.AndMask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect167AllContacts506BA0 contacts:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    ImmutableArray<NativeBattleContactPacket> selected = Enumerable.Range(contacts.FirstContactIndex, contacts.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray();
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = selected });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect167DrawBody464 body:
                    (graphics, current) = DrawNativeEffect167Body(graphics, current, body.TemporaryAddress, body.Actor, body.OffsetWorld, resources, body.PilotIdentity);
                    return current;
                case NativeBattleEffect167DrawBatch draw:
                    if (draw.CursorGlobal != NativeBattleEffect167Profile.CursorGlobal)
                    {
                        throw new NotSupportedException("Effect167 selected an unsupported private packet cursor.");
                    }
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect167 requires its current ordering table.");
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(draw.CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect167 requires its current COMMON arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, cursor, resolved) } };
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect167 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private static NativeBattleGraphicsModel ApplyNativeEffect167ModelBytes(NativeBattleGraphicsModel model, NativeBattleEffect167WriteModelBytes write)
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
        if (write.Offset == 0x2B && write.Length == 1)
        {
            uint color = model.Color40 ?? throw new NotSupportedException("Effect167 requires its current packed model color before its code-byte store.");
            return model with { Color40 = (color & 0xFFFFFF) | ((uint)write.Data.Read(0, 1)[0] << 24) };
        }
        return NativeBattleRaldo19ModelMemory.Apply(model, write.Offset, write.Data, 0, write.Length);
    }
}
