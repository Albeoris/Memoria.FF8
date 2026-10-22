using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBadBreath300Drawing
{
    public static NativeBattleBadBreath300Frame EmitNode(NativeBattleBadBreath300Frame frame, NativeBattleGfAnimationSource source, NativeBattleEffectArena context28, NativeBattleGfAnimationNode node, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<NativeBattleBadBreath300Frame, NativeBattleGfGeometryBatch, NativeBattleBadBreath300Frame> deliverPrivate)
    {
        if (!NativeBattleBadBreath300ActMesh.Visible(node))
        {
            return frame;
        }
        NativeBattleFileStaging staging = RequireStaging(frame);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Bad Breath requires its actual primitive process.");
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Bad Breath requires its actual projection controls.");
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Bad Breath ACT requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Bad Breath ACT requires its current arithmetic registers."));
        NativeBattleBadBreath300ActMeshStep drawn = NativeBattleBadBreath300ActMesh.Draw(source, node, context28, camera, temporary, frame.Memory.Arena ?? throw new NotSupportedException("Bad Breath requires its shared working arena."), registers, process.ModelRendererRegisters ?? throw new NotSupportedException("Bad Breath requires its retained FAR controls."), projection, controls.Dqa, controls.Dqb, controls.Zsf3, controls.Zsf4, constants);
        frame = frame with
        {
            Memory = frame.Memory with { Arena = drawn.Arena, GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } },
            Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88)
        };
        frame = deliverPrivate(frame, drawn.Batch);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 88) };
    }

    private static NativeBattleFileStaging RequireStaging(NativeBattleBadBreath300Frame frame) => frame.Staging ?? throw new NotSupportedException("Bad Breath requires its actual physical temporary memory.");
}
