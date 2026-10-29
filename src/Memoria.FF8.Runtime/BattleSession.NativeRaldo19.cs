using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeRaldo19(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Raldo requires its selected original effect.");
        NativeBattleRaldo19Resources resources = effect.Binding.Raldo19Resources ?? throw new NotSupportedException("Raldo requires its original module resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Raldo requires its actual process memory.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Raldo requires its actual CRT stream.");
        NativeBattleRaldo19Frame frame = new(memory, effect, random, memory.RequireRaldo19());
        if (initialize)
        {
            frame = NativeBattleRaldo19Controller.Initialize(memory, effect, random, nativeActionPackets, resources, frame.State);
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Raldo requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Raldo requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Raldo requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Raldo requires its actual view matrix.");
        Func<int, NativeBattleGraphicsModel> readModel = actor => graphics.Models[actor];
        NativeBattleRaldo19Inputs inputs = new(nativeActionPackets, view, readModel, PoseSource, Execute, AdvanceLeaf) { PackedCamera = camera.CapturePackedView1D97778(), CameraPadding18 = camera.CanonicalViewPadding };
        frame = NativeBattleRaldo19Controller.Advance(frame, resources, inputs);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Raldo requires its actual current model rig.");

        void Publish(NativeBattleRaldo19Frame current)
        {
            NativeEffectMemory = current.Memory.WithRaldo19(current.State);
            nativeCrtRandom = current.Random;
        }

        NativeBattleRaldo19Frame Execute(NativeBattleRaldo19Frame current, NativeBattleRaldo19Operation operation)
        {
            switch (operation)
            {
                case NativeBattleRaldo19Sound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleRaldo19Contact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Raldo contact lost its process memory.") };
                default:
                    throw new NotSupportedException("Raldo requires its original immediate operation owner.");
            }
        }

        NativeBattleRaldo19LeafStep AdvanceLeaf(NativeBattleRaldo19Frame current, NativeBattleRaldo19Operation operation)
        {
            switch (operation)
            {
                case NativeBattleRaldo19GenericTask generic:
                    NativeCrtRandomState genericRandom = current.Random;
                    NativeBattleCuragaControllerInputs genericInputs = new(nativeActionPackets, view, readModel, PoseSource)
                    {
                        ObserveBoneAnchor = (nodes, actor, selector) => nodes with { Memory = ObserveNativeBoneAnchor(nodes.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, 0)) }
                    };
                    NativeBattleCuragaLeafStep advanced = NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(current.ToGeneric(), generic.Address, resources.GenericNodes(), genericInputs, new(AdvanceNodes));
                    return new(current.WithGeneric(advanced.Frame) with { Random = genericRandom }, advanced.Result);

                    NativeBattleCuragaFrame AdvanceNodes(NativeBattleCuragaFrame nodes, uint address)
                    {
                        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Raldo generic nodes require their ordering table.");
                        NativeBattleCuragaNodePresentationStep drawn = NativeBattleCuragaNodeKinematics.AdvanceAndDraw87EC80(nodes, address, genericRandom, readModel, projection, camera.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
                        genericRandom = drawn.Random;
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = drawn.Ordering } };
                        return drawn.Frame;
                    }
                case NativeBattleRaldo19BallTask ball:
                    return NativeBattleRaldo19BallController.Advance(current, ball.Address, resources, new(actor => NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]), (drawing, emittedOperation) => ExecuteBall(drawing, emittedOperation, NativeBattlePilotVisualLifetimes.Identity(19, new NativeBattleEffectHierarchy(drawing.Effect.Pools).Read(ball.Address))), SelectActors));
                case NativeBattleRaldo19DebrisTask debris:
                    return NativeBattleRaldo19Debris.Advance(current, debris.Address, nativeActionPackets, DrawDebris);
                case NativeBattleRaldo19OverlayTask overlay:
                    NativeBattlePrimitiveFrame overlayPrimitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Raldo overlay requires its ordering table.");
                    NativeBattleFullscreenOverlayStep overlayStep = NativeBattleFullscreenOverlay.Advance8DDC30(new(current.Memory, current.Effect.Pools, overlayPrimitive.Pending), overlay.Address, current.Memory.FullscreenOverlayPause2795914 ?? throw new NotSupportedException("Raldo requires its actual fullscreen pause owner."), resources.Overlay);
                    graphics = graphics with { PrimitiveFrame = overlayPrimitive with { Pending = overlayStep.Frame.Ordering } };
                    return new(current with { Memory = overlayStep.Frame.Memory, Effect = current.Effect with { Pools = overlayStep.Frame.Pools } }, overlayStep.Result);
                default:
                    throw new NotSupportedException("Raldo requires its original child operation owner.");
            }
        }

        NativeBattleEffectTaskActors SelectActors(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(effect.Parameters!, nativeActionPackets, pointer, group, target);

        NativeBattleRaldo19Frame DrawDebris(NativeBattleRaldo19Frame current, NativeBattleRaldo19DebrisSprite sprite)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Raldo debris requires its retained temporary memory."), NativeBattleRaldo19DebrisRendering.TemporaryLength);
            NativeBattleEffectArena bounded = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, NativeBattleRaldo19DebrisRendering.TemporaryLength);
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(sprite.TaskAddress);
            NativeBattleRaldo19DebrisHead head = NativeBattleRaldo19DebrisRendering.PrepareHead(current, task, camera.CapturePackedView1D97778(), bounded);
            NativeBattleRaldo19DebrisDraw drawn = NativeBattleRaldo19DebrisRendering.Draw(head, resources, projection, camera.Roll, 17, NativeBattlePilotVisualLifetimes.Identity(19, task));
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Raldo debris requires its current ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(drawn.Frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Raldo debris requires its allocated common packet owner."), primitive.Pending, drawn.Batch);
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.TemporaryD8, NativeBattleRaldo19DebrisRendering.TemporaryLength);
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, NativeBattleRaldo19DebrisRendering.TemporaryLength);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return drawn.Frame with { Memory = drawn.Frame.Memory with { PrimitiveProcess = written.Process } };
        }

        NativeBattleRaldo19Frame ExecuteBall(NativeBattleRaldo19Frame current, NativeBattleRaldo19BallOperation operation, NativeBattlePilotVisualIdentity identity)
        {
            switch (operation)
            {
                case NativeBattleRaldo19BallActorWrite write:
                    NativeBattleGraphicsModel actor = NativeBattleRaldo19ModelMemory.Apply(graphics.Models[write.Actor], write.Offset, write.Source, write.SourceOffset, write.Length);
                    graphics = graphics with { Models = graphics.Models.SetItem(write.Actor, actor) };
                    return current;
                case NativeBattleRaldo19BallSound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleRaldo19BallPivot pivot:
                    NativeBattleGraphicsModel model = graphics.Models[pivot.Actor];
                    NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Raldo pivot requires the actor's actual packed position.");
                    NativeBattleGraphicsModel local = model with { Transform = transform with { Position28 = new(0, 0, 0) }, GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() };
                    return CaptureAnchor(current, local, pivot.Actor, 0x74);
                case NativeBattleRaldo19BallTargetAnchor target:
                    return CaptureAnchor(current, graphics.Models[target.Actor], target.Actor, 0x84);
                case NativeBattleRaldo19BallRender render:
                    (graphics, current) = DrawNativeRaldo19Ball(graphics, current, render.Actor, identity);
                    return current;
                default:
                    throw new NotSupportedException("Raldo ball requires its original model service owner.");
            }
        }

        NativeBattleRaldo19Frame CaptureAnchor(NativeBattleRaldo19Frame current, NativeBattleGraphicsModel model, int actor, int destination)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(actor), 241, 0);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector position;
            if (producer.Enabled)
            {
                NativeBattleScriptedColorRegisters registers = observed.ScriptedColorRegisters ?? throw new NotSupportedException("Raldo anchor requires its actual geometry outputs.");
                position = new(unchecked((short)(registers.Mac[0] ?? throw new InvalidDataException("Raldo anchor did not publish X."))), unchecked((short)(registers.Mac[1] ?? throw new InvalidDataException("Raldo anchor did not publish Y."))), unchecked((short)(registers.Mac[2] ?? throw new InvalidDataException("Raldo anchor did not publish Z."))));
            }
            else
            {
                position = producer.DisabledPosition;
            }
            byte[] xyz = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(xyz, position.X);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), position.Y);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), position.Z);
            return current with { Memory = observed, State = current.State with { BallC4 = current.State.BallC4.Write(destination, xyz) } };
        }
    }
}
