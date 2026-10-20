namespace Memoria.FF8.Runtime;

public static class NativeBattleRegen147Math
{
    public static int EnvelopeFade(short counter, short kind)
    {
        int baseline = kind == 0 ? 0 : 2048;
        int step = kind == 0 ? 256 : 128;
        if (counter < 4)
        {
            return 4096 - step * counter;
        }
        return counter >= 14 ? step * (counter - 14) + baseline : baseline;
    }
}
