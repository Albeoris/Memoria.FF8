using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class FrontierInputAccountingTests
{
    internal static void Run()
    {
        Ff7AutoPlayState state = new();
        state.Coverage.Visits.Add(new() { Visit = new("field", "program"), Events = [new() { Event = new("native", AutoPlayEventKind.Interaction) }] });
        state.NavigationGuards["guard"] = new("field", 1, EventId: "native", NavigationKey: "component", MovementFailure: new(0, 100, 0, 100));
        Ff7AutoPlayPolicy.CommitFrontierInput(state, 10, true, false, false);
        string key = state.FrontierProgressKey!;
        string guards = JsonSerializer.Serialize(state.NavigationGuards);
        string coverage = JsonSerializer.Serialize(state.Coverage);
        for (int scheduling = 0; scheduling < Ff7AutoPlayPolicy.FrontierIdleLimit + 100; scheduling++)
        {
            Ff7AutoPlayPolicy.CommitFrontierInput(state, 10, true, true, false);
        }
        Check(state.FrontierIdleSteps == 0 && state.FrontierProgressKey == key, "Cooperative planning consumed or renewed the native frontier attempt budget.");
        Ff7AutoPlayPolicy.CommitFrontierInput(state, 11, true, false, true);
        Ff7AutoPlayPolicy.CommitFrontierInput(state, 11, false, false, false);
        Check(state.FrontierIdleSteps == 0 && state.FrontierProgressKey == key, "A pending restore or uncontrollable selection changed the native frontier budget.");
        int nativeAdvances = 0;
        bool stopped = false;
        for (int attempt = 1; attempt <= Ff7AutoPlayPolicy.FrontierIdleLimit; attempt++)
        {
            if (attempt == Ff7AutoPlayPolicy.FrontierIdleLimit / 2)
            {
                state = state.Clone();
            }
            try
            {
                Ff7AutoPlayPolicy.CommitFrontierInput(state, 10, true, false, false);
            }
            catch (TimeoutException)
            {
                stopped = true;
                break;
            }
            nativeAdvances++;
            Ff7AutoPlayPolicy.CommitFrontierInput(state, 10, true, true, false);
        }
        Check(stopped && state.FrontierIdleSteps == Ff7AutoPlayPolicy.FrontierIdleLimit && nativeAdvances == Ff7AutoPlayPolicy.FrontierIdleLimit - 1, "The existing 7200-attempt supervisor changed or exhaustion occurred after native advancement.");
        Ff7AutoPlayPolicy.CommitFrontierInput(state, 10, true, true, false);
        Ff7AutoPlayPolicy.CommitFrontierInput(state, 10, true, false, true);
        Check(state.FrontierIdleSteps == Ff7AutoPlayPolicy.FrontierIdleLimit, "A scheduling yield or discarded restore input advanced an exhausted counter.");
        Check(guards == JsonSerializer.Serialize(state.NavigationGuards) && coverage == JsonSerializer.Serialize(state.Coverage), "Frontier accounting changed native guards or event history.");
        state.Coverage.Visits[0].Events[0].Completed = true;
        Ff7AutoPlayPolicy.CommitFrontierInput(state, 10, true, false, false);
        Check(state.FrontierIdleSteps == 0 && state.FrontierProgressKey != key, "Genuine completed frontier progress stopped renewing the existing budget key.");
        Console.WriteLine("Frontier input accounting checks passed.");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
