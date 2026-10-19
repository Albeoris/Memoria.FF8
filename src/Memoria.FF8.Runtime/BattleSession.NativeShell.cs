using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeShell(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Shell requires its selected effect.");
        NativeBattleShellResources resources = effect.Binding.ShellResources ?? throw new NotSupportedException("Native Shell requires its original resources.");
        NativeBattleShellFrame frame = NativeBattleShellController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Shell requires retained memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Shell requires its CRT random owner."));
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Shell requires its camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeShell(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Shell requires its selected effect.");
        NativeBattleShellResources resources = effect.Binding.ShellResources ?? throw new NotSupportedException("Native Shell requires its original resources.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Shell requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Shell requires its published projection.");
        NativeBattleShellFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Shell requires retained memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Shell requires its CRT random owner."));
        NativeBattleShellInputs inputs = new(nativeActionPackets, projection.View ?? throw new NotSupportedException("Native Shell requires its actual camera matrix."), projection, camera.Roll, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Shell requires its original interpolation constants."), index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Shell requires its current target pose."), Execute);
        NativeBattleShellFrame advanced = NativeBattleShellController.Advance(frame, resources, inputs);
        NativeEffectMemory = advanced.Memory;
        nativeCrtRandom = advanced.Random;
        return graphics with { OriginalEffect = advanced.Effect };

        NativeBattleShellFrame Execute(NativeBattleShellFrame current, NativeBattleShellOperation operation)
        {
            if (operation is NativeBattleShellStage stageWrite)
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Shell requires its stage color owners.");
                NativeBattleStageFadeWrite write = stageWrite.Write;
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = write.Fade, ColorFlags = write.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                return current;
            }
            if (operation is NativeBattleShellSound sound)
            {
                NativeEffectMemory = current.Memory;
                PublishNativeModuleSound(sound.Sound);
                return current with { Memory = NativeEffectMemory! };
            }
            if (operation is NativeBattleShellContact contact)
            {
                NativeEffectMemory = current.Memory;
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                graphics = graphics with { PendingContacts = pending };
                return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Shell contact lost process memory.") };
            }
            if (operation is not NativeBattleShellPackets packets)
            {
                throw new NotSupportedException("Native Shell operation has no proved implementation.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Shell requires its actual pending ordering table.");
            NativeBattleShellState state = current.Memory.Shell33 ?? throw new NotSupportedException("Native Shell lost its packet cursor owner.");
            int cursor = state.Cursor ?? throw new NotSupportedException("Native Shell lost its executed arena selection.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, packets.Batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory with { Shell33 = state with { Cursor = written.Cursor } } };
        }
    }
}
