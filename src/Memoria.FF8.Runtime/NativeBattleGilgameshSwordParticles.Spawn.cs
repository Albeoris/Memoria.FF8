namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGilgameshSwordParticles
{
    private static (NativeBattleEffectArena Arena, NativeCrtRandomState Random) Spawn(NativeBattleEffectArena arena, int record, int mask, bool mesh, NativeCrtRandomState random)
    {
        arena = PutDword(arena, record, unchecked((uint)mask));
        arena = PutWord(arena, record + 4, 0);
        int size = mesh ? Draw() % 256 + 64 : Draw() % 1024 + 1024;
        arena = PutWord(arena, record + 6, size);
        int theta = Draw() % 4096;
        int sine = NativeBattleCameraMath.Sine(theta);
        int cosine = NativeBattleCameraMath.Cosine(theta);
        int radius = Draw() % (mesh ? 80 : 100);
        arena = PutWord(arena, record + 8, unchecked(sine * radius) >> 12);
        arena = PutWord(arena, record + 10, -(Draw() % 40));
        arena = PutWord(arena, record + 12, unchecked(cosine * radius) >> 12);
        if (mesh)
        {
            arena = PutWord(arena, record + 14, size);
        }
        int speed = mesh ? (Draw() % 140 + 60) >> 1 : Draw() % 10 + 10;
        arena = PutWord(arena, record + 16, unchecked(sine * speed) >> 12);
        if (mesh)
        {
            arena = PutWord(arena, record + 18, -60 - Draw() % 140);
        }
        arena = PutWord(arena, record + 20, unchecked(cosine * speed) >> 12);
        if (mesh)
        {
            arena = PutWord(arena, record + 24, Draw() % 4096);
            int velocity = Draw() % 40 + 20;
            arena = PutWord(arena, record + 26, (velocity & 1) != 0 ? -velocity : velocity);
            arena = PutWord(arena, record + 28, Draw() % 2048);
            velocity = Draw() % 40 + 20;
            arena = PutWord(arena, record + 30, (velocity & 1) != 0 ? -velocity : velocity);
        }
        return (arena, random);

        int Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }
}
