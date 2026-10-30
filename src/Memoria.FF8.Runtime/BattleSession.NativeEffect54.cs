using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect54(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect54 requires its current effect.");
        NativeBattleEffect54Resources resources = effect.Binding.Effect54Resources ?? throw new NotSupportedException("Effect54 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect54 requires current process memory.");
        NativeBattleEffect54Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect54 requires its current CRT stream."), memory.Effect54 ?? throw new NotSupportedException("Effect54 requires its retained state.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect54 requires its current addressed memory.") };
        NativeBattleEffect54Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect54 requires its arithmetic constants."), _ => nativeActionPackets, (_, address) => ReadModel(address), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect54 requires its current projection."), _ => Camera().Roll, Point, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect54Controller.Initialize(frame, resources, inputs) : NativeBattleEffect54Controller.Advance(frame, resources, inputs);
        PublishNativeEffect54Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect54 requires its current camera.");
        NativeBattlePoseSource Pose(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect54 requires its current physical skeleton.");

        NativeBattleEffectArena ReadModel(uint address)
        {
            NativeBattleGraphicsModel model = graphics.Models[NativeEffect54Actor(graphics, address)];
            NativeBattleEffectArena bytes = CaptureNativeEffect210Model(model);
            if (model.GeometryBounds52 is { } bounds)
            {
                bytes = bytes.Write(0x3C, BitConverter.GetBytes(unchecked((short)bounds.Maximum.Y)));
            }
            return bytes;
        }

        NativeBattleEffect54Frame Observe(NativeBattleEffect54Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect54 requires its current point temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleEffect54PointStep Point(NativeBattleEffect54Frame current, uint address, int selector, int fraction, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect54Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : Pose(actor);
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), checked((short)fraction));
            current = Observe(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, retained.Write(0, BitConverter.GetBytes(unchecked((short)point.X))).Write(2, BitConverter.GetBytes(unchecked((short)point.Y))).Write(4, BitConverter.GetBytes(unchecked((short)point.Z))));
        }

        NativeBattleEffect54Frame Execute(NativeBattleEffect54Frame current, NativeBattleEffect54Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect54UpdateCaster8DC740 caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleEffect54Controller.Read(current, caster.TaskAddress), current.Effect.Parameters ?? throw new NotSupportedException("Effect54 requires its current parameters."), graphics.Models, Pose, producer => current = Observe(current, producer));
                    return NativeBattleEffect54Controller.Write(current, caster.TaskAddress, casterTask);
                case NativeBattleEffect54UpdateTarget8DC610 target:
                    NativeBattleModulePoolTask targetTask = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleEffect54Controller.Read(current, target.TaskAddress), graphics.Models, Pose, producer => current = Observe(current, producer));
                    return NativeBattleEffect54Controller.Write(current, target.TaskAddress, targetTask);
                case NativeBattleEffect54UpdateBounds8DC870 bounds:
                    NativeBattleModulePoolTask boundsTask = NativeBattleEffect54Controller.Read(current, bounds.TaskAddress);
                    int boundsActor = boundsTask.ReadByte(0x2D);
                    NativeBattlePoseSource poseSource = Pose(boundsActor);
                    NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[boundsActor].SequenceState ?? throw new NotSupportedException("Effect54 requires its current target pose."), poseSource);
                    return NativeBattleEffect54Controller.Write(current, bounds.TaskAddress, NativeBattleEffectModelHelpers.UpdateBounds8DC870(boundsTask, poseSource, decoded));
                case NativeBattleEffect54UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, NativeBattleEffect54Controller.ReadBytes(current, upload.Source, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect54 requires its texture scheduler.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect54RegisterCamera5099A0 registration when registration.Source == CameraAddress:
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect54 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect54Sound501330 sound:
                    PublishNativeEffect54Frame(current);
                    PublishNativeModuleSound(new(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.Source, 4)), sound.Flags, null) { Pan = sound.Pan });
                    return ImportNativeEffect54Frame(current);
                case NativeBattleEffect54StageTransition transition:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect54 requires its current stage groups.");
                    if (stage.Groups.IsDefault || stage.Groups.Length != 4 || (uint)transition.Group >= 4)
                    {
                        throw new InvalidDataException("Effect54 requires its actual four physical stage groups.");
                    }
                    NativeBattleStageGroup group = stage.Groups[transition.Group];
                    group = group with { ColorDepth = unchecked((short)transition.Intensity), ColorFlags = transition.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags };
                    NativeStage = stage with { Groups = stage.Groups.SetItem(transition.Group, group) };
                    return current;
                case NativeBattleEffect54Contact506690 contact:
                    PublishNativeEffect54Frame(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.AbsoluteIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return ImportNativeEffect54Frame(current);
                case NativeBattleEffect54DrawBatch draw:
                    if (draw.CursorGlobal is not (PrimaryCursorGlobal or SecondaryCursorGlobal))
                    {
                        throw new NotSupportedException("Effect54 requires its actual private packet cursor.");
                    }
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect54 requires its current ordering table.");
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(draw.CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect54 requires its current packet arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect54 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private static int NativeEffect54Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect54 requires an actual current physical model address.");
        }
        return checked((int)(offset / 156));
    }

    private void PublishNativeEffect54Frame(NativeBattleEffect54Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect54 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect54 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect54Frame ImportNativeEffect54Frame(NativeBattleEffect54Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect54 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect54 ?? throw new NotSupportedException("Effect54 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect54 lost its current CRT stream.") };
    }
}
