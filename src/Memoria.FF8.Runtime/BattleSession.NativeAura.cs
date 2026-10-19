using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeAura(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Aura requires its selected effect.");
        NativeBattleAuraResources resources = effect.Binding.AuraResources ?? throw new NotSupportedException("Native Aura requires its original resources.");
        NativeBattleAuraFrame frame = NativeBattleAuraController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Aura requires retained memory."), effect, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Aura requires its CRT random owner."));
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Aura requires its camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeAura(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Aura requires its selected effect.");
        NativeBattleAuraResources resources = effect.Binding.AuraResources ?? throw new NotSupportedException("Native Aura requires its original resources.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Aura requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Aura requires its published projection.");
        NativeBattleAuraFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Aura requires retained memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Aura requires its CRT random owner."));
        NativeBattleAuraInputs inputs = new(nativeActionPackets, projection, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Aura requires its original interpolation constants."), index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Aura requires its current target pose."), Execute);
        NativeBattleAuraFrame advanced = NativeBattleAuraController.Advance(frame, resources, inputs);
        NativeEffectMemory = advanced.Memory;
        nativeCrtRandom = advanced.Random;
        return graphics with { OriginalEffect = advanced.Effect };

        NativeBattleAuraFrame Execute(NativeBattleAuraFrame current, NativeBattleAuraOperation operation)
        {
            if (operation is NativeBattleAuraFade)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 0, 128) };
                return current;
            }
            if (operation is NativeBattleAuraSound sound)
            {
                NativeEffectMemory = current.Memory;
                PublishNativeModuleSound(sound.Sound);
                return current with { Memory = NativeEffectMemory! };
            }
            if (operation is NativeBattleAuraContact contact)
            {
                NativeEffectMemory = current.Memory;
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                graphics = graphics with { PendingContacts = pending };
                return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Aura contact lost process memory.") };
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Aura requires its actual pending ordering table.");
            int cursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Aura lost its executed arena selection.");
            NativeBattleEffectPacketWrite written = operation switch
            {
                NativeBattleAuraPackets packets => NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, packets.Batch),
                NativeBattleAuraSprite sprite => NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, camera.Roll, 17, 2), NativeBattleEffectGouraudWrites.RgbBytes),
                _ => throw new NotSupportedException("Native Aura operation has no proved implementation.")
            };
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }
}
