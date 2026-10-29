using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeCurse47(NativeBattleGraphicsState graphics) => RunNativeCurse47(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeCurse47(NativeBattleGraphicsState graphics) => RunNativeCurse47(graphics, false);

    private NativeBattleGraphicsState RunNativeCurse47(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Curse requires its selected original effect.");
        NativeBattleCurse47Resources resources = effect.Binding.Curse47Resources ?? throw new NotSupportedException("Curse requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Curse requires its retained process memory.");
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Curse requires its addressed file and temporary memory.");
        (memory, staging) = NativeBattleCurse47Memory.Prepare(memory, staging, resources);
        NativeBattleCurse47Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Curse requires its actual CRT stream."), memory.Curse47 ?? throw new NotSupportedException("Curse requires its retained module state.")) { Staging = staging };
        if (initialize)
        {
            frame = NativeBattleCurse47Controller.Initialize(memory, effect, frame.Random, nativeActionPackets, resources, frame.State) with { Staging = staging };
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        frame = NativeBattleCurse47Controller.Advance(frame, new(nativeActionPackets, _ => Camera().CapturePackedView1D97778(), Execute, AdvanceLeaf, resources.Sound));
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Curse requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Curse requires its current projection.");
        NativeBattleSummonColors Colors() => definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Curse requires its interpolation constants.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Curse requires its selected actor rig.");

        void Publish(NativeBattleCurse47Frame current)
        {
            NativeEffectMemory = current.Memory with { Curse47 = current.State };
            NativeFileStaging = RequireCurse47Staging(current);
            nativeCrtRandom = current.Random;
        }

        NativeBattleCurse47Frame Import(NativeBattleCurse47Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Curse external service lost its process memory.");
            return current with { Memory = latest, State = latest.Curse47 ?? throw new InvalidDataException("Curse external service lost its module state."), Staging = NativeFileStaging ?? throw new InvalidDataException("Curse external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Curse external service lost its CRT stream.") };
        }

        NativeBattleCurse47Frame ObserveAnchor(NativeBattleCurse47Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = RequireCurse47Staging(current);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleCurse47Frame Deliver(NativeBattleCurse47Frame current, NativeBattleGfGeometryBatch batch, bool common)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Curse drawing requires its pending ordering table.");
            if (common)
            {
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Curse drawing requires its COMMON process."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }
            uint cursor = current.State.ReadDword(0x264B3CC);
            NativeBattleEffectPacketWrite packets = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = packets.Ordering } };
            return current with { Memory = packets.Memory, State = current.State.WriteDword(0x264B3CC, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)packets.Cursor)) };
        }

        NativeBattleCurse47AnchorStep Anchor(NativeBattleCurse47Frame current, int actor, short selector, int distance)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), checked((byte)selector), checked((short)distance));
            NativeFileStaging = RequireCurse47Staging(current);
            current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer) };
            current = current with { Staging = NativeFileStaging };
            NativeBattleVector position = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, position);
        }

        NativeBattleCurse47Step AdvanceLeaf(NativeBattleCurse47Frame current, uint address)
        {
            uint callback = NativeBattleCurse47Controller.Read(current, address).Callback;
            switch (callback)
            {
                case NativeBattleCurse47Nodes.Callback:
                    return NativeBattleCurse47Nodes.Advance82DF60(current, address, resources.NodeSource, new(nativeActionPackets, actor => graphics.Models[actor], PoseSource, Projection(), Camera().Roll, Execute) { ObserveBone = (value, actor, selector) => Anchor(value, actor, selector, 0).Frame });
                case NativeBattleCurse47Act.FirstCallback:
                case NativeBattleCurse47Act.SecondCallback:
                case NativeBattleCurse47Act.MidpointCallback:
                    return NativeBattleCurse47Act.Advance(current, address, new((_, pointer) => resources.Act(pointer), (_, actor) => new(unchecked((byte)graphics.Models[actor].Flags0), graphics.Models[actor].ModelId4), Anchor, (value, task) => NativeBattleCurse47ActRendering.PrepareDraw(value, task, Camera().CapturePackedView1D97778(), NativeBattleEffectArena.Clear().MarkUnknown(0, 92), NativeBattleEffectArena.Clear().MarkUnknown(0, 32)), (value, task, context, node) => NativeBattleCurse47ActRendering.EmitNode(value, resources.Act(NativeBattleCurse47Controller.Read(value, task).ReadDword(0x94)), context, node, Projection(), Colors(), (output, batch) => Deliver(output, batch, true), pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(47, NativeBattleCurse47Controller.Read(value, task), node.ResourceOffset) with { ResourceGeneration = NativeBattleCurse47Controller.Read(value, task).ReadDword(0x94) })));
                case NativeBattleCurse47ParticleStage.Callback:
                    return NativeBattleCurse47ParticleStage.Advance(current, address, Execute);
                default:
                    throw new NotSupportedException($"Curse callback {callback:X8} has no original service.");
            }
        }

        NativeBattleCurse47Frame Execute(NativeBattleCurse47Frame current, NativeBattleCurse47Operation operation)
        {
            switch (operation)
            {
                case NativeBattleCurse47Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, 0, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleCurse47Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleCurse47QueueFile queue:
                    NativeBattleFileStaging files = RequireCurse47Staging(current);
                    (NativeBattleEffectMemory queuedMemory, NativeBattleFileIoState queuedIo) = NativeBattleCurse47Io.CopyAndQueueFile(current.State, current.Memory, NativeFileIo ?? throw new NotSupportedException("Curse requires its asynchronous file scheduler."), resources.File(checked((short)queue.Request.FileId)), (request, bytes) => files = files.Write(request.DestinationAddress, bytes.AsSpan()));
                    NativeFileIo = queuedIo;
                    return current with { Memory = queuedMemory, Staging = files };
                case NativeBattleCurse47TextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, checked(load.RootAddress + 0x62), (address, value) => current = WriteCurse47Completion(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleCurse47ObservedMode mode:
                    return NativeBattleCurse47Act.ObserveMode(current, mode.DirectorAddress, mode.Mode, nativeActionPackets);
                case NativeBattleCurse47Pause:
                    return current;
                case NativeBattleCurse47UpdateCaster caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleCurse47Controller.Read(current, caster.Address), current.Effect.Parameters!, graphics.Models, PoseSource, producer => current = ObserveAnchor(current, producer));
                    return NativeBattleCurse47Controller.Write(current, caster.Address, casterTask);
                case NativeBattleCurse47UpdateTargetBounds target:
                    NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleCurse47Controller.Read(current, target.Address), graphics.Models, PoseSource, producer => current = ObserveAnchor(current, producer));
                    int actor = task.ReadByte(0x2D);
                    NativeBattlePoseSource source = PoseSource(actor);
                    task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[actor].SequenceState ?? throw new NotSupportedException("Curse requires its actual target pose."), source));
                    return NativeBattleCurse47Controller.Write(current, target.Address, task);
                case NativeBattleCurse47InitializeTarget target:
                    return NativeBattleCurse47TargetSetup.Initialize82B0F0(current, target.Address, actor => graphics.Models[actor]);
                case NativeBattleCurse47StageDepthWrite depth:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Curse requires its stage depth owners.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = depth.Write.Fade, ColorFlags = depth.Write.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleCurse47NodePackets packets:
                    return Deliver(current, packets.Batch, packets.Common);
                default:
                    throw new NotSupportedException("Curse operation has no proved implementation.");
            }
        }
    }

    private static NativeBattleFileStaging RequireCurse47Staging(NativeBattleCurse47Frame frame) => frame.Staging ?? throw new NotSupportedException("Curse requires its addressed temporary memory.");
}
