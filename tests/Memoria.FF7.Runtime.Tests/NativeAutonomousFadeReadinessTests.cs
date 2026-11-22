using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeAutonomousFadeReadinessTests
{
    private sealed record Fixture(byte[] Code, FieldMachine Machine, FieldVmSnapshot Vm, Ff7FieldProcess Process);

    internal static void Run()
    {
        Fixture source = Create([0x6C, 0]);
        string before = JsonSerializer.Serialize(new { source.Code, source.Vm, source.Process });
        bool Wait(Fixture value, Ff7AutoPlayNativeReadiness? tracker = null, Func<int, int, FieldScriptAnalysis>? analyze = null) => (tracker ?? new()).ShouldWaitForAutonomousFadeCompletion(1, "source", value.Vm, value.Process, value.Code, analyze ?? ((entity, pc) => value.Machine.AnalyzeScriptAtProgramCounter(entity, pc)));
        Check(Wait(source), "An actual admitted finite fade acknowledgement lost readiness.");
        Fixture write = Create([0x6C, 0x80, 0x50, 0, 1, 0]);
        Check(Wait(write), "A finite fade followed by an autonomous literal write lost readiness.");
        foreach (int type in new[] { 1, 2, 5, 6, 7, 8, 9, 10 })
        {
            Check(Wait(source with { Process = source.Process with { FadeState = source.Process.FadeState! with { Type = type } } }), "A supported moving native fade lost its actual wait owner.");
        }
        Check(Wait(source with { Process = source.Process with { FadeState = new(0, 0, 0, 0, 0, 0, true) } }), "A completed native fade could not acknowledge its actual wait.");
        foreach (Ff7FadeProcess? fade in new Ff7FadeProcess?[] { null, new(1, 128, 0, 0, 0, 0, true), new(11, 2, 8, 0, 0, 0, true) })
        {
            Check(!Wait(source with { Process = source.Process with { FadeState = fade } }), "An absent, frozen or unsupported fade acquired autonomous readiness.");
        }
        FieldHostRequest request = source.Vm.PendingRequests!.Single();
        foreach (FieldHostRequest invalid in new[]
        {
            request with { Entity = 1 },
            request with { Priority = 6 },
            request with { ProgramCounter = 9 },
            request with { Command = new FieldFadeCommand(1, Wait: false) },
            request with { Command = new FieldDialogueCommand(0, 1) }
        })
        {
            Check(!Wait(source with { Vm = source.Vm with { PendingRequests = [invalid] } }), "A foreign or unrelated request acquired fade readiness.");
        }
        Check(!Wait(source with { Vm = source.Vm with { PendingRequests = [] } }), "An unowned fade acquired readiness.");
        Check(!Wait(source with { Vm = source.Vm with { Entities = [source.Vm.Entities[0] with { SavedProgramCounters = new int[8] }] } }), "An unadmitted ambient owner acquired fade readiness.");
        Check(!Wait(Create([0, 0])), "A different native opcode acquired fade-wait readiness.");
        FieldScriptAnalysis analysis = source.Machine.AnalyzeScriptAtProgramCounter(0, 8);
        Check(!Wait(source, analyze: (_, _) => analysis with { Complete = false }), "Incomplete continuation analysis acquired readiness.");
        Check(!Wait(Create([0x6C, 0x30, 1, 0, 1, 0])), "An input-dependent continuation acquired autonomous readiness.");
        Check(!Wait(Create([0x6C, 0x12, 2])), "A backward continuation loop acquired finite readiness.");
        Ff7AutoPlayNativeReadiness repeated = new();
        int allowed = 0;
        while (Wait(source, repeated) && allowed <= Ff7AutoPlayNativeReadiness.RepeatedObservationLimit)
        {
            allowed++;
        }
        Check(allowed == Ff7AutoPlayNativeReadiness.RepeatedObservationLimit, "Frozen fade readiness did not preserve its existing repeated-observation bound.");
        Check(before == JsonSerializer.Serialize(new { source.Code, source.Vm, source.Process }), "Fade readiness changed native execution, presentation or variables.");
        Console.WriteLine("Admitted autonomous native fade waits retain exact ownership and bounded observation.");
    }

    private static Fixture Create(byte[] body)
    {
        byte[] code = new byte[8 + body.Length];
        body.CopyTo(code, 8);
        ushort[][] entries = [new ushort[32]];
        entries[0][1] = 8;
        FieldProgram program = new(code, entries);
        FieldMachine machine = new(program);
        machine.Initialize();
        FieldVmSnapshot vm = machine.Capture();
        FieldEntitySnapshot owner = vm.Entities[0] with { ProgramCounter = 8, Priority = 1 };
        owner.SavedProgramCounters[7] = 2;
        vm = vm with { Entities = [owner], PendingRequests = [new(7, 0, 1, 8, new FieldFadeCommand(1, Wait: true))], NextRequestId = 8 };
        machine = FieldMachine.Restore(program, vm);
        return new(code, machine, vm, new() { FadeState = new(1, 128, 8, 0, 0, 0, true) });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
