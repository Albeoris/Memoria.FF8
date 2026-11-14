using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBackgroundDepthTests
{
    public static void Run()
    {
        JsonObject legacy = JsonSerializer.SerializeToNode(new Ff7FieldProcess())!.AsObject();
        legacy.Remove(nameof(Ff7FieldProcess.BackgroundLayer2Depth));
        legacy.Remove(nameof(Ff7FieldProcess.BackgroundLayer3Depth));
        Ff7FieldProcess restoredLegacy = legacy.Deserialize<Ff7FieldProcess>()!;
        Check(restoredLegacy.BackgroundLayer2Depth == 4095 && restoredLegacy.BackgroundLayer3Depth == 1, "Older process saves must recover native background depth defaults without a compatibility marker.");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original background depth host: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "ujunon2");
        Check(field.Scripts.Data.Span.Slice(0x0509, 5).SequenceEqual(new byte[] { 0x2C, 0, 3, 0xFE, 0x0F }), "The selected original beach no longer matches the reached depth command.");
        Ff7FieldHost host = new(field, (_, _) => 1);
        Check(host.Process.BackgroundLayer2Depth == 4095 && host.Process.BackgroundLayer3Depth == 1, "New fields must begin with the two native depth words.");
        ushort[] parameters = host.Process.BackgroundStates.ToArray();
        long id = 1;
        foreach ((int layer, int depth) in new[] { (3, 4094), (2, 0), (2, 65535), (3, 65535) })
        {
            FieldHostRequest request = new(id++, 1, 7, 0x0509, new FieldBackgroundCommand(FieldBackgroundOperation.Depth, layer, depth));
            Check(host.Execute(request).Completed, "Native background depth publication must complete immediately.");
            Check((layer == 2 ? host.Process.BackgroundLayer2Depth : host.Process.BackgroundLayer3Depth) == depth, "The host truncated or changed the selected depth word.");
            Check(host.Process.BackgroundStates.SequenceEqual(parameters), "Depth publication changed background animation parameter bits.");
            string saved = JsonSerializer.Serialize(host.Capture());
            Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(saved)!);
            Check(cold.Execute(request).Completed && saved == JsonSerializer.Serialize(cold.Capture()), "A cold completed depth receipt was reapplied or changed process state.");
        }
        Ff7FieldProcess next = host.CreateNextFieldProcess(428);
        Check(next.BackgroundLayer2Depth == 4095 && next.BackgroundLayer3Depth == 1, "Depth overrides must reset at the native new-field boundary.");
        Console.WriteLine("PASS original beach BGPDH operands, independent depth words, receipt/cold ownership, old saves and field reset");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
