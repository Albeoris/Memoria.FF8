using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeExcavationBombInputTests
{
    private const string Program = "CB5DD0DE1FCFDBC2FB36AA8B960EEB05A6E46AED03B256712851B87434A5ADDE";
    private sealed record Fixture(FieldVmSnapshot Vm, Ff7FieldProcess Process, Ff7Dialogue Prompt, FieldHostRequest Request, FieldScriptInputDemand Demand);

    internal static void Run()
    {
        Fixture source = Create();
        string before = JsonSerializer.Serialize(source);
        Check(Eligible(source), "The reviewed actual bomb prompt did not admit its native button.");
        foreach (int pc in new[] { 0x06C3, 0x06C9, 0x06D0 })
        {
            Fixture current = Create();
            current.Vm.Entities[0] = current.Vm.Entities[0] with { ProgramCounter = pc };
            Check(Eligible(current), "A supported current director polling site was excluded.");
        }
        Check(before == JsonSerializer.Serialize(source), "Bomb input qualification changed actual native execution or variables.");

        foreach (Func<Fixture, Fixture> invalidate in new Func<Fixture, Fixture>[]
        {
            fixture => fixture with { Vm = fixture.Vm with { ProgramIdentity = "unreviewed" } },
            fixture => fixture with { Vm = fixture.Vm with { PendingRequests = [] } },
            fixture => fixture with { Demand = fixture.Demand with { ProgramIdentity = "unreviewed" } },
            fixture => fixture with { Demand = fixture.Demand with { Entity = 1 } },
            fixture => fixture with { Demand = fixture.Demand with { Priority = 6 } },
            fixture => fixture with { Demand = fixture.Demand with { ProgramCounter = 0x06C3 } },
            fixture => fixture with { Demand = fixture.Demand with { Kind = FieldScriptInputKind.Held } },
            fixture => fixture with { Demand = fixture.Demand with { Mask = 0x20 } },
            fixture => fixture with { Demand = fixture.Demand with { Mapped = true } },
            fixture => fixture with { Demand = fixture.Demand with { AwaitingInput = false } },
            fixture => fixture with { Prompt = fixture.Prompt with { Completed = true } },
            fixture => fixture with { Prompt = fixture.Prompt with { AwaitingInput = false } },
            fixture => fixture with { Prompt = fixture.Prompt with { Text = 30 } },
            fixture => fixture with { Prompt = fixture.Prompt with { FirstChoice = 0, LastChoice = 1 } },
            fixture => fixture with { Request = fixture.Request with { Entity = 2 } },
            fixture => fixture with { Request = fixture.Request with { Priority = 7 } },
            fixture => fixture with { Request = fixture.Request with { ProgramCounter = 0x06F9 } },
            fixture => fixture with { Request = fixture.Request with { Id = 9 } },
            fixture => fixture with { Process = fixture.Process with { PlayerLocked = true } },
            fixture => fixture with { Process = fixture.Process with { MenuDisabled = false } },
            fixture => fixture with { Process = fixture.Process with { Transition = new(1, 0, 0, 0, 0) } },
            fixture => fixture with { Process = fixture.Process with { Windows = [new(3, 0, 0, 100, 50, Style: 2, Flags: 0)] } },
            fixture => fixture with { Process = fixture.Process with { Windows = [new(3, 0, 0, 100, 50, Style: 0, Flags: 1)] } }
        })
        {
            Check(!Eligible(invalidate(Create())), "An unreviewed program, prompt, input, request or control context received the bomb action.");
        }
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            fixture => fixture.Vm.MapVariables[0x0C] = 1,
            fixture => fixture.Vm.MapVariables[0x0C] = 3,
            fixture => fixture.Vm.MapVariables[0x2B] = 1,
            fixture => fixture.Vm.PersistentVariables[0xEB] = 0,
            fixture => fixture.Vm.Entities[0] = fixture.Vm.Entities[0] with { Priority = 6 },
            fixture => fixture.Vm.Entities[0] = fixture.Vm.Entities[0] with { ProgramCounter = 0x06D1 },
            fixture => fixture.Vm.Entities[1] = fixture.Vm.Entities[1] with { ProgramCounter = 0x06F9 },
            fixture => fixture.Vm.Entities[2] = fixture.Vm.Entities[2] with { Priority = 6 },
            fixture => fixture.Vm.Entities[2].SavedProgramCounters[6] = 10,
            fixture => fixture.Vm.Entities[2].SyncStates[6] = FieldSyncState.Done,
            fixture => fixture.Vm.Entities[2].SyncOwners[6] = 0
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(!Eligible(invalid), "Another phase, positional stage or busy native worker received the bomb action.");
        }
        Console.WriteLine("Reviewed excavation bomb admission, positional-phase exclusion and native context/source purity checks passed.");
    }

    private static Fixture Create()
    {
        FieldEntitySnapshot Entity(int pc, int priority) => new(pc, priority, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldHostRequest request = new(8, 1, 6, 0x06F8, new FieldDialogueCommand(3, 29));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 10, true, new byte[1280], new byte[256], [Entity(0x06C3, 7), Entity(0x06F8, 6), Entity(0, 7)], PendingRequests: [request]);
        vm.MapVariables[0x0C] = 2;
        vm.PersistentVariables[0xEB] = 1;
        Ff7Dialogue prompt = new(8, 3, 29, null, null, 0, AwaitingInput: true);
        Ff7FieldProcess process = new() { MenuDisabled = true, Windows = [new(3, 0, 0, 100, 50, Style: 2, Flags: 1)], Dialogues = [prompt] };
        FieldScriptInputDemand demand = new("native-bomb", Program, 0, 7, 0x06C9, FieldScriptInputKind.Pressed, 0x80, false, true);
        return new(vm, process, prompt, request, demand);
    }

    private static bool Eligible(Fixture fixture)
    {
        return Ff7AutoPlayScriptedInput.IsExcavationBombInput(fixture.Vm, fixture.Process, fixture.Prompt, fixture.Request, fixture.Demand);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
