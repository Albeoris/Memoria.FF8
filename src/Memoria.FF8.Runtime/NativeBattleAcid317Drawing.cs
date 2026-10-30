using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleAcid317Drawing
{
    public static NativeBattleAcid317Frame EmitNode(NativeBattleAcid317Frame frame, NativeBattleGfAnimationSource source, NativeBattleEffectArena context12, NativeBattleGfAnimationNode node, NativeBattlePackedMatrix32 camera, NativeBattlePackedMatrix32 normalizedView, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<NativeBattleAcid317Frame, NativeBattleGfGeometryBatch, NativeBattleAcid317Frame> deliverPrivate, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (!NativeBattleAcid317ActMesh.Visible(node))
        {
            return frame;
        }
        NativeBattleFileStaging staging = RequireStaging(frame);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Acid requires its actual primitive process.");
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Acid requires its actual projection controls.");
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Acid ACT requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Acid ACT requires its current arithmetic registers."));
        NativeBattleAcid317ActMeshStep drawn = NativeBattleAcid317ActMesh.Draw(source, node, context12, camera, normalizedView, temporary, frame.Memory.Arena ?? throw new NotSupportedException("Acid requires its shared working arena."), registers, process.ModelRendererRegisters ?? throw new NotSupportedException("Acid requires its retained FAR controls."), projection, controls.Dqa, controls.Dqb, controls.Zsf3, controls.Zsf4, constants, pilotIdentity: pilotIdentity);
        frame = frame with
        {
            Memory = frame.Memory with { Arena = drawn.Arena, GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } },
            Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88)
        };
        frame = deliverPrivate(frame, drawn.Batch);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 88) };
    }

    private static NativeBattleFileStaging RequireStaging(NativeBattleAcid317Frame frame) => frame.Staging ?? throw new NotSupportedException("Acid requires its actual physical temporary memory.");
}
