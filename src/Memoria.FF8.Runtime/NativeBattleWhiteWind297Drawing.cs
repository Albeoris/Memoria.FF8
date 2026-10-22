using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleWhiteWind297Drawing
{
    public static NativeBattleWhiteWind297Frame EmitNode(NativeBattleWhiteWind297Frame frame, NativeBattleGfAnimationSource source, NativeBattleEffectArena context40, NativeBattleGfAnimationNode node, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<NativeBattleWhiteWind297Frame, NativeBattleGfGeometryBatch, NativeBattleWhiteWind297Frame> deliverPrivate)
    {
        if (!NativeBattleWhiteWindActMesh.Visible(node))
        {
            return frame;
        }
        NativeBattleFileStaging staging = RequireStaging(frame);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
        NativeBattleEffectArena normalized = frame.State.ReadView(NativeBattleWhiteWind297Profile.NormalizedView, 32);
        (byte[] bytes, bool[] known) = normalized.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 billboard = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Blue Magic White Wind requires its actual primitive process.");
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Blue Magic White Wind requires its actual projection controls.");
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleWhiteWindActMeshStep drawn = NativeBattleWhiteWindActMesh.Draw(source, node, context40, billboard, temporary, frame.Memory.Arena ?? throw new NotSupportedException("Blue Magic White Wind requires its shared working arena."), registers, process.ModelRendererRegisters ?? throw new NotSupportedException("Blue Magic White Wind requires its retained FAR controls."), projection, controls.Dqa, controls.Dqb, controls.Zsf4, constants);
        frame = frame with
        {
            Memory = frame.Memory with { Arena = drawn.Arena, GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } },
            Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88)
        };
        frame = deliverPrivate(frame, drawn.Batch);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 88) };
    }

    private static NativeBattleFileStaging RequireStaging(NativeBattleWhiteWind297Frame frame) => frame.Staging ?? throw new NotSupportedException("Blue Magic White Wind requires its actual physical temporary memory.");
}
