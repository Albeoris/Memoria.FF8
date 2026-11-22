using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class AutoPlayStateCloneTests
{
    internal static void Run()
    {
        CheckClone(new());
        CheckClone(JsonSerializer.Deserialize<Ff7AutoPlayState>("{\"Version\":1,\"Status\":\"legacy\"}")!);
        Ff7AutoPlayState populated = Populated();
        CheckClone(populated);
        string? fixture = Environment.GetEnvironmentVariable("FF7_AUTOPLAY_CLONE_FIXTURE");
        if (!string.IsNullOrWhiteSpace(fixture))
        {
            using FileStream stream = File.OpenRead(fixture);
            Ff7AutoPlayState state = CombinedSessionCodec.Read(stream).Ff7?.AutoPlay ?? throw new InvalidDataException("The supplied fixture has no automatic play state.");
            CheckClone(state);
            Benchmark(state);
        }
        Console.WriteLine("PASS automatic play clone wire equivalence and mutable graph isolation");
    }

    private static Ff7AutoPlayState OldClone(Ff7AutoPlayState state)
    {
        return JsonSerializer.Deserialize<Ff7AutoPlayState>(JsonSerializer.Serialize(state))!;
    }

    private static void CheckClone(Ff7AutoPlayState source)
    {
        string before = JsonSerializer.Serialize(source);
        Ff7AutoPlayState expected = OldClone(source);
        Ff7AutoPlayState copy = source.Clone();
        Check(JsonSerializer.Serialize(copy) == JsonSerializer.Serialize(expected), "The typed coverage clone changed the former JSON clone wire image.");
        PropertyInfo historical = typeof(AutoPlayEventCoverage<Ff7AutoPlayDeferral>).GetProperty("AllowHistoricalTravelConditions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Check(copy.Coverage.Visits.SelectMany(visit => visit.Events).All(item => !(bool)historical.GetValue(item)!), "A transient historical travel permission survived publication.");
        HashSet<object> originalMutable = MutableObjects(source);
        HashSet<object> copiedMutable = MutableObjects(copy);
        Check(!originalMutable.Overlaps(copiedMutable), "The clone shares a mutable object with its source: " + string.Join(", ", originalMutable.Intersect(copiedMutable, ReferenceEqualityComparer.Instance).Select(item => item.GetType().Name)));
        foreach (object item in copiedMutable)
        {
            if (item is Array array && array.Length > 0)
            {
                array.SetValue(array.GetType().GetElementType()!.IsValueType ? Activator.CreateInstance(array.GetType().GetElementType()!) : null, 0);
            }
            else if (item is IDictionary dictionary)
            {
                dictionary.Clear();
            }
            else if (item is IList list)
            {
                list.Clear();
            }
            else if (item.GetType().IsGenericType && item.GetType().GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                item.GetType().GetMethod("Clear")!.Invoke(item, null);
            }
        }
        copy.Status = "mutated";
        Check(JsonSerializer.Serialize(source) == before, "Mutating clone collections changed the original state.");
    }

    private static HashSet<object> MutableObjects(object root)
    {
        HashSet<object> seen = new(ReferenceEqualityComparer.Instance);
        HashSet<object> mutable = new(ReferenceEqualityComparer.Instance);
        void Visit(object? value)
        {
            if (value is null || value is string || value.GetType().IsValueType || !seen.Add(value))
            {
                return;
            }
            Type type = value.GetType();
            bool collection = value is IEnumerable;
            bool mutableClass = type == typeof(Ff7AutoPlayState) || type == typeof(AutoPlayInputSearchContext) || type == typeof(AutoPlayConditionCoverage) || type == typeof(AutoPlayChoiceCoverage) || type == typeof(Ff7AutoPlayWorldVisitState) || type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(AutoPlayPlannerState<>) || type.GetGenericTypeDefinition() == typeof(AutoPlayVisitCoverage<>) || type.GetGenericTypeDefinition() == typeof(AutoPlayEventCoverage<>));
            if ((collection || mutableClass) && value is not Array { Length: 0 })
            {
                mutable.Add(value);
            }
            if (value is IDictionary dictionary)
            {
                foreach (object? child in dictionary.Values)
                {
                    Visit(child);
                }
                return;
            }
            if (value is IEnumerable items)
            {
                foreach (object? child in items)
                {
                    Visit(child);
                }
                return;
            }
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanRead && property.GetIndexParameters().Length == 0))
            {
                Visit(property.GetValue(value));
            }
        }
        Visit(root);
        return mutable;
    }

    private static T Scalars<T>(T value) where T : class
    {
        foreach (PropertyInfo property in typeof(T).GetProperties().Where(property => property.SetMethod is not null))
        {
            Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object? scalar = type == typeof(string) ? property.Name : type == typeof(bool) ? true : type == typeof(int) ? 7 : type.IsEnum ? Enum.GetValues(type).GetValue(0) : null;
            if (scalar is not null)
            {
                property.SetValue(value, scalar);
            }
        }
        return value;
    }

    private static Ff7AutoPlayState Populated()
    {
        AutoPlayChoiceSelection[] selections = [new("choice", 2)];
        AutoPlayEvent definition = new("event", AutoPlayEventKind.Transition, "destination", true, 4, "condition", ["variant"]) { EffectFingerprint = "effect" };
        AutoPlayEventRoute route = new("destination", "condition", true, selections, "target");
        AutoPlayInputSearchContext context = Scalars(new AutoPlayInputSearchContext());
        context.NavigationSource = new("navigation", "event", "condition", "target");
        context.SceneChoices = ["scene-choice"];
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = Scalars(new AutoPlayEventCoverage<Ff7AutoPlayDeferral>());
        typeof(AutoPlayEventCoverage<Ff7AutoPlayDeferral>).GetProperty("AllowHistoricalTravelConditions", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(coverage, true);
        coverage.Event = definition;
        coverage.Conditions = new() { ActiveEntry = "entry", ConsumedEntries = ["entry"], ConsumedExits = ["exit"] };
        coverage.Routes = [route];
        coverage.RetiredRoutes = [new(route, "choice", 1, 3, selections)];
        coverage.UnavailableAttempts = [new("source", "destination", "guard", "fingerprint", "target")];
        coverage.UnavailableVariants = ["unavailable"];
        coverage.CompletedAvailabilityFingerprints = ["complete"];
        coverage.ParticipantPolicyDeferrals = [new("target", "condition", "guard", selections, "choice", 1, 3)];
        coverage.PolicyDeferredVariants = ["deferred"];
        AutoPlayChoiceCoverage choice = Scalars(new AutoPlayChoiceCoverage());
        choice.CompletedOptions = [1, 2];
        choice.BeforeSelections = selections.ToList();
        choice.BeforeContext = context;
        choice.PolicyDeferredOptions = [3];
        choice.PolicyCheckpoints = new() { ["policy"] = new("before", true, "continuation", selections, context) };
        AutoPlayNavigationRoute navigationRoute = new("source", "destination", "event", route);
        Ff7AutoPlayState state = new()
        {
            Coverage = new()
            {
                Visits = [new() { Visit = new("visit", "region"), Events = [coverage] }],
                Choices = [choice],
                NavigationVisits = [new("navigation", "visit", [definition]) { RouteRediscovery = new() { ["route"] = true } }],
                NavigationRoutes = [navigationRoute],
                NavigationRetirements = [new(navigationRoute, "actual")]
            },
            NavigationGuards = new() { ["guard"] = new("visit", 1, [2], SpatialBoundaryTriangles: [3]) },
            ChoicePreferences = new() { ["choice"] = new(2, "native", "guest", new() { ["component"] = "value" }) },
            NativeRoute = new("event", "condition", 1, [new("before", new(1, 0))], "after"),
            Active = new("visit", "event", "condition", 1, 1, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 1, "before", SpatialDestinations: [5], TravelRoute: route, TravelSelections: selections, EntryGuardFingerprints: new Dictionary<string, string> { ["entry"] = "guard" })
            {
                ExecutionProvenance = new(1, "program", 1, 1, "condition", [new(1, 1, 1, 1, -1, -1, 0, 1, false)], "visit", "event", Ff7AutoPlayTargetKind.ActorTalk)
            }
        };
        state.Choice = new("choice", state.Active, RetiredCandidatePaths: ["retired"]) { NativeOwner = new(1, "program", 1, 1, 1, 1, [2]) };
        state.PendingRestore = new("restore", ["retired"]);
        return state;
    }

    private static void Benchmark(Ff7AutoPlayState state)
    {
        const int count = 10;
        _ = OldClone(state);
        _ = state.Clone();
        (double Elapsed, long Bytes) Measure(Func<Ff7AutoPlayState> clone)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch timer = Stopwatch.StartNew();
            for (int index = 0; index < count; index++)
            {
                GC.KeepAlive(clone());
            }
            return (timer.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        var former = Measure(() => OldClone(state));
        var current = Measure(state.Clone);
        Console.WriteLine($"Frozen clone benchmark count={count}: former={former.Elapsed:F3}ms/{former.Bytes}B; current={current.Elapsed:F3}ms/{current.Bytes}B. This is not whole-game speed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
