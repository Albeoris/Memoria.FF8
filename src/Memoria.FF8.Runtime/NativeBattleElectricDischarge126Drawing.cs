namespace Memoria.FF8.Runtime;

public static class NativeBattleElectricDischarge126Drawing
{
    public static NativeBattleElectricDischarge126Frame EmitNode(NativeBattleElectricDischarge126Frame frame, NativeBattleGfAnimationSource source, NativeBattleEffectArena context12, NativeBattleGfAnimationNode node, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<NativeBattleElectricDischarge126Frame, NativeBattleGfGeometryBatch, NativeBattleElectricDischarge126Frame> deliverPrivate)
    {
        if (!NativeBattleElectrocute275ActMesh.Visible(node))
        {
            return frame;
        }
        NativeBattleFileStaging staging = RequireStaging(frame);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Electric Discharge requires its actual primitive process.");
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Electric Discharge requires its actual projection controls.");
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Electric Discharge ACT requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Electric Discharge ACT requires its current arithmetic registers."));
        NativeBattleElectrocute275ActMeshStep drawn = NativeBattleElectrocute275ActMesh.Draw(source, node, context12, camera, temporary, frame.Memory.Arena ?? throw new NotSupportedException("Electric Discharge requires its shared working arena."), checked(frame.State.ReadDword(NativeBattleElectricDischarge126Profile.WorkingGlobal) + NativeBattleElectricDischarge126Profile.MorphOffset), NativeBattleElectricDischarge126Profile.MorphBytes, registers, process.ModelRendererRegisters ?? throw new NotSupportedException("Electric Discharge requires its retained FAR controls."), projection, controls.Dqa, controls.Dqb, controls.Zsf3, controls.Zsf4, constants);
        frame = frame with
        {
            Memory = frame.Memory with { Arena = drawn.Arena, GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } },
            Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88)
        };
        frame = deliverPrivate(frame, drawn.Batch);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 88) };
    }

    private static NativeBattleFileStaging RequireStaging(NativeBattleElectricDischarge126Frame frame) => frame.Staging ?? throw new NotSupportedException("Electric Discharge requires its actual physical temporary memory.");
}
