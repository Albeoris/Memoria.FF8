using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGilTests
{
    public static void Run()
    {
        foreach (var (current, amount, expected) in new[] { (0u, 0u, 0u), (0u, 1500u, 1500u), (9999999u, 1u, 10000000u), (uint.MaxValue - 1, 1u, uint.MaxValue), (uint.MaxValue - 1, 2u, uint.MaxValue), (1u, uint.MaxValue, uint.MaxValue), (uint.MaxValue, uint.MaxValue, uint.MaxValue) })
        {
            Ff7Progress progress = Progress(current);
            Ff7Progress changed = progress.AddGil(amount);
            Equal(expected, changed.Gil);
            Equal(current, progress.Gil);
            Equal(JsonSerializer.Serialize(progress with { Gil = expected }), JsonSerializer.Serialize(changed));
        }
        Ff7Progress owner = Progress(100);
        int applications = 0;
        Ff7FieldHost host = Build();
        host.ExternalCommand = command =>
        {
            applications++;
            owner = owner.AddGil(((FieldGilCommand)command).Amount);
            return new(true);
        };
        var request = new FieldHostRequest(1, 0, 6, 16, new FieldGilCommand(1500));
        Equal(new FieldHostReply(true), host.Execute(request));
        Equal(new FieldHostReply(true), host.Execute(request));
        Equal(1600u, owner.Gil);
        Equal(1, applications);
        Ff7FieldHost cold = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        cold.ExternalCommand = _ => throw new Exception("A saved gil receipt must not execute its side effect twice.");
        Equal(new FieldHostReply(true), cold.Execute(request));
        Equal(1600u, owner.Gil);
        InstalledContinuation();
    }

    private static void InstalledContinuation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original-resource gil continuation: optional fixture inputs are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        string vm = JsonSerializer.Serialize(session.Capture().Vm);
        Ff7Progress before = session.Progress;
        var request = new FieldHostRequest(long.MaxValue - 201, 0, 1, 0, new FieldGilCommand(1500));
        Equal(new FieldHostReply(true), session.Host.Execute(request));
        Ff7Progress expected = before.AddGil(1500);
        Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.Progress));
        session.Host.Execute(request);
        Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.Progress));
        Equal(vm, JsonSerializer.Serialize(session.Capture().Vm));
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        cold.Host.Execute(request);
        Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(cold.Progress));
        Equal(vm, JsonSerializer.Serialize(cold.Capture().Vm));
    }

    private static Ff7Progress Progress(uint gil)
    {
        return new() { InitialKernelData = [1], Characters = [], Party = [255, 255, 255], Inventory = [], Materia = [], Gil = gil };
    }

    private static Ff7FieldHost Build(Ff7FieldProcess? process = null)
    {
        var field = new Ff7FieldResources("gil-fixture", null!, null!, null!, [], [], null!, null!, null!);
        return new(field, (_, _) => 1, process ?? new() { BlinkStateInitialized = true });
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
