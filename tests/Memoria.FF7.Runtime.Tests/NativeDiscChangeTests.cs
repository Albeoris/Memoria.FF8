using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeDiscChangeTests
{
    internal static void Run()
    {
        foreach (byte disc in new byte[] { 0, 2, 255 })
        {
            CheckNativeHandshake(disc);
        }
        CheckBusyGlobal();
        CheckRequestIdentity();
        Check(JsonSerializer.Deserialize<Ff7FieldProcess>("{}")!.DiscChange is null, "An older process without disc-change metadata became incompatible.");
        Console.WriteLine("Native raw disc byte, admission-only write, asynchronous acknowledgment, cold state and request identity checks passed.");
    }

    private static void CheckNativeHandshake(byte disc)
    {
        Ff7FieldHost host = Host();
        bool available = false;
        List<byte> probes = [];
        host.NativeDiscMediaAvailable = value =>
        {
            probes.Add(value);
            return available;
        };
        var (program, machine) = Machine(disc, host);
        machine.AdvanceTick();
        FieldVmSnapshot first = machine.Capture();
        FieldHostRequest request = first.PendingRequests!.Single();
        Check(first.Entities[0].ProgramCounter == 1 && request is { Entity: 0, Priority: 7, ProgramCounter: 1, Command: FieldDiscChangeCommand command } && command.Disc == disc, "The initial native disc wait lost its raw byte or exact VM owner.");
        Check(host.Process.DiscChange == new Ff7DiscChange(request.Id, disc) && host.Process.GlobalCommand == Ff7GlobalCommand.DiscChange && machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0) == disc, "First actual admission failed to retain native status and publish the existing disc byte.");
        Check(machine.Variables.ReadByte(FieldBank.MapByte, 125) == 0, "Initial disc admission ran its continuation before native acknowledgment.");
        machine.Variables.WriteByte(FieldBank.Persistent3Byte, 0, 91);
        machine.AdvanceTick();
        Check(machine.Capture().PendingRequests!.Single() == request && machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0) == 91, "Repeated polling replaced its owner or rewrote the admission-only native byte.");
        host.AdvanceNativeDiscChange();
        Check(host.Process.DiscChange is { Completed: false } && probes.SequenceEqual(new[] { disc }), "Unavailable media completed a native disc wait or normalized its raw disc operand.");
        available = true;
        host.AdvanceNativeDiscChange();
        Check(host.Process.DiscChange is { Completed: true } && machine.Capture().Entities[0].ProgramCounter == 1, "Backend acknowledgment directly advanced native script execution.");
        machine.AdvanceTick();
        Check(host.Process.DiscChange is null && host.Process.GlobalCommand == Ff7GlobalCommand.None && host.Process.GlobalRequestId is null, "The acknowledged native request did not release its global owner.");
        Check(machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0) == 91 && machine.Variables.ReadByte(FieldBank.MapByte, 125) == 77 && (machine.Capture().PendingRequests ?? []).Length == 0, "The next VM poll rewrote the disc byte or failed to execute its continuation in the same callback.");

        Ff7FieldHost pendingHost = Host();
        var (_, pending) = Machine(disc, pendingHost);
        pending.AdvanceTick();
        FieldVmSnapshot savedVm = pending.Capture();
        Ff7FieldProcess savedProcess = pendingHost.Capture();
        string before = JsonSerializer.Serialize(new { savedVm, savedProcess });
        Ff7FieldProcess coldProcess = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(savedProcess))!;
        Ff7FieldHost coldHost = Host(coldProcess);
        coldHost.NativeDiscMediaAvailable = _ => true;
        FieldMachine cold = FieldMachine.Restore(program, savedVm, coldHost);
        coldHost.AdvanceNativeDiscChange();
        cold.AdvanceTick();
        Check(coldHost.Process.DiscChange is null && cold.Variables.ReadByte(FieldBank.MapByte, 125) == 77, "Cold pending disc state failed to resume the same native request.");
        Check(before == JsonSerializer.Serialize(new { savedVm, savedProcess }), "Cold continuation altered its source native snapshot.");
    }

    private static void CheckBusyGlobal()
    {
        Ff7FieldHost host = Host(new() { GlobalCommand = Ff7GlobalCommand.Transition, GlobalRequestId = 99, Transition = new(2, 0, 0, 0, 0) });
        var (_, machine) = Machine(2, host);
        machine.Variables.WriteByte(FieldBank.Persistent3Byte, 0, 91);
        machine.AdvanceTick();
        Check(host.Process.DiscChange is null && host.Process.GlobalCommand == Ff7GlobalCommand.Transition && host.Process.GlobalRequestId == 99 && machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0) == 91 && machine.Capture().Entities[0].ProgramCounter == 1, "A busy unrelated global operation admitted disc change or published its byte.");
    }

    private static void CheckRequestIdentity()
    {
        Ff7FieldHost host = Host();
        FieldHostRequest request = new(1, 0, 6, 10, new FieldDiscChangeCommand(2));
        Check(host.Execute(request) == new FieldHostReply(false, 1), "An actual first host admission did not expose its one-time write signal.");
        Check(host.Execute(request) == new FieldHostReply(false), "A repeated host poll exposed another write signal.");
        ExpectInvalid(() => host.Execute(request with { Entity = 1 }));
        ExpectInvalid(() => host.Execute(request with { Command = new FieldDiscChangeCommand(3) }));
        Ff7FieldProcess mismatched = host.Capture() with { GlobalRequestId = 2 };
        ExpectInvalid(mismatched.Validate);
    }

    private static Ff7FieldHost Host(Ff7FieldProcess? process = null)
    {
        Ff7FieldResources field = new("disc-fixture", null!, null!, null!, [], [], null!, null!, null!);
        return new(field, (_, _) => 1, process);
    }

    private static (FieldProgram Program, FieldMachine Machine) Machine(byte disc, IFieldHost host)
    {
        FieldProgram program = new([0, 0x0E, disc, 0x80, 0x50, 125, 77, 0x24, 1, 0, 0], [new ushort[32]]);
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        return (program, machine);
    }

    private static void ExpectInvalid(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("A changed native disc request or global owner was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
