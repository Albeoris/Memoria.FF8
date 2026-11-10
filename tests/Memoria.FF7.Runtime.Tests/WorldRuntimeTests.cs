using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldRuntimeTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed world execution: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        byte[] persistent = new byte[1280];
        persistent[0] = 152;
        using Ff7WorldRuntime original = new(installation, persistent, 1, new byte[] { 0, 1, 2 });
        Check(original.Process.Tick == 0 && original.Process.Entities.Length == 0, "Construction must not silently execute the destination.");
        original.Advance();
        Ff7WorldEntity cloud = original.Process.Entities.Single(value => value.Model == 0);
        Check(cloud.X == 185636 && cloud.Z == 123325 && cloud.Direction == 0 && original.ControlledModel == 0, "Original entry must author the outside-Midgar placement through world EV.");
        Check(original.Process.Vm.Models.Length == 8 && original.Process.Lights.Length == 13 && !original.MovementEnabled, "Original system/onload must create native model/light owners and retain tutorial input lock.");
        for (int i = 0; i < 29; i++)
        {
            original.Advance();
        }
        Check(original.Process.Dialogue is { Text: 53, Completed: false } && original.Process.Window is { X: 40, Y: 5, Width: 219, Height: 73 }, "Original model onload must reach its tutorial after the native 30-step counter.");
        Ff7WorldProcess saved = JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(original.Process))!;
        using Ff7WorldRuntime restored = new(installation, original.Machine.CopyPersistent(), 1, new byte[] { 0, 1, 2 });
        restored.Restore(saved);
        Check(JsonSerializer.Serialize(original.Process) == JsonSerializer.Serialize(restored.Process), "Cold world restore must preserve native and presentation owners without executing scripts.");
        bool confirmed = false;
        for (int i = 0; i < 600 && !original.MovementEnabled; i++)
        {
            if (original.Process.Dialogue is { Completed: false, AwaitingInput: true } dialogue)
            {
                original.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
                restored.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
                confirmed = true;
            }
            original.Advance();
            restored.Advance();
            Check(JsonSerializer.Serialize(original.Process) == JsonSerializer.Serialize(restored.Process), "Cold world continuation must preserve native script/window timing and input.");
        }
        Check(confirmed && original.MovementEnabled && original.Process.Dialogue is { Completed: true }, "Only native tutorial completion may release world controls.");
        bool rejected = false;
        try
        {
            restored.Restore(saved with { Entities = saved.Entities[..^1] });
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected, "World restore must reject differing entity/script ownership.");
        Console.WriteLine("PASS installed original world system/onload/on_tick, source placement, tutorial, cold continuation and corruption");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

