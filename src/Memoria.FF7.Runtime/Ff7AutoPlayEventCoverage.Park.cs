using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayEventCoverage
{
    private void ObserveParkScene(Ff7NativeTickObservation observation)
    {
        if (!Ff7FieldEventCoverageCatalog.RecognizesPark(session) || observation.Field != session.Field.Name || observation.Generation != session.FieldGeneration)
        {
            return;
        }
        FieldEntitySnapshot owner = session.Machine.Capture().Entities[0];
        int story = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
        bool entryPhase = owner.Priority == 1 && owner.ProgramCounter is >= 0x033B and <= 0x03FF;
        bool finishedReturn = observation.ScriptReturns.Any(value => value.Entity == 0 && value.Priority == 1 && value.ProgramCounter == 0x05D3);
        bool departurePhase = finishedReturn || owner.Priority == 1 && owner.ProgramCounter is >= 0x0401 and <= 0x05D3;
        if (!entryPhase && !departurePhase)
        {
            return;
        }
        string id = entryPhase ? "conditional:0:phase:entry" : "conditional:0:phase:departure";
        AutoPlayEventCoverageEntry? entry = ledger.Current?.Events.SingleOrDefault(value => value.Id == id);
        if (entry?.Status != AutoPlayEventCoverageStatus.Active && session.Host.Process.PlayerLocked)
        {
            if (ledger.Current?.Events.Any(value => value.Status == AutoPlayEventCoverageStatus.Active) == true)
            {
                return;
            }
            int sourceStory = entryPhase ? 176 : 179;
            ledger.Enter($"{session.Field.Name}:{session.Machine.ProgramIdentity}:{sourceStory}");
            Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.InspectPark(session, sourceStory);
            ledger.PublishCatalog(plan.Events.Select(target => target.Kind is Ff7FieldEventCoverageKind.Conditional or Ff7FieldEventCoverageKind.Interaction ? new AutoPlayEventCoverageEntry(target.Id, target.Availability) : new(target.Id, target.Availability, AutoPlayEventCoverageStatus.Deferred, Reason: target.Reason)), plan.Complete, plan.Reason);
            entry = ledger.Current!.Events.Single(value => value.Id == id);
            if (entry.Status == AutoPlayEventCoverageStatus.Available)
            {
                ledger.Begin(id, entry.BeforeWitness ?? $"{observation.Generation}/{observation.Tick}/resumed:{observation.ProgressWitness}");
            }
        }
        entry = ledger.Current?.Events.SingleOrDefault(value => value.Id == id);
        bool intermission = entryPhase && owner.ProgramCounter is 0x03F9 or 0x03FF && story == 179;
        bool finished = finishedReturn && story == 185;
        if (entry?.Status == AutoPlayEventCoverageStatus.Active && (intermission || finished) && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled)
        {
            ledger.Complete(id, observation.ProgressWitness, $"native park {(intermission ? "intermission 03F9" : "return 05D3")} at {observation.Generation}:{observation.Tick}");
        }
        Publish();
    }
}
