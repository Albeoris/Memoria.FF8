using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeGlobalJumpContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        byte[] content = SyntheticObjects([3], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHN_L, 333), (Jsm.Opcode.GJMP, 2), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_W, 5002), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.GJMP, 3), (Jsm.Opcode.RET, 9)],
            [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.RET, 9)]]);
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
            engine.LoadField(100, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        live.Step();
        JsonElement saved = live.Capture();
        JsonElement fiber = saved.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
        Require(fiber.GetProperty("NativeGlobalJumps").GetArrayLength() == 2 && fiber.GetProperty("Stack").GetArrayLength() == 19, "Nested GJMP lost original return operands or LBL result frames.");
        EventEngine restored = Create();
        restored.Restore(saved);
        for (int index = 0; index < 5; index++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Nested GJMP/RET9 diverged after independent restore.");
        }
        JsonElement final = live.Capture();
        JsonElement finished = final.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
        Require(CapturedVariables(final).GetProperty("5000").GetInt32() == 333 && CapturedVariables(final).GetProperty("5002").GetInt32() == 77 && finished.GetProperty("NativeGlobalJumps").GetArrayLength() == 0 && finished.GetProperty("WaitReason").GetString() == "NativeHalt", "RET9 failed native result-bank pop order, preserved operands, resumed caller or request lifetime.");
        JsonNode corrupt = JsonNode.Parse(saved.GetRawText())!;
        JsonNode savedFiber = corrupt["Fibers"]!.AsArray().Single(item => item!["ScriptId"]!.GetValue<int>() == 1)!;
        savedFiber["NativeGlobalJumps"]![0]!["ReturnOperation"] = 0;
        JsonElement before = restored.Capture();
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(corrupt));
            throw new InvalidOperationException("Global call accepted a forged return instruction.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, restored.Capture()), "Rejected global call changed its receiving VM.");
        Console.WriteLine("Native51C530/51C570/51C5C0: nested GJMP, LBL banks, RET9, operand preservation and independent continuation.");
    }
}
