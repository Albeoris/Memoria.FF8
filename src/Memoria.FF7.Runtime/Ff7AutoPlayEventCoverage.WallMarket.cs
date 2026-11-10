using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayEventCoverage
{
    private void ObserveWallMarketScene(Ff7NativeTickObservation observation)
    {
        if (!Ff7FieldEventCoverageCatalog.RecognizesWallMarket(session) || observation.Field != session.Field.Name || observation.Generation != session.FieldGeneration)
        {
            return;
        }
        FieldEntitySnapshot owner = session.Machine.Capture().Entities[2];
        bool finished = observation.ScriptReturns.Any(returned => returned.Entity == 2 && returned.Priority == 6 && returned.ProgramCounter == 0x07B6);
        bool inScene = owner.Priority == 6 && owner.ProgramCounter is >= 0x079E and <= 0x07B6;
        if (!inScene && !finished)
        {
            return;
        }
        const string id = "conditional:2:script:1";
        AutoPlayEventCoverageEntry? entry = ledger.Current?.Events.SingleOrDefault(value => value.Id == id);
        if (entry?.Status != AutoPlayEventCoverageStatus.Active && session.Host.Process.PlayerLocked)
        {
            ledger.Enter($"{session.Field.Name}:{session.Machine.ProgramIdentity}:185");
            Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
            ledger.PublishCatalog(plan.Events.Select(target => target.Id == id ? new AutoPlayEventCoverageEntry(target.Id, target.Availability) : new(target.Id, target.Availability, AutoPlayEventCoverageStatus.Deferred, Reason: target.Reason)), plan.Complete, plan.Reason);
            entry = ledger.Current!.Events.Single(value => value.Id == id);
            if (entry.Status == AutoPlayEventCoverageStatus.Available)
            {
                ledger.Begin(id, entry.BeforeWitness ?? $"{observation.Generation}/{observation.Tick}/resumed:{observation.ProgressWitness}");
            }
        }
        entry = ledger.Current?.Events.SingleOrDefault(value => value.Id == id);
        if (entry?.Status == AutoPlayEventCoverageStatus.Active && finished && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188 && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled)
        {
            ledger.Complete(id, observation.ProgressWitness, $"native arrival return 07B6 at {observation.Generation}:{observation.Tick}");
        }
        Publish();
    }
}
