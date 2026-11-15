using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal abstract partial record BattleExecutionReference
{
    internal sealed record SavedModelFlags(ImmutableDictionary<BattleModelOwner, ushort> Flags) : BattleExecutionReference;
}
