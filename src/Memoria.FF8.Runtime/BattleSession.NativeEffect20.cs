using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect20(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect20 requires its selected original effect.");
        NativeBattleEffect20Resources resources = effect.Binding.Effect20Resources ?? throw new NotSupportedException("Effect20 requires its original module resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect20 requires its actual process memory.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Effect20 requires its actual CRT stream.");
        NativeBattleEffect20Frame frame = new(memory, effect, random, memory.RequireEffect20());
        if (initialize)
        {
            frame = NativeBattleEffect20Controller.Initialize(memory, effect, random, nativeActionPackets, resources, frame.State);
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect20 requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect20 requires its current camera.");
        NativeBattleEffect20Inputs inputs = new(nativeActionPackets, camera.CapturePackedView1D97778(), actor => graphics.Models[actor], PoseSource, Execute, AdvanceOverlay);
        frame = NativeBattleEffect20Controller.Advance(frame, resources, inputs);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect20 requires its actual current model rig.");

        void Publish(NativeBattleEffect20Frame current)
        {
            NativeEffectMemory = current.Memory.WithEffect20(current.State);
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect20Frame Execute(NativeBattleEffect20Frame current, NativeBattleEffect20Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect20Sound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleEffect20Contact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Effect20 contact lost its process memory.") };
                case NativeBattleEffect20StageInitialize:
                    NativeBattleStageState stage = RequireStage();
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = 0, ColorFlags = group.ColorFlags & 0xFF000000 }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect20StageFade fade:
                    stage = RequireStage();
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Value }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect20ClearActorFlags124Bit2 clear:
                    NativeBattleGraphicsModel actor = graphics.Models[clear.Actor];
                    NativeBattleSequenceState sequence = actor.SequenceState ?? throw new NotSupportedException("Effect20 requires its current actor flags.");
                    graphics = graphics with { Models = graphics.Models.SetItem(clear.Actor, actor with { SequenceState = sequence with { MeshMask124 = sequence.MeshMask124 & ~2u } }) };
                    return current;
                case NativeBattleEffect20VibrationEnqueue vibration:
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect20 requires its retained vibration process.")).Enqueue(resources.Vibration.Bank, 1, 128);
                    NativeVibrationProcess = queued.State;
                    NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
                    hierarchy = hierarchy.Write(vibration.Address, hierarchy.Read(vibration.Address).WriteWord(0x74, unchecked((ushort)queued.Handle)));
                    return current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
                case NativeBattleEffect20VibrationStop vibration:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect20 requires its retained vibration process.")).Stop4A2940(vibration.Handle);
                    return current;
                case NativeBattleEffect20RingSequence ring:
                    if (ring.Address != 0x1D97668)
                    {
                        throw new NotSupportedException("Effect20 requires its original actor ring owner.");
                    }
                    graphics = ScheduleNativeRingSequence505C00(graphics, 6, ring.Count);
                    return current;
                case NativeBattleEffect20Activate activation:
                    graphics = QueueNativeDescriptor(graphics, 113, 0x487670, checked((ushort)activation.Actor));
                    return current;
                case NativeBattleEffect20BodyDraw body:
                    (graphics, current) = DrawNativeEffect20Body(graphics, current, body.Address);
                    return current;
                case NativeBattleEffect20SpriteDraw sprite:
                    (graphics, current) = DrawNativeEffect20Sprite(graphics, current, sprite.Address);
                    return current;
                case NativeBattleEffect20ParticleDraw particle:
                    (graphics, current) = DrawNativeEffect20Particle(graphics, current, particle.Address);
                    return current;
                default:
                    throw new NotSupportedException("Effect20 requires its original immediate operation owner.");
            }
        }

        NativeBattleStageState RequireStage()
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect20 requires its four actual stage groups.");
            if (stage.Groups.Length != 4)
            {
                throw new InvalidDataException("Effect20 requires four physical stage descriptors.");
            }
            return stage;
        }

        NativeBattleEffect20LeafStep AdvanceOverlay(NativeBattleEffect20Frame current, uint address)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect20 overlay requires its ordering table.");
            NativeBattleFullscreenOverlayStep advanced = NativeBattleFullscreenOverlay.Advance8DDC30(new(current.Memory, current.Effect.Pools, primitive.Pending), address, current.Memory.FullscreenOverlayPause2795914 ?? throw new NotSupportedException("Effect20 requires its actual fullscreen pause owner."), resources.Overlay);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = advanced.Frame.Ordering } };
            return new(current with { Memory = advanced.Frame.Memory, Effect = current.Effect with { Pools = advanced.Frame.Pools } }, advanced.Result);
        }
    }
}
