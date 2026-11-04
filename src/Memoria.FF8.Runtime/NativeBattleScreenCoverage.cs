namespace Memoria.FF8.Runtime;

/// <summary>Explicit presentation coverage of a screen overlay, expressed as fractions of the visible viewport.</summary>
public sealed record NativeBattleScreenCoverage(float Left, float Top, float Right, float Bottom)
{
    public static NativeBattleScreenCoverage Full { get; } = new(0, 0, 1, 1);
}
