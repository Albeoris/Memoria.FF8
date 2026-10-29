using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRinoa88ParticleDrawing
{
    public static NativeBattleGfGeometryBatch DrawSprite7720C0(NativeBattleModulePoolTask task, ImmutableArray<byte> sprite, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        if (task.Callback is not (0x772060 or 0x772990) || task.ReadDword(76) != (task.Callback == 0x772060 ? 0x154ECECu : 0x154EE30u))
        {
            throw new InvalidDataException("Native7720C0 requires its actual body-particle sprite owner.");
        }
        return NativeBattleSearchSprites.Draw7582A0(task, sprite, view, projection, cameraRoll, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(88, task));
    }

    public static NativeBattleGfGeometryBatch DrawMesh(NativeBattleModulePoolTask task, ImmutableArray<byte> mesh, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (task.Callback is not (0x771660 or 0x772D90))
        {
            throw new InvalidDataException("Native Rinoa88 mesh drawing requires its actual156-byte task.");
        }
        if ((task.ReadByte(38) & 4) != 0)
        {
            return new([], 0);
        }
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770();
        if (task.Callback == 0x772D90)
        {
            local = NativeBattleCuragaMatrices.RotateY8DD8A0(local, Word(70));
            local = NativeBattleCuragaMatrices.RotateX8DD7E0(local, Word(68));
            local = NativeBattleCuragaMatrices.RotateZ8DD960(local, Word(72));
        }
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(48)), unchecked((int)task.ReadDword(52)), unchecked((int)task.ReadDword(56))) with { X = Word(28), Y = Word(30), Z = Word(32) };
        NativeBattleActionMatrix camera = task.Callback == 0x772D90 ? NativeBattlePoseMatrices.Compose56C2F0(view, local) : local;
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleRinoa88MeshContext operands = new(240, task.ReadDword(64), Word(80), Word(82), 0, 0, full, full, 17, 2);
        return NativeBattleRinoa88MeshPackets.EmitBatch76A650(mesh, camera, projection, operands, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(88, task));

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }
}
