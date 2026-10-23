namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBoneAnchor67E550Step(NativeBattleEffectArena Temporary20, NativeBattleEffectArena Point8, int Result);

public static class NativeBattleBoneAnchor67E550
{
    public static NativeBattleBoneAnchor67E550Step Query(int selector, NativeBattleEffectArena temporary20, NativeBattleEffectArena retainedPoint8, Func<int, byte> readAlias, Func<int, short> readParent, Func<int, NativeBattlePackedMatrix32> readCurrentMatrix, Func<NativeBattlePackedMatrix32> readWorld)
    {
        int bone = selector >= 0xF0 ? readAlias(selector) : selector;
        if (readParent(bone) < 0)
        {
            return new(temporary20, retainedPoint8, 1);
        }
        NativeBattleActionMatrix current = readCurrentMatrix(bone).ReadMatrix();
        NativeBattleActionMatrix world = readWorld().ReadMatrix();
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(world, current.X, current.Y, current.Z);
        NativeBattleEffectArena temporary = temporary20.Write(0, BitConverter.GetBytes(x)).Write(4, BitConverter.GetBytes(y)).Write(8, BitConverter.GetBytes(z));
        NativeBattleEffectArena point = retainedPoint8.Write(0, BitConverter.GetBytes(unchecked((short)(x + world.X)))).Write(2, BitConverter.GetBytes(unchecked((short)(y + world.Y)))).Write(4, BitConverter.GetBytes(unchecked((short)(z + world.Z))));
        return new(temporary, point, 0);
    }
}
