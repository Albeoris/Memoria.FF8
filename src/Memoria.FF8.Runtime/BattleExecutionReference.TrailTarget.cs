using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal abstract partial record BattleExecutionReference
{
    internal sealed record TrailTarget(BattleModelOwner? Owner) : BattleExecutionReference
    {
        internal ImmutableArray<byte>? EncodeOriginal(NativeBattleModelAliases aliases, int width)
        {
            if (Owner is { } owner)
            {
                return aliases.EncodeOperand(owner, width);
            }
            return width switch
            {
                1 => [byte.MaxValue],
                2 => [byte.MaxValue, byte.MaxValue],
                _ => throw new ArgumentOutOfRangeException(nameof(width))
            };
        }
    }
}
