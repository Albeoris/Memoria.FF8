using System.Text.Json;
using Memoria.FF7.Events;

if (args.Contains("--audit-installed", StringComparer.Ordinal))
{
    return InstalledAudit.Run();
}

if (args.Contains("--materia-only", StringComparer.Ordinal))
{
    ExtendedTests.MateriaOperandsAndStatus();
    Console.WriteLine("PASS native materia operands and status");
    return 0;
}

if (args.Contains("--fade-only", StringComparer.Ordinal))
{
    ExtendedTests.FadeOperandsAndYield();
    Console.WriteLine("PASS native fade operands and yield");
    return 0;
}

if (args.Contains("--model-effect-only", StringComparer.Ordinal))
{
    ExtendedTests.ModelEffectPayloadAndLegacyPendingRestore();
    ExtendedTests.OpeningHostOperands();
    Console.WriteLine("PASS native model effect payload, legacy pending restoration and existing KAWAI13 decoding");
    return 0;
}

var tests = new (string Name, Action Run)[]
{
    ("typed bank aliases and widths", BankWidths),
    ("native saturated byte addition and cold continuation", SaturatedByteAddTests.Run),
    ("native saturated byte subtraction and cold continuation", SaturatedByteAddTests.RunSubtract),
    ("native SPECIAL arrow suppression operands", ExtendedTests.SpecialArrowOperandsAndContinuation),
    ("native turn wait operands and cold continuation", ExtendedTests.TurnWaitOperandsAndContinuation),
    ("native gil operands and cold continuation", ExtendedTests.GilOperandsAndContinuation),
    ("native all-character recovery and cold continuation", ExtendedTests.AllRecoveryContinuation),
    ("initialization first RET continuation", InitContinuation),
    ("WAIT zero yields", WaitZero),
    ("WAIT counter and continuation", WaitCount),
    ("eight instruction cycle budget", CycleBudget),
    ("partial cycle cursor retries exact handler and budget", CycleCursorTests.Run),
    ("short forward and backward control flow", ShortBranches),
    ("long forward and backward operand widths", LongBranches),
    ("request completion and suspended continuation", RequestCompletion),
    ("request busy skip versus wait", BusyRequests),
    ("lower priority queued behind urgent request", QueuedRequest),
    ("preemption resets entity wait counter", PreemptionWait),
    ("JSON snapshot resumes exact request wait", SnapshotContinuation),
    ("snapshot detached and contract validated", SnapshotValidation),
    ("unsupported and malformed bytecode context", ContextualFault),
    ("initialization budget guards loops", InitBudget),
    ("entity order and absent request target", EntityOrder),
    ("REQSW advances on acceptance before completion", RequestStartWait),
    ("single byte yield and init yield handling", YieldAndInitialization),
    ("SETWORD source bank fixed operand width", SetWordReference),
    ("unsigned 16 bit program counter wrapping", ProgramCounterWrap),
    ("initialization request preserves slots but resets script0 PC", InitRequestReset),
    ("initialization request requires unvisited entity PC facts", InitRequestUnknownPc),
    ("initialization RET continuation is restorable", InitReturnBoundary),
    ("yielded continuation must remain inside section", YieldedContinuationBoundary),
    ("conditional comparisons and widths", ExtendedTests.Comparisons),
    ("typed host requests survive JSON continuation", ExtendedTests.HostSnapshot),
    ("typed host operand decode and choice result", ExtendedTests.HostOperands),
    ("field initialization bank locks and bit operations", ExtendedTests.FieldResetAndBits),
    ("request preemption cancels pending movement", ExtendedTests.CancelMove),
    ("external triggers and unsigned move speed", ExtendedTests.ExternalTriggersAndSpeed),
    ("canonical party bank and host projection", ExtendedTests.CanonicalParty),
    ("native music lock and player target operands", ExtendedTests.MusicLockAndControlTarget),
    ("reached opening host operand widths and selectors", ExtendedTests.OpeningHostOperands),
    ("native item packed operands and same-cycle continuation", ExtendedTests.ItemOperandsAndContinuation),
    ("native fixed animation movement operands and continuation", ExtendedTests.FixedAnimationMoveOperandsAndContinuation),
    ("native movie camera gate operands and continuation", ExtendedTests.MovieCameraOperandsAndContinuation),
    ("native instant entity facing operands and yield", ExtendedTests.ImmediateFaceOperandsAndYield),
    ("native party recovery single-byte continuation", ExtendedTests.PartyRecoveryContinuation),
    ("native wait-free window rectangle operands", ExtendedTests.WindowSizeWhenFreeOperands),
    ("native dynamic actor approach operands and cancellation", ExtendedTests.ActorApproachOperandsAndPreemption),
    ("native camera shake bank selectors and continuation", ExtendedTests.CameraShakeOperandsAndContinuation),
    ("native preload raw word and same-cycle continuation", ExtendedTests.PreloadRequestRawWordAndContinuation),
    ("native model effect payload and legacy pending restoration", ExtendedTests.ModelEffectPayloadAndLegacyPendingRestore),
    ("native timer and special window operands", ExtendedTests.TimerAndSpecialWindowOperands),
    ("native view offset operands and yield", ExtendedTests.ViewOffsetOperandsAndYield),
    ("native materia operands and status", ExtendedTests.MateriaOperandsAndStatus),
    ("native fade operands and yield", ExtendedTests.FadeOperandsAndYield),
    ("native coordinate query and signed remainder", ExtendedTests.CoordinateQueryAndRemainder)
};
int failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception error)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {error}");
    }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} passed");
