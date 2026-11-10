using System.Numerics;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayEventCoverage
{
    private void ChooseWallMarketBoutiqueInput(Ff7AutoplayDriver driver, AutoPlayEventCoverageEntry entry, ref Ff7AutoplayInput input, ref bool handled)
    {
        if (!Ff7FieldEventCoverageCatalog.RecognizesWallMarketBoutique(session) || entry.Id != "conditional:9:script:15")
        {
            return;
        }
        ledger.Arm(entry.Id, $"{session.FieldGeneration}/{session.Machine.Tick}/{session.CaptureEventProgressWitness()}");
        Ff7Line counter = session.Host.Process.Lines.Single(line => line.Entity == 2);
        input = counter.Touch
            ? new(Held: (ushort)((session.Machine.Input.Held & 0x20) == 0 ? 0x20 : 0))
            : driver.Approach(new Vector3((counter.X1 + counter.X2) / 2f, (counter.Y1 + counter.Y2) / 2f, (counter.Z1 + counter.Z2) / 2f));
        Publish();
        handled = true;
    }

    private void ObserveWallMarketBoutique(Ff7NativeTickObservation observation)
    {
        if (!Ff7FieldEventCoverageCatalog.RecognizesWallMarketBoutique(session) || session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) != 188 || observation.Field != session.Field.Name || observation.Generation != session.FieldGeneration)
        {
            return;
        }
        const string id = "conditional:9:script:15";
        AutoPlayEventCoverageEntry? entry = ledger.Current?.Events.SingleOrDefault(value => value.Id == id);
        if (entry is null)
        {
            return;
        }
        FieldEntitySnapshot owner = session.Machine.Capture().Entities[9];
        if (entry.Status == AutoPlayEventCoverageStatus.Available && owner.Priority == 6 && owner.ProgramCounter is >= 0x0DAF and <= 0x0DC0 && ledger.Current!.Events.All(value => value.Status != AutoPlayEventCoverageStatus.Active))
        {
            ledger.Begin(id, entry.BeforeWitness ?? $"{observation.Generation}/{observation.Tick}/{observation.ProgressWitness}");
        }
        entry = ledger.Current!.Events.Single(value => value.Id == id);
        if (entry.Status == AutoPlayEventCoverageStatus.Active && observation.ScriptReturns.Any(value => value.Entity == 9 && value.Priority == 6 && value.ProgramCounter == 0x0DC0))
        {
            ledger.Complete(id, observation.ProgressWitness, $"native boutique counter return 0DC0 at {observation.Generation}:{observation.Tick}");
        }
        Publish();
    }
}
