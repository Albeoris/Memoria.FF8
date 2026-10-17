namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaRandomValue(NativeCrtRandomState Random, int Value);
public sealed record NativeBattleCuragaRandomVector(NativeCrtRandomState Random, NativeBattleVector Value);

/// <summary>Typed Curaga API for the shared, normalized original restoration random kernels.</summary>
public static class NativeBattleCuragaRandom
{
    public static NativeBattleCuragaRandomValue Range881F20(NativeCrtRandomState random, int first, int second)
    {
        NativeBattleEffectNodeRandomValue draw = NativeBattleEffectNodeRandom.Range881F20(random, first, second);
        return new(draw.Random, draw.Value);
    }

    public static NativeBattleCuragaRandomValue Angle882030(NativeCrtRandomState random, short center, short span)
    {
        NativeBattleEffectNodeRandomValue draw = NativeBattleEffectNodeRandom.Angle882030(random, center, span);
        return new(draw.Random, draw.Value);
    }

    public static NativeBattleCuragaRandomValue ProductRange881F50(NativeCrtRandomState random, int first, int second)
    {
        NativeBattleEffectNodeRandomValue draw = NativeBattleEffectNodeRandom.ProductRange881F50(random, first, second);
        return new(draw.Random, draw.Value);
    }

    public static NativeBattleCuragaRandomVector Euler881FE0(NativeCrtRandomState random, NativeBattleVector center, NativeBattleVector span)
    {
        NativeBattleEffectNodeRandomVector draw = NativeBattleEffectNodeRandom.Euler881FE0(random, center, span);
        return new(draw.Random, draw.Value);
    }
}
