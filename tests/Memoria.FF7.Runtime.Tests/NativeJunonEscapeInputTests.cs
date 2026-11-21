using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeJunonEscapeInputTests
{
    private const string Program = "F0869447B99A74D8601DEEAA41FA288C1091C5F2FA5B55ECF81E01C986F590E3";
    private sealed record Fixture(FieldVmSnapshot Vm, Ff7FieldProcess Process, byte[] Script);

    internal static void Run()
    {
        foreach ((byte stage, int pc, ushort mask) in new[] { ((byte)0, 0x0871, (ushort)0x20), ((byte)1, 0x0897, (ushort)0x20), ((byte)2, 0x08CE, (ushort)0x10), ((byte)3, 0x08E6, (ushort)0x30), ((byte)4, 0x0917, (ushort)0x90), ((byte)5, 0x0953, (ushort)0x80), ((byte)6, 0x099D, (ushort)0x40) })
        {
            Fixture source = Create(stage, pc);
            string before = JsonSerializer.Serialize(source);
            Check(Read(source) is { } input && input.Held == mask && input.Horizontal == 0 && input.Vertical == 0 && !input.Talk && !input.Run, "The current native stage did not receive its complete progressing held mask.");
            Check(Read(source with { Vm = source.Vm with { Input = new(Held: mask, RawHeld: mask) } })?.Held == mask, "A held-input stage was incorrectly converted into artificial press/release pulses.");
            Check(before == JsonSerializer.Serialize(source), "The input strategy changed native stage, ownership, script bytes or controls.");
        }
        foreach ((byte stage, int pc) in new[] { ((byte)0, 0x0864), ((byte)1, 0x088A), ((byte)2, 0x08B3), ((byte)3, 0x08D9), ((byte)4, 0x0911), ((byte)5, 0x094D), ((byte)6, 0x0989) })
        {
            Check(Read(Create(stage, pc)) is not null, "A native dispatch head between handler-budget boundaries lost stage input.");
        }
        Fixture normal = Create(0, 0x0871);
        foreach (Func<Fixture, Fixture> invalidate in new Func<Fixture, Fixture>[]
        {
            source => source with { Vm = source.Vm with { ProgramIdentity = "other" } },
            source => WithVariable(source, FieldBank.Persistent0Word, 0, 1017),
            source => WithVariable(source, FieldBank.MapByte, 0x13, 7),
            source => source with { Process = source.Process with { PlayerLocked = false } },
            source => source with { Process = source.Process with { MenuDisabled = false } },
            source => source with { Process = source.Process with { Actors = [source.Process.Actors.Single() with { Entity = 2 }] } },
            source => source with { Process = source.Process with { GlobalCommand = Ff7GlobalCommand.Movie } },
            source => source with { Process = source.Process with { Dialogues = [new(1, 0, 1, null, null, 0)] } }
        })
        {
            Check(Read(invalidate(normal)) is null, "An unrelated program, stage, owner or modal context acquired the puzzle recipe.");
        }
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            source => source.Vm.Entities[0] = source.Vm.Entities[0] with { ProgramCounter = 0x0524 },
            source => source.Vm.Entities[3] = source.Vm.Entities[3] with { Priority = 7 },
            source => source.Vm.Entities[3].SyncOwners[6] = 1,
            source => source.Vm.Entities[3].SyncStates[6] = FieldSyncState.None,
            source => source.Vm.Entities[3].SavedProgramCounters[7] = 0,
            source => source.Vm.Entities[3] = source.Vm.Entities[3] with { PartyCharacter = 0 },
            source => source.Script[0x0525] = 0xCB
        })
        {
            Fixture invalid = Create(0, 0x0871);
            invalidate(invalid);
            Check(Read(invalid) is null, "A different live synchronous worker inherited the puzzle recipe.");
        }
        CheckHud();
        CheckNativeWorkBoundary();
        Console.WriteLine("Native Junon escape input follows actual held-mask stages without altering native state or unrelated input semantics.");
    }

    private static void CheckHud()
    {
        Fixture source = Create(3, 0x08E6);
        source.Vm.Entities[2] = source.Vm.Entities[2] with { Priority = 6, ProgramCounter = 0x074A };
        FieldHostRequest request = new(162, 2, 6, 0x074A, new FieldDialogueCommand(3, 108));
        source = source with { Vm = source.Vm with { PendingRequests = [request] }, Process = source.Process with { Dialogues = [new(162, 3, 108, null, null, 0)], Windows = [new(3, 0, 0, 100, 40, Style: 2, Flags: 1)] } };
        string before = JsonSerializer.Serialize(source);
        Check(Read(source)?.Held == 0x30, "The exact nonconfirmable native instruction overlay blocked the actual chord.");
        Check(Read(source with { Vm = source.Vm with { PendingRequests = [] } }) is null, "An unowned overlay inherited puzzle input.");
        Check(Read(source with { Process = source.Process with { Dialogues = [source.Process.Dialogues.Single() with { Text = 109 }] } }) is null, "A different display inherited the instruction-overlay exception.");
        Check(Read(source with { Process = source.Process with { Windows = [source.Process.Windows.Single() with { Flags = 0 }] } }) is null, "An ordinary confirmable message inherited held puzzle input.");
        Check(before == JsonSerializer.Serialize(source), "HUD qualification changed its actual request or display.");
    }

    private static void CheckNativeWorkBoundary()
    {
        Fixture source = Create(0, 0x086E);
        source.Script[0x086E] = 0xBA;
        FieldHostRequest request = new(7, 3, 6, 0x086E, new FieldAnimation(4, 1, true, true));
        source = source with { Vm = source.Vm with { PendingRequests = [request] }, Process = source.Process with { AnimationRequests = [new(7, 3)] } };
        Check(Read(source) is { Held: 0 }, "A current owned blocking animation received another puzzle action instead of native neutral continuation.");
        Check(Read(source with { Vm = source.Vm with { PendingRequests = [] }, Process = source.Process with { AnimationRequests = [] } }) is { Held: 0 }, "An actual current unpublished blocking animation received a premature puzzle action.");
        Fixture ladder = Create(5, 0x095D);
        ladder.Script[0x095D] = 0xC2;
        ladder = ladder with { Vm = ladder.Vm with { PendingRequests = [new(8, 3, 6, 0x095D, new FieldActorLadder(-16, -105, 5, 83, 0, 26, 0, 1))] } };
        Check(Read(ladder) is null, "The puzzle strategy took ownership of the genuine native ladder continuation.");
    }

    private static Ff7AutoplayInput? Read(Fixture source) => Ff7AutoPlayScriptedInput.ReadNativeJunonEscapeInput(source.Vm, source.Process, source.Script);

    private static Fixture Create(byte stage, int pc)
    {
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 4).Select(_ => new FieldEntitySnapshot(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[0] = entities[0] with { ProgramCounter = 0x0523 };
        entities[3] = entities[3] with { ProgramCounter = pc, Priority = 6, ModelBound = true, PartyCharacter = 2 };
        entities[3].SavedProgramCounters[7] = 0x07B7;
        entities[3].SyncStates[6] = FieldSyncState.Waiting;
        entities[3].SyncOwners[6] = 0;
        FieldVariables variables = new();
        variables.WriteWord(FieldBank.Persistent0Word, 0, 1016);
        variables.WriteByte(FieldBank.MapByte, 0x13, stage);
        byte[] script = new byte[0x0A00];
        script[0x0523] = 3;
        script[0x0524] = 3;
        script[0x0525] = 0xCC;
        script[pc] = 0x30;
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 1, true, variables.CopyPersistent(), variables.CopyMap(), entities, PendingRequests: [], Input: new());
        Ff7FieldProcess process = new() { PlayerLocked = true, MenuDisabled = true, Actors = [new() { Slot = 0, Entity = 3, Model = 0, PlayerControlled = true, PartyCharacter = 2 }] };
        return new(vm, process, script);
    }

    private static Fixture WithVariable(Fixture source, FieldBank bank, byte address, int value)
    {
        FieldVariables variables = new(source.Vm.PersistentVariables, source.Vm.MapVariables);
        if (bank == FieldBank.Persistent0Word)
        {
            variables.WriteWord(bank, address, (short)value);
        }
        else
        {
            variables.WriteByte(bank, address, (byte)value);
        }
        return source with { Vm = source.Vm with { PersistentVariables = variables.CopyPersistent(), MapVariables = variables.CopyMap() } };
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
