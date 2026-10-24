using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect345Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect345(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect345 requires its current effect.");
        NativeBattleEffect345Resources resources = effect.Binding.Effect345Resources ?? throw new NotSupportedException("Effect345 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect345 requires current process memory.");
        NativeBattleEffect345Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect345 requires its current CRT stream."), memory.Effect345 ?? throw new NotSupportedException("Effect345 requires its retained state.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect345 requires its current addressed memory.") };
        NativeBattleEffect345Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect345 requires its arithmetic constants."), _ => nativeActionPackets, (_, address) => ReadModel(address), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect345 requires its current projection."), _ => Camera().Roll, _ => NativeStage?.ModelColors ?? throw new NotSupportedException("Effect345 requires its current stage model colors."), ReadPhysical, Point, InitializeModel, SelectModel, AdvanceModel, _ => new(Camera().CapturePackedView1D97778(), Camera().Projection ?? throw new NotSupportedException("Effect345 requires its current projection."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect345 requires its original shadow source."), definition.NativeGraphics?.PrimitiveCapabilities ?? throw new NotSupportedException("Effect345 requires its primitive capabilities."), definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect345 requires its arithmetic constants.")), Execute)
        {
            ReadModel = (_, actor) => graphics.Models[actor],
            ReadPoseSource = (_, actor) => Pose(actor)
        };
        frame = initialize ? NativeBattleEffect345Controller.Initialize(frame, resources, inputs) : NativeBattleEffect345Controller.Advance(frame, resources, inputs);
        PublishNativeEffect345Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect345 requires its current camera.");
        NativeBattlePoseSource Pose(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect345 requires its current physical skeleton.");

        NativeBattleEffectArena ReadModel(uint address)
        {
            NativeBattleGraphicsModel model = graphics.Models[NativeEffect345Actor(graphics, address)];
            NativeBattleEffectArena bytes = CaptureNativeEffect301Model(model);
            if (model.GeometryBounds52 is { } bounds)
            {
                bytes = bytes.Write(0x3C, BitConverter.GetBytes(unchecked((short)bounds.Maximum.Y)));
            }
            return bytes;
        }

        NativeBattleEffect345Frame Observe(NativeBattleEffect345Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect345 requires its current point temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleEffect345PointStep Point(NativeBattleEffect345Frame current, uint address, int selector, int fraction, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect345Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : Pose(actor);
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), checked((short)fraction));
            current = Observe(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, retained.Write(0, BitConverter.GetBytes(unchecked((short)point.X))).Write(2, BitConverter.GetBytes(unchecked((short)point.Y))).Write(4, BitConverter.GetBytes(unchecked((short)point.Z))));
        }

        NativeBattleEffect345OwnedModelStep InitializeModel(NativeBattleEffect345Frame current, uint address, NativeBattleEffectOwnedModelResource resource, ushort argument)
        {
            uint defaultColor = NativeStage?.ModelColors?.Color44 ?? throw new NotSupportedException("Effect345 requires its current default model color.");
            NativeBattleEffectOwnedModelStep step = NativeBattleEffectOwnedModel.Initialize8DD0F0(NativeBattleEffect345Controller.Read(current, address), address, resource, argument, defaultColor);
            current = NativeBattleEffect345Controller.Write(current, address, step.Task);
            return new(current with { State = current.State with { OwnedModels = current.State.OwnedModels.SetItem(address, step.State) } }, 0);
        }

        NativeBattleEffect345OwnedModelStep SelectModel(NativeBattleEffect345Frame current, uint address, short animation)
        {
            NativeBattleEffectOwnedModelStep step = NativeBattleEffectOwnedModel.Select8DD190(NativeBattleEffect345Controller.Read(current, address), OwnedModel(current, address), resources.Model, animation);
            return PublishModel(current, address, step, true);
        }

        NativeBattleEffect345OwnedModelStep AdvanceModel(NativeBattleEffect345Frame current, uint address)
        {
            NativeBattleEffectOwnedModelStep step = NativeBattleEffectOwnedModel.AdvanceOnce8DD220(NativeBattleEffect345Controller.Read(current, address), OwnedModel(current, address), resources.Model);
            return PublishModel(current, address, step, !step.Completed);
        }

        NativeBattleEffectOwnedModelState OwnedModel(NativeBattleEffect345Frame current, uint address) => current.State.OwnedModels.TryGetValue(address, out NativeBattleEffectOwnedModelState? model) ? model : throw new NotSupportedException("Effect345 requires its retained decoded model owner.");

        NativeBattleEffect345OwnedModelStep PublishModel(NativeBattleEffect345Frame current, uint address, NativeBattleEffectOwnedModelStep step, bool publishPose)
        {
            current = NativeBattleEffect345Controller.Write(current, address, step.Task);
            current = current with { State = current.State with { OwnedModels = current.State.OwnedModels.SetItem(address, step.State) } };
            if (publishPose)
            {
                current = current with { Staging = NativeBattleEffectOwnedModelRendering.PublishDecodedPose(current.Staging ?? throw new NotSupportedException("Effect345 requires its current addressed pose."), step.State, resources.Model) };
            }
            return new(current, step.Completed ? 1 : 0);
        }

        NativeBattleEffectArena ReadPhysical(NativeBattleEffect345Frame current, uint address, int length)
        {
            if (address >= 0x1D972C0 && (ulong)address + (uint)length <= 0x1D972C0ul + (uint)graphics.Models.Length * 156)
            {
                int actor = checked((int)((address - 0x1D972C0) / 156));
                int offset = checked((int)((address - 0x1D972C0) % 156));
                if (length > 156 - offset)
                {
                    throw new InvalidDataException("Effect345 physical read crosses a current model row.");
                }
                return NativeBattleEffectNodeContextMemory.Copy(ReadModel(0x1D972C0u + (uint)actor * 156), offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
            }
            return NativeBattleEffect345Controller.ReadBytes(current, address, length);
        }

        NativeBattleEffect345Frame Execute(NativeBattleEffect345Frame current, NativeBattleEffect345Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect345UpdateCaster8DC740 caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleEffect345Controller.Read(current, caster.TaskAddress), current.Effect.Parameters ?? throw new NotSupportedException("Effect345 requires its current parameters."), graphics.Models, Pose, producer => current = Observe(current, producer));
                    return NativeBattleEffect345Controller.Write(current, caster.TaskAddress, casterTask);
                case NativeBattleEffect345UpdateTarget8DC610 target:
                    NativeBattleModulePoolTask targetTask = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleEffect345Controller.Read(current, target.TaskAddress), graphics.Models, Pose, producer => current = Observe(current, producer));
                    return NativeBattleEffect345Controller.Write(current, target.TaskAddress, targetTask);
                case NativeBattleEffect345UpdateBounds8DC870 bounds:
                    NativeBattleModulePoolTask boundsTask = NativeBattleEffect345Controller.Read(current, bounds.TaskAddress);
                    int boundsActor = boundsTask.ReadByte(0x2D);
                    NativeBattlePoseSource poseSource = Pose(boundsActor);
                    NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[boundsActor].SequenceState ?? throw new NotSupportedException("Effect345 requires its current target pose."), poseSource);
                    return NativeBattleEffect345Controller.Write(current, bounds.TaskAddress, NativeBattleEffectModelHelpers.UpdateBounds8DC870(boundsTask, poseSource, decoded));
                case NativeBattleEffect345UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = upload.Source switch
                    {
                        NativeBattleEffect345Resources.EmbeddedTim0Address => resources.EmbeddedTim0,
                        NativeBattleEffect345Resources.EmbeddedTim1Address => resources.EmbeddedTim1,
                        _ when upload.Source == current.State.ReadDword(InitializerGlobal) => new(resources.MainTim.Name, NativeBattleEffect345Controller.ReadBytes(current, upload.Source, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray()),
                        _ => throw new NotSupportedException("Effect345 TIM source has no current resource owner.")
                    };
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect345 requires its texture scheduler.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect345RegisterCamera5099A0 registration when registration.Source == NativeBattleEffect345Resources.CameraAddress:
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect345 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect345ReleaseSource508630 release:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(release.Source, release.CompletionAddress, (address, value) => current = NativeBattleEffect345Controller.WriteBytes(current, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect345Sound501330 sound:
                    PublishNativeEffect345Frame(current);
                    PublishNativeModuleSound(new(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.Source, 4).AsSpan()), sound.Flags, null) { Pan = sound.Pan });
                    return ImportNativeEffect345Frame(current);
                case NativeBattleEffect345StageTransition transition:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect345 requires its current stage groups.");
                    if (stage.Groups.IsDefault || stage.Groups.Length != 4 || (uint)transition.Group >= 4)
                    {
                        throw new InvalidDataException("Effect345 requires its actual four physical stage groups.");
                    }
                    NativeBattleStageGroup group = stage.Groups[transition.Group];
                    group = group with { ColorDepth = unchecked((short)transition.Intensity), ColorFlags = transition.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags };
                    NativeStage = stage with { Groups = stage.Groups.SetItem(transition.Group, group) };
                    return current;
                case NativeBattleEffect345Contact506690 contact:
                    PublishNativeEffect345Frame(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.AbsoluteIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return ImportNativeEffect345Frame(current);
                case NativeBattleEffect345DrawBatch draw:
                    if (draw.CursorGlobal is not (PrimaryCursorGlobal or SecondaryCursorGlobal))
                    {
                        throw new NotSupportedException("Effect345 requires its actual private packet cursor.");
                    }
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect345 requires its current ordering table.");
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(draw.CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect345 requires its current packet arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect345 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private static int NativeEffect345Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect345 requires an actual current physical model address.");
        }
        return checked((int)(offset / 156));
    }

    private void PublishNativeEffect345Frame(NativeBattleEffect345Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect345 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect345 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect345Frame ImportNativeEffect345Frame(NativeBattleEffect345Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect345 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect345 ?? throw new NotSupportedException("Effect345 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect345 lost its current CRT stream.") };
    }
}
