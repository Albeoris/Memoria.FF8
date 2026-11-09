using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCheckpointTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CHECKPOINT_FILE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP standalone checkpoint: optional resources are unset.");
            return;
        }
        using FileStream stream = File.OpenRead(path);
        Ff7SessionSave save = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("The configured checkpoint has no FFVII continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        string progress = JsonSerializer.Serialize(save.Progress);
        string vm = JsonSerializer.Serialize(save.Vm);
        using Ff7Session original = Ff7Session.Restore(installation, save);
        Ff7SessionSave restored = original.Capture();
        Check(progress == JsonSerializer.Serialize(restored.Progress), "Restoring the standalone checkpoint changed progress, rewards or RNG.");
        Check(vm == JsonSerializer.Serialize(restored.Vm), "Restoring the standalone checkpoint changed script PCs, banks or input.");
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(restored))!);
        for (int tick = 0; tick < 30; tick++)
        {
            original.AdvanceTick();
            cold.AdvanceTick();
            Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The standalone checkpoint cold continuation diverged.");
        }
        Console.WriteLine($"standalone field={original.Field.Name} tick={original.Machine.Tick} save={original.CanSave}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
