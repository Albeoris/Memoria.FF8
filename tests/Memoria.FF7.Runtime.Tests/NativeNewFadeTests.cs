using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeNewFadeTests
{
    public static void Run()
    {
        Ff7FieldHost host = Host(new() { NewFadeOrigin = new(0, 0, 0), FadeState = new(4, 0, 1, 0, 0, 0, true, 0x80ABCDEF) });
        host.Execute(Request(1, new FieldFadeCommand(12, 0, 60, 60, 30, NewFade: true)));
        Equal(0, host.Process.FadeState!.Position);
        Equal(0x80ABCDEFu, host.Process.FadeState.FieldColorArgb);
        host.AdvanceActors();
        Equal(0xFFFFFDFDu, host.Process.FadeState!.FieldColorArgb);
        Equal(true, host.Process.FadeState.UsesFieldColor);
        Equal<uint?>(null, host.Process.FadeState.QuadColorArgb);
        Equal(new Ff7FadeColorOrigin(0, 0, 0), host.Process.NewFadeOrigin);
        Equal(host.Process.FadeState, Ff7NativeFade.Advance(Ff7NativeFade.Start(new(12, 0, 60, 60, 30, NewFade: true), new(4, 0, 1, 0, 0, 0, true, 0x80ABCDEF), newFadeOrigin: new(0, 0, 0)), true, new(0, 0, 0)));
        for (int tick = 1; tick < 10; tick++)
        {
            host.AdvanceActors();
        }
        Ff7FieldHost cold = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        for (int tick = 10; tick < 30; tick++)
        {
            host.AdvanceActors();
            cold.AdvanceActors();
            Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(cold.Capture()));
        }
        Equal(12, host.Process.FadeState!.Type);
        Equal(true, host.Process.FadeState.Completed);
        Equal(0xFFFFC3C3u, host.Process.FadeState.FieldColorArgb);
        Equal(new Ff7FadeColorOrigin(0, 60, 60), host.Process.NewFadeOrigin);
        host.Execute(Request(2, new FieldFadeCommand(12, 255, 255, 255, 15, NewFade: true)));
        FieldHostRequest wait = Request(3, new FieldFadeCommand(0, Wait: true));
        Equal(false, host.Execute(wait).Completed);
        host.AdvanceActors();
        Equal(0xFFEEB6B6u, host.Process.FadeState!.FieldColorArgb);
        for (int tick = 1; tick < 14; tick++)
        {
            host.AdvanceActors();
        }
        Equal(false, host.Execute(wait).Completed);
        host.AdvanceActors();
        Equal(true, host.Execute(wait).Completed);
        Equal(0xFF000000u, host.Process.FadeState!.FieldColorArgb);
        Equal(new Ff7FadeColorOrigin(255, 255, 255), host.Process.NewFadeOrigin);
        host.AdvanceActors();
        Equal(15, host.Process.FadeState!.Position);

        Ff7FieldHost next = Host(host.CreateNextFieldProcess() with { CameraShakeX = null, CameraShakeY = null });
        Equal(new Ff7FadeColorOrigin(255, 255, 255), next.Process.NewFadeOrigin);
        next.Execute(Request(4, new FieldFadeCommand(12, 0, 0, 0, 300, NewFade: true)));
        for (int tick = 0; tick < 300; tick++)
        {
            next.AdvanceActors();
        }
        Equal(300, next.Process.FadeState!.Position);
        Equal(true, next.Process.FadeState.Completed);
        Equal(0xFFFFFFFFu, next.Process.FadeState.FieldColorArgb);
        Equal(new Ff7FadeColorOrigin(0, 0, 0), next.Process.NewFadeOrigin);

        Ff7FieldHost interrupted = Host(new() { NewFadeOrigin = new(10, 20, 30) });
        interrupted.Execute(Request(5, new FieldFadeCommand(12, 110, 120, 130, 20, NewFade: true)));
        interrupted.AdvanceActors();
        interrupted.Execute(Request(6, new FieldFadeCommand(12, 30, 40, 50, 10, NewFade: true)));
        interrupted.AdvanceActors();
        Equal(0xFFF3E9DFu, interrupted.Process.FadeState!.FieldColorArgb);
        Equal(new Ff7FadeColorOrigin(10, 20, 30), interrupted.Process.NewFadeOrigin);
        interrupted.Execute(Request(7, new FieldFadeCommand(0)));
        Equal(new Ff7FadeColorOrigin(10, 20, 30), interrupted.Process.NewFadeOrigin);
        interrupted.AdvanceActors();
        Equal(new Ff7FadeColorOrigin(0, 0, 0), interrupted.Process.NewFadeOrigin);
        string beforeFailure = JsonSerializer.Serialize(interrupted.Capture());
        Throws<NotSupportedException>(() => interrupted.Execute(Request(8, new FieldFadeCommand(12, 1, 2, 3, 0, NewFade: true))));
        Equal(beforeFailure, JsonSerializer.Serialize(interrupted.Capture()));
        Ff7FieldHost legacyTerminal = Host(new() { FadeState = new(12, 30, 30, 10, 20, 30, false) });
        Equal(new Ff7FadeColorOrigin(10, 20, 30), legacyTerminal.Process.NewFadeOrigin);
        Equal(30, legacyTerminal.Process.FadeState!.Position);
        Ff7FieldHost legacyUnknownPhase = Host(new() { NewFadeOrigin = new(10, 20, 30) });
        legacyUnknownPhase.AdvanceActors();
        Equal(new Ff7FadeColorOrigin(10, 20, 30), legacyUnknownPhase.Process.NewFadeOrigin);
        Ff7FieldHost negative = Host();
        negative.Execute(Request(9, new FieldFadeCommand(12, 10, 20, 30, -1, NewFade: true)));
        Equal(false, negative.Process.FadeState!.Completed);
        negative.AdvanceActors();
        Equal(-1, negative.Process.FadeState!.Position);
        Equal(true, negative.Process.FadeState.Completed);
        Equal(0xFFF5EBE1u, negative.Process.FadeState.FieldColorArgb);
        Equal(new Ff7FadeColorOrigin(10, 20, 30), negative.Process.NewFadeOrigin);
        negative.Execute(Request(10, new FieldFadeCommand(12, 20, 30, 40, short.MinValue, NewFade: true)));
        negative.AdvanceActors();
        Equal((int)short.MinValue, negative.Process.FadeState!.Position);
        Equal(true, negative.Process.FadeState.Completed);
        Ff7FieldHost maximum = Host(new() { NewFadeOrigin = new(1, 2, 3), FadeState = new(12, 32766, 32767, 10, 20, 30, false) });
        maximum.AdvanceActors();
        Equal(32767, maximum.Process.FadeState!.Position);
        Equal(true, maximum.Process.FadeState.Completed);
        Ff7FieldHost maximumCold = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(maximum.Capture()))!);
        maximum.AdvanceActors();
        maximumCold.AdvanceActors();
        Equal(-32768, maximum.Process.FadeState!.Position);
        Equal(false, maximum.Process.FadeState.Completed);
        Equal(0xFFF5EBE1u, maximum.Process.FadeState.FieldColorArgb);
        Equal(JsonSerializer.Serialize(maximum.Capture()), JsonSerializer.Serialize(maximumCold.Capture()));
        InstalledPartial();
    }

    private static void InstalledPartial()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NEW_FADE_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed new-fade partial continuation: original resources or external fixture are unset.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        Ff7ClockState? clock = session.Progress.Clock;
        Equal(true, session.HasPendingTick);
        session.AdvanceTick();
        Equal(clock, session.Progress.Clock);
        Equal(saved.Vm.Tick + 1, session.Machine.Tick);
        Equal(1, session.Host.Process.FadeState!.Position);
        Equal(0xFFFFFDFDu, session.Host.Process.FadeState.FieldColorArgb);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        for (int tick = 0; tick < 10; tick++)
        {
            session.AdvanceTick();
            cold.AdvanceTick();
            Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(cold.Capture()));
        }
        Equal(true, File.ReadAllBytes(path).SequenceEqual(original));
    }

    private static Ff7FieldHost Host(Ff7FieldProcess? process = null)
    {
        Ff7FieldResources field = new("fade-fixture", null!, null!, null!, [], [], null!, null!, null!);
        return new(field, (_, _) => 1, process);
    }

    private static FieldHostRequest Request(long id, FieldCommand command) => new(id, 0, 7, 16, command);

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new Exception($"Expected {typeof(T).Name}.");
    }
}
