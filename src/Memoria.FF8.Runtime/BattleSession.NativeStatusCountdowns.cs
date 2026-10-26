namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void BeginNativePcUiFrame()
    {
        if (definition.NativeGraphics?.PcUiIcons is not { } binding)
        {
            return;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native49B120 requires its retained PC UI process.");
        NativeBattlePcUiImmediateState ui = memory.UiImmediate ?? throw new NotSupportedException("Native49B120 requires its retained UI state; an old missing owner requires explicit migration.");
        if (ui.Profile is null)
        {
            ui = ui.Initialize4972A0(binding.Device);
        }
        NativeEffectMemory = memory with { UiImmediate = ui.BeginFrame49B120(binding.Rules) };
    }

    private void PublishNativePcUiFrame()
    {
        if (definition.NativeGraphics?.PcUiIcons is null)
        {
            return;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native499EA0 requires its retained PC UI process.");
        NativeBattlePcUiImmediateState ui = memory.UiImmediate ?? throw new NotSupportedException("Native499EA0 requires its retained UI batches.");
        (ui, NativeBattlePcUiFrame frame) = ui.Publish499EA0();
        NativeEffectMemory = memory with { UiImmediate = ui, UiImmediateFrame = frame };
    }

    private void AdvanceNativeStatusCountdowns(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model)
    {
        NativeBattlePcUiBinding binding = definition.NativeGraphics?.PcUiIcons ?? throw new NotSupportedException("Native50A410 requires its selected original icon resource and device profile.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native50A410 requires its retained graphics arithmetic and UI owners.");
        NativeBattlePcUiImmediateState ui = memory.UiImmediate ?? throw new NotSupportedException("Native50A410 requires its retained PC UI state; an old missing owner requires explicit migration.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native5034E0 requires its prior camera owner.");
        uint statuses = model.Flags8 ?? throw new NotSupportedException("Native50A410 requires its actual status DWORD8.");
        NativeBattleVector anchor = ReadNativeGraphicsBone(graphics, model.Index, 0xF0, 0x1000);
        NativeBattleStatusCountdownProjectionResult projected = NativeBattleStatusCountdownProjection.Project50A410(memory, NativeFileStaging ?? throw new NotSupportedException("Native50A410 requires its physical temporary buffer."), camera, anchor, statuses);
        NativeFileStaging = projected.Staging;
        memory = projected.Memory;
        uint screen = projected.Screen;
        uint flags = projected.Flags;
        ushort depth = projected.QuarterDepth;
        if ((flags & 0x60000) == 0)
        {
            BattleCombatant actor = combatants.SingleOrDefault(candidate => candidate.NativeIndex == model.Index) ?? throw new NotSupportedException("Native50A410 requires the participant mapped to its physical graphics row.");
            foreach (NativeBattleCountdownText text in NativeBattleStatusCountdowns.Layout50A410(statuses, unchecked((short)screen), unchecked((short)(screen >> 16)), depth, flags, actor.NativeStatusTimers[12], actor.NativeStatusTimers[10]))
            {
                ui = ui.DrawString4A93D0(text.Glyphs, text.PackedPosition, text.Color, binding.IconCount, binding.Encode, binding.Opaque, binding.Semi, binding.Rules);
            }
        }
        NativeEffectMemory = memory with { UiImmediate = ui };
    }
}