return failed == 0 ? 0 : 1;

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new Exception($"Expected {expected}, got {actual}.");
    }
}

static T Throws<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T error)
    {
        return error;
    }
    throw new Exception($"Expected {typeof(T).Name}.");
}

static FieldProgram Program(byte[] code, int entities = 1, params (int Entity, int Script, ushort Offset)[] overrides)
{
    var entries = Enumerable.Range(0, entities).Select(_ => Enumerable.Repeat((ushort)16, 32).ToArray()).ToArray();
    foreach (var item in overrides)
    {
        entries[item.Entity][item.Script] = item.Offset;
    }
    return new FieldProgram(code, entries);
}

static byte[] Code(params byte[] body)
{
    var code = new byte[Math.Max(512, body.Length + 16)];
    body.CopyTo(code, 16);
    return code;
}

static FieldMachine Ready(byte[] body)
{
    var machine = new FieldMachine(Program(Code(body)));
    machine.Initialize();
    return machine;
}

static void BankWidths()
{
    var variables = new FieldVariables();
    foreach (var pair in new[] { (FieldBank.Persistent0Byte, FieldBank.Persistent0Word), (FieldBank.Persistent1Byte, FieldBank.Persistent1Word), (FieldBank.Persistent2Byte, FieldBank.Persistent2Word), (FieldBank.Persistent3Byte, FieldBank.Persistent3Word), (FieldBank.Persistent4Byte, FieldBank.Persistent4Word), (FieldBank.MapByte, FieldBank.MapWord) })
    {
        variables.WriteWord(pair.Item2, 4, unchecked((short)0xFEDC));
        Equal((byte)0xDC, variables.ReadByte(pair.Item1, 4));
        Equal((byte)0xFE, variables.ReadByte(pair.Item1, 5));
        Equal(unchecked((short)0xFEDC), variables.ReadWord(pair.Item2, 4));
        Equal((short)0xDC, variables.ReadWord(pair.Item1, 4));
        variables.WriteWord(pair.Item1, 4, 0x1234);
        Equal((byte)0x34, variables.ReadByte(pair.Item2, 4));
        Equal((byte)0xFE, variables.ReadByte(pair.Item2, 5));
    }
    Equal((short)-1, variables.ReadWord(FieldBank.Immediate, 65535));
    Throws<NotSupportedException>(() => variables.WriteByte(FieldBank.Immediate, 0, 1));
    Throws<NotSupportedException>(() => variables.ReadByte((FieldBank)8, 0));
    Throws<InvalidDataException>(() => variables.WriteWord(FieldBank.MapWord, 255, 1));
    variables.WriteWord(FieldBank.Persistent0Word, 255, 0x1234);
    Equal((byte)0x12, variables.ReadByte(FieldBank.Persistent1Byte, 0));
}

