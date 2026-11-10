using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayEventCoverage
{
    partial void ObserveConditionalCoverage(Ff7NativeTickObservation observation)
    {
        ObserveRoomReset(observation);
        ObserveParkScene(observation);
        ObserveItemShopCoverage(observation);
        ObserveWallMarketScene(observation);
        ObserveWallMarketInn(observation);
        ObserveAbandonedItemStore(observation);
        if (!Ff7FieldEventCoverageCatalog.RecognizesReunion(session) || observation.Field != session.Field.Name || observation.Generation != session.FieldGeneration)
        {
            return;
        }
        FieldEntitySnapshot owner = session.Machine.Capture().Entities[2];
        bool inScene = owner.Priority == 7 && (owner.ProgramCounter is >= 0x0326 and <= 0x038D or >= 0x0396 and <= 0x03FA);
        if (!inScene)
        {
            return;
        }
        const string id = "conditional:2:script:0";
        AutoPlayEventCoverageEntry? entry = ledger.Current?.Events.SingleOrDefault(value => value.Id == id);
        if (entry is null)
        {
            ledger.Enter($"{session.Field.Name}:{session.Machine.ProgramIdentity}:173");
            Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.InspectReunion(session, true);
            ledger.PublishCatalog(plan.Events.Select(target => target.Kind == Ff7FieldEventCoverageKind.Conditional ? new AutoPlayEventCoverageEntry(target.Id, target.Availability) : new(target.Id, target.Availability, AutoPlayEventCoverageStatus.Deferred, Reason: target.Reason)), plan.Complete, plan.Reason);
            entry = ledger.Current!.Events.Single(value => value.Id == id);
        }
        if (entry.Status == AutoPlayEventCoverageStatus.Available && session.Host.Process.PlayerLocked)
        {
            ledger.Begin(id, entry.BeforeWitness ?? $"{observation.Generation}/{observation.Tick}/resumed:{observation.ProgressWitness}");
        }
        entry = ledger.Current!.Events.Single(value => value.Id == id);
        if (entry.Status == AutoPlayEventCoverageStatus.Active && owner.ProgramCounter is 0x038D or 0x03FA && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 176 && session.Machine.Variables.ReadByte(FieldBank.MapByte, 2) == 1)
        {
            ledger.Complete(id, observation.ProgressWitness, $"native conditional return {owner.ProgramCounter:X4} at {observation.Generation}:{observation.Tick}");
        }
        Publish();
    }

    private void ObserveRoomReset(Ff7NativeTickObservation observation)
    {
        AutoPlayEventCoverageEntry? active = ledger.Current?.Events.SingleOrDefault(value => value.Status == AutoPlayEventCoverageStatus.Active);
        if (active is null || ledger.Current is null || !Ff7FieldEventCoverageCatalog.CompletesOwnedReset(ledger.Current, active, observation))
        {
            return;
        }
        ledger.Complete(active.Id, observation.ProgressWitness, $"native child-owned room reset {observation.TransitionOwner!.ProgramCounter:X4} at {observation.Generation}:{observation.Tick}");
        Publish();
    }

    partial void ObserveItemShopCoverage(Ff7NativeTickObservation observation);
}
