using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWindowNumberTests
{
    public static void Run()
    {
        Ff7Window legacy = JsonSerializer.Deserialize<Ff7Window>("{\"Id\":2,\"X\":8,\"Y\":149,\"Width\":304,\"Height\":74}")!;
        Check(legacy.NativeNumber == 0 && legacy.NativeNumberDigits == 6, "Older windows must recover initial numeric value0 and native digit width6 without replay.");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original window37: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "junonr4");
        Check(field.Scripts.Data.Span.Slice(0x0C7D, 8).SequenceEqual(new byte[] { 0x37, 0x60, 2, 29, 0, 0, 0, 2 }), "The reached original parade number operands changed.");
        Ff7Window configured = legacy with { Style = 1, Flags = 2, SpecialType = 2, SpecialX = 101, SpecialY = 102, Parameters = new Ff7WindowParameters().Set(0, new(FieldBank.MapWord, 29)) };
        Ff7FieldHost host = new(field, (_, _) => 1, new Ff7FieldProcess { Windows = [configured] });
        Check(host.Execute(new(1, 9, 7, 0x0C7D, new FieldWindowCommand(FieldWindowOperation.Number, 2, Flags: 2, Parameter: 123456))).Completed, "37's global number write must complete without an allocated caller model.");
        Ff7Window numbered = host.Process.Windows.Single();
        Check(numbered == (configured with { NativeNumber = 123456, NativeNumberDigits = 2 }), "A number write must preserve the existing geometry, style, special-display and text-parameter owners.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Window numeric state changed after an independent cold restore.");
        host.Execute(new(2, 9, 7, 0, new FieldWindowCommand(FieldWindowOperation.Reset, 2)));
        Ff7Window reset = host.Process.Windows.Single();
        Check(reset.NativeNumber == 123456 && reset.NativeNumberDigits == 6 && reset.SpecialType == 0 && reset.Parameters is null, "Native window reset must restore width6 and ordinary defaults while retaining the32bit number.");
        RunCheckpoint(installation);
        Console.WriteLine("PASS native37 window ownership, numeric persistence, reset, old defaults and cold continuation");
    }

    private static void RunCheckpoint(Ff7Installation installation)
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PARADE_SAVE");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP reached parade number: MEMORIA_FF7_PARADE_SAVE is not configured.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The parade fixture has no FFVII continuation.");
        Check(save.FieldName == "junonr4" && save.Vm.Tick == 0, "The configured parade fixture must retain its reached initializer.");
        using Ff7Session live = Ff7Session.Restore(installation, save);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(save))!);
        for (int callback = 0; callback < 2; callback++)
        {
            live.AdvanceTick(advanceClock: false);
            cold.AdvanceTick(advanceClock: false);
            Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The original parade number callback diverged from its independent cold continuation.");
        }
        Check(live.Machine.Capture().Tick == 2 && live.Host.Process.Windows.Single(window => window.Id == 2).NativeNumberDigits == 2, "The reached original37 did not retain its numeric window width at callback2.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The parade standalone changed during read-only continuation.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