static void InitContinuation()
{
    var machine = Ready([0x80, 0x50, 0, 9, 0, 0x81, 0x60, 1, 0x34, 0xF2, 0]);
    Equal((byte)9, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    Equal(21, machine.Capture().Entities[0].ProgramCounter);
    machine.AdvanceTick();
    Equal(unchecked((short)0xF234), machine.Variables.ReadWord(FieldBank.MapWord, 1));
    Equal(26, machine.Capture().Entities[0].ProgramCounter);
}

static void WaitZero()
{
    var machine = Ready([0, 0x24, 0, 0, 0x80, 0x50, 0, 7, 0]);
    machine.AdvanceTick();
    Equal(20, machine.Capture().Entities[0].ProgramCounter);
    Equal((byte)0, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    machine.AdvanceTick();
    Equal((byte)7, machine.Variables.ReadByte(FieldBank.MapByte, 0));
}

static void WaitCount()
{
    var machine = Ready([0, 0x24, 2, 0, 0x80, 0x50, 0, 7, 0]);
    machine.AdvanceTick();
    Equal((ushort)2, machine.Capture().Entities[0].WaitCounter);
    Equal(17, machine.Capture().Entities[0].ProgramCounter);
    machine.AdvanceTick();
    Equal((ushort)1, machine.Capture().Entities[0].WaitCounter);
    machine.AdvanceTick();
    Equal((byte)7, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    Equal((ushort)0, machine.Capture().Entities[0].WaitCounter);
}

static void CycleBudget()
{
    var body = new List<byte> { 0 };
    for (byte i = 1; i <= 9; i++)
    {
        body.AddRange([0x80, 0x50, 0, i]);
    }
    body.Add(0);
    var machine = Ready(body.ToArray());
    machine.AdvanceTick();
    Equal((byte)8, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    machine.AdvanceTick();
    Equal((byte)9, machine.Variables.ReadByte(FieldBank.MapByte, 0));
}

static void ShortBranches()
{
    var machine = Ready([0, 0x10, 3, 0xFF, 0xFF, 0x80, 0x50, 0, 8, 0x12, 4]);
    machine.AdvanceTick();
    Equal((byte)8, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    Equal(21, machine.Capture().Entities[0].ProgramCounter);
}

static void LongBranches()
{
    var code = Code(0, 0x11, 0x10, 1);
    code[290] = 0x13;
    code[291] = 0x11;
    code[292] = 1;
    var machine = new FieldMachine(Program(code));
    machine.Initialize();
    machine.AdvanceTick();
    Equal(17, machine.Capture().Entities[0].ProgramCounter);
}

static FieldProgram Requests(byte type, byte[] targetScript)
{
    var code = Code(0, type, 1, 0x21, 0x80, 0x50, 0, 99, 0);
    code[48] = 0;
    code[49] = 0;
    targetScript.CopyTo(code, 64);
    return Program(code, 2, (1, 0, 48), (1, 1, 64));
}

static void RequestCompletion()
{
    var machine = new FieldMachine(Requests(3, [0x24, 0, 0, 0x80, 0x50, 1, 8, 0]));
    machine.Initialize();
    machine.AdvanceTick();
    var snapshot = machine.Capture();
    Equal(17, snapshot.Entities[0].ProgramCounter);
    Equal(FieldSyncState.Waiting, snapshot.Entities[1].SyncStates[1]);
    Equal(49, snapshot.Entities[1].SavedProgramCounters[7]);
    machine.AdvanceTick();
    Equal(FieldSyncState.Done, machine.Capture().Entities[1].SyncStates[1]);
    Equal((byte)0, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    machine.AdvanceTick();
    Equal((byte)99, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    Equal((byte)8, machine.Variables.ReadByte(FieldBank.MapByte, 1));
    Equal(FieldSyncState.None, machine.Capture().Entities[1].SyncStates[1]);
    Equal(7, machine.Capture().Entities[1].Priority);
}

static void BusyRequests()
{
    foreach (byte type in new byte[] { 1, 2, 3 })
    {
        var machine = new FieldMachine(Requests(type, [0]));
        machine.Initialize();
        var snapshot = machine.Capture();
        var target = snapshot.Entities[1];
        snapshot.Entities[1] = target with { Priority = 1 };
        machine = FieldMachine.Restore(Requests(type, [0]), snapshot);
        machine.AdvanceTick();
        Equal(type == 1 ? (byte)99 : (byte)0, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    }
}

static void QueuedRequest()
{
    var program = Requests(3, [0x80, 0x50, 1, 8, 0]);
    var machine = new FieldMachine(program);
    machine.Initialize();
    var state = machine.Capture();
    state.Entities[1] = state.Entities[1] with { Priority = 0 };
    state.Entities[1].SavedProgramCounters[7] = 49;
    machine = FieldMachine.Restore(program, state);
    machine.AdvanceTick();
    Equal((byte)8, machine.Variables.ReadByte(FieldBank.MapByte, 1));
    Equal(FieldSyncState.Done, machine.Capture().Entities[1].SyncStates[1]);
    machine.AdvanceTick();
    Equal((byte)99, machine.Variables.ReadByte(FieldBank.MapByte, 0));
}

static void PreemptionWait()
{
    var program = Requests(1, [0x24, 0, 0, 0]);
    var machine = new FieldMachine(program);
    machine.Initialize();
    var state = machine.Capture();
    state.Entities[1] = state.Entities[1] with { WaitCounter = 200 };
    machine = FieldMachine.Restore(program, state);
    machine.AdvanceTick();
    Equal((ushort)0, machine.Capture().Entities[1].WaitCounter);
    Equal(67, machine.Capture().Entities[1].ProgramCounter);
}

static void SnapshotContinuation()
{
    var program = Requests(3, [0x24, 2, 0, 0x80, 0x50, 1, 8, 0]);
    var original = new FieldMachine(program);
    original.Initialize();
    original.AdvanceTick();
    string json = JsonSerializer.Serialize(original.Capture());
    var restored = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(json)!);
    for (int i = 0; i < 5; i++)
    {
        original.AdvanceTick();
        restored.AdvanceTick();
        Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}

static void SnapshotValidation()
{
    var program = Program(Code(0, 0));
    var machine = new FieldMachine(program);
    machine.Initialize();
    var snapshot = machine.Capture();
    Throws<InvalidOperationException>(() => new FieldMachine(program).Capture());
    Throws<InvalidDataException>(() => FieldMachine.Restore(program, snapshot with { Initialized = false }));
    snapshot.MapVariables[0] = 8;
    Equal((byte)0, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    Throws<InvalidDataException>(() => FieldMachine.Restore(program, snapshot with { Profile = "ff8" }));
    Throws<InvalidDataException>(() => FieldMachine.Restore(Program(Code(0, 0x5F, 0)), snapshot));
    Throws<InvalidDataException>(() => FieldMachine.Restore(program, snapshot with { PersistentVariables = new byte[1] }));
    Throws<InvalidDataException>(() => FieldMachine.Restore(program, snapshot with { MapVariables = null! }));
    var validEntity = snapshot.Entities[0];
    snapshot.Entities[0] = validEntity with { ProgramCounter = 512 };
    Throws<InvalidDataException>(() => FieldMachine.Restore(program, snapshot));
    snapshot.Entities[0] = validEntity with { Priority = 8 };
    Throws<InvalidDataException>(() => FieldMachine.Restore(program, snapshot));
    snapshot.Entities[0] = validEntity with { SavedProgramCounters = new int[1] };
    Throws<InvalidDataException>(() => FieldMachine.Restore(program, snapshot));
    snapshot.Entities[0] = validEntity;
    snapshot.Entities[0].SyncStates[0] = (FieldSyncState)255;
    Throws<InvalidDataException>(() => FieldMachine.Restore(program, snapshot));
    snapshot.Entities[0].SyncStates[0] = FieldSyncState.Waiting;
    Throws<InvalidDataException>(() => FieldMachine.Restore(program, snapshot));
    snapshot.Entities[0].SyncStates[0] = FieldSyncState.None;
    snapshot.Entities[0].SyncOwners[0] = 1;
    Throws<InvalidDataException>(() => FieldMachine.Restore(program, snapshot));
}

static void ContextualFault()
{
    var machine = Ready([0, 0xFF]);
    var error = Throws<FieldExecutionException>(() => machine.AdvanceTick());
    Equal(17, error.Context.ProgramCounter);
    Equal((byte)255, error.Context.Opcode);
    Equal(0, error.Context.Entity);
    var code = new byte[] { 0, 0x24, 1 };
    var entries = new[] { new ushort[32] };
    machine = new FieldMachine(new FieldProgram(code, entries));
    machine.Initialize();
    Throws<FieldExecutionException>(() => machine.AdvanceTick());
    machine = Ready([0, 0x80, 0x80, 0, 1]);
    Throws<FieldExecutionException>(() => machine.AdvanceTick());
    machine = Ready([0, 1, 2, 1]);
    Throws<FieldExecutionException>(() => machine.AdvanceTick());
}

static void InitBudget()
{
    var machine = new FieldMachine(Program(Code(0x12, 0)));
    Throws<FieldExecutionException>(() => machine.Initialize(20));
}

static void EntityOrder()
{
    var code = Code(0, 1, 255, 255, 0x80, 0x50, 0, 1, 0);
    code[48] = 0;
    new byte[] { 0x80, 0x50, 0, 2, 0 }.CopyTo(code, 49);
    var machine = new FieldMachine(Program(code, 2, (1, 0, 48)));
    machine.Initialize();
    machine.AdvanceTick();
    Equal((byte)2, machine.Variables.ReadByte(FieldBank.MapByte, 0));
}


static void RequestStartWait()
{
    var program = Requests(2, [0x24, 4, 0, 0]);
    var machine = new FieldMachine(program);
    machine.Initialize();
    machine.AdvanceTick();
    Equal((byte)99, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    Equal((ushort)4, machine.Capture().Entities[1].WaitCounter);
    Equal(FieldSyncState.None, machine.Capture().Entities[1].SyncStates[1]);
}

static void YieldAndInitialization()
{
    var machine = Ready([0x24, 0, 0, 0x80, 0x50, 0, 1, 0, 0x5F, 0x80, 0x50, 0, 2, 0]);
    Equal((byte)1, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    machine.AdvanceTick();
    Equal(25, machine.Capture().Entities[0].ProgramCounter);
    Equal((byte)1, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    machine.AdvanceTick();
    Equal((byte)2, machine.Variables.ReadByte(FieldBank.MapByte, 0));
}

static void SetWordReference()
{
    var machine = Ready([0, 0x81, 0x60, 0, 0x34, 0xF2, 0x81, 0x26, 1, 0, 0xFE, 0]);
    machine.AdvanceTick();
    Equal(unchecked((short)0xF234), machine.Variables.ReadWord(FieldBank.Persistent0Word, 1));
    Equal(27, machine.Capture().Entities[0].ProgramCounter);
}

static void ProgramCounterWrap()
{
    var code = new byte[65536];
    code[16] = 0;
    code[17] = 0x12;
    code[18] = 18;
    code[65535] = 0x5F;
    var machine = new FieldMachine(Program(code));
    machine.Initialize();
    machine.AdvanceTick();
    Equal(65535, machine.Capture().Entities[0].ProgramCounter);
    machine.AdvanceTick();
    Equal(0, machine.Capture().Entities[0].ProgramCounter);
}


static FieldProgram InitRequests()
{
    var code = Code(1, 1, 0x21, 0, 0);
    new byte[] { 0x80, 0x50, 0, 7, 0, 0 }.CopyTo(code, 48);
    new byte[] { 0x80, 0x50, 0, 9, 0, 0 }.CopyTo(code, 64);
    return Program(code, 2, (1, 0, 48), (1, 1, 64));
}

static void InitRequestReset()
{
    var program = InitRequests();
    var machine = new FieldMachine(program, initialEntityProgramCounters: new ushort[] { 16, 96 });
    machine.Initialize();
    var state = machine.Capture();
    Equal((byte)7, machine.Variables.ReadByte(FieldBank.MapByte, 0));
    Equal(53, state.Entities[1].ProgramCounter);
    Equal(1, state.Entities[1].Priority);
    Equal(96, state.Entities[1].SavedProgramCounters[7]);
    var restored = FieldMachine.Restore(program, state);
    restored.AdvanceTick();
    Equal(96, restored.Capture().Entities[1].ProgramCounter);
    Equal(7, restored.Capture().Entities[1].Priority);
    Throws<ArgumentException>(() => new FieldMachine(program, initialEntityProgramCounters: new ushort[] { 16 }));
    Throws<ArgumentException>(() => new FieldMachine(program, initialEntityProgramCounters: new ushort[] { 16, 512 }));
}

static void InitRequestUnknownPc()
{
    var machine = new FieldMachine(InitRequests());
    var fault = Throws<FieldExecutionException>(() => machine.Initialize());
    Equal(0, fault.Context.Entity);
    Equal(16, fault.Context.ProgramCounter);
    Equal((byte)1, fault.Context.Opcode);
    Equal(false, machine.Initialized);
    Throws<InvalidOperationException>(() => machine.Capture());
}

static void InitReturnBoundary()
{
    var program = new FieldProgram([0], new[] { new ushort[32] });
    var machine = new FieldMachine(program);
    var fault = Throws<FieldExecutionException>(() => machine.Initialize());
    Equal(0, fault.Context.ProgramCounter);
    Equal((byte)0, fault.Context.Opcode);
    Equal(false, machine.Initialized);
    Throws<InvalidOperationException>(() => machine.Capture());

    var wrappedProgram = new FieldProgram(new byte[65536], new[] { Enumerable.Repeat(ushort.MaxValue, 32).ToArray() });
    machine = new FieldMachine(wrappedProgram);
    machine.Initialize();
    Equal(0, machine.Capture().Entities[0].ProgramCounter);
    Equal(0, FieldMachine.Restore(wrappedProgram, machine.Capture()).Capture().Entities[0].ProgramCounter);
}

static void YieldedContinuationBoundary()
{
    foreach (var item in new[] { (Code: new byte[] { 0, 0x5F }, InvalidPc: 2), (Code: new byte[] { 0, 0x12, 3 }, InvalidPc: 65534) })
    {
        var program = new FieldProgram(item.Code, new[] { new ushort[32] });
        var machine = new FieldMachine(program);
        machine.Initialize();
        var fault = Throws<FieldExecutionException>(() => machine.AdvanceTick());
        Equal(item.InvalidPc, fault.Context.ProgramCounter);
        Equal(0, fault.Context.Entity);
        Equal(0L, fault.Context.Tick);
        var captureFault = Throws<FieldExecutionException>(() => machine.Capture());
        Equal(item.InvalidPc, captureFault.Context.ProgramCounter);
    }
}
