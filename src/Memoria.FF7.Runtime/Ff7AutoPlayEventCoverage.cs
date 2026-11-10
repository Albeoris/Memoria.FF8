using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

/// <summary>Observes native interactions and completes actor coverage before permitting story navigation.</summary>
public sealed partial class Ff7AutoPlayEventCoverage : IDisposable
{
    private readonly Ff7Session session;
    private readonly AutoPlayEventCoverageLedger ledger;
    private bool disposed;

    public Ff7AutoPlayEventCoverage(Ff7Session session)
    {
        this.session = session;
        session.CoverageOwner?.Dispose();
        session.CoverageOwner = this;
        ledger = new(session.EventCoverage);
        session.InteractionScriptRequested += ObserveInteraction;
        session.NativeTickCompleted += ObserveCompletedTick;
    }

    public bool CanLeaveField => ledger.CanLeave;
    public AutoPlayEventCoverageState Capture() => ledger.Capture();

    public bool TryChooseInput(Ff7AutoplayDriver driver, out Ff7AutoplayInput input)
    {
        input = default;
        if (session.World is not null)
        {
            return false;
        }
        if (session.HasPendingTick || session.Host.Process.Tick == 0 || session.Host.Process.PlayerLocked || session.Host.Process.Join is not null || session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed))
        {
            return true;
        }
        if (ledger.Current?.Events.Any(entry => entry.Status == AutoPlayEventCoverageStatus.Active) == true)
        {
            Publish();
            return true;
        }
        string scope = $"{session.Field.Name}:{session.Machine.ProgramIdentity}:{session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)}";
        ledger.Enter(scope);
        Ff7Actor[] actors = session.Host.Process.Actors.Where(actor => actor.EntityBound && !actor.PlayerControlled && actor.TalkEnabled && HasTalkScript(actor.Entity)).OrderBy(actor => actor.Entity).ToArray();
        AutoPlayEventCoverageEntry[] catalog = actors.Select(actor => new AutoPlayEventCoverageEntry(EventId(actor.Entity), Availability(actor))).ToArray();
        List<AutoPlayEventCoverageEntry> allEvents = catalog.ToList();
        bool complete = false;
        string reason = "Actor talk discovery is available; conditional, LINE and exit-causing event coverage requires its native catalog.";
        ExtendCoverageCatalog(allEvents, ref complete, ref reason);
        ledger.PublishCatalog(allEvents, complete, reason);
        HashSet<string> available = allEvents.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        foreach (AutoPlayEventCoverageEntry entry in ledger.Current!.Events.Where(entry => entry.Status == AutoPlayEventCoverageStatus.Available && !available.Contains(entry.Id)))
        {
            ledger.Defer(entry.Id, "The native owner no longer exposes this talk interaction in the current state.");
        }
        Publish();
        if (!complete)
        {
            throw new NotSupportedException($"FFVII autoplay event coverage for {session.Field.Name} requires the native conditional and LINE-event catalog before physical interaction or a story exit.");
        }
        AutoPlayEventCoverageEntry? next = ledger.Current!.Events.FirstOrDefault(entry => entry.Status == AutoPlayEventCoverageStatus.Available && available.Contains(entry.Id));
        if (next is not null)
        {
            bool run = true;
            ConfigureCoverageNavigation(driver, ref run);
            if (next.Id.StartsWith("actor:", StringComparison.Ordinal) && TryReadTarget(next.Id, out int entity, out int script))
            {
                if (script == 1)
                {
                    input = driver.TalkTo(entity, run);
                }
                else if (script == 2)
                {
                    input = driver.Contact(entity, run);
                }
                else
                {
                    throw new NotSupportedException($"FFVII actor event {next.Id} requires its native physical activation contract.");
                }
            }
            else
            {
                bool handled = false;
                ChooseLineCoverageInput(driver, next, ref input, ref handled);
                if (!handled)
                {
                    throw new NotSupportedException($"FFVII native coverage event {next.Id} requires its physical interaction policy.");
                }
            }
            return true;
        }
        if (!ledger.CanLeave)
        {
            throw new NotSupportedException($"FFVII autoplay event coverage for {session.Field.Name} requires the native conditional and LINE-event catalog before a story exit.");
        }
        return false;
    }

    private void ObserveInteraction(int entity, int script, bool accepted)
    {
        if (!accepted || ledger.Current is null)
        {
            return;
        }
        AutoPlayEventCoverageEntry? requested = ledger.Current.Events.FirstOrDefault(entry => entry.Status == AutoPlayEventCoverageStatus.Available && TryReadTarget(entry.Id, out int target, out int slot) && target == entity && slot == script);
        if (requested is not null)
        {
            ledger.Begin(requested.Id, $"{session.FieldGeneration}/{session.Machine.Tick}/{session.CaptureEventProgressWitness()}");
            Publish();
        }
    }

    private void ObserveCompletedTick(Ff7NativeTickObservation observation)
    {
        string? coveredScope = ledger.CanLeave && ledger.Current?.Id.StartsWith(observation.Field + ":", StringComparison.Ordinal) == true ? ledger.Current.Id : null;
        ObserveConditionalCoverage(observation);
        ObserveCompletedActorTick(observation);
        if (ledger.CanLeave && ledger.Current?.Id.StartsWith(observation.Field + ":", StringComparison.Ordinal) == true)
        {
            coveredScope = ledger.Current.Id;
        }
        ObserveTourCoverage(observation, coveredScope);
    }

    private void ObserveCompletedActorTick(Ff7NativeTickObservation observation)
    {
        AutoPlayEventCoverageEntry? active = ledger.Current?.Events.SingleOrDefault(entry => entry.Status == AutoPlayEventCoverageStatus.Active);
        int separator = active?.BeforeWitness?.IndexOf('/') ?? -1;
        if (active is null || active.BeforeWitness is null || separator < 0 || !long.TryParse(active.BeforeWitness.AsSpan(0, separator), out long generation) || observation.Generation != generation || !TryReadTarget(active.Id, out int entity, out _))
        {
            return;
        }
        FieldScriptReturn? returned = observation.ScriptReturns.FirstOrDefault(value => value.Entity == entity && value.Priority == 1);
        if (returned is not null)
        {
            ledger.Complete(active.Id, observation.ProgressWitness, $"native return {returned.ProgramCounter:X4} at {observation.Generation}:{observation.Tick}");
        }
        else if (observation.TransitionOwner is { Priority: 1 } owner && owner.Entity == entity)
        {
            ledger.Complete(active.Id, observation.ProgressWitness, $"native field handoff {owner.ProgramCounter:X4} at {observation.Generation}:{observation.Tick}");
        }
        else if (observation.Transition is not null)
        {
            ledger.Defer(active.Id, "A native field handoff interrupted the interaction without a proved owner completion.");
        }
        Publish();
    }

    private bool HasTalkScript(int entity)
    {
        int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[1];
        return session.Field.Scripts.Data.Span[entry] != 0;
    }

    private string Availability(Ff7Actor actor)
    {
        return $"{session.Field.Scripts.Entities[actor.Entity].ScriptOffsets[1]:X4}:{actor.Model}:{actor.TalkRadius}";
    }

    private static string EventId(int entity) => $"actor:{entity}:script:1";
    private static bool TryReadTarget(string id, out int entity, out int script)
    {
        string[] parts = id.Split(':');
        entity = -1;
        script = -1;
        return parts.Length == 4 && parts[2] == "script" && int.TryParse(parts[1], out entity) && int.TryParse(parts[3], out script) && entity is >= 0 and <= 255 && script is >= 1 and <= 31;
    }

    partial void ExtendCoverageCatalog(List<AutoPlayEventCoverageEntry> catalog, ref bool complete, ref string reason);
    partial void ObserveConditionalCoverage(Ff7NativeTickObservation observation);
    partial void ConfigureCoverageNavigation(Ff7AutoplayDriver driver, ref bool run);
    partial void ChooseLineCoverageInput(Ff7AutoplayDriver driver, AutoPlayEventCoverageEntry entry, ref Ff7AutoplayInput input, ref bool handled);
    private void Publish() => session.EventCoverage = ledger.Capture();

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        session.InteractionScriptRequested -= ObserveInteraction;
        session.NativeTickCompleted -= ObserveCompletedTick;
        if (ReferenceEquals(session.CoverageOwner, this))
        {
            session.CoverageOwner = null;
        }
        disposed = true;
    }
}
