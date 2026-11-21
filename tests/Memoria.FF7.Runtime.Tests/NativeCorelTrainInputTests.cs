using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeCorelTrainInputTests
{
    private const string Program = "E9C23833A74BC5D9419A218ACEDAB92664C5BBFB839CC5577AB17960C1FCB7EB";
    private sealed record Fixture(FieldVmSnapshot Vm, Ff7FieldProcess Process, byte[] Script);

    internal static void Run()
    {
        foreach ((byte stage, int pc, ushort chord) in new[] { ((byte)4, 0x08B4, (ushort)0x1010), ((byte)4, 0x08B8, (ushort)0x1010), ((byte)4, 0x08D3, (ushort)0x1010), ((byte)4, 0x08D7, (ushort)0x1010), ((byte)2, 0x09F6, (ushort)0x4040), ((byte)2, 0x09FA, (ushort)0x4040), ((byte)2, 0x0A15, (ushort)0x4040), ((byte)2, 0x0A19, (ushort)0x4040) })
        {
            Fixture source = Create(stage, pc);
            string before = JsonSerializer.Serialize(source);
            Check(Read(source, new()) is { } input && input.Held == chord && input.Horizontal == 0 && input.Vertical == 0 && !input.Talk && !input.Run, "The current native train poll did not receive the full simultaneous chord.");
            foreach (ushort previous in new ushort[] { chord, (ushort)(chord & 0xFF), (ushort)(chord & 0xFF00) })
            {
                Check(Read(source, new(RawHeld: previous)) is { Held: 0 }, "A partially held native chord was not released before the next rising edge.");
            }
            Check(Read(source, new(RawHeld: 0x20))?.Held == chord, "An unrelated held button suppressed the native braking chord.");
            Check(before == JsonSerializer.Serialize(source), "Train input changed counters, native control state, script bytes or ownership.");
        }
        Fixture normal = Create(4, 0x08B4);
        foreach (Func<Fixture, Fixture> invalidate in new Func<Fixture, Fixture>[]
        {
            source => source with { Vm = source.Vm with { ProgramIdentity = "other" } },
            source => source with { Process = source.Process with { PlayerLocked = false } },
            source => source with { Process = source.Process with { MenuDisabled = false } },
            source => source with { Process = source.Process with { GlobalCommand = Ff7GlobalCommand.Movie } },
            source => source with { Process = source.Process with { Actors = [source.Process.Actors.Single() with { Entity = 10 }] } },
            source => source with { Process = source.Process with { Actors = [source.Process.Actors.Single() with { EntityBound = false }] } },
            source => source with { Process = source.Process with { Dialogues = [new(1, 0, 1, null, null, 0)] } },
            source => WithStage(source, 2),
            source => WithStage(source, 0)
        })
        {
            Check(Read(invalidate(normal), new()) is null, "An unrelated native stage, player or modal state inherited train input.");
        }
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            source => source.Vm.Entities[11] = source.Vm.Entities[11] with { Priority = 6 },
            source => source.Vm.Entities[11] = source.Vm.Entities[11] with { ModelBound = false },
            source => source.Vm.Entities[11] = source.Vm.Entities[11] with { PartyCharacter = 0 },
            source => source.Vm.Entities[11] = source.Vm.Entities[11] with { ProgramCounter = 0x08B5 },
            source => source.Script[0x08B4] = 0x30,
            source => source.Script[0x08B5] = 0x20
        })
        {
            Fixture invalid = Create(4, 0x08B4);
            invalidate(invalid);
            Check(Read(invalid, new()) is null, "A different live actor, instruction or key mask acquired the braking recipe.");
        }
        CheckHud();
        Console.WriteLine("Native train braking preserves complete chord edges and exact current poll/HUD scope without native writes.");
    }

    private static void CheckHud()
    {
        Fixture source = Create(4, 0x08B4);
        source.Vm.Entities[4] = source.Vm.Entities[4] with { Priority = 6, ProgramCounter = 0x0622 };
        source.Vm.Entities[13] = source.Vm.Entities[13] with { Priority = 6, ProgramCounter = 0x0C59 };
        FieldHostRequest[] requests = [new(41, 4, 6, 0x0622, new FieldDialogueCommand(1, 222)), new(42, 13, 6, 0x0C59, new FieldDialogueCommand(2, 207))];
        source = source with { Vm = source.Vm with { PendingRequests = requests }, Process = source.Process with { Dialogues = [new(41, 1, 222, null, null, 0), new(42, 2, 207, null, null, 0)], Windows = [new(1, 0, 0, 100, 40, Style: 2, Flags: 1), new(2, 0, 0, 100, 40, Style: 1, Flags: 1)] } };
        string before = JsonSerializer.Serialize(source);
        Check(Read(source, new())?.Held == 0x1010, "Exact native train HUD requests prevented braking.");
        Check(Read(source with { Vm = source.Vm with { PendingRequests = [] } }, new()) is null, "A display without its actual pending producer acquired the HUD exception.");
        Check(Read(source with { Process = source.Process with { Dialogues = [source.Process.Dialogues[0] with { Text = 223 }] } }, new()) is null, "A different message acquired the native train HUD exception.");
        Check(Read(source with { Process = source.Process with { Windows = source.Process.Windows.Select(window => window with { Flags = 0 }).ToArray() } }, new()) is null, "An ordinary confirmable display acquired braking input.");
        Check(before == JsonSerializer.Serialize(source), "HUD qualification changed actual native requests or display state.");
    }

    private static Fixture Create(byte stage, int pc)
    {
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 14).Select(_ => new FieldEntitySnapshot(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[11] = entities[11] with { ProgramCounter = pc, ModelBound = true, PartyCharacter = 8 };
        FieldVariables variables = new();
        variables.WriteByte(FieldBank.MapByte, 0x16, stage);
        variables.WriteWord(FieldBank.Persistent0Word, 0, 1110);
        byte[] script = new byte[0x0D00];
        script[pc] = 0x31;
        ushort mask = pc switch { 0x08B4 or 0x09F6 => 0x1000, 0x08B8 or 0x09FA => 0x10, 0x08D3 or 0x0A15 => 0x4000, _ => 0x40 };
        script[pc + 1] = (byte)mask;
        script[pc + 2] = (byte)(mask >> 8);
        script[pc + 3] = 4;
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 1, true, variables.CopyPersistent(), variables.CopyMap(), entities, PendingRequests: [], Input: new());
        Ff7FieldProcess process = new() { PlayerLocked = true, MenuDisabled = true, Actors = [new() { Slot = 0, Entity = 11, Model = 6, PlayerControlled = true, PartyCharacter = 8 }] };
        return new(vm, process, script);
    }

    private static Fixture WithStage(Fixture source, byte stage)
    {
        FieldVariables variables = new(source.Vm.PersistentVariables, source.Vm.MapVariables);
        variables.WriteByte(FieldBank.MapByte, 0x16, stage);
        return source with { Vm = source.Vm with { MapVariables = variables.CopyMap() } };
    }

    private static Ff7AutoplayInput? Read(Fixture source, FieldInputState previous) => Ff7AutoPlayScriptedInput.ReadNativeCorelTrainInput(source.Vm, source.Process, source.Script, previous);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
