namespace Memoria.FF8.Runtime;

public static class NativeBattleEffectIdentity
{
    /// <summary>Convert a selected one-based effect binding to the original50AF20 table index.</summary>
    public static int IndexFromBindingEffect(int effect)
    {
        if (effect is < 1 or > 400)
        {
            throw new ArgumentOutOfRangeException(nameof(effect));
        }
        return effect - 1;
    }
}
