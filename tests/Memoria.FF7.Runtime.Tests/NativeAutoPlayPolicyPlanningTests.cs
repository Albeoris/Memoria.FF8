using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayPolicyPlanningTests
{
    public static void Run()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayVisit visit = new("current", "region");
        planner.Observe(visit, [new("repeatable", AutoPlayEventKind.Interaction, ConditionSignature: "before"), new("unseen", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "ready")]);
        planner.Completed(visit.Key, "repeatable", entrySignature: "before", entrySignatureAuthoritative: true);
        planner.Observe(visit, [new("repeatable", AutoPlayEventKind.Interaction, ConditionSignature: "changed"), new("unseen", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: "ready")]);
        Check(planner.Plan(visit.Key)?.Event.Id == "unseen", "A changed repeatable event must not starve an unexecuted frontier.");
        planner.Completed(visit.Key, "unseen", entrySignature: "ready", entrySignatureAuthoritative: true);
        Check(planner.Plan(visit.Key)?.Event.Id == "repeatable", "A changed predicate must remain eligible after the unexecuted frontier is covered.");
        Ff7AutoPlayState restored = state.Clone();
        Check(Ff7AutoPlayPolicy.CreatePlanner(restored).Plan(visit.Key)?.Event.Id == "repeatable", "Cold planner restoration lost the changed predicate.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
