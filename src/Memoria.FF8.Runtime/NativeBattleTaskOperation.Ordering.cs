using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleTaskOperation
{
    private readonly List<NativeBattleLogicalOrdering.AutomaticScope> orderingScopes = [];

    internal void AdmitOrdering(NativeBattleLogicalOrdering? ordering)
    {
        RequireCurrent();
        if (ordering is null)
        {
            return;
        }
        EnlistOrdering(ordering);
    }

    private void EnlistOrdering(NativeBattleLogicalOrdering ordering)
    {
        foreach (NativeBattleLogicalOrdering.AutomaticScope scope in orderingScopes)
        {
            if (scope.Matches(ordering))
            {
                return;
            }
        }
        if (orderingScopes.Count != 0)
        {
            throw new InvalidOperationException("A task callback cannot replace its unfinished ordering authority.");
        }
        parent?.EnlistOrdering(ordering);
        orderingScopes.EnsureCapacity(orderingScopes.Count + 1);
        orderingScopes.Add(NativeBattleLogicalOrdering.AutomaticScope.Begin(ordering));
    }

    internal static void AcceptPublishedOrdering(NativeBattleLogicalOrdering? ordering)
    {
        PreflightPublishedOrdering(ordering);
        RememberPublishedOrdering(ordering);
    }

    private static void PreflightPublishedOrdering(NativeBattleLogicalOrdering? ordering)
    {
        if (current is null || ordering is null)
        {
            return;
        }
        bool found = false;
        bool admitted = false;
        for (NativeBattleTaskOperation? operation = current; operation is not null; operation = operation.parent)
        {
            foreach (NativeBattleLogicalOrdering.AutomaticScope scope in operation.orderingScopes)
            {
                admitted = true;
                if (scope.Matches(ordering))
                {
                    found = true;
                    scope.ValidatePublication();
                }
            }
        }
        if (admitted && !found)
        {
            throw new InvalidOperationException("Published ordering replaces an unfinished task ordering authority.");
        }
    }

    private static void RememberPublishedOrdering(NativeBattleLogicalOrdering? ordering)
    {
        if (current is null || ordering is null)
        {
            return;
        }
        for (NativeBattleTaskOperation? operation = current; operation is not null; operation = operation.parent)
        {
            foreach (NativeBattleLogicalOrdering.AutomaticScope scope in operation.orderingScopes)
            {
                if (scope.Matches(ordering))
                {
                    scope.RememberPublication();
                }
            }
        }
    }

    private void PreflightOrdering()
    {
        foreach (NativeBattleLogicalOrdering.AutomaticScope scope in orderingScopes)
        {
            scope.Preflight();
        }
    }

    private void CompleteOrdering(bool rollback, ref ExceptionDispatchInfo? failure)
    {
        for (int index = orderingScopes.Count - 1; index >= 0; index--)
        {
            try
            {
                orderingScopes[index].Complete(rollback);
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
    }
}
