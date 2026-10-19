using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFunguarLaser277Resources(NativeBattleGfAnimationSource Muzzle, NativeBattleGfAnimationSource Ray, NativeBattleGfAnimationSource Impact, NativeBattleGfAnimationSource Hit, ImmutableArray<byte> Beam, NativeBattleModuleResource Spark, NativeBattleModuleResource Glow, NativeBattleEffectHeapAllocation Camera, uint Sound);

public sealed record NativeBattleFunguarLaser277State([property: JsonRequired] int BufferOffset)
{
    public static NativeBattleFunguarLaser277State FromColdProcess() => new(0);
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFunguarLaser277State? FunguarLaser277 { get; init; }
}

public static class NativeBattleFunguarLaser277
{
    public static (NativeBattleActionMatrix Matrix, int Length) Basis50CBA0(int x, int y, int z)
    {
        int factor = 1;
        while (Math.Abs(x) > 10000 || Math.Abs(y) > 10000 || Math.Abs(z) > 10000)
        {
            x = x / 2 + (x & 1);
            y = y / 2 + (y & 1);
            z = z / 2 + (z & 1);
            factor = unchecked(factor * 2);
        }
        int squared = NativeBattleSleepingGas168.NormalizeSquare(x, y, z);
        int length = unchecked((int)Math.Sqrt(squared) * factor);
        return (NativeBattlePandemona291Restoration.Basis50CBA0(x, y, z, default), length);
    }

    public static (short Lift, int Width, int Fade) Ease68433C(int counter, short size)
    {
        uint elapsed = unchecked((uint)(counter - 15));
        if (elapsed < 20)
        {
            int width = 4096 - NativeBattleCameraMath.Cosine((int)((elapsed << 10) / 20));
            return (unchecked((short)(size * 2 - (size * width >> 10))), width, 0);
        }
        if (elapsed < 40)
        {
            int lift = unchecked(size * 2 - ((((int)elapsed + 44) * size << 2) >> 6));
            return (unchecked((short)lift), unchecked(0x380 - ((int)elapsed << 5)), unchecked(((int)elapsed - 20) << 9));
        }
        return (unchecked((short)(size * 2)), 0, 0);
    }

    public static NativeBattleVector Floor68449D(NativeBattleVector origin, NativeBattleVector target)
    {
        int dy = target.Y - origin.Y;
        int x = unchecked((origin.X - target.X) * origin.Y);
        int z = unchecked((origin.Z - target.Z) * origin.Y);
        return new(unchecked((short)(x / dy + origin.X)), 0, unchecked((short)(z / dy + origin.Z)));
    }
}
