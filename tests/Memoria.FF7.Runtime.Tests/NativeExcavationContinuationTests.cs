using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeExcavationContinuationTests
{
    private const string Program = "CB5DD0DE1FCFDBC2FB36AA8B960EEB05A6E46AED03B256712851B87434A5ADDE";
    private sealed record Fixture(FieldVmSnapshot Vm, Ff7FieldProcess Process, byte[] Script);

    internal static void Run()
    {
        Fixture fixture = Create();
        string before = JsonSerializer.Serialize(fixture);
        Check(Ff7AutoPlayScriptedInput.IsExcavationBombContinuation(fixture.Vm, fixture.Process), "The actual admitted bomb continuation was excluded.");
        Check(new Ff7AutoPlayNativeReadiness().ShouldWaitForExcavationBombContinuation(1, fixture.Vm, fixture.Process, fixture.Script), "The actual finite child turn was starved by planning.");
        Check(before == JsonSerializer.Serialize(fixture), "Readiness changed native execution, actor state or script bytes.");
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            source => source.Vm.PersistentVariables[0xEB] = 0,
            source => source.Vm.MapVariables[0x0C] = 0,
            source => source.Vm.Entities[0] = source.Vm.Entities[0] with { Priority = 6 },
            source => source.Vm.Entities[0] = source.Vm.Entities[0] with { ProgramCounter = 0x06C9 },
            source => source.Vm.Entities[2] = source.Vm.Entities[2] with { Priority = 7 },
            source => source.Vm.Entities[2].SyncStates[6] = FieldSyncState.Done,
            source => source.Vm.Entities[2].SyncOwners[6] = 1,
            source => source.Vm.Entities[2].SavedProgramCounters[7] = 0,
            source => source.Vm.Entities[2] = source.Vm.Entities[2] with { ProgramCounter = 0x0705 }
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(!Ff7AutoPlayScriptedInput.IsExcavationBombContinuation(invalid.Vm, invalid.Process), "An unadmitted, unrelated or positional native stage acquired bomb readiness.");
        }
        Check(!Ff7AutoPlayScriptedInput.IsExcavationBombContinuation(fixture.Vm with { ProgramIdentity = "unreviewed" }, fixture.Process), "An unreviewed native program acquired readiness.");
        Check(!Ff7AutoPlayScriptedInput.IsExcavationBombContinuation(fixture.Vm, fixture.Process with { Transition = new(1, 0, 0, 0, 0) }), "Field departure inherited continuation readiness.");
        foreach (int pc in new[] { 0x0895, 0x0AB4 })
        {
            Fixture footer = Create();
            footer.Vm.MapVariables[0x0C] = 1;
            footer.Vm.Entities[2] = footer.Vm.Entities[2] with { ProgramCounter = pc };
            Check(Ff7AutoPlayScriptedInput.IsExcavationBombContinuation(footer.Vm, footer.Process), "The already-admitted post-decrement footer was interrupted.");
        }
        Fixture positional = Create();
        positional.Vm.MapVariables[0x0C] = 1;
        positional.Vm.Entities[2] = positional.Vm.Entities[2] with { ProgramCounter = 0x0705 };
        Check(!Ff7AutoPlayScriptedInput.IsExcavationBombContinuation(positional.Vm, positional.Process), "A fresh positional stage inherited the completed bomb continuation.");
        Fixture wrongRequest = Create();
        wrongRequest.Script[0x06CF] = 0xC2;
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWaitForExcavationBombContinuation(1, wrongRequest.Vm, wrongRequest.Process, wrongRequest.Script), "Another requested script inherited the bomb continuation.");
        CheckNestedProgressAndBounds();
        Console.WriteLine("Admitted excavation continuation, nested turn progress, phase exclusion and readiness bounds passed.");
    }

    private static void CheckNestedProgressAndBounds()
    {
        Fixture fixture = Create();
        Ff7AutoPlayNativeReadiness readiness = new();
        Ff7Actor actor = fixture.Process.Actors.Single();
        for (int step = 0; step < 10; step++)
        {
            Ff7FieldProcess progressing = fixture.Process with { Actors = [actor with { TurnStep = step }] };
            Check(readiness.ShouldWaitForExcavationBombContinuation(1, fixture.Vm with { Tick = step }, progressing, fixture.Script), "Real nested native turn progress was ignored.");
        }
        bool rejected = false;
        try
        {
            for (int tick = 0; tick <= Ff7AutoPlayNativeReadiness.RepeatedObservationLimit + 1; tick++)
            {
                readiness.ShouldWaitForExcavationBombContinuation(1, fixture.Vm with { Tick = tick + 10 }, fixture.Process, fixture.Script);
            }
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }
        Check(rejected, "Tick-only changes kept a frozen admitted continuation alive indefinitely.");
    }

    private static Fixture Create()
    {
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 15).Select(_ => new FieldEntitySnapshot(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[0] = entities[0] with { ProgramCounter = 0x06CD };
        entities[2] = entities[2] with { ProgramCounter = 0x0879, Priority = 6 };
        entities[2].SavedProgramCounters[7] = 0x06FD;
        entities[2].SyncStates[6] = FieldSyncState.Waiting;
        entities[2].SyncOwners[6] = 0;
        entities[14] = entities[14] with { ProgramCounter = 0x0CF1, Priority = 6 };
        entities[14].SavedProgramCounters[7] = 0x0CF0;
        entities[14].SyncStates[6] = FieldSyncState.Waiting;
        entities[14].SyncOwners[6] = 2;
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 1, true, new byte[1280], new byte[256], entities);
        vm.PersistentVariables[0xEB] = 1;
        vm.MapVariables[0x0C] = 2;
        byte[] script = new byte[0x0D20];
        script[0x06CD] = 0x03;
        script[0x06CE] = 2;
        script[0x06CF] = 0xC3;
        script[0x0879] = 0x03;
        script[0x087A] = 14;
        script[0x087B] = 0xC4;
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 14, Model = 0, TurnSteps = 10 }] };
        return new(vm, process, script);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
