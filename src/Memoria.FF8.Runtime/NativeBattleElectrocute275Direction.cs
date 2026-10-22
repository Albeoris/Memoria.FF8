namespace Memoria.FF8.Runtime;

public static class NativeBattleElectrocute275Direction
{
    public static NativeBattleVector Compute(short pitch, short yaw)
    {
        int cosine = NativeBattleCameraMath.Cosine(pitch);
        int y = unchecked(-NativeBattleCameraMath.Sine(pitch));
        int x = unchecked(-unchecked(NativeBattleCameraMath.Sine(yaw) * cosine)) >> 12;
        int z = unchecked(NativeBattleCameraMath.Cosine(yaw) * cosine) >> 12;
        return new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
    }
}
