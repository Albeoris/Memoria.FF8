using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleCinematicGridProjection ProjectNativeCinematicLatticeVertex(NativeBattleWaterExecutionFrame frame, NativeBattleActionMatrix basis, uint source, uint output)
    {
        NativeBattleEffectMemory memory = RequireWaterMemory();
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic projection requires its retained depth-cue controls.");
        NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic projection requires its retained packed input.");
        byte[] raw = source >= frame.TemporaryFrame.Address && (long)source - frame.TemporaryFrame.Address <= 0x180 - 6 ? frame.ReadTemporary(checked((int)(source - frame.TemporaryFrame.Address)), 6) : frame.Arena.Read(ArenaOffset(source, 6), 6);
        registers = registers with { NormalInput = [.. raw, 0, 0], NormalInputKnown = null };
        NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(basis, NativeBattleScriptedMeshPreparation.Vector(raw), NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic projection requires its current camera projection."), controls.Dqa, controls.Dqb);
        frame = StoreNativeGridDword(frame, output, projected.Projected.Screen);
        NativeEffectMemory = memory with { ScriptedColorRegisters = registers };
        return new(frame, projected.Projected.Depth >> 2) { PilotWorld = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(basis, NativeBattleScriptedMeshPreparation.Vector(raw)), NativeCamera!.Projection!) };
    }
}
