namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWhiteWind226Resources(NativeBattleGfAnimationSource Caster, NativeBattleGfAnimationSource Sweep, NativeBattleGfAnimationSource Target, NativeBattleEffectHeapAllocation Camera, uint Sound);

public static class NativeBattleWhiteWind226
{
    public static short AnchorY(short minimumY, short angle36) => unchecked((short)(((minimumY * 2 / 3 + minimumY / 2) / 2) + angle36));
    public static int SizeScale(short scale38) => scale38 / 2 * 4 + 4096;

    public static NativeBattleActionMatrix Billboard673810(NativeBattleActionMatrix view, NativeBattleVector position)
    {
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, position.X, position.Y, position.Z);
        return view with { X = unchecked(view.X + x), Y = unchecked(view.Y + y), Z = unchecked(view.Z + z) };
    }
}
