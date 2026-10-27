namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshMasamuneProjectionStep(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task);

public static class NativeBattleGilgameshMasamuneProjection
{
    public static NativeBattleGilgameshMasamuneProjectionStep Project(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, short? cameraPadding)
    {
        if (task.Callback != 0x590790 || task.Payload.IsDefault || task.PayloadLength != 24)
        {
            throw new InvalidDataException("Masamune projection requires its actual raw flare task.");
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Masamune projection requires the actual current camera matrix.");
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown();
        NativeBattleScriptedColorRegisters colors = memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown();
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Masamune projection requires its current depth-cue controls.");
        geometry = NativeBattleGeometry.LoadMatrix(geometry, view, true, true, cameraPadding);
        bool knownW = task.UnknownPayloadRanges is not { } unknown || !unknown.Any(range => range.Offset < 0x18 && range.Offset + range.Length > 0x16);
        short? w = knownW ? unchecked((short)task.ReadWord(0x16)) : null;
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(new(geometry, colors), 0, new(unchecked((short)task.ReadWord(0x10)), unchecked((short)task.ReadWord(0x12)), unchecked((short)task.ReadWord(0x14))), w);
        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
        uint screen = registers.Geometry.Sxy[2] ?? throw new InvalidDataException("Masamune projection did not publish its packed screen position.");
        NativeBattleModulePoolTask projected = task.WriteWord(0x10, unchecked((ushort)(unchecked((short)screen) - 360))).WriteWord(0x12, unchecked((ushort)(unchecked((short)(screen >> 16)) - 308)));
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, projected);
    }
}
