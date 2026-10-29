using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicWireMorph3F(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        Dictionary<uint, System.Numerics.Vector3> pilotWorld = [];
        NativeBattleActionMatrix? basis = null;
        NativeBattleCinematicWireMorphServices services = new((current, selector) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, selector).Selected, current =>
        {
            NativeBattleCinematicScaledBasisResult prepared = NativeBattleCinematicScaledBasis.Prepare(current, resourceLength);
            basis = prepared.Matrix;
            return prepared.Frame;
        }, (current, source, output) =>
        {
            NativeBattleCinematicGridProjection projected = ProjectNativeCinematicLatticeVertex(current, basis ?? throw new InvalidOperationException("Native wire morph requires its prepared basis."), source, output);
            if (projected.PilotWorld is { } world)
            {
                pilotWorld[output] = world;
            }
            return projected.Frame with { Arena = projected.Frame.Arena.Write(ArenaOffset(output + 4, 2), BitConverter.GetBytes(unchecked((ushort)projected.QuarterDepth))) };
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        services = services with { PilotWorld = pilotWorld, PilotIdentity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank)) };
        return (NativeBattleCinematicWireMorph3F.Draw(frame, services), graphics);
    }
}
