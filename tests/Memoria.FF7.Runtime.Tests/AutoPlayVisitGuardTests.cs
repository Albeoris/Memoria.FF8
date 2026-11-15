using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class AutoPlayVisitGuardTests
{
    public static void Run()
    {
        FieldVariables variables = new();
        variables.WriteByte(FieldBank.Persistent0Byte, 8, 1);
        variables.WriteWord(FieldBank.Persistent1Word, 12, -1234);
        Ff7Progress progress = new() { InitialKernelData = [1], Characters = [], Party = [255, 255, 255], Inventory = [], Materia = [], Gil = 500, AvailableCharacters = 4 };
        Ff7FieldProcess process = new();
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        planner.Observe(new("remote-field", "resource"), [new("talk", AutoPlayEventKind.Interaction)]);
        planner.ObserveNavigation("remote-field", "remote-component", [new("talk", AutoPlayEventKind.Interaction)]);
        AutoPlayVisit remote = new("remote-component", "resource");
        Check(NeedsRefresh(), "A legacy visit without an observational guard must request native reobservation.");
        FieldVariableRead[] reads = [new(FieldBank.Persistent0Byte, 8, false), new(FieldBank.Persistent1Word, 12, true), new(FieldBank.MapWord, 2, true), new(FieldBank.MapByte, 4, false), new(FieldBank.Immediate, 7, false)];
        FieldHostRead[] hosts = [new(FieldHostReadKind.Gil), new(FieldHostReadKind.ItemQuantity, 23), new(FieldHostReadKind.PartyAvailable, 2), new(FieldHostReadKind.MateriaCapacity), new(FieldHostReadKind.Actor, 99), new(FieldHostReadKind.PreviousMap)];
        string beforeVariables = JsonSerializer.Serialize(new { Persistent = variables.CopyPersistent(), Map = variables.CopyMap() });
        string beforeProgress = JsonSerializer.Serialize(progress);
        state.VisitGuards["remote-field"] = Capture();
        Check(state.VisitGuards["remote-field"].Reads.Length == 2 && state.VisitGuards["remote-field"].HostReads.Length == 4, "Visit guards must export only persistent and canonical global host dependencies.");
        Check(!NeedsRefresh(), "Observing a legacy visit must bound its missing-guard refresh.");
        Check(beforeVariables == JsonSerializer.Serialize(new { Persistent = variables.CopyPersistent(), Map = variables.CopyMap() }) && beforeProgress == JsonSerializer.Serialize(progress), "Capturing and comparing a visit guard mutated native variables or progress.");
        variables.WriteByte(FieldBank.Persistent0Byte, 9, 17);
        variables.WriteWord(FieldBank.MapWord, 2, 23);
        variables.WriteByte(FieldBank.MapByte, 4, 91);
        Check(!NeedsRefresh(), "Unrelated persistent or field-local scratch writes must not refresh a remote visit.");
        variables.WriteByte(FieldBank.Persistent0Byte, 8, 2);
        Check(NeedsRefresh(), "A changed observed global byte must refresh the remote visit.");
        state.VisitGuards["remote-field"] = Capture();
        variables.WriteWord(FieldBank.Persistent1Word, 12, -1233);
        Check(NeedsRefresh(), "A changed observed global word must refresh the remote visit.");
        state.VisitGuards["remote-field"] = Capture();
        progress = progress with { Gil = 499 };
        Check(NeedsRefresh(), "A changed observed Gil dependency must refresh the remote visit.");
        state.VisitGuards["remote-field"] = Capture();
        progress = progress with { Inventory = [new(23, 1)] };
        Check(NeedsRefresh(), "A changed observed item quantity must refresh the remote visit.");
        state.VisitGuards["remote-field"] = Capture();
        progress = progress with { AvailableCharacters = 0 };
        Check(NeedsRefresh(), "A changed observed party availability bit must refresh the remote visit.");
        state.VisitGuards["remote-field"] = Capture();
        progress = progress with { Materia = Enumerable.Range(0, 200).Select(_ => new Ff7MateriaState(0, 0)).ToArray() };
        Check(NeedsRefresh(), "A changed observed native materia-capacity predicate must refresh the remote visit.");
        state.VisitGuards["remote-field"] = Capture();
        Ff7AutoPlayState restored = state.Clone();
        Check(!Ff7AutoPlayPolicy.NeedsVisitRefresh(restored, remote, variables, progress, process), "Cold restoration changed a visit's dependency receipt.");
        variables.WriteByte(FieldBank.Persistent0Byte, 8, 3);
        Check(Ff7AutoPlayPolicy.NeedsVisitRefresh(restored, remote, variables, progress, process), "Cold restoration discarded the observed persistent dependency.");

        Ff7AutoPlayVisitGuard Capture()
        {
            return Ff7AutoPlayPolicy.CaptureVisitGuard(reads, hosts, variables, progress, process);
        }

        bool NeedsRefresh()
        {
            return Ff7AutoPlayPolicy.NeedsVisitRefresh(state, remote, variables, progress, process);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
