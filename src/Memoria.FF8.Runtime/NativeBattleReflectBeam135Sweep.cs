using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal readonly record struct NativeBattleReflectBeam135Recipient(BattleModelOwner Owner, BattleContactOccurrence Occurrence, short X, int Order);

internal abstract partial record BattleExecutionReference
{
    internal sealed record ReflectBeam135Sweep(ImmutableArray<NativeBattleReflectBeam135Recipient> Recipients) : BattleExecutionReference;
}
