using System.Text.Json;
using Memoria.FF7.Events;

internal static class ExtendedTests
{
    public static void MusicLockAndControlTarget()
    {
        var host = new RecordingHost { Complete = true };
        var vm = New(Code(0, 0xA1, 0, 0xF5, 1, 0xBF, 0, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(true, host.Requests.Any(request => request.Command == new FieldMusicCommand(FieldMusicOperation.Lock, 1)));
        Check(true, host.Requests.Any(request => request.Command == new FieldActorProperty(FieldActorPropertyKind.PlayerCharacter, 0)));
        host = new RecordingHost { Complete = true };
        vm = New(Code(0, 0xBF, 0, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(0, host.Requests.Count);
    }

    public static void Comparisons()
    {
        foreach (byte opcode in new byte[] { 0x14, 0x15, 0x16, 0x17, 0x18, 0x19 })
        {
            foreach ((ushort left, ushort right) in new (ushort, ushort)[] { (3, 1), (1, 3), (7, 7), (256, 256), (65535, 1), (128, 7) })
            {
                for (byte operation = 0; operation <= 10; operation++)
                {
                    bool word = opcode >= 0x16;
                    bool unsigned = opcode >= 0x18;
                    int x = word ? unsigned ? left : unchecked((short)left) : (byte)left;
                    int y = word ? unsigned ? right : unchecked((short)right) : (byte)right;
                    if (operation >= 9 && (y < 0 || y >= 32))
                    {
                        continue;
                    }
                    bool expected = operation switch
                    {
                        0 => x == y,
                        1 => x != y,
                        2 => x > y,
                        3 => x < y,
                        4 => x >= y,
                        5 => x <= y,
                        6 => (byte)(x & y) != 0,
                        7 => (byte)(x ^ y) != 0,
                        8 => (byte)(x | y) != 0,
                        9 => (byte)(x & (1 << y)) != 0,
                        _ => (byte)(x & (1 << y)) == 0
                    };
                    int origin = 17;
                    int branch = word ? 7 : 5;
                    int length = branch + ((opcode & 1) == 1 ? 2 : 1);
                    var code = new byte[128];
                    code[origin] = opcode;
                    code[origin + 2] = (byte)left;
                    code[origin + (word ? 4 : 3)] = (byte)right;
                    if (word)
                    {
                        code[origin + 3] = (byte)(left >> 8);
                        code[origin + 5] = (byte)(right >> 8);
                    }
                    code[origin + (word ? 6 : 4)] = operation;
                    code[origin + branch] = (byte)(length + 5 - branch);
                    new byte[] { 0x80, 0x50, 0, 1 }.CopyTo(code, origin + length);
                    new byte[] { 0x80, 0x50, 0, 2 }.CopyTo(code, origin + length + 5);
                    var vm = New(code);
                    vm.Initialize();
                    vm.AdvanceTick();
                    Check(expected ? 1 : 2, vm.Variables.ReadByte(FieldBank.MapByte, 0));
                }
            }
        }
    }

    public static void HostSnapshot()
    {
        var host = new RecordingHost { Complete = false };
        var code = Code(0, 0xF8, 53, 0xF9, 0);
        var program = Program(code);
        var vm = new FieldMachine(program, host: host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(17, vm.Capture().Entities[0].ProgramCounter);
        string json = JsonSerializer.Serialize(vm.Capture());
        var snapshot = JsonSerializer.Deserialize<FieldVmSnapshot>(json)!;
        Check(1L, snapshot.PendingRequests!.Single().Id);
        Check(53, ((FieldMovieCommand)snapshot.PendingRequests!.Single().Command).MovieId);
        var restored = FieldMachine.Restore(program, snapshot, host);
        restored.AdvanceTick();
        Check(1L, host.Requests.Last().Id);
        host.Complete = true;
        restored.AdvanceTick();
        Check(19, restored.Capture().Entities[0].ProgramCounter);
        Check(2L, restored.Capture().PendingRequests!.Single().Id);
        restored.AdvanceTick();
        Check(20, restored.Capture().Entities[0].ProgramCounter);
        Check(0, restored.Capture().PendingRequests!.Length);
        var bad = snapshot with { NextRequestId = 1 };
        Throws<InvalidDataException>(() => FieldMachine.Restore(program, bad, host));
        var duplicate = snapshot with { PendingRequests = [snapshot.PendingRequests![0], snapshot.PendingRequests![0]] };
        Throws<InvalidDataException>(() => FieldMachine.Restore(program, duplicate, host));
    }

    public static void HostOperands()
    {
        var host = new RecordingHost { Complete = true, Value = 1 };
        var code = Code(0xA1, 3, 0, 0xA5, 0, 0, 0xFE, 0xFF, 3, 0, 4, 0, 5, 0, 0x48, 0x05, 2, 7, 0, 2, 8, 0);
        var vm = New(code, host);
        vm.Initialize();
        Check(3, ((FieldActorBind)host.Requests[0].Command).ModelIndex);
        vm.AdvanceTick();
        var place = (FieldActorPlacement)host.Requests.Last().Command;
        Check((short)-2, place.X);
        Check((short)3, place.Y);
        Check((short)4, place.Z!.Value);
        Check((short)5, place.Triangle!.Value);
        vm.AdvanceTick();
        var choice = (FieldDialogueCommand)host.Requests.Last().Command;
        Check(2, choice.Window);
        Check(7, choice.Text);
        Check((byte)1, vm.Variables.ReadByte(FieldBank.MapByte, 8));
    }

    public static void FieldResetAndBits()
    {
        var vars = new FieldVariables();
        vars.WriteByte(FieldBank.Persistent0Byte, 31, 0x80);
        vars.WriteByte(FieldBank.MapByte, 4, 9);
        var vm = new FieldMachine(Program(Code(0, 0x83, 0x10, 31, 1, 0x82, 0x50, 0, 7, 0x84, 0x50, 0, 7, 0)), vars);
        vm.Initialize();
        Check((byte)0x83, vars.ReadByte(FieldBank.Persistent0Byte, 31));
        Check((byte)0, vars.ReadByte(FieldBank.MapByte, 4));
        vm.AdvanceTick();
        Check((byte)0x81, vars.ReadByte(FieldBank.Persistent0Byte, 31));
        Check((byte)0, vars.ReadByte(FieldBank.MapByte, 0));
    }

    public static void CanonicalParty()
    {
        var host = new RecordingHost { Complete = false };
        var variables = new FieldVariables();
        variables.WriteByte(FieldBank.Persistent1Byte, 9, 0);
        variables.WriteByte(FieldBank.Persistent1Byte, 10, 255);
        variables.WriteByte(FieldBank.Persistent1Byte, 11, 255);
        var program = Program(Code(0, 0xC8, 1, 0xCA, 3, 255, 254, 0));
        var vm = new FieldMachine(program, variables, host: host);
        vm.Initialize();
        vm.AdvanceTick();
        Check((byte)1, variables.ReadByte(FieldBank.Persistent1Byte, 10));
        Check((ushort)2, ((FieldPartyProjection)host.Requests.Last().Command).VisibilityOrMask);
        var state = JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(vm.Capture()))!;
        vm = FieldMachine.Restore(program, state, host);
        host.Complete = true;
        vm.AdvanceTick();
        Check((byte)3, vm.Variables.ReadByte(FieldBank.Persistent1Byte, 9));
        Check((byte)0, vm.Variables.ReadByte(FieldBank.Persistent1Byte, 10));
        Check((byte)255, vm.Variables.ReadByte(FieldBank.Persistent1Byte, 11));
        Check((ushort)9, ((FieldPartyProjection)host.Requests.Last().Command).VisibilityOrMask);
        vm.SynchronizePartyFromMain(new[] { 0, 1, 255 });
        Check((byte)1, vm.Variables.ReadByte(FieldBank.Persistent1Byte, 9));
        Check((byte)0, vm.Variables.ReadByte(FieldBank.Persistent1Byte, 10));
    }
    public static void ExternalTriggersAndSpeed()
    {
        var code = Code(0, 0, 0);
        new byte[] { 0x80, 0x50, 0, 9, 0 }.CopyTo(code, 64);
        var entries = Enumerable.Repeat((ushort)16, 32).ToArray();
        entries[1] = 64;
        var vm = new FieldMachine(new FieldProgram(code, new[] { entries }));
        vm.Initialize();
        Check(true, vm.TryRequestScript(0, 2));
        Check(7, vm.Capture().Entities[0].Priority);
        Check(true, vm.TryRequestScript(0, 1));
        Check(1, vm.Capture().Entities[0].Priority);
        Check(false, vm.TryRequestScript(0, 1));
        vm.AdvanceTick();
        Check((byte)9, vm.Variables.ReadByte(FieldBank.MapByte, 0));
        Check(17, vm.Capture().Entities[0].ProgramCounter);
        Check(7, vm.Capture().Entities[0].Priority);
        var host = new RecordingHost { Complete = true };
        vm = New(Code(0xA1, 0, 0, 0xB2, 0, 0xFF, 0xFF, 0), host);
        vm.Initialize();
        vm.AdvanceTick();
        var speed = (FieldActorProperty)host.Requests.Last().Command;
        Check(FieldActorPropertyKind.MoveSpeed, speed.Property);
        Check(65535, speed.Value);
    }
    public static void CancelMove()
    {
        var host = new RecordingHost { Complete = false };
        var code = Code(0, 1, 1, 0x21, 0);
        new byte[] { 0xA1, 0, 0, 0xA8, 0, 10, 0, 20, 0, 0 }.CopyTo(code, 48);
        code[80] = 0;
        var rows = new[] { Enumerable.Repeat((ushort)16, 32).ToArray(), Enumerable.Repeat((ushort)48, 32).ToArray() };
        rows[1][1] = 80;
        var program = new FieldProgram(code, rows);
        var vm = new FieldMachine(program, host: host);
        host.Complete = true;
        vm.Initialize();
        var state = vm.Capture();
        // Resume caller on RET while target starts its move, then restore caller to REQ.
        state.Entities[0] = state.Entities[0] with { ProgramCounter = 20 };
        vm = FieldMachine.Restore(program, state, host);
        host.Complete = false;
        vm.AdvanceTick();
        state = vm.Capture();
        Check(1, state.PendingRequests!.Length);
        long moveId = state.PendingRequests[0].Id;
        state.Entities[0] = state.Entities[0] with { ProgramCounter = 17 };
        vm = FieldMachine.Restore(program, state, host);
        vm.AdvanceTick();
        Check(moveId, host.Cancelled.Single().Id);
        var restarted = vm.Capture().PendingRequests!.Single();
        Check(true, restarted.Id > moveId);
        Check(true, restarted.Command is FieldActorMove);
    }

    private static byte[] Code(params byte[] body)
    {
        var code = new byte[256];
        body.CopyTo(code, 16);
        return code;
    }
    private static FieldProgram Program(byte[] code) => new(code, new[] { Enumerable.Repeat((ushort)16, 32).ToArray() });
    private static FieldMachine New(byte[] code, IFieldHost? host = null) => new(Program(code), host: host);
    private static void Check<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
    private static void Throws<T>(Action run) where T : Exception
    {
        try
        {
            run();
        }
        catch (T)
        {
            return;
        }
        throw new Exception($"Expected {typeof(T).Name}.");
    }
    private sealed class RecordingHost : IFieldHost
    {
        public bool Complete { get; set; }
        public int Value { get; set; }
        public List<FieldHostRequest> Requests { get; } = [];
        public List<FieldHostRequest> Cancelled { get; } = [];
        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return new FieldHostReply(Complete, Value);
        }
        public void Cancel(FieldHostRequest request)
        {
            Cancelled.Add(request);
        }
    }
}
