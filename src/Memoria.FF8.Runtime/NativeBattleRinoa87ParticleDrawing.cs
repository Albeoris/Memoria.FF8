using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87ParticleDraw(NativeBattleGfGeometryBatch Packets, bool CommonCursor);
public static class NativeBattleRinoa87ParticleDrawing
{
    public static NativeBattleRinoa87ParticleDraw Draw(NativeBattleModulePoolTask task, uint callback, ImmutableArray<byte> sprite, ImmutableDictionary<uint, ImmutableArray<byte>> meshes, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short cameraRoll)
    {
        if ((task.ReadByte(38) & 4) != 0)
        {
            return new(new([], 0), callback != 0x775780);
        }
        if (callback == 0x775780)
        {
            if (task.ReadDword(76) != 0x1557D34 || sprite.IsDefault)
            {
                throw new InvalidDataException("Native775780 requires its actual selected1557D34 sprite bank.");
            }
            return new(NativeBattleSearchSprites.Draw7582A0(task, sprite, view, projection, cameraRoll, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(87, task)), false);
        }
        if (callback != 0x77E060 || !meshes.TryGetValue(task.ReadDword(76), out ImmutableArray<byte> mesh))
        {
            throw new NotSupportedException("Native77E060 requires its actual selected original mesh.");
        }
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770();
        local = NativeBattleCuragaMatrices.RotateY8DD8A0(local, Word(70));
        local = NativeBattleCuragaMatrices.RotateX8DD7E0(local, Word(68));
        local = NativeBattleCuragaMatrices.RotateZ8DD960(local, Word(72));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(48)), unchecked((int)task.ReadDword(52)), unchecked((int)task.ReadDword(56))) with { X = Word(28), Y = Word(30), Z = Word(32) };
        NativeBattleActionMatrix camera = NativeBattlePoseMatrices.Compose56C2F0(view, local);
        NativeBattleRinoa87MeshContext operands = new(240, task.ReadDword(64), Word(80), Word(82), 17, 2);
        return new(NativeBattleRinoa87MeshPackets.EmitBatch776920(mesh, camera, projection, operands, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(87, task)), true);
        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }
}
