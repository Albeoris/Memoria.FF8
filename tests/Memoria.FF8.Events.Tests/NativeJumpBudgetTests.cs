using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeJumpBudgetContract()
    {
        byte[] program = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.JMP, -2), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            GameSession session = CreateInitializedSession();
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 2 };
            engine.LoadField(600, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        EventEngine original = Create();
        int commands = 0;
        original.CommandExecuted += _ => commands++;
        original.Step();
        Require(commands == 5, $"A backward-jump callback did not execute five stores within its sixteen-instruction budget: {commands}.");
        JsonElement snapshot = original.Capture();
        JsonElement fiber = snapshot.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
        Require(fiber.GetProperty("Position").GetInt32() == 5 && fiber.GetProperty("WaitReason").GetString() == "None", "Native JMP must retain its relative PC and exhaust the same sixteen-operation budget without inventing a frame wait.");
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
