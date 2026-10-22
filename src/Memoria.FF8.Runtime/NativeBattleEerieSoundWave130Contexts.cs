namespace Memoria.FF8.Runtime;

public static class NativeBattleEerieSoundWave130Contexts
{
    public static NativeBattleEffectArena Compose(NativeBattlePackedMatrix32 camera, NativeBattleActionMatrix world, int depth, uint morph)
    {
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), world);
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(composed, null);
        return NativeBattleEffectArena.Clear().MarkUnknown(0, 40).Write(0, packed.Bytes.AsSpan(0, 18)).Write(20, packed.Bytes.AsSpan(20, 12)).Write(32, BitConverter.GetBytes(depth)).Write(36, BitConverter.GetBytes(morph));
    }

    public static NativeBattleEffectArena Impact(NativeBattlePackedMatrix32 camera, NativeBattleVector position, uint morph)
    {
        NativeBattleActionMatrix view = camera.ReadMatrix();
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, position.X, position.Y, position.Z);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 40);
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                int offset = (row * 3 + column) * 2;
                context = context.Write(offset, BitConverter.GetBytes(view.Rotation[row * 3 + column]));
            }
        }
        return context.Write(20, BitConverter.GetBytes(unchecked(x + view.X))).Write(24, BitConverter.GetBytes(unchecked(y + view.Y))).Write(28, BitConverter.GetBytes(unchecked(z + view.Z))).Write(32, BitConverter.GetBytes(-128)).Write(36, BitConverter.GetBytes(morph));
    }
}
