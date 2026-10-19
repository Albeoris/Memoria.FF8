using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicWireMorph3F(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleActionMatrix? basis = null;
        NativeBattleCinematicWireMorphServices services = new((current, selector) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, selector).Selected, current =>
        {
            NativeBattleCinematicScaledBasisResult prepared = NativeBattleCinematicScaledBasis.Prepare(current, resourceLength);
            basis = prepared.Matrix;
            return prepared.Frame;
        }, (current, source, output) =>
        {
            NativeBattleCinematicGridProjection projected = ProjectNativeCinematicLatticeVertex(current, basis ?? throw new InvalidOperationException("Native wire morph requires its prepared basis."), source, output);
            return projected.Frame with { Arena = projected.Frame.Arena.Write(ArenaOffset(output + 4, 2), BitConverter.GetBytes(unchecked((ushort)projected.QuarterDepth))) };
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        return (NativeBattleCinematicWireMorph3F.Draw(frame, services), graphics);
    }
}
