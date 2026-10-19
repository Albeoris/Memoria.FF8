using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeProtect(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Protect requires its selected effect.");
        NativeBattleProtectResources resources = effect.Binding.ProtectResources ?? throw new NotSupportedException("Native Protect requires its original resources.");
        NativeBattleProtectFrame frame = NativeBattleProtectController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Protect requires retained memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Protect requires its CRT random owner."));
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Protect requires its camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeProtect(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Protect requires its selected effect.");
        NativeBattleProtectResources resources = effect.Binding.ProtectResources ?? throw new NotSupportedException("Native Protect requires its original resources.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Protect requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Protect requires its published projection.");
        NativeBattleProtectFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Protect requires retained memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Protect requires its CRT random owner."));
        NativeBattleProtectInputs inputs = new(nativeActionPackets, projection.View ?? throw new NotSupportedException("Native Protect requires its actual camera matrix."), projection, camera.Roll, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Protect requires its original interpolation constants."), index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Protect requires its current target pose."), Execute);
        NativeBattleProtectFrame advanced = NativeBattleProtectController.Advance(frame, resources, inputs);
        NativeEffectMemory = advanced.Memory;
        nativeCrtRandom = advanced.Random;
        return graphics with { OriginalEffect = advanced.Effect };

        NativeBattleProtectFrame Execute(NativeBattleProtectFrame current, NativeBattleProtectOperation operation)
        {
            if (operation is NativeBattleProtectStage stageWrite)
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Protect requires its stage color owners.");
                NativeBattleStageFadeWrite write = stageWrite.Write;
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = write.Fade, ColorFlags = write.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                return current;
            }
            if (operation is NativeBattleProtectSound sound)
            {
                NativeEffectMemory = current.Memory;
                PublishNativeModuleSound(sound.Sound);
                return current with { Memory = NativeEffectMemory! };
            }
            if (operation is NativeBattleProtectContact contact)
            {
                NativeEffectMemory = current.Memory;
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                graphics = graphics with { PendingContacts = pending };
                return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Protect contact lost process memory.") };
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Protect requires its actual pending ordering table.");
            NativeBattleProtectState state = current.Memory.Protect32 ?? throw new NotSupportedException("Native Protect lost its retained packet cursor owner.");
            int cursor = state.Cursor ?? throw new NotSupportedException("Native Protect lost its executed arena selection.");
            NativeBattleEffectPacketWrite written = operation switch
            {
                NativeBattleProtectPackets packets => NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, packets.Batch, packets.Flags20),
                NativeBattleProtectSprite sprite => NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, camera.Roll, 17, 2), NativeBattleEffectGouraudWrites.RgbBytes),
                _ => throw new NotSupportedException("Native Protect operation has no proved implementation.")
            };
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory with { Protect32 = state with { Cursor = written.Cursor } } };
        }
    }
}
