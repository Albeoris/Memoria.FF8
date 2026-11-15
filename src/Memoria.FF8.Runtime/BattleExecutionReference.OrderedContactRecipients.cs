using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal abstract partial record BattleExecutionReference
{
    internal sealed record OrderedContactRecipients(ImmutableArray<(BattleModelOwner Owner, BattleContactOccurrence Occurrence)> Recipients) : BattleExecutionReference;
}
