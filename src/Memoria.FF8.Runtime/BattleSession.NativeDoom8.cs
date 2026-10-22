using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDoom8(NativeBattleGraphicsState graphics) => RunNativeDoom8(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeDoom8(NativeBattleGraphicsState graphics) => RunNativeDoom8(graphics, false);

    private NativeBattleGraphicsState RunNativeDoom8(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Doom requires its selected original effect.");
        NativeBattleDoom8Resources resources = effect.Binding.Doom8Resources ?? throw new NotSupportedException("Doom requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Doom requires its retained process memory.");
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Doom requires its addressed file and temporary memory.");
        (memory, staging) = NativeBattleDoom8Memory.Prepare(memory, staging, resources);
        NativeBattleDoom8Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Doom requires its actual CRT stream."), memory.Doom8 ?? throw new NotSupportedException("Doom requires its retained module state.")) { Staging = staging };
        if (initialize)
        {
            frame = NativeBattleDoom8Controller.Initialize(memory, effect, frame.Random, nativeActionPackets, resources, frame.State) with { Staging = staging };
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Doom requires its camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        frame = NativeBattleDoom8Controller.Advance(frame, new(nativeActionPackets, _ => Camera().CapturePackedView1D97778(), Execute, AdvanceLeaf, resources.DirectorSound));
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Doom requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Doom requires its current projection.");
        NativeBattleSummonColors Colors() => definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Doom requires its interpolation constants.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Doom requires its actual selected actor rig.");
        NativeBattleEffectTaskActors SelectActors(uint parameters, sbyte header, sbyte target) => NativeBattleCureController.SelectActors(effect.Parameters!, nativeActionPackets, parameters, header, target);

        void Publish(NativeBattleDoom8Frame current)
        {
            NativeEffectMemory = current.Memory with { Doom8 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Doom lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleDoom8Frame Import(NativeBattleDoom8Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Doom external service lost its process memory.");
            return current with { Memory = latest, State = latest.Doom8 ?? throw new InvalidDataException("Doom external service lost its module owner."), Staging = NativeFileStaging ?? throw new InvalidDataException("Doom external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Doom external service lost its CRT stream.") };
        }

        void RequireTargetPointer(NativeBattleDoom8Frame current)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(RequireDoom8Staging(current).Read(0x1636C04, 4));
            if (pointer != NativeBattleDoom8Strips.TargetAddress)
            {
                throw new NotSupportedException("Doom strips require their actual retained target allocation.");
            }
        }

        NativeBattleDoom8Frame ObserveAnchor(NativeBattleDoom8Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = RequireDoom8Staging(current);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleDoom8Frame Deliver(NativeBattleDoom8Frame current, NativeBattleGfGeometryBatch batch, bool common)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Doom drawing requires its pending ordering table.");
            if (common)
            {
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Doom drawing requires its COMMON packet process."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }
            uint cursor = current.State.ReadDword(0x2758504);
            NativeBattleEffectPacketWrite packets = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = packets.Ordering } };
            return current with { Memory = packets.Memory, State = current.State.WriteDword(0x2758504, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)packets.Cursor)) };
        }

        NativeBattleDoom8Step AdvanceStrips(NativeBattleDoom8Frame current)
        {
            RequireTargetPointer(current);
            return NativeBattleDoom8Strips.AdvanceStrips8C84A0(current, resources.RequireStripTables(), Camera().CapturePackedView1D97778(), Projection(), (value, batch) => Deliver(value, batch, true));
        }

        NativeBattleDoom8Step AdvanceLeaf(NativeBattleDoom8Frame current, uint address)
        {
            uint callback = NativeBattleDoom8Controller.Read(current, address).Callback;
            switch (callback)
            {
                case NativeBattleDoom8Nodes.Callback:
                    return NativeBattleDoom8Nodes.Advance8C88D0(current, address, resources.NodeSource, new(nativeActionPackets, actor => graphics.Models[actor], PoseSource, Projection(), Camera().Roll, Colors(), Execute));
                case NativeBattleDoom8Reaper.Callback:
                    NativeBattleSummonColors sourceColors = Colors();
                    return NativeBattleDoom8Reaper.Advance(current, address, new(sourceColors.Transition, sourceColors.Normal, (value, model) => NativeBattleDoom8Models.Advance(value, model, resources.ReaperModel, false), Execute));
                case 0x8C58B0:
                case 0x8C8050:
                case 0x8C8790:
                    return NativeBattleDoom8Act.Advance(current, address, new(ReadAct, (value, task) => NativeBattleDoom8ActRendering.Prepare8C59E0(value, task, Camera().CapturePackedView1D97778(), NativeBattleEffectArena.Clear().MarkUnknown(0, 92), NativeBattleEffectArena.Clear().MarkUnknown(0, 32)), (value, task, context, node) => NativeBattleDoom8ActRendering.DrawNode8C5B10(value, ReadAct(value, NativeBattleDoom8Controller.Read(value, task).ReadDword(0x94)), context, node, Projection(), Colors(), (output, batch) => Deliver(output, batch, false)), Execute, AdvanceStrips, index => resources.RequirePlacementSelectors()[index], SelectActors));
                case 0x8CD4B0:
                case 0x8CD670:
                case 0x8CDED0:
                case 0x8CDFB0:
                    return NativeBattleDoom8Particles.Advance(current, address, nativeActionPackets, Execute);
                case 0x8DDC30:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Doom overlay requires its pending ordering table.");
                    NativeBattleFullscreenOverlayStep overlay = NativeBattleFullscreenOverlay.Advance8DDC30(new(current.Memory, current.Effect.Pools, primitive.Pending), address, current.Memory.FullscreenOverlayPause2795914 ?? throw new NotSupportedException("Doom overlay requires its retained pause owner."), new(0x163ACB4, resources.RequireOverlayColors()));
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = overlay.Frame.Ordering } };
                    return new(current with { Memory = overlay.Frame.Memory, Effect = current.Effect with { Pools = overlay.Frame.Pools } }, overlay.Result);
                default:
                    throw new NotSupportedException($"Doom callback {callback:X8} has no original service.");
            }
        }

        NativeBattleGfAnimationSource ReadAct(NativeBattleDoom8Frame current, uint pointer)
        {
            for (short id = 383; id <= 385; id++)
            {
                int index = id - 383;
                if (pointer == BinaryPrimitives.ReadUInt32LittleEndian(current.State.ReadIo().Control.AsSpan().Slice(index * 4)))
                {
                    NativeBattleEffectFileAsset file = resources.File(id);
                    return new(pointer, RequireDoom8Staging(current).Read(pointer, file.Data.Length).ToImmutableArray());
                }
            }
            throw new NotSupportedException("Doom ACT requires a loaded source selected by its actual IO directory.");
        }

        NativeBattleDoom8Frame Execute(NativeBattleDoom8Frame current, NativeBattleDoom8Operation operation)
        {
            return ExecuteDoom8(operation, current);
        }

        NativeBattleDoom8Frame ExecuteDoom8(NativeBattleDoom8Operation operation, NativeBattleDoom8Frame current)
        {
            switch (operation)
            {
                case NativeBattleDoom8Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(sound.Sound);
                    return Import(current);
                case NativeBattleDoom8Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleDoom8UploadTim upload:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Doom requires its texture scheduler.");
                    int timIndex = upload.Address switch
                    {
                        0x181CFAC => 0,
                        0x18253CC => 1,
                        _ => throw new NotSupportedException("Doom director selected an unsupported original TIM.")
                    };
                    textures = textures.EnqueueTim505E30(resources.DirectorTims[timIndex]).State;
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleDoom8QueueFile queue:
                    NativeBattleFileStaging files = RequireDoom8Staging(current);
                    (NativeBattleEffectMemory queuedMemory, NativeBattleFileIoState queuedIo) = NativeBattleDoom8Io.CopyAndQueueFile(current.State.ReadIo(), current.Memory, NativeFileIo ?? throw new NotSupportedException("Doom requires its asynchronous file scheduler."), resources.File(checked((short)queue.FileId)), (request, bytes) => files = files.Write(request.DestinationAddress, bytes.AsSpan()));
                    NativeFileIo = queuedIo;
                    return current with { Memory = queuedMemory, Staging = files };
                case NativeBattleDoom8TextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, checked(load.RootAddress + 0x62), (address, value) => current = WriteDoom8Completion(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleDoom8ModeChanged mode:
                    return NativeBattleDoom8Fronts.SpawnObservedMode(current, mode.DirectorAddress, nativeActionPackets, Colors().Normal, Execute);
                case NativeBattleDoom8UpdateCaster caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleDoom8Controller.Read(current, caster.Address), current.Effect.Parameters!, graphics.Models, PoseSource, producer => current = ObserveAnchor(current, producer));
                    return NativeBattleDoom8Controller.Write(current, caster.Address, casterTask);
                case NativeBattleDoom8UpdateTargetBounds target:
                    NativeBattleModulePoolTask targetTask = NativeBattleDoom8Controller.Read(current, target.Address);
                    targetTask = NativeBattleEffectModelHelpers.UpdateTarget8DC610(targetTask, graphics.Models, PoseSource, producer => current = ObserveAnchor(current, producer));
                    int actor = targetTask.ReadByte(0x2D);
                    NativeBattlePoseSource source = PoseSource(actor);
                    targetTask = NativeBattleEffectModelHelpers.UpdateBounds8DC870(targetTask, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[actor].SequenceState ?? throw new NotSupportedException("Doom requires its target pose."), source));
                    return NativeBattleDoom8Controller.Write(current, target.Address, targetTask);
                case NativeBattleDoom8InitializeTarget target:
                    return NativeBattleDoom8TargetSetup.Initialize8C52C0(current, target.Address, actor => graphics.Models[actor]);
                case NativeBattleDoom8StageDepthWrite depth:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Doom requires its stage depth owners.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = depth.Write.Fade, ColorFlags = depth.Write.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleDoom8NodePackets packets:
                    return Deliver(current, packets.Batch, packets.Common);
                case NativeBattleDoom8DrawDust dust:
                    return NativeBattleDoom8ParticleDrawing.DrawDust(current, dust.Address, resources, Camera().CapturePackedView1D97778(), Projection(), Camera().Roll, Deliver);
                case NativeBattleDoom8DrawSmoke smoke:
                    return NativeBattleDoom8ParticleDrawing.DrawSmoke(current, smoke.Address, resources, Camera().CapturePackedView1D97778(), Projection(), Colors(), Deliver);
                case NativeBattleDoom8ActInitializeTarget target:
                    RequireTargetPointer(current);
                    return NativeBattleDoom8Strips.InitializeTarget8C8120(current, target.TaskAddress, target.Actor, resources.RequireStripTables(), Execute);
                case NativeBattleDoom8ActPlace place:
                    RequireTargetPointer(current);
                    return NativeBattleDoom8Strips.Place8C8350(current, place.TaskAddress, place.Selector, resources.RequireStripTables());
                case NativeBattleDoom8StripLookBasis basis:
                    return ObserveDoom8Basis(current, basis.Direction, basis.Up);
                case NativeBattleDoom8StripAnchor anchor:
                    NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[anchor.Actor], PoseSource(anchor.Actor), 0xF1, 0);
                    current = ObserveDoom8Anchor(current, producer);
                    NativeBattleVector position = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
                    byte[] xyz = new byte[6];
                    BinaryPrimitives.WriteInt16LittleEndian(xyz, position.X);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), position.Y);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), position.Z);
                    return current with { State = current.State.Write(anchor.Destination, xyz) };
                default:
                    return ExecuteDoom8Model(current, operation, resources, Camera(), Deliver);
            }
        }
    }

    private static NativeBattleFileStaging RequireDoom8Staging(NativeBattleDoom8Frame frame) => frame.Staging ?? throw new NotSupportedException("Doom requires its addressed temporary memory.");
}
