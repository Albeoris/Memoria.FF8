namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect311Timing
{
    public static int StageIntensity665550(short frame) => frame < 8 ? unchecked(frame * 375) : frame > 66 ? unchecked((74 - frame) * 375) : 3000;

    public static short Radius6658B3(int phase)
    {
        return unchecked((short)(phase < 16 ? NativeBattleCameraMath.Cosine(phase << 6) : phase > 34 ? NativeBattleCameraMath.Sine((phase - 34) << 6) : 4096));
    }

    public static int ScreenColor6657B7(int phase) => phase < 48 ? 3072 : unchecked((int)(((uint)(phase + 0x3FFFD0) << 10) >> 3) + 3136);
}
