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
        return (NativeBattleCinematicFrameCopy26.Draw(frame, services), graphics);

        NativeBattlePrimitiveProcess Process() => RequireWaterMemory().PrimitiveProcess ?? throw new NotSupportedException("Native cinematic frame copy requires its canonical primitive process.");
        NativeBattleWaterExecutionFrame Append(NativeBattleWaterExecutionFrame current, uint output, uint ordering, NativeBattleGfGeometryBatch batch, ushort flags)
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics, flags);
            graphics = written.Graphics;
            return written.Frame;
        }
    }
}
