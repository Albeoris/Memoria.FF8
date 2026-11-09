using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeEntryFadeTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed entry fade: original resources are unset.");
            return;
        }
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(Ff7Installation.Discover(root));
        Ff7FieldHost prior = Host(new() { NewFadeOrigin = new(0, 60, 60), FadeState = new(12, 30, 30, 0, 60, 60, false, 0xFFFFC3C3) }, executable);
        Ff7FieldHost entry = Host(prior.CreateNextFieldProcess(132), executable);
        Equal(new Ff7FadeColorOrigin(0, 60, 60), entry.Process.NewFadeOrigin);
        Equal(1, entry.Process.FadeState!.Type);
        Equal(256, entry.Process.FadeState.Position);
        Equal(16, entry.Process.FadeState.Speed);
        Equal(true, entry.Process.FadeState.EntryCurve);
        Equal(0x80FFFFFFu, entry.Process.FadeState.FieldColorArgb);
        Equal<uint?>(null, entry.Process.FadeState.QuadColorArgb);
        entry.AdvanceActors();
        Equal(240, entry.Process.FadeState!.Position);
        Equal((uint?)0xFE000000u, entry.Process.FadeState.QuadColorArgb);
        entry.AdvanceActors();
        Equal(224, entry.Process.FadeState!.Position);
        Equal((uint?)0xFE000000u, entry.Process.FadeState.QuadColorArgb);
        for (int callback = 2; callback < 8; callback++)
        {
            entry.AdvanceActors();
        }
        Equal(128, entry.Process.FadeState!.Position);
        Equal((uint?)0xA8000000u, entry.Process.FadeState.QuadColorArgb);
        Ff7FieldHost cold = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(entry.Capture()))!, executable);
        for (int callback = 8; callback < 16; callback++)
        {
            entry.AdvanceActors();
            cold.AdvanceActors();
            Equal(JsonSerializer.Serialize(entry.Capture()), JsonSerializer.Serialize(cold.Capture()));
        }
        Equal(0, entry.Process.FadeState!.Type);
        Equal(true, entry.Process.FadeState.EntryCurve);
        Equal((uint?)0, entry.Process.FadeState.QuadColorArgb);
        Equal(new Ff7FadeColorOrigin(0, 60, 60), entry.Process.NewFadeOrigin);
        entry.AdvanceActors();
        Equal(false, entry.Process.FadeState!.EntryCurve);
        Equal<uint?>(null, entry.Process.FadeState.QuadColorArgb);
        Equal(new Ff7FadeColorOrigin(0, 0, 0), entry.Process.NewFadeOrigin);

        Ff7FieldProcess initial = Ff7FieldHost.CreateInitialFieldProcess(116);
        Equal(256, initial.FadeState!.Position);
        Equal(true, initial.FadeState.EntryCurve);
        Equal<uint?>(null, initial.FadeState.QuadColorArgb);
        Equal(new Ff7FadeColorOrigin(0, 0, 0), initial.NewFadeOrigin);
        Ff7FadeProcess seeded = initial.FadeState;
        foreach (int type in new[] { 0, 1, 2, 4, 5, 6, 9, 10, 12 })
        {
            FieldFadeCommand command = new(type, 0, 0, 0, type == 12 ? 30 : 2, type is 1 or 5 or 9 ? 255 : 0, NewFade: type == 12);
            Ff7FadeProcess started = Ff7NativeFade.Start(command, seeded, newFadeOrigin: new(0, 0, 0));
            Equal(true, started.EntryCurve);
            Ff7FadeProcess advanced = Ff7NativeFade.Advance(started, newFadeOrigin: new(0, 0, 0), entryAlpha: executable.NativeFieldEntryFadeAlpha);
            Equal(type == 1, advanced.EntryCurve);
        }
        Ff7FadeProcess movieTerminal = Ff7NativeFade.Advance(seeded, true, entryAlpha: executable.NativeFieldEntryFadeAlpha);
        Equal(0, movieTerminal.Type);
        Equal(true, movieTerminal.EntryCurve);
        Equal((uint?)0, movieTerminal.QuadColorArgb);
        Ff7FadeProcess colored = Ff7NativeFade.Start(new(1, 100, 80, 60, 16, 255), seeded);
        Equal(true, colored.UsesFieldColor);
        colored = Ff7NativeFade.Advance(colored, entryAlpha: executable.NativeFieldEntryFadeAlpha);
        Equal((uint?)0xFE000000u, colored.QuadColorArgb);
        Equal(0x80FFFFFFu, colored.FieldColorArgb);
        Equal(true, colored.UsesFieldColor);
        Ff7FadeProcess white = Ff7NativeFade.Advance(Ff7NativeFade.Start(new(1, 255, 255, 255, 16, 255), seeded), entryAlpha: executable.NativeFieldEntryFadeAlpha);
        Equal((uint?)0xFE000000u, white.QuadColorArgb);
        Equal(0x80FFFFFFu, white.FieldColorArgb);
        Equal(false, white.UsesFieldColor);
        Ff7FadeProcess black = Ff7NativeFade.Advance(Ff7NativeFade.Start(new(1, 0, 0, 0, 16, 255), seeded with { UsesFieldColor = true, FieldColorArgb = 0x80ABCDEF }), entryAlpha: executable.NativeFieldEntryFadeAlpha);
        Equal((uint?)0xFE000000u, black.QuadColorArgb);
        Equal(0x80ABCDEFu, black.FieldColorArgb);
        Equal(false, black.UsesFieldColor);
        Ff7FadeProcess legacy = JsonSerializer.Deserialize<Ff7FadeProcess>("{\"Type\":1,\"Position\":128,\"Speed\":16,\"Red\":0,\"Green\":0,\"Blue\":0,\"UsesFieldColor\":false}")!;
        Equal(false, legacy.EntryCurve);
        Equal((uint?)0x70000000u, Ff7NativeFade.Advance(legacy).QuadColorArgb);
    }

    private static Ff7FieldHost Host(Ff7FieldProcess process, Ff7PcExecutableData executable)
    {
        PcWalkmeshTriangle triangle = new(0, Vector3.Zero, new(100, 0, 0), new(0, 100, 0), -1, -1, -1);
        Ff7FieldResources field = new("entry-fade-fixture", null!, null!, null!, [], [triangle], null!, null!, null!);
        return new(field, (_, _) => 1, process)
        {
            NativeFieldEntryFadeAlpha = executable.NativeFieldEntryFadeAlpha,
            CameraShakeRandomTable = executable.NativeCameraShakeRandomTable,
            Movement = new([triangle], executable.NativeSinCos, executable.NativeArcAngles)
        };
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
