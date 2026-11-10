using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRunInputTests
{
    public static void Run()
    {
        FieldProgram program = new([0, 0x5F, 0x12, 1], [new ushort[32]]);
        FieldMachine machine = new(program);
        machine.Initialize();
        machine.SetInput(0x20, 0x20, 0x80, 0x80);
        machine.SetMovementInput(1, -1, true);
        CheckButtons(machine.Input, 0x60, 0x60, 0, 0xC0, 0xC0, 0, "Initial native run");
        machine.SetInput(0x20, 0, 0x80, 0);
        machine.SetMovementInput(1, -1, true);
        CheckButtons(machine.Input, 0x60, 0, 0, 0xC0, 0, 0, "Repeated native run");
        machine.SetInput(0x60, 0x40, 0xC0, 0x40);
        machine.SetMovementInput(1, -1, false);
        CheckButtons(machine.Input, 0x20, 0, 0x40, 0x80, 0, 0x40, "Host Cancel changes run to walk");
        machine.SetInput(0x60, 0, 0xC0, 0);
        machine.SetMovementInput(1, -1, false);
        CheckButtons(machine.Input, 0x20, 0, 0, 0x80, 0, 0, "Repeated host walk");
        machine.SetInput(0x20, 0, 0x80, 0);
        machine.SetMovementInput(1, -1, true);
        CheckButtons(machine.Input, 0x60, 0x40, 0, 0xC0, 0x40, 0, "Released host Cancel restores native run");
        machine.SetInput(0x40, 0x40);
        machine.SetMovementInput(0, 0, false);
        CheckButtons(machine.Input, 0x40, 0x40, 0x20, 0x40, 0x40, 0x80, "Idle Cancel remains a physical button");
        FieldMachine cold = FieldMachine.Restore(program, machine.Capture());
        Check(JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Native input cold restoration changed button edges.");
        FieldVmSnapshot legacy = machine.Capture() with { Input = machine.Input with { PreviousHeld = null, PreviousRawHeld = null } };
        FieldMachine legacyCold = FieldMachine.Restore(program, legacy);
        Check(JsonSerializer.Serialize(legacy) == JsonSerializer.Serialize(legacyCold.Capture()), "An older input packet without previous-held facts changed native state.");
        legacyCold.SetInput(0x20, 0);
        legacyCold.SetMovementInput(1, 0, true);
        Check((legacyCold.Input.Held & 0x40) != 0 && (legacyCold.Input.Pressed & 0x40) == 0, "The next input packet did not recover its prior held state from an older save.");
        CheckInstalledSession();
    }

    private static void CheckInstalledSession()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_RUN_INPUT_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed input packet ownership: select original resources and an external standalone fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        string before = JsonSerializer.Serialize(session.Capture());
        FieldInputState latched = session.Machine.Input;
        session.SetInput(0x40, 0x40);
        session.MovePlayer(1, -1, true);
        if (session.HasPendingTick)
        {
            Check(session.Machine.Input == latched, "Deferred run input changed the latched native cycle.");
            Check(JsonSerializer.Serialize(session.Capture()) == before, "Deferred run input changed pending native state.");
        }
        else
        {
            Check(session.Machine.Input is { Run: true, Horizontal: 1, Vertical: -1 } && (session.Machine.Input.Held & 0x40) != 0, "Session movement and native run button disagree.");
        }
        using Ff7Session cold = Ff7Session.Restore(installation, session.Capture());
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Installed input packet did not cold-restore exactly.");
        Check(File.ReadAllBytes(path).SequenceEqual(source), "The native input probe changed its source checkpoint.");
    }

    private static void CheckButtons(FieldInputState input, ushort held, ushort pressed, ushort released, ushort rawHeld, ushort rawPressed, ushort rawReleased, string context)
    {
        Check(input.Held == held && input.Pressed == pressed && input.Released == released && input.RawHeld == rawHeld && input.RawPressed == rawPressed && input.RawReleased == rawReleased, context + " produced inconsistent native held/pressed/released buttons.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
