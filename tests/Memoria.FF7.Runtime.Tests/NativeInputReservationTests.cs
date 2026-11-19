using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeInputReservationTests
{
    internal static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_RESERVATION_SAVE") ?? throw new InvalidOperationException("Select an external native navigation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7SessionSave failed;
        using (MemoryStream stream = new(bytes, writable: false))
        using (Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!))
        using (Ff7AutoPlayPolicy policy = Policy(session))
        {
            Ff7AutoplayDriver driver = new(session);
            int setup = 0;
            while (true)
            {
                driver.BeginPlanningStep();
                Ff7AutoplayInput input = policy.ChooseInput(driver);
                if (driver.PlanningPending)
                {
                    break;
                }
                Check(++setup <= 4 && session.Battle is null, "The native fixture did not reach detached planning within its bounded readiness setup.");
                driver.Apply(input);
                session.AdvanceNavigationTick();
            }
            Check(policy.State.NativeRoute is null, "The fresh-planning fixture already owned a native trace.");
            Ff7AutoPlayPolicy.TrackFrontierProgress(policy.State, Story(session));
            string key = policy.State.FrontierProgressKey!;
            policy.State.FrontierIdleSteps = Ff7AutoPlayPolicy.FrontierIdleLimit - 1;
            string native = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
            long tick = session.Host.Process.Tick;
            Stopwatch elapsed = Stopwatch.StartNew();
            bool stopped = false;
            int planning = 0;
            while (planning++ < 5000 && elapsed.Elapsed < TimeSpan.FromMinutes(2))
            {
                driver.BeginPlanningStep();
                try
                {
                    policy.ChooseInput(driver);
                }
                catch (TimeoutException error) when (error.Message.Contains("frontier work", StringComparison.Ordinal))
                {
                    stopped = true;
                    break;
                }
                Check(driver.PlanningPending && policy.State.FrontierIdleSteps == Ff7AutoPlayPolicy.FrontierIdleLimit - 1 && policy.State.FrontierProgressKey == key, "Fresh native planning consumed the counter or returned an unexpected uncommitted input.");
                Check(session.Host.Process.Tick == tick && Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) == native, "Detached planning advanced its real native source.");
            }
            Check(stopped && policy.State.FrontierProgressKey == key, "The actual fresh proof did not hit its unchanged pre-application frontier limit.");
            Check(policy.State.NativeRoute is { Cursor: 0 } fresh && fresh.Steps[0].Before == native && session.AutoPlayState!.NativeRoute is { Cursor: 0 } && session.AutoPlayState.FrontierIdleSteps == Ff7AutoPlayPolicy.FrontierIdleLimit, "Counter failure consumed a freshly planned native input or lost its exhausted published budget.");
            Check(session.Host.Process.Tick == tick && Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) == native && Reservation(policy) is null, "Fresh-proof rollback changed native state or retained an ephemeral input reservation.");
            failed = session.Capture();
            Console.WriteLine($"Fresh native reservation: readiness={setup}, planning={planning}, trace={policy.State.NativeRoute!.Steps.Length}, cursor=0, counter={policy.State.FrontierIdleSteps}, source unchanged.");
        }
        using (Ff7Session session = Ff7Session.Restore(installation, failed))
        using (Ff7AutoPlayPolicy policy = Policy(session))
        {
            Ff7AutoPlayNativeRoute proof = policy.State.NativeRoute!;
            string native = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
            Check(proof.Cursor == 0 && proof.Steps[0].Before == native, "Cold restore lost the unapplied native source contract.");
            policy.State.FrontierIdleSteps = Ff7AutoPlayPolicy.FrontierIdleLimit - 1;
            bool stopped = false;
            try
            {
                policy.ChooseInput(new(session));
            }
            catch (TimeoutException error) when (error.Message.Contains("frontier work", StringComparison.Ordinal))
            {
                stopped = true;
            }
            Check(stopped && ReferenceEquals(policy.State.NativeRoute, proof) && session.AutoPlayState!.NativeRoute is { Cursor: 0 } && session.AutoPlayState.FrontierIdleSteps == Ff7AutoPlayPolicy.FrontierIdleLimit && native == Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) && Reservation(policy) is null, "Counter failure consumed an already owned native input or broke cold source authentication.");
        }
        using (Ff7Session session = Ff7Session.Restore(installation, failed))
        using (Ff7AutoPlayPolicy policy = Policy(session))
        {
            Ff7AutoPlayNativeRoute proof = policy.State.NativeRoute!;
            policy.State.FrontierIdleSteps = Ff7AutoPlayPolicy.FrontierIdleLimit - 2;
            Ff7AutoplayDriver driver = new(session);
            Ff7AutoplayInput input = policy.ChooseInput(driver);
            Check(input == proof.Steps[0].Input && policy.State.FrontierIdleSteps == Ff7AutoPlayPolicy.FrontierIdleLimit - 1 && policy.State.NativeRoute is { Cursor: 1 } && Reservation(policy) is null, "A committed genuine input did not advance exactly one counter and cursor before application.");
            driver.Apply(input);
            session.AdvanceNavigationTick();
            string expected = proof.Steps.Length > 1 ? proof.Steps[1].Before : proof.After;
            Check(Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) == expected, "The committed input no longer reaches its real native proof endpoint.");
        }
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Native input reservation tests changed their standalone source.");
        Console.WriteLine("PASS fresh/existing pre-application reservation rollback, cold source validity and one genuine committed native input; standalone unchanged.");
    }

    private static Ff7AutoPlayPolicy Policy(Ff7Session session)
    {
        return new(session, (_, _) => throw new InvalidOperationException("The native reservation fixture unexpectedly requested a choice checkpoint."));
    }

    private static short Story(Ff7Session session)
    {
        return session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
    }

    private static object? Reservation(Ff7AutoPlayPolicy policy)
    {
        return typeof(Ff7AutoPlayPolicy).GetField("nativeInputReservation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(policy);
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
