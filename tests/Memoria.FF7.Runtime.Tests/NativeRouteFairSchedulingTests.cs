using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRouteFairSchedulingTests
{
    internal static void Run()
    {
        CheckNativeWaitBeyond256();
        CheckNestedFairnessAndCancellation();
        CheckTerminalQuantum();
        CheckLongColdRoute();
        Console.WriteLine("Native route fair scheduling checks passed.");
    }

    private static void CheckNativeWaitBeyond256()
    {
        Ff7FieldHost host = new(NativeDialogueTests.Field(), (_, _) => 1, new() { PlayerLocked = true });
        FieldMachine machine = new(new FieldProgram([0, 0x24, 44, 1, 0x33, 0, 0], [new ushort[32]]), host: host);
        machine.Initialize();
        bool admitted = false;
        int released = 0;
        int nativeRequests = 0;
        long acceptedTick = -1;
        host.NativeOperationExecuted += (request, reply) =>
        {
            Check(request.Command is FieldControlCommand { Property: FieldControlKind.PlayerLocked, Value: 0 } && request.Entity == 0 && request.Priority == 7 && request.ProgramCounter == 4 && reply.Completed, "The native wait completed through an unrelated owner or fabricated control receipt.");
            nativeRequests++;
            acceptedTick = machine.Tick;
        };
        IEnumerable<int> Infinite()
        {
            try
            {
                while (true)
                {
                    yield return 0;
                }
            }
            finally
            {
                released++;
            }
        }
        IEnumerable<int> Native()
        {
            using Ff7NativeRouteClockWitness clock = new(machine);
            try
            {
                while (host.Process.PlayerLocked)
                {
                    yield return 0;
                    machine.AdvanceTick();
                }
                Check(clock.Independent, "The native wait fixture unexpectedly observed a native clock variable.");
                admitted = true;
            }
            finally
            {
                released++;
            }
        }
        using IEnumerator<int> work = Ff7AutoplayDriver.EnumerateFairNativeCandidates([Infinite(), Native()], () => admitted).GetEnumerator();
        int scheduling = 0;
        while (!admitted && scheduling++ < 1000)
        {
            long before = machine.Tick;
            bool pending = work.MoveNext();
            Check(machine.Tick - before is 0 or 1, "One cooperative allowance executed more than one native callback.");
            Check(pending || admitted, "Unresolved native work became a false terminal refusal.");
            if (machine.Tick <= 256)
            {
                Check(!admitted && nativeRequests == 0, "The native owner was admitted before its real wait completed.");
            }
        }
        Check(admitted && acceptedTick > 256 && nativeRequests == 1 && released == 2 && !host.Process.PlayerLocked, "An infinite first candidate starved a later native owner beyond 256 callbacks or leaked its sibling.");
    }

    private static void CheckNestedFairnessAndCancellation()
    {
        const int targetCount = 64;
        int active = 0;
        int peak = 0;
        bool accepted = false;
        int laterCallbacks = 0;
        Ff7AutoPlayState state = new();
        string before = JsonSerializer.Serialize(state);
        IEnumerable<int> Branch(int target, int mode, int direction)
        {
            active++;
            peak = Math.Max(peak, active);
            try
            {
                while (true)
                {
                    yield return 0;
                    if (target == targetCount - 1 && mode == 1 && direction == 15 && ++laterCallbacks == 300)
                    {
                        accepted = true;
                        yield break;
                    }
                }
            }
            finally
            {
                active--;
            }
        }
        IEnumerable<int> Mode(int target, int mode)
        {
            return Ff7AutoplayDriver.EnumerateFairNativeCandidates(Enumerable.Range(0, 16).Select(direction => Branch(target, mode, direction)), () => accepted);
        }
        IEnumerable<int> Target(int target)
        {
            return Ff7AutoplayDriver.EnumerateFairNativeCandidates([Mode(target, 0), Mode(target, 1)], () => accepted);
        }
        long baseline = GC.GetTotalMemory(true);
        using (IEnumerator<int> work = Ff7AutoplayDriver.EnumerateFairNativeCandidates(Enumerable.Range(0, targetCount).Select(Target), () => accepted).GetEnumerator())
        {
            for (int scheduling = 0; scheduling < targetCount * 2 * 16 * 2; scheduling++)
            {
                Check(work.MoveNext() && !accepted, "An unresolved nested group acquired a false final admission.");
            }
            Check(active == targetCount * 2 * 16 && peak == active && state.SourceRevalidationAttempts.Count == 0 && state.NativeRoute is null && before == JsonSerializer.Serialize(state), "Fair lazy setup lost a candidate or changed source-task, guard or completion metadata while pending.");
            long retained = GC.GetTotalMemory(true) - baseline;
            Console.WriteLine($"Fair authored setup: targets={targetCount}, live candidates={active}, retained managed delta={retained} bytes.");
        }
        Check(active == 0 && !accepted && state.SourceRevalidationAttempts.Count == 0, "Cancelling nested target/mode/direction work leaked candidates or completed a pending task.");
        laterCallbacks = 0;
        using (IEnumerator<int> work = Ff7AutoplayDriver.EnumerateFairNativeCandidates([Target(0), Target(targetCount - 1)], () => accepted).GetEnumerator())
        {
            while (!accepted && work.MoveNext())
            {
            }
        }
        Check(accepted && laterCallbacks == 300 && active == 0, "An unknown earlier target, mode or direction starved a later valid branch beyond 256 callbacks.");
        Check(before == JsonSerializer.Serialize(state), "Fair scheduling fabricated native admission or task history instead of returning its existing branch result.");
    }

    private static void CheckTerminalQuantum()
    {
        int callbacks = 0;
        int disposed = 0;
        IEnumerable<int> Terminal()
        {
            try
            {
                yield return 0;
                callbacks++;
            }
            finally
            {
                disposed++;
            }
        }
        using IEnumerator<int> work = Ff7AutoplayDriver.EnumerateFairNativeCandidates([Terminal(), Terminal()], () => false).GetEnumerator();
        while (true)
        {
            int before = callbacks;
            bool pending = work.MoveNext();
            Check(callbacks - before <= 1, "A terminal candidate consumed another branch's cooperative allowance.");
            if (!pending)
            {
                break;
            }
        }
        Check(callbacks == 2 && disposed == 2, "Actual terminal branches were truncated or disposed more than once.");
    }

    private static void CheckLongColdRoute()
    {
        Ff7AutoPlayRouteStep[] steps = Enumerable.Range(0, 600).Select(_ => new Ff7AutoPlayRouteStep(new('A', 64), new(1, 0))).ToArray();
        Ff7AutoPlayState state = new() { NativeRoute = new("target", "condition", 1, steps, new('B', 64), Cursor: 300) };
        state.Validate();
        Ff7AutoPlayState cold = state.Clone();
        Check(cold.NativeRoute is { Steps.Length: 600, Cursor: 300 } && cold.NativeRoute.Steps.SequenceEqual(steps), "A valid native continuation beyond 256 callbacks was rejected or truncated on cold restore.");
        cold.NativeRoute = cold.NativeRoute! with { Cursor = 601 };
        bool rejected = false;
        try
        {
            cold.Validate();
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected, "Removing the route length horizon also removed actual cursor corruption validation.");
    }

    internal static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_FAIR_SAVE") ?? throw new InvalidOperationException("Select an external native navigation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        using Ff7Session source = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(stream).Ff7!);
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(source).Observe();
        Ff7AutoPlayState state = source.AutoPlayState ?? throw new InvalidOperationException("The selected fixture has no native source navigation state.");
        Ff7AutoPlayTarget target = observation.Events.First(target => target.Kind == Ff7AutoPlayTargetKind.Gateway && state.NavigationGuards.Values.Any(guard => guard.EventId == target.Id && guard.NavigationKey == observation.NavigationKey && guard.MovementFailure is not null));
        string native = JsonSerializer.Serialize(source.Capture());
        string metadata = JsonSerializer.Serialize(state);
        Ff7AutoplayDriver driver = new(source);
        Ff7NativeRouteProbe probe = new() { SourceRelease = (candidate, actual) => Ff7AutoPlayPolicy.CanReleaseNativeSource(state, observation, target, candidate, actual) };
        IEnumerable<int>[] modes = driver.EnumerateNativeRouteModes(target, observation.Events, probe);
        long baseline = GC.GetTotalMemory(true);
        long peak = baseline;
        int scheduling = 0;
        Stopwatch elapsed = Stopwatch.StartNew();
        using (IEnumerator<int> work = Ff7AutoplayDriver.EnumerateFairNativeCandidates(modes, () => probe.Result is not null).GetEnumerator())
        {
            while (probe.Result is null && scheduling++ < 10000 && elapsed.Elapsed < TimeSpan.FromSeconds(30))
            {
                Check(work.MoveNext() || probe.Result is not null, "All native candidates ended without the fixture's known positive source release.");
                if (scheduling % 64 == 0)
                {
                    peak = Math.Max(peak, GC.GetTotalMemory(true));
                }
            }
        }
        Check(probe.Result is { SourceReleased: true } && driver.ActiveNativeRouteCandidates == 0, "The bounded positive original-source diagnostic did not release its actual source or dispose all forks.");
        Check(native == JsonSerializer.Serialize(source.Capture()) && metadata == JsonSerializer.Serialize(state) && bytes.SequenceEqual(File.ReadAllBytes(path)), "Fair detached proof changed native source, guards, attempt history or standalone bytes.");
        Console.WriteLine($"Fair original source: target={target.Id}, scheduling={scheduling}, native prefix={probe.Result!.Steps.Length}, peak live forks={driver.PeakNativeRouteCandidates}, sampled retained managed delta={peak - baseline} bytes, elapsed={elapsed.Elapsed.TotalSeconds:F2}s; original source unchanged.");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
