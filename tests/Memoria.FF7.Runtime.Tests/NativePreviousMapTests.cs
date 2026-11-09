using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePreviousMapTests
{
    public static void Run()
    {
        var request = new FieldHostRequest(1, 0, 7, 0, new FieldControlCommand(FieldControlKind.PreviousMap, 0));
        Ff7FieldHost boot = Host(Ff7FieldHost.CreateInitialFieldProcess(116));
        var initialField = new Ff7FieldResources("previous-map-fixture", null!, null!, null!, [], [], null!, null!, null!);
        var priorBootstrap = new Ff7FieldHost(initialField, (_, _) => 1);
        Equal(new Ff7FadeProcess(1, 256, 16, 0, 0, 0, false, EntryCurve: true), boot.Process.FadeState);
        Equal<uint?>(null, boot.Process.FadeState!.QuadColorArgb);
        Equal(JsonSerializer.Serialize(priorBootstrap.Capture()), JsonSerializer.Serialize(boot.Capture() with { PreviousMap = null, FadeState = priorBootstrap.Process.FadeState }));
        Equal((byte)0, boot.Process.BlinkRandomIndex);
        Equal(true, boot.Process.BlinkStateInitialized);
        Equal(new FieldHostReply(true, 116), boot.Execute(request));
        var source = new Ff7FieldProcess { PreviousMap = 116, NativeMusicTrack = 2, MusicTicks = 123, PreloadedMap = 169, BlinkStateInitialized = true };
        Ff7FieldHost current = Host(source);
        Ff7FieldProcess next = current.CreateNextFieldProcess(166);
        Equal<ushort?>(166, next.PreviousMap);
        Equal<int?>(2, next.NativeMusicTrack);
        Equal(123L, next.MusicTicks);
        Equal<int?>(169, next.PreloadedMap);
        Equal<ushort?>(116, current.Process.PreviousMap);
        Ff7FieldHost destination = Host(next);
        Equal(new FieldHostReply(true, 166), destination.Execute(request));
        Ff7FieldProcess saved = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(destination.Capture()))!;
        Ff7FieldHost cold = Host(saved);
        Equal<ushort?>(166, cold.Process.PreviousMap);
        Equal(new FieldHostReply(true, 166), cold.Execute(request));
        Equal(new FieldHostReply(true, 65535), Host(source with { PreviousMap = ushort.MaxValue }).Execute(request));
        Equal(new FieldHostReply(true, 0), Host(source with { PreviousMap = 0 }).Execute(request));

        Ff7FieldHost legacy = Host(source with { PreviousMap = null });
        string before = JsonSerializer.Serialize(legacy.Capture());
        try
        {
            legacy.Execute(request);
            throw new Exception("A required missing previous map must not fabricate zero.");
        }
        catch (NotSupportedException)
        {
            Equal(before, JsonSerializer.Serialize(legacy.Capture()));
        }
        Equal(true, legacy.Execute(request with { Id = 2, Command = new FieldControlCommand(FieldControlKind.MapDisplay, 1) }).Completed);
        Equal<ushort?>(null, legacy.Process.PreviousMap);
        Ff7FieldProcess absent = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(legacy.Capture()).Replace(",\"PreviousMap\":null", "", StringComparison.Ordinal))!;
        Equal<ushort?>(null, Host(absent).Process.PreviousMap);
        InstalledContinuation();
    }

    private static void InstalledContinuation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        string? expectedSource = Environment.GetEnvironmentVariable("MEMORIA_FF7_PREVIOUS_MAP");
        string? expectedDestination = Environment.GetEnvironmentVariable("MEMORIA_FF7_CURRENT_MAP");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(path) || !ushort.TryParse(expectedSource, out ushort previousMap) || !ushort.TryParse(expectedDestination, out ushort currentMap))
        {
            Console.WriteLine("SKIP original-resource previous-map continuation: optional fixture inputs are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7SessionSave saved = envelope.Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using (Ff7Session legacy = Ff7Session.Restore(installation, saved with { Process = saved.Process with { PreviousMap = null } }))
        {
            Equal(currentMap, legacy.MapId);
            Equal<ushort?>(null, legacy.Host.Process.PreviousMap);
            Equal(JsonSerializer.Serialize(saved.Vm), JsonSerializer.Serialize(legacy.Capture().Vm));
        }
        Ff7SessionSave prepared = saved with { Process = saved.Process with { PreviousMap = previousMap } };
        using Ff7Session session = Ff7Session.Restore(installation, prepared);
        Equal(currentMap, session.MapId);
        Equal<ushort?>(previousMap, session.Host.Process.PreviousMap);
        string vm = JsonSerializer.Serialize(session.Capture().Vm);
        string progress = JsonSerializer.Serialize(session.Progress);
        var request = new FieldHostRequest(long.MaxValue - 202, 0, 7, 0, new FieldControlCommand(FieldControlKind.PreviousMap, 0));
        Equal(new FieldHostReply(true, previousMap), session.Host.Execute(request));
        Equal(vm, JsonSerializer.Serialize(session.Capture().Vm));
        Equal(progress, JsonSerializer.Serialize(session.Progress));
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        Equal(currentMap, cold.MapId);
        Equal<ushort?>(previousMap, cold.Host.Process.PreviousMap);
        Equal(new FieldHostReply(true, previousMap), cold.Host.Execute(request));
        Equal(vm, JsonSerializer.Serialize(cold.Capture().Vm));
        Equal(progress, JsonSerializer.Serialize(cold.Progress));
    }

    private static Ff7FieldHost Host(Ff7FieldProcess process)
    {
        var field = new Ff7FieldResources("previous-map-fixture", null!, null!, null!, [], [], null!, null!, null!);
        return new(field, (_, _) => 1, process);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
