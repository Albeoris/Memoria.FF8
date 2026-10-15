using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeJumpBudgetContract()
    {
        byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.JMP, -1), (Jsm.Opcode.RET, 0));
        EventEngine Create()
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session);
            engine.LoadField(100, program, []);
            return engine;
        }
        EventEngine original = Create();
        original.Step();
        JsonElement snapshot = original.Capture();
        JsonElement fiber = snapshot.GetProperty("Fibers")[0];
        Require(fiber.GetProperty("Position").GetInt32() == 0 && fiber.GetProperty("WaitReason").GetString() == "None", "Native JMP must retain its relative PC and exhaust the same sixteen-operation budget without inventing a frame wait.");
        EventEngine restored = Create();
        restored.Restore(snapshot);
        for (int frame = 0; frame < 4; frame++)
        {
            original.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(original.Capture(), restored.Capture()), "Native budget exhaustion lost its PC or introduced a wait after independent restore.");
        }
        Console.WriteLine("Native51C4D0/529FF0: backward JMP continues in the shared sixteen-operation budget and restores independently.");
    }
}
