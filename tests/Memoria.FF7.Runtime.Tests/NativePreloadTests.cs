using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePreloadTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original-resource preload request: optional fixture inputs are unset.");
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
        string progress = JsonSerializer.Serialize(session.Progress);
        Ff7FieldResources field = session.Field;
        long tick = session.Host.Process.Tick;
        FieldHostRequest request = new(long.MaxValue - 500, 0, 1, 0, new FieldPreloadCommand(ushort.MaxValue));
        Equal(new FieldHostReply(true), session.Host.Execute(request));
        Equal((ushort?)ushort.MaxValue, session.Host.Process.PreloadedMap);
        Equal(true, ReferenceEquals(field, session.Field));
        Equal(tick, session.Host.Process.Tick);
        Equal(vm, JsonSerializer.Serialize(session.Capture().Vm));
        Equal(progress, JsonSerializer.Serialize(session.Progress));
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        Equal((ushort?)ushort.MaxValue, cold.Host.Process.PreloadedMap);
        Equal(field.Name, cold.Field.Name);
        Equal(vm, JsonSerializer.Serialize(cold.Capture().Vm));
        Equal(new FieldHostReply(true), cold.Host.Execute(request));
        Equal(JsonSerializer.Serialize(session.Progress), JsonSerializer.Serialize(cold.Progress));
        bool unsupported = false;
        try
        {
            cold.Host.Execute(request with { Id = request.Id - 1, Command = new FieldPreloadCommand(Wait: true) });
        }
        catch (NotSupportedException)
        {
            unsupported = true;
        }
        Equal(true, unsupported);
        Equal((ushort?)ushort.MaxValue, cold.Host.Process.PreloadedMap);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
