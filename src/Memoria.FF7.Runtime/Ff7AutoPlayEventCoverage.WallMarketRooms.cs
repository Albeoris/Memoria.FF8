using System.Numerics;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayEventCoverage
{
    private void ChooseWallMarketInnInput(Ff7AutoplayDriver driver, AutoPlayEventCoverageEntry entry, ref Ff7AutoplayInput input, ref bool handled)
    {
        if (!Ff7FieldEventCoverageCatalog.RecognizesWallMarketInn(session) || entry.Id != "conditional:11:script:3")
        {
            return;
        }
        ledger.Arm(entry.Id, $"{session.FieldGeneration}/{session.Machine.Tick}/{session.CaptureEventProgressWitness()}");
        Ff7Line counter = session.Host.Process.Lines.Single(line => line.Entity == 7);
        input = counter.Touch
            ? new(Held: (ushort)((session.Machine.Input.Held & 0x20) == 0 ? 0x20 : 0))
            : driver.Approach(new Vector3((counter.X1 + counter.X2) / 2f, (counter.Y1 + counter.Y2) / 2f, (counter.Z1 + counter.Z2) / 2f));
        Publish();
        handled = true;
    }

    private void ObserveWallMarketInn(Ff7NativeTickObservation observation)
    {
        if (!Ff7FieldEventCoverageCatalog.RecognizesWallMarketInn(session) || session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) != 188 || observation.Field != session.Field.Name || observation.Generation != session.FieldGeneration)
        {
            return;
        }
        const string id = "conditional:11:script:3";
        AutoPlayEventCoverageEntry? entry = ledger.Current?.Events.SingleOrDefault(value => value.Id == id);
        if (entry is null)
        {
            return;
        }
        FieldEntitySnapshot owner = session.Machine.Capture().Entities[11];
        if (entry.Status == AutoPlayEventCoverageStatus.Available && owner.Priority == 5 && owner.ProgramCounter is >= 0x0666 and <= 0x06EF && session.Host.Process.PlayerLocked)
        {
            ledger.Begin(id, entry.BeforeWitness ?? $"{observation.Generation}/{observation.Tick}/{observation.ProgressWitness}");
        }
        entry = ledger.Current!.Events.Single(value => value.Id == id);
        bool returned = observation.ScriptReturns.Any(value => value.Entity == 11 && value.Priority == 5 && value.ProgramCounter == 0x06EF);
        if (entry.Status == AutoPlayEventCoverageStatus.Active && returned && session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0A) == 3 && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled)
        {
            ledger.Complete(id, observation.ProgressWitness, $"native inn decline return 06EF at {observation.Generation}:{observation.Tick}");
        }
        Publish();
    }
}
