using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRoomResetCoverageTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP native reset ownership: original field resources are not configured.");
            return;
        }
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root), "ealin_2");
        string scope = $"ealin_2:{FieldProgram.FromPcScripts(field.Scripts).Identity}:170";
        Check(field.Scripts.Data.Span.Slice(0x0CFB, 6).SequenceEqual(new byte[] { 3, 2, 0x47, 3, 2, 0x69 }), "The native noise reset no longer owns its Aerith7/9 call path.");
        Check(field.Scripts.Data.Span.Slice(0x0DD1, 6).SequenceEqual(new byte[] { 3, 2, 0x47, 3, 2, 0x68 }), "The native door reset no longer owns its Aerith7/8 call path.");
        AutoPlayEventCoverageLedger ledger = new();
        ledger.Enter(scope);
        ledger.PublishCatalog([new("line:11:script:2", "noise"), new("line:14:script:2", "door")], true);
        foreach (var (id, pc) in new[] { ("line:11:script:2", 0x09CF), ("line:14:script:2", 0x0944) })
        {
            ledger.Begin(id, "172/100/before");
            AutoPlayEventCoverageEntry active = ledger.Current!.Events.Single(entry => entry.Id == id);
            FieldTransitionCommand transition = new(190, -288, 48, 59, 128);
            FieldHostRequest owner = new(1, 2, 3, pc, transition);
            Ff7NativeTickObservation observation = new("ealin_2", 172, 200, [], transition, "after", owner);
            Check(field.Scripts.Data.Span[pc] == 0x60 && Ff7FieldEventCoverageCatalog.CompletesOwnedReset(ledger.Current, active, observation), "The exact original child-owned reset did not match its parent interaction.");
            Check(!Ff7FieldEventCoverageCatalog.CompletesOwnedReset(ledger.Current, active, observation with { Generation = 173 }), "A different visit generation falsely completed a reset.");
            Check(!Ff7FieldEventCoverageCatalog.CompletesOwnedReset(ledger.Current, active, observation with { TransitionOwner = owner with { ProgramCounter = pc + 1 } }), "A nearby opcode falsely completed a reset.");
            Check(!Ff7FieldEventCoverageCatalog.CompletesOwnedReset(ledger.Current, active, observation with { TransitionOwner = owner with { Entity = 1 } }), "A different child entity falsely completed a reset.");
            Check(!Ff7FieldEventCoverageCatalog.CompletesOwnedReset(ledger.Current, active, observation with { TransitionOwner = owner with { Priority = 1 } }), "A different child priority falsely completed a reset.");
            Check(!Ff7FieldEventCoverageCatalog.CompletesOwnedReset(ledger.Current, active, observation with { Transition = transition with { Map = 188 } }), "A different field handoff falsely completed a reset.");
            ledger.Complete(id, observation.ProgressWitness, $"native child handoff {pc:X4}");
            ledger = new(JsonSerializer.Deserialize<AutoPlayEventCoverageState>(JsonSerializer.Serialize(ledger.Capture()))!);
            ledger.Enter(scope);
            Check(ledger.Current!.Events.Single(entry => entry.Id == id).Status == AutoPlayEventCoverageStatus.Completed, "A same-epoch native reset or cold restore discarded its finite-event completion.");
        }
        Check(ledger.CanLeave && ledger.AllEventsCompleted, "Both finite reset owners must complete before departure is allowed.");
        Console.WriteLine("PASS native reset call paths, exact child handoff ownership and same-epoch cold ledger");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
