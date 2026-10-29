using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicOverlay(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsState graphics)
    {
        uint start = CoreDword(frame, CoreDword(frame, 0x27973EC) + 0x7C);
        NativeBattlePilotVisualIdentity? pilotIdentity = null;
        if (graphics.OriginalEffect!.Binding.Effect == 238)
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, NativeBattleEffectInitializerPolicy.Assets(graphics.OriginalEffect.Binding)[1].Data.Length);
            pilotIdentity = NativeBattleCinematicVisualIdentity.Identity(238, frame, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank));
        }
        NativeBattleCinematicOverlayStep overlay = NativeBattleCinematicOverlay.Draw(frame, OtBucket, pilotIdentity);
        frame = overlay.Frame;
        NativeBattleEffectMemory memory = RequireWaterMemory() with { Arena = frame.Arena, ScriptedEffects = frame.Core };
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic overlay requires its current ordering table.");
        if ((ReadSlot(frame, 0x4C, 1)[0] & 1) != 0)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, ArenaOffset(start, 0), primitive.Pending, overlay.Packets);
            memory = written.Memory;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        }
        else
        {
            NativeBattlePrimitiveProcess common = memory.PrimitiveProcess ?? throw new NotSupportedException("Native cinematic overlay requires its current COMMON packet owner.");
            if (start != common.ByteCursor)
            {
                throw new InvalidDataException("Native cinematic overlay COMMON cursor disagrees with its current packet owner.");
            }
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(common, primitive.Pending, overlay.Packets);
            memory = memory with { PrimitiveProcess = written.Process };
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        }
        NativeEffectMemory = memory;
        return (frame with { Arena = memory.Arena!, Core = memory.ScriptedEffects! }, graphics);
    }
}
