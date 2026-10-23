namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDegenerator306ModelDrawing
{
    private static (NativeBattleEffectArena Temporary, NativeBattleGeometryRegisterStep Registers, NativeBattleGfGeometryAttempt Attempt) DrawFace(byte[] source, bool quad, NativeBattleEffectArena scratch, int vertexCount, NativeBattleEffectArena temporary, NativeBattleGeometryRegisterStep registers, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, int tableBaseBucket, byte depthShift, int offset) => NativeBattleSkinnedBodyPackets.EmitBaseFace(source, quad, scratch, vertexCount, temporary, registers, projection, controls, tableBaseBucket, depthShift, offset, 0x3C, 0x30, 0x38);
}
