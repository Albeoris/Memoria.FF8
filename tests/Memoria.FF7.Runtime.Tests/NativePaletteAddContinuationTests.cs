using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePaletteAddContinuationTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PALETTE_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed palette-add continuation: configure original resources and a palette checkpoint.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The palette fixture has no FFVII continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(original.Field.Name == "mrkt2" && original.Machine.Tick == 0 && original.Machine.ProgramIdentity == "B1070D7852D99DA97CC4F9164EEE3586381900F3CD4553ED2E4D316D29D318A3", "The selected fixture does not own the reviewed palette initialization boundary.");
        Check(original.Field.Scripts.Data.Span.Slice(0x0878, 11).SequenceEqual(new byte[] { 0xEF, 5, 0x55, 0, 0, 0x10, 1, 0x11, 0x11, 0x11, 0xFE }), "The original palette-add operands differ from the reviewed command.");
        long version = original.Host.Process.PaletteVersion;
        long generation = original.FieldGeneration;
        for (int tick = 0; tick < 4; tick++)
        {
            original.AdvanceTick();
            cold.AdvanceTick();
            Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The original palette initialization changed after a JSON cold restore.");
            Check(original.FieldGeneration == generation, "The bounded palette check unexpectedly left the field.");
        }
        Check(original.Machine.Tick == 4 && original.Host.Process.PaletteVersion > version, "The original palette range and following load did not complete within their native callback.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The palette checkpoint was modified.");
        Console.WriteLine("PASS original palette-add initialization and four cold-equal callbacks");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
