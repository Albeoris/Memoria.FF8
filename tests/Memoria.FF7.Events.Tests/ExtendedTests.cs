using System.Text.Json;
using Memoria.FF7.Events;

internal static class ExtendedTests
{
    public static void TurnWaitOperandsAndContinuation()
    {
        var host = new RecordingHost { Complete = true };
        byte[] code = Code(0xA1, 0, 0, 0xDE, 0x80, 0x50, 8, 7, 0x5F, 0x12, 0);
        var vm = New(code, host);
        vm.Initialize();
        host.Complete = false;
        vm.AdvanceTick();
        FieldVmSnapshot snapshot = vm.Capture();
        FieldHostRequest pending = snapshot.PendingRequests!.Single();
        Check(new FieldActorProperty(FieldActorPropertyKind.TurnWait, 0), pending.Command);
        Check(19, snapshot.Entities[0].ProgramCounter);
        Check((byte)0, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        var coldHost = new RecordingHost { Complete = true };
        vm = FieldMachine.Restore(Program(code), JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(snapshot))!, coldHost);
        vm.AdvanceTick();
        Check(pending.Id, coldHost.Requests.Single().Id);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        Check(25, vm.Capture().Entities[0].ProgramCounter);

        var unboundHost = new RecordingHost { Complete = false };
        var unbound = New(Code(0, 0xDE, 0x80, 0x50, 8, 7, 0x5F, 0x12, 0), unboundHost);
        unbound.Initialize();
        unbound.AdvanceTick();
        Check(0, unboundHost.Requests.Count);
        Check((byte)7, unbound.Variables.ReadByte(FieldBank.MapByte, 8));
    }

    public static void SpecialArrowOperandsAndContinuation()
    {
        foreach (byte value in new byte[] { 0, 1, 255 })
        {
            var host = new RecordingHost { Complete = true };
            var vm = New(Code(0, 0x0F, 0xF5, value, 0x80, 0x50, 8, 7, 0x5F, 0x12, 0), host);
            vm.Initialize();
            vm.AdvanceTick();
            Check(new FieldControlCommand(FieldControlKind.CursorDisabled, value), host.Requests.Single().Command);
            Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
            Check(25, vm.Capture().Entities[0].ProgramCounter);
        }
        byte[] truncated = Code(0, 0x0F, 0xF5)[..19];
        var malformed = New(truncated, new RecordingHost());
        malformed.Initialize();
        Throws<FieldExecutionException>(() => malformed.AdvanceTick());
        var unsupported = New(Code(0, 0x0F, 0xF6, 1), new RecordingHost());
        unsupported.Initialize();
        Throws<FieldExecutionException>(() => unsupported.AdvanceTick());
    }

