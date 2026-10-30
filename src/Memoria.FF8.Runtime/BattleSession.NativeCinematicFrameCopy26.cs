using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicFrameCopy26(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicCapture96Services capture = new(_ => Process().Buffer80 ?? throw new NotSupportedException("Native cinematic frame copy requires its current draw-buffer selector."), (_, buffer) =>
        {
            NativeSoftwareDrawingEnvironmentBuffers environments = Process().Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native cinematic frame copy requires its current drawing environments.");
            return buffer == 0 ? environments.Buffer0 : environments.Buffer1;
        }, (current, output, ordering, batch) => Append(current, output, ordering, batch, 0));
        NativeBattleCinematicFrameCopy26Services services = new(capture, (current, output, ordering, batch) => Append(current, output, ordering, batch, 1));
        NativeBattlePilotVisualIdentity? pilotIdentity = null;
        if (graphics.OriginalEffect?.Binding.Effect is 208 or 211 or 256 or 258)
        {
            ImmutableArray<NativeBattleEffectInitializerAsset> assets = NativeBattleEffectInitializerPolicy.Assets(graphics.OriginalEffect.Binding);
            if (assets.Length != 2)
            {
                throw new NotSupportedException("Native cinematic frame copy requires both executed initializer resources.");
            }
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, assets[1].Data.Length);
            pilotIdentity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect.Binding.Effect, frame, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank));
        }
        return (NativeBattleCinematicFrameCopy26.Draw(frame, services, pilotIdentity: pilotIdentity), graphics);

        NativeBattlePrimitiveProcess Process() => RequireWaterMemory().PrimitiveProcess ?? throw new NotSupportedException("Native cinematic frame copy requires its canonical primitive process.");
        NativeBattleWaterExecutionFrame Append(NativeBattleWaterExecutionFrame current, uint output, uint ordering, NativeBattleGfGeometryBatch batch, ushort flags)
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics, flags);
            graphics = written.Graphics;
            return written.Frame;
        }
    }
}
