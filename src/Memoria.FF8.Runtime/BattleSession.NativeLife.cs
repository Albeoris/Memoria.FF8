namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeLife(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Life-family entry requires its selected effect.");
        NativeBattleLifeResources resources = effect.Binding.LifeResources ?? throw new NotSupportedException("Native Life-family entry requires its authenticated original resources.");
        NativeBattleLifeFrame frame = NativeBattleLifeController.Initialize8649C0(NativeEffectMemory ?? throw new NotSupportedException("Native Life requires its retained571870 memory."), effect, nativeActionPackets, resources);
        NativeEffectMemory = frame.Memory;
        NativeBattleLifeProfile profile = NativeBattleLifeProfile.ForEntry(effect.Binding.Entry);
        if (profile.RegistersAnimation && (effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Life requires its actual camera owner.")) with { ControlResource = frame.Memory.Textures!.Animation, ControlCursor = 0 };
        }
        return graphics with { OriginalEffect = frame.Effect with { Life = frame.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeLife(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Life callback requires its selected effect.");
        NativeBattleLifeResources resources = effect.Binding.LifeResources ?? throw new NotSupportedException("Native Life callback requires its original resources.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Life requires its current camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Life requires its prior published projection.");
        NativeBattleLifeFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Life requires its retained effect memory."), effect, effect.Life ?? throw new NotSupportedException("Native Life requires its executed entry state."));
        NativeBattleLifeInputs inputs = new(nativeActionPackets, projection, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Life requires the target's actual pose source."), nativeCrtRandom ?? throw new NotSupportedException("Native Life requires its55CBD2 random owner."), Execute)
        {
            CameraRoll = camera.Roll,
            ApplyContact = ApplyContact
        };
        NativeBattleLifeStep step = NativeBattleLifeController.Advance864AB0(frame, resources, inputs);
        NativeEffectMemory = step.Frame.Memory;
        nativeCrtRandom = step.Random;
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        return graphics with { OriginalEffect = step.Frame.Effect with { Life = step.Frame.State } };

        NativeBattleLifeFrame ApplyContact(NativeBattleLifeFrame current, int index)
        {
            NativeEffectMemory = current.Memory;
            var pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Life contact lost its effect memory.") };
        }

        NativeBattleLifeFrame Execute(NativeBattleLifeFrame current, NativeBattleModulePacketBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Life requires its initialized original OT.");
            int cursor = current.State.DrawCursor ?? throw new NotSupportedException("Native Life packets require the current864AB0/8D4530 draw-buffer selection.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch, NativeBattleEffectGouraudWrites.RgbBytes);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeEffectMemory = written.Memory;
            return current with { Memory = written.Memory, State = current.State with { DrawCursor = written.Cursor } };
        }
    }
}