    public static void ModelEffectPayloadAndLegacyPendingRestore()
    {
        byte[] code = Code(0xA1, 0, 0, 0x28, 7, 0, 1, 1, 0, 255, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1);
        FieldProgram program = Program(code);
        RecordingHost host = new() { Complete = true };
        FieldMachine vm = new(program, host: host);
        vm.Initialize();
        host.Complete = false;
        vm.AdvanceTick();
        FieldVmSnapshot saved = vm.Capture();
        FieldHostRequest request = saved.PendingRequests!.Single();
        FieldModelEffectCommand decoded = (FieldModelEffectCommand)request.Command;
        Check(true, decoded.Payload!.SequenceEqual(new byte[] { 1, 1, 0, 255 }));
        FieldVmSnapshot legacy = saved with { PendingRequests = [request with { Command = decoded with { Payload = null } }] };
        FieldVmSnapshot corrupt = legacy with { PendingRequests = [request with { Command = decoded with { Mode = 0, Payload = null } }] };
        Throws<InvalidDataException>(() => FieldMachine.Restore(program, corrupt, host));
        vm = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(legacy))!, host);
        FieldHostRequest restored = vm.Capture().PendingRequests!.Single();
        Check(request.Id, restored.Id);
        Check(saved.NextRequestId, vm.Capture().NextRequestId);
        Check(true, ((FieldModelEffectCommand)restored.Command).Payload!.SequenceEqual(decoded.Payload));
        host.Complete = true;
        vm.AdvanceTick();
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
    }

    public static void PreloadRequestRawWordAndContinuation()
    {
        var host = new RecordingHost { Complete = true };
        var vm = New(Code(0, 0xD8, 255, 255, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(new FieldPreloadCommand(ushort.MaxValue), host.Requests.Single().Command);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        byte[] truncated = new byte[19];
        truncated[17] = 0xD8;
        vm = New(truncated, new RecordingHost());
        vm.Initialize();
        Throws<FieldExecutionException>(() => vm.AdvanceTick());
    }

    public static void CameraShakeOperandsAndContinuation()
    {
        var host = new RecordingHost { Complete = true };
        var vm = New(Code(0, 0x5E, 0x13, 0x56, 3, 10, 11, 12, 13, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.Variables.WriteByte(FieldBank.Persistent0Byte, 10, 255);
        vm.Variables.WriteByte(FieldBank.Persistent1Byte, 11, 2);
        vm.Variables.WriteByte(FieldBank.MapByte, 12, 5);
        vm.Variables.WriteByte(FieldBank.MapWord, 13, 3);
        vm.AdvanceTick();
        Check(new FieldCameraShake(3, 255, 2, 5, 3), host.Requests.Single().Command);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        host = new RecordingHost { Complete = true };
        vm = New(Code(0, 0x5E, 0xAA, 0xAA, 0, 0, 0, 0, 0, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(new FieldCameraShake(0, 0, 0, 0, 0), host.Requests.Single().Command);
        host = new RecordingHost { Complete = true };
        vm = New(Code(0, 0x5E, 0, 0, 2, 0, 0, 5, 2, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(new FieldCameraShake(2, 0, 0, 5, 2), host.Requests.Single().Command);
        byte[] truncated = new byte[24];
        truncated[17] = 0x5E;
        truncated[20] = 2;
        vm = New(truncated, new RecordingHost());
        vm.Initialize();
        Throws<FieldExecutionException>(() => vm.AdvanceTick());
    }

    public static void ActorApproachOperandsAndPreemption()
    {
        var host = new RecordingHost { Complete = false };
        byte[] code = Code(0xA1, 0, 0, 0xAA, 1, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1);
        new byte[] { 0xA1, 1, 0, 0 }.CopyTo(code, 80);
        new byte[] { 0x80, 0x50, 9, 1, 0 }.CopyTo(code, 96);
        var rows = new[] { Enumerable.Repeat((ushort)16, 32).ToArray(), Enumerable.Repeat((ushort)80, 32).ToArray() };
        rows[0][1] = 96;
        var program = new FieldProgram(code, rows);
        var vm = new FieldMachine(program, host: host);
        host.Complete = true;
        vm.Initialize();
        host.Complete = false;
        vm.AdvanceTick();
        FieldHostRequest request = vm.Capture().PendingRequests!.Single();
        Check(new FieldActorMove(0, 0, TargetEntity: 1), request.Command);
        Check(19, vm.Capture().Entities[0].ProgramCounter);
        vm = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(vm.Capture()))!, host);
        host.Complete = true;
        vm.AdvanceTick();
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        vm = new FieldMachine(program, host: host);
        vm.Initialize();
        host.Complete = false;
        vm.AdvanceTick();
        FieldVmSnapshot state = vm.Capture();
        long cancelledId = state.PendingRequests!.Single().Id;
        state.Entities[1] = state.Entities[1] with { ModelBound = false };
        vm = FieldMachine.Restore(program, state, host);
        vm.AdvanceTick();
        Check(cancelledId, host.Cancelled.Single().Id);
        Check(0, vm.Capture().PendingRequests!.Length);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        host = new RecordingHost { Complete = true };
        vm = new FieldMachine(program, host: host);
        vm.Initialize();
        host.Complete = false;
        vm.AdvanceTick();
        cancelledId = vm.Capture().PendingRequests!.Single().Id;
        Check(true, vm.TryRequestScript(0, 1));
        Check(cancelledId, host.Cancelled.Single().Id);
        Check(0, vm.Capture().PendingRequests!.Length);
    }

    public static void WindowSizeWhenFreeOperands()
    {
        var host = new RecordingHost { Complete = true };
        var vm = New(Code(0, 0x2F, 0, 145, 0, 8, 0, 136, 0, 41, 0, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(new FieldWindowCommand(FieldWindowOperation.SizeWhenFree, 0, 145, 8, 136, 41), host.Requests.Single().Command);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        byte[] truncated = new byte[26];
        truncated[17] = 0x2F;
        vm = New(truncated, new RecordingHost());
        vm.Initialize();
        Throws<FieldExecutionException>(() => vm.AdvanceTick());
    }

    public static void PartyRecoveryContinuation()
    {
        var host = new RecordingHost { Complete = true };
        var vm = New(Code(0, 0x3F, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(new FieldPartyRecovery(), host.Requests.Single().Command);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
    }

    public static void ImmediateFaceOperandsAndYield()
    {
        var host = new RecordingHost { Complete = true };
        var code = Code(0xA1, 0, 0, 0xB6, 1, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1);
        new byte[] { 0xA1, 1, 0, 0 }.CopyTo(code, 80);
        var rows = new[] { Enumerable.Repeat((ushort)16, 32).ToArray(), Enumerable.Repeat((ushort)80, 32).ToArray() };
        var program = new FieldProgram(code, rows);
        var vm = new FieldMachine(program, host: host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(new FieldActorFace(1, 0, 0, Immediate: true), host.Requests.Last().Command);
        Check(21, vm.Capture().Entities[0].ProgramCounter);
        Check((byte)0, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        Check(0, vm.Capture().PendingRequests!.Length);
        vm = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(vm.Capture()))!, host);
        vm.AdvanceTick();
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        foreach (int unbound in new[] { 0, 1 })
        {
            host = new RecordingHost { Complete = true };
            vm = new FieldMachine(program, host: host);
            vm.Initialize();
            FieldVmSnapshot state = vm.Capture();
            state.Entities[unbound] = state.Entities[unbound] with { ModelBound = false };
            vm = FieldMachine.Restore(program, state, host);
            vm.AdvanceTick();
            Check(false, host.Requests.Any(request => request.Command is FieldActorFace));
            Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        }
    }

    public static void MovieCameraOperandsAndContinuation()
    {
        foreach (byte value in new byte[] { 0, 1, 2, 255 })
        {
            var host = new RecordingHost { Complete = true };
            var vm = New(Code(0, 0xFB, value, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
            vm.Initialize();
            vm.AdvanceTick();
            Check(new FieldControlCommand(FieldControlKind.MovieCameraDisabled, value), host.Requests.Single().Command);
            Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        }
        byte[] truncated = new byte[18];
        truncated[17] = 0xFB;
        var malformed = New(truncated, new RecordingHost());
        malformed.Initialize();
        Throws<FieldExecutionException>(() => malformed.AdvanceTick());
    }

    public static void FixedAnimationMoveOperandsAndContinuation()
    {
        var host = new RecordingHost { Complete = true };
        var vm = New(Code(0xA1, 0, 0, 0xAD, 0x62, 0, 0, 2, 0, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.Variables.WriteWord(FieldBank.MapWord, 0, -448);
        vm.Variables.WriteWord(FieldBank.Persistent0Word, 2, -3056);
        vm.AdvanceTick();
        FieldHostRequest request = host.Requests.Last();
        Check(new FieldActorMove(-448, -3056, Animated: false), request.Command);
        Check(19, vm.Capture().Entities[0].ProgramCounter);
        Check((byte)0, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        FieldVmSnapshot saved = JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(vm.Capture()))!;
        vm = FieldMachine.Restore(Program(Code(0xA1, 0, 0, 0xAD, 0x62, 0, 0, 2, 0, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1)), saved, host);
        vm.AdvanceTick();
        Check(request.Id, host.Requests.Last().Id);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        host = new RecordingHost { Complete = true };
        vm = New(Code(0, 0xAD, 0, 0x40, 0xFE, 0x10, 0xF4, 0x80, 0x50, 8, 9, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(0, host.Requests.Count);
        Check((byte)9, vm.Variables.ReadByte(FieldBank.MapByte, 8));
    }

    public static void ItemOperandsAndContinuation()
    {
        var host = new RecordingHost { Complete = true, Value = -1 };
        var vm = New(Code(0, 0x58, 0x65, 0, 0, 2, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.Variables.WriteWord(FieldBank.MapWord, 0, 0x0201);
        vm.Variables.WriteByte(FieldBank.MapByte, 2, 128);
        vm.Variables.WriteByte(FieldBank.Persistent3Byte, 31, 0xA5);
        vm.AdvanceTick();
        Check(new FieldItemCommand(0x0201), host.Requests.Single().Command);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        Check((byte)0xA5, vm.Variables.ReadByte(FieldBank.Persistent3Byte, 31));
        host = new RecordingHost { Complete = true };
        vm = New(Code(0, 0x58, 0, 0, 0, 1, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(new FieldItemCommand(0x0200), host.Requests.Single().Command);
        host = new RecordingHost { Complete = true };
        vm = New(Code(0, 0x58, 0, 0xFF, 0xFF, 255, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(new FieldItemCommand(ushort.MaxValue), host.Requests.Single().Command);
        byte[] truncated = new byte[20];
        truncated[17] = 0x58;
        vm = New(truncated, host);
        vm.Initialize();
        Throws<FieldExecutionException>(() => vm.AdvanceTick());
    }

    public static void TimerAndSpecialWindowOperands()
    {
        var host = new RecordingHost { Complete = true };
        var vm = New(Code(0, 0x38, 0x55, 0xA5, 0, 1, 2, 0x36, 1, 1, 250, 255, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.Variables.WriteByte(FieldBank.MapByte, 0, 2);
        vm.Variables.WriteByte(FieldBank.MapByte, 1, 250);
        vm.Variables.WriteByte(FieldBank.MapByte, 2, 255);
        vm.AdvanceTick();
        Check(new FieldTimerCommand(22455), host.Requests[0].Command);
        Check(new FieldWindowCommand(FieldWindowOperation.Special, 1, 250, 255, Style: 1), host.Requests[1].Command);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        host = new RecordingHost { Complete = true };
        vm = New(Code(0, 0x38, 0, 0, 0, 10, 0, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(new FieldTimerCommand(600), host.Requests.Single().Command);
        byte[] truncated = new byte[20];
        truncated[17] = 0x38;
        vm = New(truncated, host);
        vm.Initialize();
        Throws<FieldExecutionException>(() => vm.AdvanceTick());
    }

    public static void FadeOperandsAndYield()
    {
        var host = new RecordingHost { Complete = true };
        var vm = New(Code(0, 0x6B, 0x55, 0x05, 0, 1, 2, 16, 2, 0, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.Variables.WriteByte(FieldBank.MapByte, 0, 255);
        vm.Variables.WriteByte(FieldBank.MapByte, 1, 200);
        vm.Variables.WriteByte(FieldBank.MapByte, 2, 201);
        vm.AdvanceTick();
        Check(new FieldFadeCommand(2, 255, 200, 201, 16), host.Requests.Single().Command);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
    }
    public static void MateriaOperandsAndStatus()
    {
        foreach (int result in new[] { -1, 0x12345635 })
        {
            var host = new RecordingHost { Complete = true, Value = result };
            var vm = New(Code(0, 0x5B, 0x55, 0x55, 0, 1, 2, 3, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
            vm.Initialize();
            vm.Variables.WriteByte(FieldBank.MapByte, 0, 0x35);
            vm.Variables.WriteByte(FieldBank.MapByte, 1, 0x56);
            vm.Variables.WriteByte(FieldBank.MapByte, 2, 0x34);
            vm.Variables.WriteByte(FieldBank.MapByte, 3, 0x12);
            vm.AdvanceTick();
            Check(new FieldMateriaCommand(0x12345635), host.Requests.Single().Command);
            Check(result == -1 ? (byte)0 : (byte)1, vm.Variables.ReadByte(FieldBank.Persistent3Byte, 31));
            Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        }
    }
    public static void ViewOffsetOperandsAndYield()
    {
        var host = new RecordingHost { Complete = true };
        var vm = New(Code(0, 0x6A, 0x0F, 0x85, 0xFF, 0xFF, 0xFF, 0, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(new FieldCameraCommand(FieldCameraOperation.ViewOffset, 0, -123), host.Requests.Single().Command);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        host = new RecordingHost { Complete = true };
        vm = New(Code(0, 0x6A, 0x66, 0, 0, 2, 0, 1, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.Variables.WriteWord(FieldBank.MapWord, 0, -300);
        vm.Variables.WriteWord(FieldBank.MapWord, 2, 260);
        vm.AdvanceTick();
        Check(new FieldCameraCommand(FieldCameraOperation.ViewOffset, 1, -300, Steps: 4), host.Requests.Single().Command);
        Check((byte)0, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        vm.AdvanceTick();
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        Check(1, host.Requests.Count);
        host = new RecordingHost { Complete = true };
        vm = New(Code(0xA1, 0, 0, 0x63, 0xF6, 4, 0, 0, 2, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.Variables.WriteWord(FieldBank.MapWord, 4, 300);
        vm.AdvanceTick();
        Check(new FieldCameraCommand(FieldCameraOperation.Entity, 2, Steps: 300, Entity: 0), host.Requests.Last().Command);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
        host = new RecordingHost { Complete = true };
        vm = New(Code(0, 0x63, 0xFF, 0xFF, 0xFF, 0, 2, 0x80, 0x50, 8, 7, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check(0, host.Requests.Count);
        Check((byte)7, vm.Variables.ReadByte(FieldBank.MapByte, 8));
    }

    public static void CoordinateQueryAndRemainder()
    {
        var host = new RecordingHost { Complete = true, Value = (456 << 16) | 65413 };
        var vm = New(Code(0xA1, 0, 0, 0xB8, 0x66, 0, 0, 2, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check((short)-123, vm.Variables.ReadWord(FieldBank.MapWord, 0));
        Check((short)456, vm.Variables.ReadWord(FieldBank.MapWord, 2));
        host.Position = new(-123, 456, -7, 321);
        vm = New(Code(0xA1, 0, 0, 0xC1, 0x66, 0x66, 0, 0, 2, 4, 6, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.AdvanceTick();
        Check((short)-123, vm.Variables.ReadWord(FieldBank.MapWord, 0));
        Check((short)456, vm.Variables.ReadWord(FieldBank.MapWord, 2));
        Check((short)-7, vm.Variables.ReadWord(FieldBank.MapWord, 4));
        Check((short)321, vm.Variables.ReadWord(FieldBank.MapWord, 6));
        int requests = host.Requests.Count;
        vm = New(Code(0, 0xC1, 0x66, 0x66, 0, 0, 2, 4, 6, 0x5F, 0x12, 1), host);
        vm.Initialize();
        vm.Variables.WriteWord(FieldBank.MapWord, 0, 42);
        vm.AdvanceTick();
        Check((short)42, vm.Variables.ReadWord(FieldBank.MapWord, 0));
        Check(requests, host.Requests.Count);
        vm = New(Code(0, 0x80, 0x50, 0, 255, 0x8D, 0x50, 0, 10, 0x81, 0x60, 2, 249, 255, 0x8E, 0x60, 2, 3, 0, 0x5F, 0x12, 1));
        vm.Initialize();
        vm.AdvanceTick();
        Check((byte)5, vm.Variables.ReadByte(FieldBank.MapByte, 0));
        Check((short)-1, vm.Variables.ReadWord(FieldBank.MapWord, 2));
        vm = New(Code(0, 0x8D, 0x50, 0, 0));
        vm.Initialize();
        Throws<FieldExecutionException>(() => vm.AdvanceTick());
    }

    public static void OpeningHostOperands()
    {
        var host = new RecordingHost { Complete = true, Value = 202 };
        byte[] code = Code(0xA1, 0, 0, 0xAB, 0, 15, 2, 0xC0, 0, 0, 255, 255, 2, 0, 7, 0, 17, 0, 0xC2, 0, 0, 255, 255, 2, 0, 253, 255, 7, 0, 3, 4, 128, 2, 0x08, 30, 0x09, 0, 0, 0, 249, 255, 247, 255, 112, 250, 255, 248, 255, 32, 30, 0xC3, 0, 0, 1, 10, 0, 20, 0, 30, 0, 4, 0, 0xC4, 0x28, 5, 13, 0, 99, 0x29, 0xD8, 118, 0, 0xD9, 0xE5, 0, 13, 0, 255, 0xEB, 13, 16, 8, 15, 0x49, 0, 6, 0, 0x99, 5, 7, 0x5F, 0x12, 1);
        var vm = New(code, host);
        vm.Initialize();
        for (int tick = 0; tick < 8; tick++)
        {
            vm.AdvanceTick();
        }
        Check(true, host.Requests.Any(request => request.Command == new FieldActorOffset(10, 20, 30, 1, 4)));
        Check(true, host.Requests.Any(request => request.Command == new FieldActorJump(-1, 2, 7, 17)));
        Check(true, host.Requests.Any(request => request.Command == new FieldActorLadder(-1, 2, -3, 7, 3, 4, 128, 2)));
        Check(true, host.Requests.Any(request => request.Command == new FieldActorFace(0, 15, 2)));
        Check(true, host.Requests.Any(request => request.Command == new FieldPartyJoin(30, 0, 0, 0)));
        Check(true, host.Requests.Any(request => request.Command == new FieldPartySplit(30, 0, 0, 0, -7, -9, 112, -6, -8, 32, new(FieldBank.Immediate, 112), new(FieldBank.Immediate, 32))));
        Check(true, host.Requests.Any(request => request.Command is FieldModelEffectCommand { Operation: 13, Mode: 0, Wait: false } effect && effect.Payload!.SequenceEqual(new byte[] { 0, 99 })));
        Check(true, host.Requests.Any(request => request.Command == new FieldModelEffectCommand(13, Wait: true)));
        Check(true, host.Requests.Any(request => request.Command == new FieldPreloadCommand(118)));
        Check(true, host.Requests.Any(request => request.Command == new FieldPreloadCommand(Wait: true)));
        Check(true, host.Requests.Any(request => request.Command == new FieldPaletteCommand(FieldPaletteOperation.Store, 13, 0, 256)));
        Check(true, host.Requests.Any(request => request.Command == new FieldPaletteCommand(FieldPaletteOperation.Store, 13, 16, 16, First: 8)));
        Check(true, host.Requests.Any(request => request.Command == new FieldMenuCommand(6, 0)));
        Check((byte)202, vm.Variables.ReadByte(FieldBank.MapByte, 7));
    }

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
        public FieldActorPosition? Position { get; set; }
        public List<FieldHostRequest> Requests { get; } = [];
        public List<FieldHostRequest> Cancelled { get; } = [];
        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return new FieldHostReply(Complete, Value, Position);
        }
        public void Cancel(FieldHostRequest request)
        {
            Cancelled.Add(request);
        }
    }
}
