using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeGaeaWarmthInputTests
{
    private const string Program = "5CA6E94C40B2BE8DA121FF1E0A758A5076BA09BDE67DDA155E440EA01000B61C";
    private const string SecondProgram = "97E4CD1C842F666BDB1E727CF2F000FE595A11409ED2A51116ADB85DD086F741";
    private const string ThirdProgram = "B34260E0B629A52DD758E497A823D077296D9890CA92CBE1D26C3A2683CF8E05";
    private sealed record Fixture(FieldVmSnapshot Vm, Ff7FieldProcess Process);

    internal static void Run()
    {
        Fixture source = Create();
        FieldVariables variables = new(source.Vm.PersistentVariables, source.Vm.MapVariables);
        FieldInputState previous = new(Held: 0x20, RawHeld: 0x20, Horizontal: 1, Vertical: -1, Run: true);
        string before = JsonSerializer.Serialize(new { source, previous, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() });
        Ff7AutoplayInput? first = Ff7AutoPlayPolicy.ReadNativeGaeaWarmthInput(source.Vm, source.Process, variables, previous);
        Check(first is { Held: 0x80, Horizontal: 0, Vertical: 0, Talk: false, Run: false }, "The live native warmth worker did not receive a stationary raw warming edge.");
        Check(first == Ff7AutoPlayPolicy.ReadNativeGaeaWarmthInput(source.Vm, source.Process, variables, previous), "A planning poll changed warming input without an actual native input change.");
        Check(Read(source, previous with { RawHeld = 0x80 }) is { Held: 0 }, "A physically held warming key did not receive its release phase.");
        Check(Read(source, previous with { Held = 0x80 }) is { Held: 0 }, "A mapped held warming key did not receive its release phase.");
        Check(Read(source, previous with { Held = 0, RawHeld = 0 }) is { Held: 0x80 }, "A released native key did not receive the next genuine warming edge.");
        Check(Read(source with { Process = source.Process with { PlayerLocked = true, MenuDisabled = true } }) is { Held: 0x80 }, "The exact native warmth worker was excluded during its own warming control lock.");
        Check(before == JsonSerializer.Serialize(new { source, previous, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() }), "Warmth input advice modified native temperature, input, control state or variables.");

        foreach (int temperature in new[] { 27, 37 })
        {
            Check(Read(WithMap(source, FieldBank.MapWord, 9, temperature)) is { Held: 0x80 }, "A supported native warming temperature was excluded.");
        }
        foreach (int temperature in new[] { 26, 38, 39 })
        {
            Check(Read(WithMap(source, FieldBank.MapWord, 9, temperature)) is null, "A completed, already-collapsed or unsupported temperature received warming input.");
        }
        foreach (Func<Fixture, Fixture> invalidate in new Func<Fixture, Fixture>[]
        {
            value => value with { Vm = value.Vm with { ProgramIdentity = "unreviewed" } },
            value => value with { Vm = value.Vm with { Initialized = false } },
            value => WithMap(value, FieldBank.MapByte, 0x0D, 1),
            value => WithMap(value, FieldBank.MapByte, 0x0E, 2),
            value => value with { Process = value.Process with { GlobalCommand = Ff7GlobalCommand.Transition } },
            value => value with { Process = value.Process with { Transition = new(1, 0, 0, 0, 0) } },
            value => value with { Process = value.Process with { Dialogues = [new(1, 0, 0, null, null, 0)] } },
            value => value with { Process = value.Process with { Actors = [value.Process.Actors.Single() with { Entity = 16 }] } },
            value => value with { Process = value.Process with { Actors = [value.Process.Actors.Single() with { EntityBound = false }] } },
            value => value with { Process = value.Process with { Actors = [value.Process.Actors.Single() with { PlayerControlled = false }] } },
            value => value with { Process = value.Process with { Actors = [value.Process.Actors.Single() with { Ladder = new(1, 0, 0, 0, 0, 1) }] } },
            value => value with { Process = value.Process with { Actors = [value.Process.Actors.Single() with { Jump = new(1, 0, 0, 0, 10) }] } },
            value => value with { Process = value.Process with { Motions = [new(1, 17, 0, 0, false, false)] } }
        })
        {
            Check(Read(invalidate(Create())) is null, "An unrelated native profile, phase, player, movement or modal context received warming input.");
        }
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            value => value.Vm.Entities[4] = value.Vm.Entities[4] with { Priority = 7 },
            value => value.Vm.Entities[4] = value.Vm.Entities[4] with { ProgramCounter = 0x0739 },
            value => value.Vm.Entities[4] = value.Vm.Entities[4] with { ProgramCounter = 0x0771 },
            value => value.Vm.Entities[4].SyncStates[6] = FieldSyncState.Waiting
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(Read(invalid) is null, "A missing, unrelated or synchronous native warmth worker received input.");
        }
        CheckSecondCliff();
        CheckThirdCliff();
        CheckTemperatureHud();
        CheckWarmthDemandOwnership();
        Console.WriteLine("Native warmth input alternates real key edges only in the reviewed live worker context without changing native state.");
    }

    private static void CheckWarmthDemandOwnership()
    {
        foreach ((string program, int pc) in new[] { (Program, 0x0740), (SecondProgram, 0x0976), (ThirdProgram, 0x089A) })
        {
            FieldScriptInputDemand demand = new("warmth", program, 4, 6, pc, FieldScriptInputKind.Pressed, 0x80, false, true);
            Check(Ff7AutoPlayPolicy.IsNativeGaeaWarmthDemand(demand), "The reviewed warming demand remained available to the generic input chooser.");
            foreach (FieldScriptInputDemand unrelated in new[]
            {
                demand with { ProgramIdentity = "unreviewed" },
                demand with { ProgramCounter = pc + 1 },
                demand with { Entity = 3 },
                demand with { Priority = 1 },
                demand with { Mapped = true },
                demand with { Kind = FieldScriptInputKind.Held },
                demand with { Mask = 0x20 }
            })
            {
                Check(!Ff7AutoPlayPolicy.IsNativeGaeaWarmthDemand(unrelated), "Another native input demand was removed from ordinary scripted-input handling.");
            }
        }
    }

    private static void CheckTemperatureHud()
    {
        foreach (int profile in new[] { 0, 1, 2 })
        {
            bool second = profile == 1;
            bool third = profile == 2;
            int text = third ? 3 : second ? 5 : 1;
            Fixture source = CreateHud(second, third);
            string before = JsonSerializer.Serialize(source);
            Check(Read(source) is { Held: 0x80 } && Read(source, new(RawHeld: 0x80)) is { Held: 0 }, "The exact native temperature HUD blocked warming press or release.");
            Check(before == JsonSerializer.Serialize(source), "HUD qualification changed the actual display request or native state.");
            foreach (Func<Fixture, Fixture> invalidate in new Func<Fixture, Fixture>[]
            {
                value => value with { Vm = value.Vm with { PendingRequests = [] } },
                value => value with { Vm = value.Vm with { PendingRequests = [value.Vm.PendingRequests!.Single() with { Id = 50 }] } },
                value => value with { Vm = value.Vm with { PendingRequests = [value.Vm.PendingRequests!.Single() with { Entity = 3 }] } },
                value => value with { Vm = value.Vm with { PendingRequests = [value.Vm.PendingRequests!.Single() with { Priority = 5 }] } },
                value => value with { Vm = value.Vm with { PendingRequests = [value.Vm.PendingRequests!.Single() with { ProgramCounter = value.Vm.Entities[2].ProgramCounter + 1 }] } },
                value => value with { Vm = value.Vm with { PendingRequests = [value.Vm.PendingRequests!.Single() with { Command = new FieldDialogueCommand(1, 99) }] } },
                value => value with { Vm = value.Vm with { PendingRequests = [value.Vm.PendingRequests!.Single() with { Command = new FieldDialogueCommand(0, text) }] } },
                value => value with { Vm = value.Vm with { PendingRequests = [value.Vm.PendingRequests!.Single() with { Command = new FieldDialogueCommand(1, text, 0, 1) }] } },
                value => value with { Process = value.Process with { Dialogues = [value.Process.Dialogues.Single() with { Text = 99 }] } },
                value => value with { Process = value.Process with { Dialogues = [value.Process.Dialogues.Single() with { Window = 0 }] } },
                value => value with { Process = value.Process with { Dialogues = [value.Process.Dialogues.Single() with { FirstChoice = 0, LastChoice = 1 }] } },
                value => value with { Process = value.Process with { Windows = [] } },
                value => value with { Process = value.Process with { Windows = [value.Process.Windows.Single() with { Style = 1 }] } },
                value => value with { Process = value.Process with { Windows = [value.Process.Windows.Single() with { Flags = 0 }] } }
            })
            {
                Check(Read(invalidate(CreateHud(second, third))) is null, "An unrelated prompt, window or native request inherited the temperature-HUD exception.");
            }
            foreach (Action<Fixture> invalidate in new Action<Fixture>[]
            {
                value => value.Vm.Entities[2] = value.Vm.Entities[2] with { Priority = 7 },
                value => value.Vm.Entities[2] = value.Vm.Entities[2] with { ProgramCounter = value.Vm.Entities[2].ProgramCounter + 1 }
            })
            {
                Fixture invalid = CreateHud(second, third);
                invalidate(invalid);
                Check(Read(invalid) is null, "A different live display owner inherited temperature-HUD input admission.");
            }
        }
    }

    private static Fixture CreateHud(bool second, bool third = false)
    {
        Fixture source = Create(second, third);
        int pc = third ? 0x087F : second ? 0x095B : 0x0725;
        int text = third ? 3 : second ? 5 : 1;
        source.Vm.Entities[2] = source.Vm.Entities[2] with { Priority = 6, ProgramCounter = pc };
        source.Vm.Entities[2].SavedProgramCounters[7] = 0x0700;
        FieldHostRequest request = new(49, 2, 6, pc, new FieldDialogueCommand(1, text));
        return source with
        {
            Vm = source.Vm with { PendingRequests = [request] },
            Process = source.Process with { Dialogues = [new(49, 1, text, null, null, 0)], Windows = [new(1, 0, 0, 100, 20, Style: 0, Flags: 1)] }
        };
    }

    private static void CheckSecondCliff()
    {
        Fixture source = Create(second: true);
        string before = JsonSerializer.Serialize(source);
        Check(Read(source) is { Held: 0x80, Horizontal: 0, Vertical: 0, Run: false } && Read(source, new(RawHeld: 0x80)) is { Held: 0 }, "The second reviewed cliff did not receive genuine warming press and release phases.");
        Check(Read(WithMap(source, FieldBank.MapWord, 8, 27)) is not null && Read(WithMap(source, FieldBank.MapWord, 8, 37)) is not null, "The second cliff lost its native warming temperature band.");
        Check(Read(WithMap(source, FieldBank.MapWord, 8, 26)) is null && Read(WithMap(source, FieldBank.MapWord, 8, 38)) is null, "The second cliff warmed outside its supported native temperature band.");
        Check(Read(WithMap(source, FieldBank.MapByte, 0x0E, 1)) is null && Read(WithMap(source, FieldBank.MapByte, 0x0F, 2)) is null, "The second cliff ignored its actual busy or cold flag.");
        Check(Read(source with { Process = source.Process with { Actors = [source.Process.Actors.Single() with { Entity = 17 }] } }) is null, "The first cliff's player binding authorized warming on the second cliff.");
        source.Vm.Entities[4] = source.Vm.Entities[4] with { ProgramCounter = 0x073A };
        Check(Read(source) is null, "The first cliff's worker site authorized warming on the second cliff.");
        source.Vm.Entities[4] = source.Vm.Entities[4] with { ProgramCounter = 0x0970 };
        Check(before == JsonSerializer.Serialize(source), "Second-cliff warming qualification changed native state.");
    }

    private static Ff7AutoplayInput? Read(Fixture source, FieldInputState? previous = null)
    {
        return Ff7AutoPlayPolicy.ReadNativeGaeaWarmthInput(source.Vm, source.Process, new(source.Vm.PersistentVariables, source.Vm.MapVariables), previous ?? new());
    }

    private static void CheckThirdCliff()
    {
        Fixture source = Create(third: true);
        string before = JsonSerializer.Serialize(source);
        Check(Read(source) is { Held: 0x80, Horizontal: 0, Vertical: 0, Run: false } && Read(source, new(RawHeld: 0x80)) is { Held: 0 }, "The third reviewed cliff lost actual warming press and release phases.");
        foreach (int temperature in new[] { 27, 37 })
        {
            Check(Read(WithMap(source, FieldBank.MapWord, 8, temperature)) is not null, "The third cliff lost its native temperature band.");
        }
        foreach (int temperature in new[] { 26, 38, 39 })
        {
            Check(Read(WithMap(source, FieldBank.MapWord, 8, temperature)) is null, "The third cliff received warming outside its native temperature band.");
        }
        Check(Read(WithMap(source, FieldBank.MapByte, 0x0D, 1)) is null && Read(WithMap(source, FieldBank.MapByte, 0x0E, 2)) is null, "The third cliff ignored its actual busy or cold flag.");
        Check(Read(source with { Process = source.Process with { Actors = [source.Process.Actors.Single() with { Entity = 22 }] } }) is null, "Another cliff's player binding authorized third-cliff warming.");
        Check(Read(source with { Vm = source.Vm with { ProgramIdentity = SecondProgram } }) is null, "Another program borrowed the third-cliff worker site.");
        foreach (int pc in new[] { 0x0893, 0x08CB, 0x0970 })
        {
            source.Vm.Entities[4] = source.Vm.Entities[4] with { ProgramCounter = pc };
            Check(Read(source) is null, "An unrelated worker instruction authorized third-cliff warming.");
        }
        source.Vm.Entities[4] = source.Vm.Entities[4] with { ProgramCounter = 0x0894 };
        source.Vm.Entities[4].SyncStates[6] = FieldSyncState.Waiting;
        Check(Read(source) is null, "A synchronous unrelated worker acquired third-cliff warming input.");
        source.Vm.Entities[4].SyncStates[6] = FieldSyncState.None;
        Check(before == JsonSerializer.Serialize(source), "Third-cliff qualification changed native variables, worker state or player state.");
    }

    private static Fixture Create(bool second = false, bool third = false)
    {
        int player = third ? 20 : second ? 22 : 17;
        FieldEntitySnapshot[] entities = Enumerable.Range(0, player + 1).Select(_ => new FieldEntitySnapshot(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[4] = entities[4] with { Priority = 6, ProgramCounter = third ? 0x0894 : second ? 0x0970 : 0x073A };
        entities[4].SavedProgramCounters[7] = 0x0700;
        entities[player] = entities[player] with { ModelBound = true };
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, third ? ThirdProgram : second ? SecondProgram : Program, 30, true, new byte[1280], new byte[256], entities, Input: new());
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = player, Model = 0, PlayerControlled = true }] };
        return WithMap(new(vm, process), FieldBank.MapWord, third || second ? (byte)8 : (byte)9, 36);
    }

    private static Fixture WithMap(Fixture source, FieldBank bank, byte address, int value)
    {
        FieldVariables variables = new(source.Vm.PersistentVariables, source.Vm.MapVariables);
        if (bank == FieldBank.MapWord)
        {
            variables.WriteWord(bank, address, (short)value);
        }
        else
        {
            variables.WriteByte(bank, address, (byte)value);
        }
        return source with { Vm = source.Vm with { MapVariables = variables.CopyMap() } };
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
