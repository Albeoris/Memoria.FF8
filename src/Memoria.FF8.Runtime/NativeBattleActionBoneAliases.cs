using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleActionBoneAliases
{
    public static short Resolve(ImmutableArray<byte> aliases, short selector)
    {
        if (aliases.IsDefaultOrEmpty || selector >= 240)
        {
            return selector;
        }
        if (selector < 0 || selector >= aliases.Length || aliases[selector] == byte.MaxValue)
        {
            throw new NotSupportedException($"The transferred action reaches unmapped source bone {selector}.");
        }
        return aliases[selector];
    }
}
