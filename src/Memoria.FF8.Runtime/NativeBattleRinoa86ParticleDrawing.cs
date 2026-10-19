using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa86ParticleDraw(NativeBattleGfGeometryBatch Packets, bool CommonCursor);

/// <summary>7833D0/7806A0/782990's exact camera basis, mesh operands and packet-bank selection.</summary>
public static class NativeBattleRinoa86ParticleDrawing
{
    public static NativeBattleRinoa86ParticleDraw Draw(NativeBattleModulePoolTask task, uint callback, NativeBattleRinoa86Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short cameraRoll)
    {
        if ((task.ReadByte(38) & 4) != 0)
        {
            return new(new([], 0), callback != 0x782990);
        }
        if (callback == 0x782990)
        {
            if (task.ReadDword(76) != 0x156177C || resources.Sprite.IsDefault)
            {
                throw new InvalidDataException("Native782990 requires its actual selected sprite bank.");
            }
            return new(NativeBattleSearchSprites.Draw7582A0(task, resources.Sprite, view, projection, cameraRoll), false);
        }
        if (!resources.Meshes.TryGetValue(task.ReadDword(76), out ImmutableArray<byte> mesh))
        {
            throw new NotSupportedException("Native Rinoa86 particle mesh lacks its original supplied resource.");
        }
        NativeBattleActionMatrix local;
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleTextureWindow after = full;
        ushort u = 0;
        ushort v = 0;
        switch (callback)
        {
            case 0x7833D0:
                local = NativeBattleCuragaMatrices.Identity8DD770();
                local = NativeBattleCuragaMatrices.RotateY8DD8A0(local, Word(70));
                local = NativeBattleCuragaMatrices.RotateX8DD7E0(local, Word(68));
                local = NativeBattleCuragaMatrices.RotateZ8DD960(local, Word(72));
                u = task.ReadWord(144);
                v = task.ReadWord(146);
                after = new(Word(84), Word(86), Word(88), Word(90));
                break;
            case 0x7806A0:
                local = new([view.Rotation[0], view.Rotation[3], view.Rotation[6], view.Rotation[1], view.Rotation[4], view.Rotation[7], view.Rotation[2], view.Rotation[5], view.Rotation[8]], 0, 0, 0, 4096);
                local = NativeBattleCuragaMatrices.RotateZ8DD960(local, Word(72));
                break;
            default:
                throw new NotSupportedException($"Native Rinoa86 particle draw callback {callback:X8} has no selected contract.");
        }
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(48)), unchecked((int)task.ReadDword(52)), unchecked((int)task.ReadDword(56))) with { X = Word(28), Y = Word(30), Z = Word(32) };
        NativeBattleActionMatrix camera = NativeBattlePoseMatrices.Compose56C2F0(view, local);
        NativeBattleRinoa86MeshContext operands = new(240, task.ReadDword(64), Word(80), Word(82), u, v, full, after, 17, 2);
        return new(NativeBattleRinoa86MeshPackets.EmitBatch7807B0(mesh, camera, projection, operands, colors), true);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }
}
