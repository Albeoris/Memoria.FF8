using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeStablesRadiusTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_STABLES_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original stables initialization: select original resources and a standalone before its gateway.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("The fixture requires FFVII state.");
        Check(saved.FieldName == "farm" && saved.Battle is null && saved.World is null, "The fixture must precede the ordinary farm-to-stables gateway.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session first = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Ff7AutoplayDriver driver = new(first);
        Ff7AutoplayDriver coldDriver = new(cold);
        for (int callback = 0; callback < 300 && first.Field.Name == "farm"; callback++)
        {
            Ff7AutoplayInput input = driver.Gateway(345);
            Check(input == coldDriver.Gateway(345), "Independent cold gateway planning changed ordinary controls.");
            driver.Apply(input);
            coldDriver.Apply(input);
            first.AdvanceTick();
            cold.AdvanceTick();
        }
        Check(first.Field.Name == "frcyo" && cold.Field.Name == "frcyo", "Ordinary native gateway controls did not initialize the stables.");
        Ff7Actor actor = first.Host.GetActor(7);
        Check(actor.TalkRadius == 300, "Original frcyo D6 did not publish its word-width talk radius to the initialized actor.");
        Check(JsonSerializer.Serialize(first.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold original stables entry differs after the initializer.");
        using Ff7Session restored = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(first.Capture()))!);
        first.SetInput(0, 0);
        first.MovePlayer(0, 0, true);
        restored.SetInput(0, 0);
        restored.MovePlayer(0, 0, true);
        first.AdvanceTick();
        restored.AdvanceTick();
        Check(JsonSerializer.Serialize(first.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Stables word-radius state changed on its first cold neutral continuation.");
        Check(SHA256.HashData(source).SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Original gateway fixture changed during validation.");
        Console.WriteLine("PASS original farm-to-stables initialization, entity7 talk radius300 and exact cold continuation; source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
