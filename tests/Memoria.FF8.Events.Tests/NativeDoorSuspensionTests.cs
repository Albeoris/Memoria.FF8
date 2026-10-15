using System.Reflection;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDoorOperandSuspensionContract()
    {
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        (Jsm.Opcode, int)[] Empty(int label) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] Expression(int label, int result, int operand, int destination, bool defaultFrame = false)
        {
            List<(Jsm.Opcode, int)> operations = [(Jsm.Opcode.LBL, label), (Jsm.Opcode.PSHN_L, result), (Jsm.Opcode.POPI_L, 1)];
            operations.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), defaultFrame ? 13 : 12));
            operations.AddRange([(Jsm.Opcode.PSHN_L, operand), (Jsm.Opcode.PSHN_L, 23), (Jsm.Opcode.CAL, 2), (Jsm.Opcode.POPM_W, destination), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_W, destination + 1)]);
            if (defaultFrame)
            {
                operations.Add((Jsm.Opcode.HALT, 0));
            }
            operations.Add((Jsm.Opcode.RET, 8));
            return operations.ToArray();
        }
        (Jsm.Opcode, int)[] door = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([(16 << 7) | 7, 0], [Empty(0), door, Expression(17, 77, 100, 5000, defaultFrame: true), Empty(18), Expression(19, 555, 333, 5002), Expression(20, 999, 444, 5004), Empty(21), Empty(22), Empty(23)]);
        content[1] = 1;
        content[3] = 1;
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.SetCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.MarkReplayUnavailable("Declared native door scheduler component; requests are staged through the current Queue boundary.");
            var engine = new EventEngine(session, session.State.Variables);
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        live.Step();
        JsonElement defaultBefore = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
        Require(defaultBefore.GetProperty("Stack")[0].GetInt32() == 100 && defaultBefore.GetProperty("Results").GetProperty("1").GetInt32() == 77, "Synthetic default did not retain an unfinished expression at the original sixteen-instruction budget.");
        // Component fixture only: stage requests at the same Queue/slot-tagging
        // boundary used by DispatchNativeDoorEvents. No research save or journal
        // is constructed from this declared synthetic instruction program.
        foreach ((int script, int priority, byte slot) in new[] { (19, 1, (byte)6), (20, 2, (byte)5) })
        {
            QueueNativeDoorWaitComponent(live, script, slot);
        }
        JsonElement suspended = live.Capture();
        EventEngine restored = Create();
        restored.Restore(suspended);
        List<int> labels = [];
        live.CommandExecuted += command =>
        {
            if (command.Name == "LBL")
            {
                labels.Add(command.ScriptId);
            }
        };
        live.Step();
        restored.Step();
        JsonElement active = live.Capture();
        JsonElement stillDefault = active.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
        Require(JsonElement.DeepEquals(defaultBefore, stillDefault), "Higher native door slot changed the suspended default's PC, operands, result bank or wait ownership.");
        JsonElement facing = active.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 19);
        Require(facing.GetProperty("Stack")[0].GetInt32() == 333 && facing.GetProperty("Results").GetProperty("1").GetInt32() == 555 && labels.SequenceEqual([19]) && JsonElement.DeepEquals(active, restored.Capture()), "Native slot6 did not retain its independent unfinished expression/result bank ahead of queued slot5.");
        EventEngine midBudget = Create();
        midBudget.Restore(active);
        for (int tick = 0; tick < 4; tick++)
        {
            live.Step();
            restored.Step();
            midBudget.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()) && JsonElement.DeepEquals(live.Capture(), midBudget.Capture()), "Door RET8 resumption diverged after independent pending-request or mid-expression restore.");
        }
        JsonElement variables = live.Capture().GetProperty("Variables");
        Require(variables.GetProperty("5000").GetInt32() == 2300 && variables.GetProperty("5001").GetInt32() == 77 && variables.GetProperty("5002").GetInt32() == 7659 && variables.GetProperty("5003").GetInt32() == 555 && variables.GetProperty("5004").GetInt32() == 10212 && variables.GetProperty("5005").GetInt32() == 999 && labels.SequenceEqual([19, 20]), "Native slots failed6→5→default0 order, lower operand/result restoration or same-budget RET8 continuation.");
        Console.WriteLine("Native door slots6→5→0: meaningful default and two meaningful requests, unfinished operands/PC/eight-result bank ownership, native sixteen-op budgets, RET8 continuation and independent pending/mid-expression restores passed (synthetic component requests).");
    }
}
