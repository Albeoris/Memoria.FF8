using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeActorDirector(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("The actor director requires its selected original effect.");
        NativeBattleActorDirectorResources resources = effect.Binding.ActorDirectorResources ?? throw new NotSupportedException("The actor director requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("The actor director requires its retained process owners.");
        NativeBattleActorDirectorState state = memory.RequireActorDirector(effect.Binding.Effect);
        NativeBattleActorDirectorFrame frame = new(memory, effect, state);
        if (initialize)
        {
            frame = NativeBattleActorDirectorController.Initialize(frame, resources, nativeActionPackets);
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("The actor director requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("The actor director requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("The actor director requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("The actor director requires its current view matrix.");
        Func<int, NativeBattleGraphicsModel> readModel = index => graphics.Models[index];
        NativeBattleActorDirectorInputs inputs = new(nativeActionPackets, view, readModel, PoseSource, Execute, AdvanceGeneric) { PackedCamera = camera.CapturePackedView1D97778(), CopiedCameraPadding = camera.CanonicalViewPadding, AdvanceOverlay = AdvanceOverlay, StartVibration = StartVibration };
        frame = NativeBattleActorDirectorController.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("The actor director requires its actual posed model source.");

        void Publish(NativeBattleActorDirectorFrame current)
        {
            NativeEffectMemory = current.Memory.WithActorDirector(current.State);
        }

        NativeBattleActorDirectorFrame Execute(NativeBattleActorDirectorFrame current, NativeBattleActorDirectorOperation operation)
        {
            switch (operation)
            {
                case NativeBattleActorDirectorStopVibration stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("The actor director requires its retained vibration queue.")).Stop4A2940(stop.Handle);
                    return current;
                case NativeBattleActorDirectorSound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleActorDirectorContact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("The actor director contact lost its canonical process memory.") };
                case NativeBattleActorDirectorStage fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("The actor director requires its original four stage groups.");
                    if (stage.Groups.IsDefault || stage.Groups.Length != 4)
                    {
                        throw new InvalidDataException("The actor director fade requires all four original stage groups.");
                    }
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Depth, ColorFlags = fade.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleActorDirectorGenericTask:
                    return current;
                default:
                    throw new NotSupportedException("The actor director operation requires its original immediate owner.");
            }
        }

        NativeBattleActorDirectorVibrationStep StartVibration(NativeBattleActorDirectorFrame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("The actor director requires its retained vibration queue.")).Enqueue(resources.Vibration ?? throw new NotSupportedException("The actor director requires its original vibration resource."), 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, unchecked((short)queued.Handle));
        }

        NativeBattleActorDirectorLeafStep AdvanceOverlay(NativeBattleActorDirectorFrame current, uint address)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("The actor director overlay requires its current ordering table.");
            NativeBattleFullscreenOverlayStep advanced = NativeBattleFullscreenOverlay.Advance8DDC30(new(current.Memory, current.Effect.Pools, primitive.Pending), address, current.Memory.FullscreenOverlayPause2795914 ?? throw new NotSupportedException("The actor director overlay pause owner is unknown."), resources.Overlay ?? throw new NotSupportedException("The actor director requires its original fullscreen programme."), current.State.Effect == 82 ? NativeBattlePilotVisualLifetimes.Identity(82, new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address)) : null);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = advanced.Frame.Ordering } };
            return new(current with { Memory = advanced.Frame.Memory, Effect = current.Effect with { Pools = advanced.Frame.Pools } }, advanced.Result);
        }

        NativeBattleActorDirectorLeafStep AdvanceGeneric(NativeBattleActorDirectorFrame current, uint address)
        {
            NativeBattleCuragaControllerInputs genericInputs = new(nativeActionPackets, view, readModel, PoseSource);
            NativeBattleCuragaLeafStep advanced = NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(current.ToGeneric(), address, resources.GenericNodes(), genericInputs, new(AdvanceNodes));
            return new(current.WithGeneric(advanced.Frame), advanced.Result);
        }

        NativeBattleCuragaFrame AdvanceNodes(NativeBattleCuragaFrame current, uint address)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("The actor director requires its current ordering table.");
            NativeBattleCuragaNodePresentationStep drawn = NativeBattleCuragaNodeKinematics.AdvanceAndDraw87EC80(current, address, nativeCrtRandom ?? throw new NotSupportedException("The actor director requires its retained CRT random owner."), readModel, projection, camera.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
            nativeCrtRandom = drawn.Random;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = drawn.Ordering } };
            return drawn.Frame;
        }
    }
}
