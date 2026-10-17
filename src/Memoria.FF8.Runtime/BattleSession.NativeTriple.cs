namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeTriple89DEF0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native89DEF0 requires its selected effect.");
        NativeBattleTripleResources resources = effect.Binding.TripleResources ?? throw new NotSupportedException("Native89DEF0 requires its original executable resources.");
        NativeBattleTripleFrame frame = NativeBattleTripleController.Initialize89DEF0(NativeEffectMemory ?? throw new NotSupportedException("Native89DEF0 requires retained effect memory."), effect, nativeActionPackets, resources);
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Triple requires its current camera owner.")) with { ControlResource = frame.Memory.Textures!.Animation, ControlCursor = 0 };
        }
        return graphics with { OriginalEffect = frame.Effect with { Triple = frame.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeTriple89DFF0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Triple callback requires its selected effect.");
        NativeBattleTripleResources resources = effect.Binding.TripleResources ?? throw new NotSupportedException("Native Triple callback requires its original resources.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Triple requires its actual camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Triple requires its prior published projection.");
        NativeBattleTripleFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Triple requires retained effect memory."), effect, effect.Triple ?? throw new NotSupportedException("Native Triple requires its executed89DEF0 state."));
        NativeBattleTripleInputs inputs = new(nativeActionPackets, projection, camera.Roll, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native572200 requires authenticated interpolation constants."), index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Triple requires its actual target pose source."), Execute) { ApplyContact = ApplyContact };
        NativeBattleTripleStep step = NativeBattleTripleController.Advance89DFF0(frame, resources, inputs, nativeCrtRandom ?? throw new NotSupportedException("Native Triple requires its55CBD2 random owner."));
        NativeEffectMemory = step.Frame.Memory;
        nativeCrtRandom = step.Random;
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        return graphics with { OriginalEffect = step.Frame.Effect with { Triple = step.Frame.State } };

        NativeBattleTripleFrame ApplyContact(NativeBattleTripleFrame current, int index)
        {
            NativeEffectMemory = current.Memory;
            var pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Triple contact lost process memory.") };
        }

        NativeBattleTripleFrame Execute(NativeBattleTripleFrame current, NativeBattleTripleOperation operation)
        {
            NativeEffectMemory = current.Memory;
            if (operation is NativeBattleTripleFlash)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 2, 128) };
                return current;
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Triple requires its actual pending ordering table.");
            int cursor = current.State.DrawCursor ?? throw new NotSupportedException("Native Triple requires its executed89DFF0 draw-buffer cursor.");
            NativeBattleEffectPacketWrite written = operation switch
            {
                NativeBattleTriplePackets packets => NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, packets.Batch, flags20: packets.Flags20),
                NativeBattleTripleSprite sprite => NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, camera.Roll, 17, 2), NativeBattleEffectGouraudWrites.RgbBytes),
                _ => throw new NotSupportedException("Native Triple operation lacks its original immediate implementation.")
            };
            NativeEffectMemory = written.Memory;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State with { DrawCursor = written.Cursor } };
        }
    }
}
