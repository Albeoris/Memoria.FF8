using System.Numerics;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayEventCoverage
{
    partial void ChooseItemShopCoverageInput(Ff7AutoplayDriver driver, AutoPlayEventCoverageEntry entry, ref Ff7AutoplayInput input, ref bool handled)
    {
        if (!Ff7FieldEventCoverageCatalog.RecognizesWallMarketItemShop(session) || entry.Id != "line:2:script:1")
        {
            return;
        }
        Ff7Line counter = session.Host.Process.Lines.Single(line => line.Entity == 2);
        Vector3 midpoint = new((counter.X1 + counter.X2) / 2f, (counter.Y1 + counter.Y2) / 2f, counter.Z1);
        input = counter.OnLine ? driver.Steer(midpoint, run: false) with { Talk = session.Host.Process.Tick % 16 == 0 } : driver.Approach(midpoint, run: false);
        handled = true;
    }

    partial void ObserveItemShopCoverage(Ff7NativeTickObservation observation)
    {
        if (!Ff7FieldEventCoverageCatalog.RecognizesWallMarketItemShop(session) || observation.Field != session.Field.Name || observation.Generation != session.FieldGeneration || !session.Host.Process.PlayerLocked)
        {
            return;
        }
        FieldVmSnapshot snapshot = session.Machine.Capture();
        FieldEntitySnapshot line = snapshot.Entities[2];
        FieldEntitySnapshot counter = snapshot.Entities[7];
        if (line.Priority != 1 || line.ProgramCounter != 0x0303 || counter.Priority != 6 || counter.ProgramCounter is < 0x03E0 or > 0x04F5)
        {
            return;
        }
        const string id = "line:2:script:1";
        string scope = $"{session.Field.Name}:{session.Machine.ProgramIdentity}:188";
        if (ledger.Current?.Id == scope && ledger.Current.Events.Any(entry => entry.Id == id && entry.Status is AutoPlayEventCoverageStatus.Active or AutoPlayEventCoverageStatus.Completed))
        {
            return;
        }
        Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
        if (!plan.Complete)
        {
            return;
        }
        ledger.Enter(scope);
        ledger.PublishCatalog(plan.Events.Select(target => target.Kind == Ff7FieldEventCoverageKind.Interaction ? new AutoPlayEventCoverageEntry(target.Id, target.Availability) : new(target.Id, target.Availability, AutoPlayEventCoverageStatus.Deferred, Reason: target.Reason)), true, plan.Reason);
        ledger.Begin(id, $"{observation.Generation}/{observation.Tick}/resumed:{observation.ProgressWitness}");
        Publish();
    }
}
