using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldDialogueTests
{
    public static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (game is null)
        {
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        using Ff7WorldRuntime world = new(installation, new byte[1280], 1, new byte[] { 0, 1, 2 });
        using Ff7WorldRuntime cold = new(installation, new byte[1280], 1, new byte[] { 0, 1, 2 });
        Ff7WorldProcess initial = world.Process;
        Check(initial.Window is null && initial.Dialogue is null, "An unused world window must retain its absent optional presentation metadata.");
        cold.Restore(JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(initial))!);
        Check(cold.Process.Window is null && cold.Process.Dialogue is null, "Restoring an older unused world window must not eagerly change its presentation capture.");
        string execution = JsonSerializer.Serialize(initial.Vm);
        Apply(world, WorldOperation.WindowAttributes, 0x1FF, 0x18001);
        Check(world.Process.Window is { Id: 0, X: 8, Y: 149, Width: 304, Height: 73, Style: -1, Flags: -32767 }, "Native attributes before geometry must materialize the reset world window and retain signed byte/word operands.");
        Check(world.Process.Dialogue is null && JsonSerializer.Serialize(world.Process.Vm) == execution, "Setting native window attributes started dialogue or replayed execution.");
        cold.Restore(JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(world.Process))!);
        foreach (Ff7WorldRuntime runtime in new[] { world, cold })
        {
            Apply(runtime, WorldOperation.Window, 1, 2, 100, 50);
        }
        Check(world.Process.Window is { X: 8, Y: 8, Width: 100, Height: 50, Style: -1, Flags: -32767 }, "Native geometry configuration lost previously assigned world window attributes.");
        Check(world.Process.Window == cold.Process.Window, "Cold restoration changed attributes-before-geometry continuation.");
        world.Restore(initial);
        Apply(world, WorldOperation.Text, 0);
        Check(world.Process.Window is { X: 8, Y: 149, Width: 304, Height: 73, Style: 0, Flags: 0 } && world.Process.Dialogue is { Text: 0, Completed: false }, "Native text without explicit geometry failed to use the reset world window.");
        Check(JsonSerializer.Serialize(world.Process.Vm) == execution, "Opening the native reset window replayed world script execution.");
        Console.WriteLine("PASS native world attributes/text before geometry, operand widths and cold presentation continuation.");
    }

    private static void Apply(Ff7WorldRuntime world, WorldOperation operation, params int[] values)
    {
        world.Execute(new(world.Process.Vm.NextRequestId, 0, 0, 0, new(operation, values)));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
