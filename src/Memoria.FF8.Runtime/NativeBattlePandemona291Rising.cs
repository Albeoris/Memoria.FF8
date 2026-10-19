using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291Rising
{
    public static int BodyY100(int age) => unchecked(((age - 100) << 13) / 24 - 8192);

    public static ImmutableArray<NativeBattlePandemona291LayerRequest> Layers100(int center, int age)
    {
        int elapsed = unchecked(age - 100);
        int angle = unchecked(elapsed << 6);
        int firstFade = elapsed < 24 ? 0 : unchecked((int)((uint)(unchecked((elapsed - 24) << 12)) / 20));
        int otherFade = elapsed < 24 ? 0 : unchecked((int)((uint)(unchecked((elapsed - 24) << 12)) / 30));
        if (firstFade > 4096)
        {
            return [];
        }
        NativeBattlePandemona291LayerRequest first = new(0x13A0E58, 0x13A4280, center, angle, 4096, 0, 0, 11776, 8192, 0, unchecked((byte)(elapsed << 5)), firstFade);
        if (otherFade > 4096)
        {
            return [first];
        }
        return [first, new(0x13A4F88, 0x13A5EF0, center, angle, 4096, 0, 0, -30720, 8192, 1, unchecked((byte)(elapsed << 5)), otherFade), new(0x13A6508, 0x13A7BF0, center, angle, 4096, 0, 0, -30720, 8192, 1, unchecked((byte)(elapsed * 40)), otherFade)];
    }
}
