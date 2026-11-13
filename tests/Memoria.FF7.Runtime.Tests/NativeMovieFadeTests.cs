using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMovieFadeTests
{
    public static void Run()
    {
        Ff7FadeProcess visible = new(0, 0, 1, 0, 0, 0, false);
        Ff7FadeProcess prepared = Ff7NativeFade.Start(new(4, 0, 0, 0, 1), visible);
        Check(prepared.Completed && !prepared.HidesField, "FADE4 must complete its command without running the presentation callback early.");
        Ff7FadeProcess hidden = Ff7NativeFade.Advance(prepared);
        Check(hidden.HidesField && hidden.QuadColorArgb is null && hidden.FieldColorArgb == visible.FieldColorArgb, "FADE4 hides geometry independently of tint or an alpha quad.");
        Ff7FadeProcess cold = JsonSerializer.Deserialize<Ff7FadeProcess>(JsonSerializer.Serialize(hidden))!;
        for (int tick = 0; tick < 90; tick++)
        {
            hidden = Ff7NativeFade.Advance(hidden);
            cold = Ff7NativeFade.Advance(cold);
            Check(hidden == cold && hidden.HidesField, "The authored postmovie wait exposed the field or diverged after cold restore.");
        }
        foreach (int type in new[] { 0, 1, 2, 5, 6, 9, 10, 12 })
        {
            FieldFadeCommand command = new(type, 0, 0, 0, 1, NewFade: type == 12);
            Ff7FadeProcess next = Ff7NativeFade.Start(command, hidden, newFadeOrigin: new(0, 0, 0));
            Check(next.HidesField, "An authored fade command cleared callback-owned field hiding early.");
            next = Ff7NativeFade.Advance(next, newFadeOrigin: new(0, 0, 0));
            Check(!next.HidesField, $"Fade callback {type} did not restore field drawing.");
        }
        Ff7FadeProcess legacy = JsonSerializer.Deserialize<Ff7FadeProcess>("{\"Type\":4,\"Position\":0,\"Speed\":1,\"Red\":0,\"Green\":0,\"Blue\":0,\"UsesFieldColor\":false}")!;
        Check(legacy.HidesField, "An old completed FADE4 must recover its native hidden presentation without script replay.");
        Check(!Ff7FieldHost.CreateInitialFieldProcess(138).FadeState!.HidesField, "Field initialization must reset field hiding.");
        RunInstalledTrainTransition();
    }

    private static void RunInstalledTrainTransition()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed train movie transition: original resources are unset.");
            return;
        }
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root), "md8brdg");
        ReadOnlySpan<byte> original = field.Scripts.Data.Span;
        int start = original.IndexOf(new byte[] { 0xF8, 23, 0xF9 });
        Check(start >= 0, "The selected field has no train movie request.");
        int transition = original[start..].IndexOf(new byte[] { 0x60, 138, 0 });
        Check(transition > 0, "The train movie has no expected destination transition.");
        byte[] code = [0, .. original.Slice(start, transition + 10), 0];
        FieldProgram program = new(code, [new ushort[32]]);
        Ff7FieldHost host = new(field, (_, _) => 1);
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        for (int tick = 0; tick < 4 && host.Process.Movie is not { Playing: true }; tick++)
        {
            machine.AdvanceTick();
        }
        Check(host.Process.Movie is { PreparedId: 23, Playing: true }, "The original train movie did not acquire its waiting script.");
        host.PublishMovieFrame(0, true);
        machine.AdvanceTick();
        host.AdvanceActors();
        Check(host.Process.FadeState is { Type: 4, HidesField: true }, "The movie's first resumed callback did not hide the bridge.");
        Check(machine.Capture().Entities[0].WaitCounter == 90, "The original postmovie wait was not reached in the completion callback.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        FieldMachine coldMachine = FieldMachine.Restore(program, machine.Capture(), cold);
        for (int tick = 0; tick < 92 && host.Process.Transition is null; tick++)
        {
            machine.AdvanceTick();
            host.AdvanceActors();
            coldMachine.AdvanceTick();
            cold.AdvanceActors();
            Check(host.Process.FadeState!.HidesField && cold.Process.FadeState!.HidesField, "The bridge reappeared during the original wait.");
            Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The postmovie process diverged after cold restore.");
        }
        Check(host.Process.Transition?.Map == 138 && cold.Process.Transition?.Map == 138, "The hidden bridge did not reach the original cargo-car transition.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
