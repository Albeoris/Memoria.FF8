namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGilgameshParticles
{
    private static (NativeBattleEffectArena Arena, NativeCrtRandomState Random) Spawn(uint callback, NativeBattleEffectArena arena, int record, int mask, NativeCrtRandomState random)
    {
        arena = WriteDword(arena, record, unchecked((uint)mask));
        arena = WriteWord(arena, record + 4, 0);
        int size = callback switch
        {
            0x5915C0 => Draw() % 8192 + 1536,
            0x591850 => Draw() % 1536 + 768,
            _ => Draw() % 4096 + 1536
        };
        arena = WriteWord(arena, record + 6, size);
        if (callback == 0x5915C0)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                arena = WriteWord(arena, record + 8 + axis * 2, Draw() % 1200 - 600);
            }
            return (arena, random);
        }
        int x = Draw() % 8192 - 4096;
        int y = Draw() % 8192 - 4096;
        int z = callback == 0x591850 ? -2048 - Draw() % 2048 : Draw() % 8192 - 4096;
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        int[] components = [direction.X, direction.Y, direction.Z];
        int distance = callback == 0x591850 ? Draw() % 500 + 200 : Draw() % 400 + 400;
        for (int axis = 0; axis < 3; axis++)
        {
            arena = WriteWord(arena, record + 8 + axis * 2, unchecked(components[axis] * distance) >> 12);
        }
        if (callback == 0x591B80)
        {
            arena = WriteWord(arena, record + 0x0E, size);
        }
        int velocity = callback == 0x591850 ? Draw() % 300 + 100 : Draw() % 380 + 140;
        for (int axis = 0; axis < 3; axis++)
        {
            arena = WriteWord(arena, record + 0x10 + axis * 2, unchecked(components[axis] * velocity) >> 12);
            if (callback == 0x591B80)
            {
                arena = WriteWord(arena, record + 0x18 + axis * 2, components[axis]);
            }
        }
        return (arena, random);

        int Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }
}
