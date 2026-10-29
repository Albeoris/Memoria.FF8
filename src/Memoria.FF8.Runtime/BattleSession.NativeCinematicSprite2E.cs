namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicSprite2E(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicSprite2EServices services = new(current =>
        {
            NativeBattleScriptedGraphicsMemory registers = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native cinematic anchor requires its retained depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic anchor requires its actual projection controls.");
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            return NativeBattleCinematicAnchor.Project(current, bank, projection, registers.Dqa, registers.Dqb, true);
        }, Append);
        NativeBattleWaterSlotBank selectedBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        int selectedIndex = NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, selectedBank);
        NativeBattlePilotVisualIdentity identity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, selectedBank, selectedIndex);
        return (NativeBattleCinematicSprite2E.Draw(frame, services, identity), graphics);

        NativeBattleWaterExecutionFrame Append(NativeBattleWaterExecutionFrame current, uint output, uint ordering, NativeBattleGfGeometryBatch batch)
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        }
    }
}
