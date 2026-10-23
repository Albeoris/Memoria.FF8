using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect83Frame ApplyNativeEffect83StageTransition(NativeBattleEffect83Frame frame, NativeBattleEffect83StageTransition transition)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect83 requires its current stage groups.");
        if (stage.Groups.IsDefault || stage.Groups.Length != 4 || (uint)transition.Index >= 4)
        {
            throw new InvalidDataException("Effect83 stage transition requires its actual four physical groups.");
        }
        NativeBattleStageGroup group = stage.Groups[transition.Index];
        group = group with { ColorDepth = unchecked((short)transition.Intensity), ColorFlags = transition.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags };
        NativeStage = stage with { Groups = stage.Groups.SetItem(transition.Index, group) };
        return frame;
    }

    private NativeBattleGraphicsState RunNativeEffect83(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect83 requires its selected effect.");
        NativeBattleEffect83Resources resources = effect.Binding.Effect83Resources ?? throw new NotSupportedException("Effect83 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect83 requires its process memory.");
        NativeBattleEffect83Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect83 requires its current CRT stream."), memory.Effect83 ?? throw new NotSupportedException("Effect83 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect83 requires its temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect83 requires its arithmetic constants.");
        NativeBattleEffect83Inputs inputs = new(colors, _ => nativeActionPackets, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, actor) => graphics.Models[actor], (_, actor) => Pose(actor), _ => Camera().Roll, ReadColor, Execute);
        frame = initialize ? NativeBattleEffect83Controller.Initialize(frame, resources, inputs) : NativeBattleEffect83Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect83 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect83 requires its current projection.");
        NativeBattlePoseSource Pose(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect83 requires its current actor skeleton.");
        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Effect83 requires an actual physical model row.");
            }
            return checked((int)(delta / 156));
        }
        void Publish(NativeBattleEffect83Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect83 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect83 lost its temporary memory.");
            nativeCrtRandom = current.Random;
        }
        NativeBattleEffect83Frame Import(NativeBattleEffect83Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect83 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect83 ?? throw new InvalidDataException("Effect83 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect83 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect83 service lost its temporary memory.") };
        }
        uint ReadColor(NativeBattleEffect83Frame current, uint address)
        {
            NativeBattleStageColors modelColors = NativeStage?.ModelColors ?? throw new NotSupportedException("Effect83 requires its current model colors.");
            return address switch
            {
                0xB8B7D8 => modelColors.Color40,
                0xB8B9A8 => modelColors.Color44,
                _ => throw new InvalidDataException("Effect83 selected an unsupported model color owner.")
            };
        }
        NativeBattleEffect83Frame Deliver(NativeBattleEffect83Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect83 requires its current ordering table.");
            if (cursorGlobal == 0x1D8E054)
            {
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect83 requires its current ordinary packet process."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            uint pointer = current.State.ReadDword(cursorGlobal);
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(pointer, 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect83 requires its current COMMON bytes.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => arena.Read(offset, 1)[0]);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(0x20DFAB8u + (uint)written.Cursor)) };
        }
        NativeBattleEffect83Frame Execute(NativeBattleEffect83Frame current, NativeBattleEffect83Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect83ObserveBone bone:
                    NativeBattleBoneAnchorProducer observed = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[bone.Actor], Pose(bone.Actor), bone.Selector, 0);
                    return current with { Memory = ObserveNativeBoneAnchor(current.Memory, observed) };
                case NativeBattleEffect83UpdateCaster8DC740 caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleEffect83Controller.Read(current, caster.TaskAddress), current.Effect.Parameters!, graphics.Models, Pose, producer => current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer) });
                    return NativeBattleEffect83Controller.Write(current, caster.TaskAddress, casterTask);
                case NativeBattleEffect83UpdateTarget8DC610 target:
                    NativeBattleModulePoolTask targetTask = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleEffect83Controller.Read(current, target.TaskAddress), graphics.Models, Pose, producer => current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer) });
                    return NativeBattleEffect83Controller.Write(current, target.TaskAddress, targetTask);
                case NativeBattleEffect83UpdateBounds8DC870 bounds:
                    NativeBattleModulePoolTask boundsTask = NativeBattleEffect83Controller.Read(current, bounds.TaskAddress);
                    int boundsActor = boundsTask.ReadByte(0x2D);
                    NativeBattlePoseSource source = Pose(boundsActor);
                    boundsTask = NativeBattleEffectModelHelpers.UpdateBounds8DC870(boundsTask, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[boundsActor].SequenceState ?? throw new NotSupportedException("Effect83 requires its target pose."), source));
                    return NativeBattleEffect83Controller.Write(current, bounds.TaskAddress, boundsTask);
                case NativeBattleEffect83Point502170 point:
                    int pointActor = Actor(point.ModelAddress);
                    NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[pointActor], Pose(pointActor), checked((byte)point.Selector), 0);
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer) };
                    NativeBattleVector vector = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
                    NativeBattleEffectArena result = NativeBattleEffect83Controller.ReadBytes(current, point.OutputAddress, 8);
                    result = result.Write(0, BitConverter.GetBytes(vector.X)).Write(2, BitConverter.GetBytes(vector.Y)).Write(4, BitConverter.GetBytes(vector.Z));
                    return NativeBattleEffect83Controller.WriteBytes(current, point.OutputAddress, result, 8);
                case NativeBattleEffect83UploadTim upload:
                    NativeBattleEffectHeapAllocation tim;
                    if (upload.Address == EmbeddedTimAddress)
                    {
                        tim = resources.EmbeddedTim;
                    }
                    else if (upload.Address == current.State.ReadDword(InitializerGlobal))
                    {
                        tim = new("effect83-main", NativeBattleEffect83Controller.ReadBytes(current, upload.Address, TextureLength).Read(0, TextureLength).ToImmutableArray());
                    }
                    else
                    {
                        throw new InvalidDataException("Effect83 selected an unsupported embedded TIM.");
                    }
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Effect83 requires its texture scheduler.");
                    return current with { Memory = current.Memory with { Textures = textures.EnqueueTim505E30(tim).State } };
                case NativeBattleEffect83RegisterCamera camera:
                    NativeBattleEffectTextureState registered = (current.Memory.Textures ?? throw new NotSupportedException("Effect83 requires its texture scheduler.")).Register5099A0(camera.Camera);
                    NativeCamera = Camera().RegisterControlResource(registered.Animation);
                    return current with { Memory = current.Memory with { Textures = registered } };
                case NativeBattleEffect83Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect83Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect83TextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionByteAddress, (address, value) => current = NativeBattleEffect83Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect83TargetFlags flags:
                    int flagActor = Actor(flags.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[flagActor];
                    ushort mask = flags.WordWidth ? flags.AndMask : (ushort)(0xFF00 | (byte)flags.AndMask);
                    graphics = graphics with { Models = graphics.Models.SetItem(flagActor, model with { Flags0 = (ushort)((model.Flags0 & mask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect83PublishMatrix1D99AB0 matrix:
                    return current with { Memory = current.Memory.PublishEffectLight1D99AB0(matrix.Matrix) };
                case NativeBattleEffect83DeliverBatch packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                case NativeBattleEffect83DrawOwnedModel owned:
                    NativeBattleEffectOwnedModelRenderInputs renderInputs = new(Camera().CapturePackedView1D97778(), Projection(), definition.NativeGraphics!.Rules.ShadowSource ?? throw new NotSupportedException("Effect83 requires its original shadow."), definition.NativeGraphics.PrimitiveCapabilities ?? throw new NotSupportedException("Effect83 requires primitive capabilities."), colors);
                    NativeBattleEffectOwnedModelRenderFrame render = new(current.Memory, NativeBattleEffect83Controller.Read(current, owned.TaskAddress), current.State.OwnedModels[owned.TaskAddress], resources.Model, current.State.ReadView(ProjectedAddress, ProjectedLength), ProjectedAddress, ProjectedLength, current.State.ReadDword(0x25CA07C), current.State.ReadDword(0x25C9F74));
                    NativeBattleEffectOwnedModelRenderStep drawn = NativeBattleEffectOwnedModelRendering.DrawNormal(render, current.Staging!, renderInputs);
                    current = current with { Memory = drawn.Frame.Memory, Staging = drawn.Staging, State = current.State.WriteView(ProjectedAddress, drawn.Frame.ProjectedScratch, ProjectedLength) };
                    current = NativeBattleEffect83Controller.Write(current, owned.TaskAddress, drawn.Frame.Task);
                    return Deliver(current, drawn.Batch, 0x25C9F74, 0);
                case NativeBattleEffect83TargetCapture79E590 capture:
                    return CaptureNativeEffect83Target(current, capture.TaskAddress, graphics);
                case NativeBattleEffect83TargetDraw79DA20 draw:
                    (graphics, current) = DrawNativeEffect83Target(graphics, current, draw.BodyAddress, Deliver);
                    return current;
                case NativeBattleEffect83StageTransition transition:
                    return ApplyNativeEffect83StageTransition(current, transition);
                default:
                    throw new NotSupportedException("Effect83 emitted an unsupported native operation.");
            }
        }
    }
}
