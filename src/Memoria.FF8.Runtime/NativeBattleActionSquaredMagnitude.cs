namespace Memoria.FF8.Runtime;

public static partial class NativeBattleActionGeometryRules
{
    /// <summary>56BC50's returned squared magnitude after its retained single-precision stores.</summary>
    public static int SquaredMagnitude56BC50(int x, int y, int z)
    {
        float retainedSquare = (float)((double)(float)z * (float)z + (double)(float)y * (float)y + (double)x * x);
        return Round(retainedSquare);
    }
}
