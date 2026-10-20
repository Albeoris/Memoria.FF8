using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeBlind(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Blind requires its selected effect.");
        NativeBattleBlindResources resources = effect.Binding.BlindResources ?? throw new NotSupportedException("Native Blind requires its original resources.");
        NativeBattleBlindFrame frame = NativeBattleBlindController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Blind requires its current memory."), effect, resources, NativeFileStaging ?? throw new NotSupportedException("Native Blind requires its retained temporary memory."));
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Blind requires its current camera.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeBlind(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Blind requires its selected effect.");
        NativeBattleBlindResources resources = effect.Binding.BlindResources ?? throw new NotSupportedException("Native Blind requires its original resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Blind requires its published camera.");
        NativeBattleBlindInputs inputs = new(nativeActionPackets, projection, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Blind requires its original interpolation constants."), index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Blind requires its current target pose."), Execute);
        NativeBattleBlindFrame advanced = NativeBattleBlindController.Advance(new(NativeEffectMemory ?? throw new NotSupportedException("Native Blind requires current memory."), effect, NativeFileStaging ?? throw new NotSupportedException("Native Blind requires its retained temporary memory.")), resources, inputs);
        NativeEffectMemory = advanced.Memory;
        NativeFileStaging = advanced.Staging;
        return graphics with { OriginalEffect = advanced.Effect };

        NativeBattleBlindFrame Execute(NativeBattleBlindFrame current, NativeBattleBlindOperation operation)
        {
            if (operation is NativeBattleBlindFlash flash)
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Blind requires current stage color owners.");
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)flash.Strength), ColorFlags = 0 }).ToImmutableArray() };
                return current;
            }
            if (operation is NativeBattleBlindFade)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 0, 0x60) };
                return current;
            }
            if (operation is NativeBattleBlindSound sound)
            {
                NativeEffectMemory = current.Memory;
                NativeFileStaging = current.Staging;
                PublishNativeModuleSound(sound.Sound);
                return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
            }
            if (operation is NativeBattleBlindContact contact)
            {
                NativeEffectMemory = current.Memory;
                NativeFileStaging = current.Staging;
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                graphics = graphics with { PendingContacts = pending };
                return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Blind contact lost its memory owner."), Staging = NativeFileStaging! };
            }
            if (operation is not NativeBattleBlindPackets packets)
            {
                throw new NotSupportedException("Native Blind operation has no proved implementation.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Blind requires its actual ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Blind lost its selected packet arena."), primitive.Pending, packets.Batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }
}
