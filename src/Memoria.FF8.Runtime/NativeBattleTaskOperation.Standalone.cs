namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleTaskOperation
{
    internal static NativeBattleTaskOperation? BeginStandalone(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) => IsActive ? null : new(memory, effect);

    internal static NativeBattleLogicalOrderingTransaction? ProtectOrdering(NativeBattleLogicalOrdering ordering) => ordering.Transaction is null ? new(ordering) : null;

    internal static NativeBattleLogicalOrdering CompleteOrdering(NativeBattleLogicalOrderingTransaction? operation, NativeBattleLogicalOrdering ordering) => operation?.Freeze() ?? ordering;

    internal static T CompleteStandalone<T>(NativeBattleTaskOperation? operation, T result)
    {
        operation?.Commit();
        return result;
    }
}
