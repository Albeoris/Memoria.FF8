namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDouble8D5080(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native8D5080 requires its selected effect.");
        NativeBattleDoubleResources resources = effect.Binding.DoubleResources ?? throw new NotSupportedException("Native8D5080 requires its original executable resources.");
        NativeBattleDoubleFrame frame = NativeBattleDoubleController.Initialize8D5080(NativeEffectMemory ?? throw new NotSupportedException("Native8D5080 requires retained effect memory."), effect, nativeActionPackets, resources);
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Double requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect with { Double = frame.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeDouble8D5180(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Double callback requires its selected effect.");
        NativeBattleDoubleResources resources = effect.Binding.DoubleResources ?? throw new NotSupportedException("Native Double callback requires its original resources.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Double requires its actual camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Double requires its prior published projection.");
        NativeBattleDoubleFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Double requires retained effect memory."), effect, effect.Double ?? throw new NotSupportedException("Native Double requires its executed8D5080 state."));
        NativeBattleDoubleInputs inputs = new(nativeActionPackets, projection, camera.Roll, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native572200 requires authenticated interpolation constants."), index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Double requires its actual target pose source."), Execute) { ApplyContact = ApplyContact };
        NativeBattleDoubleStep step = NativeBattleDoubleController.Advance8D5180(frame, resources, inputs, nativeCrtRandom ?? throw new NotSupportedException("Native Double requires its55CBD2 random owner."));
        NativeEffectMemory = step.Frame.Memory;
        nativeCrtRandom = step.Random;
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        return graphics with { OriginalEffect = step.Frame.Effect with { Double = step.Frame.State } };

        NativeBattleDoubleFrame ApplyContact(NativeBattleDoubleFrame current, int index)
        {
            NativeEffectMemory = current.Memory;
            var pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Double contact lost process memory.") };
        }

        NativeBattleDoubleFrame Execute(NativeBattleDoubleFrame current, NativeBattleDoubleOperation operation)
        {
            NativeEffectMemory = current.Memory;
            if (operation is NativeBattleDoubleFlash)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 2, 128) };
                return current;
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Double requires its actual pending ordering table.");
            int cursor = current.State.DrawCursor ?? throw new NotSupportedException("Native Double requires its executed8D5180 draw-buffer cursor.");
            NativeBattleEffectPacketWrite written = operation switch
            {
                NativeBattleDoublePackets packets => NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, packets.Batch, flags20: packets.Flags20),
                NativeBattleDoubleSprite sprite => NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, camera.Roll, 17, 2), NativeBattleEffectGouraudWrites.RgbBytes),
                _ => throw new NotSupportedException("Native Double operation lacks its original immediate implementation.")
            };
            NativeEffectMemory = written.Memory;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State with { DrawCursor = written.Cursor } };
        }
    }
}
