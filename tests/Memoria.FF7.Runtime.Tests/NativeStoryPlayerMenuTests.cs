using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeStoryPlayerMenuTests
{
    internal static void Run()
    {
        Ff7FieldResources field = new("story-player-menu-fixture", null!, null!, null!, [], [], null!, null!, null!);
        FieldProgram program = new([0, 0x49, 0, 9, 0, 0x80, 0x50, 0x12, 0x42, 0], [new ushort[32]]);
        Ff7FieldHost host = new(field, (_, _) => 1, new() { BlinkStateInitialized = true, PlayerLocked = true, MenuDisabled = true });
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.AdvanceTick();
        FieldVmSnapshot opened = machine.Capture();
        Check(host.PendingStoryPlayerMenu is { Mode: 9, Parameter: 0 } && opened.Entities.Single().ProgramCounter == 5 && opened.PendingRequests is { Length: 0 } && machine.Variables.ReadByte(FieldBank.MapByte, 0x12) == 0, "The ordinary MENU must admit while story-locked, advance past the opcode immediately and yield before the next instruction.");

        Ff7FieldProcess process = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!;
        Ff7FieldHost restored = new(field, (_, _) => 1, process);
        FieldMachine resumed = FieldMachine.Restore(program, opened, restored);
        foreach ((Ff7FieldHost currentHost, FieldMachine currentMachine) in new[] { (host, machine), (restored, resumed) })
        {
            Ff7Menu menu = currentHost.PendingStoryPlayerMenu ?? throw new InvalidOperationException("The script-owned player menu was lost on restore.");
            currentHost.AdvanceNativeMenu();
            Check(currentHost.PendingStoryPlayerMenu == menu && currentHost.Process.GlobalRequestId == menu.RequestId, "The ordinary player menu must wait on its presentation owner.");
            currentHost.CompleteMenu(menu.RequestId);
            Check(currentHost.PendingStoryPlayerMenu is null && currentHost.Process.GlobalCommand == Ff7GlobalCommand.None && currentHost.Process.GlobalRequestId is null && currentHost.Process.Menu is { Completed: true }, "Closing the player menu must release its global owner immediately.");
            currentMachine.AdvanceTick();
            Check(currentMachine.Variables.ReadByte(FieldBank.MapByte, 0x12) == 0x42, "The resumed VM must execute the next instruction without polling or reopening the menu.");
        }
        Check(JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(resumed.Capture()), "Warm and restored scripted menu continuations diverged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
