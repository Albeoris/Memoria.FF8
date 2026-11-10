using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeWindowParameterTests
{
    public static void Run()
    {
        var field = NativeDialogueTests.Field([0xFE, 0xDE, 255]);
        Ff7FieldHost host = new(field, (_, _) => 1);
        FieldHostRequest binding = new(1, 11, 5, 0x682, new FieldWindowCommand(FieldWindowOperation.Parameter, 1, Parameter: 0, ParameterBank: FieldBank.MapWord, ParameterAddress: 12));
        Check(host.Execute(binding).Completed && host.Process.Windows.Single().Parameters!.P0 == new Ff7WindowParameter(FieldBank.MapWord, 12), "The native binding must complete immediately and belong to its selected window/slot.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        Check(cold.Process.Windows.Single().Parameters == host.Process.Windows.Single().Parameters, "Cold window state lost a live parameter source.");
        cold.Execute(new(2, 11, 5, 0x688, new FieldWindowCommand(FieldWindowOperation.SizeWhenFree, 1, 8, 8, 304, 74)));
        Check(cold.Process.Windows.Single().Parameters!.P0.Address == 12, "Window resizing must preserve numeric bindings.");
        cold.Execute(new(3, 11, 5, 0x700, new FieldWindowCommand(FieldWindowOperation.Reset, 1)));
        Check(cold.Process.Windows.Single().Parameters is null, "Native window reset must restore zero/default bindings.");
        Check(Ff7DialogueCursor.FormatNumber(FieldTextTokenKind.DecimalVariable, 65535) == "65535" && Ff7DialogueCursor.FormatNumber(FieldTextTokenKind.HexVariable, 0xA0) == "A0" && Ff7DialogueCursor.FormatNumber(FieldTextTokenKind.RightAlignedVariable, 10) == "   10", "Native decimal, hexadecimal and five-column right-aligned formatting differ.");
        var tokens = FieldText.Read([0xFE, 0xDE, 255]);
        Ff7Window window = new(1, 8, 8, 304, 74);
        Ff7Dialogue dialogue = new(4, 1, 0, null, null, 0, Presentation: new(2, 304, 74));
        int expansions = 0;
        for (int tick = 0; tick < 3; tick++)
        {
            dialogue = Ff7DialogueCursor.AdvanceTick(dialogue, tokens, window, 255, false, expandToken: _ =>
            {
                expansions++;
                return "65535";
            });
        }
        Check(expansions == 1 && dialogue.Presentation!.ExpansionOffset == 1 && dialogue.Numbers!.P0 == "65535", "A numeric token must bind once when first revealed, then expose its native glyph cursor.");
        Ff7Dialogue restored = JsonSerializer.Deserialize<Ff7Dialogue>(JsonSerializer.Serialize(dialogue))!;
        restored = Ff7DialogueCursor.AdvanceTick(restored, tokens, window with { Flags = 2 }, 0, false, expandToken: _ => throw new Exception("A cached numeric token re-read its mutable source."));
        Check(restored.AwaitingInput && restored.Numbers!.P0 == "65535", "Cold numeric reveal must preserve the formatted value through the shared cursor.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
