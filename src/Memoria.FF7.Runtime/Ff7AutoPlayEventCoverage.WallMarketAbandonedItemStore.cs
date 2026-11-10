using System.Numerics;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayEventCoverage
{
    private void ChooseAbandonedItemStoreInput(Ff7AutoplayDriver driver, AutoPlayEventCoverageEntry entry, ref Ff7AutoplayInput input, ref bool handled)
    {
        if (!Ff7FieldEventCoverageCatalog.RecognizesWallMarketAbandonedItemStore(session) || entry.Id != "conditional:4:script:1")
        {
            return;
        }
        ledger.Arm(entry.Id, $"{session.FieldGeneration}/{session.Machine.Tick}/{session.CaptureEventProgressWitness()}");
        Ff7Line terminal = session.Host.Process.Lines.Single(line => line.Entity == 5);
        input = terminal.Touch
            ? new(Held: (ushort)((session.Machine.Input.Held & 0x20) == 0 ? 0x20 : 0))
            : driver.Approach(new Vector3((terminal.X1 + terminal.X2) / 2f, (terminal.Y1 + terminal.Y2) / 2f, terminal.Z1));
        Publish();
        handled = true;
    }

    private void ObserveAbandonedItemStore(Ff7NativeTickObservation observation)
    {
        if (!Ff7FieldEventCoverageCatalog.RecognizesWallMarketAbandonedItemStore(session) || observation.Field != session.Field.Name || observation.Generation != session.FieldGeneration)
        {
            return;
        }
        const string id = "conditional:4:script:1";
        AutoPlayEventCoverageEntry? entry = ledger.Current?.Events.SingleOrDefault(value => value.Id == id);
        if (entry is null)
        {
            return;
        }
        FieldEntitySnapshot child = session.Machine.Capture().Entities[4];
        if (entry.Status == AutoPlayEventCoverageStatus.Available && child.Priority == 5 && child.ProgramCounter is >= 0x05CA and <= 0x0609 && session.Host.Process.PlayerLocked)
        {
            ledger.Begin(id, entry.BeforeWitness ?? $"{observation.Generation}/{observation.Tick}/{observation.ProgressWitness}");
        }
        entry = ledger.Current!.Events.Single(value => value.Id == id);
        if (entry.Status == AutoPlayEventCoverageStatus.Active && observation.ScriptReturns.Any(receipt => receipt.Entity == 5 && receipt.Priority == 1 && receipt.ProgramCounter == 0x0772) && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled)
        {
            ledger.Complete(id, observation.ProgressWitness, $"native terminal parent return0772 after child0609 at{observation.Generation}:{observation.Tick}");
        }
        Publish();
    }
}
