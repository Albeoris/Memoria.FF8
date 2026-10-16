using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeTerminalCommandsContract()
    {
        EventEngine Scroll(int layer, int rawDuration)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session) { CanSetBackgroundScrollPosition = (_, _, _) => true, CanSetLinearBackgroundScroll = _ => true, CanRestoreLinearBackgroundScroll = _ => true };
            engine.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.DSCROLL2, 0), (Jsm.Opcode.PSHN_L, layer), (Jsm.Opcode.PSHN_L, 65548), (Jsm.Opcode.PSHN_L, -65560), (Jsm.Opcode.PSHN_L, rawDuration), (Jsm.Opcode.LSCROLL2, 0), (Jsm.Opcode.PSHN_L, layer), (Jsm.Opcode.SCROLLSYNC2, 0), (Jsm.Opcode.RET, 0)), []);
            engine.Scene.BackgroundScrollPositions[1] = new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 3, 2);
            if (layer == 0)
            {
                engine.ResolveNativeNormalBackgroundScrollCenter = () => ((short)40, (short)-80);
            }
            engine.Step();
            return engine;
        }

        EventEngine scroll = Scroll(1, 65540);
        BackgroundScrollPosition? issued = null;
        scroll.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.LSCROLL2))
            {
                issued = scroll.Scene.BackgroundScrollPositions[1];
            }
        };
        scroll.Step();
        Require(issued is { X: 10, Y: 20, TargetX: 12, TargetY: -24, Duration: 4, Status: 0, Mode: 4 }, "LSCROLL2 overwrote current history or lost signed lowWORD operands.");
        EventEngine restored = Scroll(1, 65540);
        restored.Restore(scroll.Capture());
        for (int tick = 0; tick < 5; tick++)
        {
            scroll.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(scroll.Capture(), restored.Capture()), "LSCROLL2 phase/synchronization changed after independent restore.");
        }
        Require(scroll.Scene.BackgroundScrollPositions[1] is { X: 12, Y: -24, Counter: 4, Status: 2 }, "LSCROLL2 failed its original inclusive endpoint.");
        EventEngine zero = Scroll(1, 0);
        zero.Step();
        Require(zero.Scene.BackgroundScrollPositions[1] is { X: 10, Y: 20, Status: 2, Counter: 0 }, "Zero-duration linear scroll must retain the previous center.");
        foreach (EventEngine invalid in new[] { Scroll(65537, 4), Scroll(1, -1) })
        {
            try
            {
                invalid.Step();
                throw new InvalidOperationException("LSCROLL2 accepted narrowed layer or negative duration.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement fiber = invalid.Capture().GetProperty("Fibers")[0];
            Require(invalid.LastFault?.Opcode == "LSCROLL2" && fiber.GetProperty("Position").GetInt32() == 9 && fiber.GetProperty("Stack").GetArrayLength() == 4, "LSCROLL2 refusal consumed operands or advanced PC.");
        }

        EventEngine layerZero = Scroll(0, 4);
        layerZero.Step();
        Require(layerZero.Scene.BackgroundScrollPositions[0] is { X: 40, Y: -80, PreviousX: 40, PreviousY: -80 }, "Layer0 linear scroll lost its authentic center because another layer record existed.");

        GameSession menuSession = new(new GameState());
        EventEngine menu = new(menuSession);
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.MENUTUTO, 0), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0));
        menu.LoadField(600, NativeTutorialOrdinaryProgram(content), []);
        menu.Step();
        menu.Step();
        Require(menu.Scene.NativeTutorial is { RawArgument: 0, MenuId: 26, TutorialId: null } && menu.Scene.MinimalMenuCompletions.Count == 0 && !menuSession.State.Variables.ContainsKey(5000), "MENUTUTO changed its zero-operand request or immediate return3 yield.");
        GameSession menuRestoredSession = new(new GameState());
        EventEngine menuRestored = new(menuRestoredSession);
        menuRestored.LoadField(600, NativeTutorialOrdinaryProgram(content), []);
        menuRestored.Restore(menu.Capture());
        menu.CompleteNativeTutorial();
        menuRestored.CompleteNativeTutorial();
        Require(menuSession.State.Variables[5000] == 77 && JsonElement.DeepEquals(menu.Capture(), menuRestored.Capture()), "MENUTUTO consumed an operand or lost its exact next-update continuation.");
        Console.WriteLine("Terminal commands: LSCROLL2 widths/history/inclusive endpoint/zero duration/refusal/layer0 ownership and zero-operand MENUTUTO restored yield passed.");
    }
}
