using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSelfRequestContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        EventEngine Create((Jsm.Opcode, int)[] main, (Jsm.Opcode, int)[] requested, (Jsm.Opcode, int)[]? lower = null, bool cross = false)
        {
            (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)];
            List<(Jsm.Opcode, int)[]> scripts = [initialize, [(Jsm.Opcode.LBL, 1), .. main], [(Jsm.Opcode.LBL, 2), .. requested]];
            if (lower is not null)
            {
                scripts.Add([(Jsm.Opcode.LBL, 3), .. lower]);
            }
            ushort[] labels = [(ushort)(scripts.Count - 1)];
            if (cross)
            {
                labels = [(ushort)(scripts.Count - 1), (ushort)((16 << 7) | 2)];
                scripts.AddRange([[(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.POPM_B, 5010), (Jsm.Opcode.RET, 8)]]);
            }
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
            byte[] content = SyntheticObjects(labels, scripts);
            int operationsOffset = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6));
            int operationIndex = 0;
            foreach (var operation in scripts.SelectMany(script => script))
            {
                if (operation == (Jsm.Opcode.RET, 0))
                {
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(content.AsSpan(operationsOffset + operationIndex * 4), (int)Jsm.Opcode.RET << 24);
                }
                operationIndex++;
            }
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        static JsonElement Fiber(EventEngine engine, int script) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == script);
        static bool Has(EventEngine engine, string address) => CapturedVariables(engine.Capture()).TryGetProperty(address, out _);
        static int Value(EventEngine engine, string address) => CapturedVariables(engine.Capture()).GetProperty(address).GetInt32();

        (Jsm.Opcode, int)[] bankMain = [(Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 66), (Jsm.Opcode.POPI_L, 6), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.PSHI_L, 6), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_B, 5002), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] bankRequest = [(Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.RET, 8)];
        EventEngine bank = Create(bankMain, bankRequest);
        List<(string Name, int Script, long Frame)> commands = [];
        bool savedLiveBank = false;
        bank.CommandExecuted += command =>
        {
            commands.Add((command.Name, command.ScriptId, bank.Frame));
            if (command is { Name: "LBL", ScriptId: 2 })
            {
                JsonElement requested = Fiber(bank, 2);
                savedLiveBank = requested.GetProperty("NativeDoorSavedResults")[1].GetInt32() == 11 && requested.GetProperty("NativeDoorSavedResults")[6].GetInt32() == 66 && !requested.GetProperty("Results").TryGetProperty("1", out _);
            }
        };
        bank.Step();
        JsonElement caller = Fiber(bank, 1);
        Require(savedLiveBank && Value(bank, "5000") == 0 && Value(bank, "5001") == 11 && !Has(bank, "5002") && caller.GetProperty("Stack").GetArrayLength() == 1 && caller.GetProperty("Stack")[0].GetInt32() == 66, "Self REQ/LBL/RET8 lost live-bank save/clear, reversed return restoration or the single sixteen-instruction budget.");
        Require(commands.Any(command => command is { Name: "REQ", Script: 1 }) && commands.Any(command => command is { Name: "LBL", Script: 2 }) && commands.Any(command => command is { Name: "RET", Script: 2 }) && commands.All(command => command.Frame == bank.Frame), "Self request selection, requested LBL and RET8 lower-slot resume did not execute within the issuing callback.");
        EventEngine bankCopy = Create(bankMain, bankRequest);
        bankCopy.Restore(bank.Capture());
        bank.Step();
        bankCopy.Step();
        Require(Value(bank, "5002") == 66 && JsonElement.DeepEquals(bank.Capture(), bankCopy.Capture()), "Shared-budget self request resumed its remaining expression differently after restore.");

        (Jsm.Opcode, int)[] longRequest = [.. bankRequest.Take(4), .. Enumerable.Repeat((Jsm.Opcode.NOP, 0), 20), (Jsm.Opcode.RET, 8)];
        EventEngine pending = Create(bankMain, longRequest);
        pending.Step();
        Require(Has(pending, "5000") && !Has(pending, "5001") && Fiber(pending, 2).GetProperty("Started").GetBoolean() && Fiber(pending, 2).GetProperty("NativeResultFrameEntered").GetBoolean(), "Self request did not retain its entered result frame and suspended lower caller at a budget checkpoint.");
        JsonElement saved = pending.Capture();
        EventEngine pendingCopy = Create(bankMain, longRequest);
        pendingCopy.Restore(saved);
        Require(saved.GetRawText() == pendingCopy.Capture().GetRawText(), "Mid-preemption self request failed exact independent restoration.");
        for (int callback = 0; callback < 4 && !Has(pending, "5002"); callback++)
        {
            pending.Step();
            pendingCopy.Step();
            Require(JsonElement.DeepEquals(pending.Capture(), pendingCopy.Capture()), "Pending self request changed its result bank, continuation or budget after restore.");
        }
        Require(Value(pending, "5001") == 11 && Value(pending, "5002") == 66, "Pending requested RET8 did not restore and resume the original caller result bank.");

        (Jsm.Opcode, int)[] busyMain = [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 65535), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        EventEngine busy = Create(busyMain, [(Jsm.Opcode.RET, 8)]);
        busy.Step();
        Require(Value(busy, "5000") == 77 && busy.LastFault is null && !busy.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 65535) && Fiber(busy, 1).GetProperty("Stack").GetArrayLength() == 0, "Busy self REQ looked up its invalid script, retained operands or yielded before its same-budget successor.");

        (Jsm.Opcode, int)[] queueMain = [(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.PSHN_L, 70), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] high = [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.PSHN_L, 72), (Jsm.Opcode.POPM_B, 5002), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] low = [(Jsm.Opcode.PSHN_L, 71), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 8)];
        EventEngine queued = Create(queueMain, high, low);
        List<int> writes = [];
        queued.CommandExecuted += command =>
        {
            if (command.Name == "POPM_B")
            {
                writes.Add(command.ScriptId);
            }
        };
        queued.Step();
        Require(writes.SequenceEqual([2, 3, 1]) && Value(queued, "5002") == 72 && Value(queued, "5001") == 71 && Value(queued, "5000") == 70, "Lower self request preempted its higher issuer or RET8 failed to select queued slot1 before default slot0 in the remaining budget.");

        (Jsm.Opcode, int)[] crossMain = [(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 18), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        EventEngine cross = Create(crossMain, [(Jsm.Opcode.RET, 8)], cross: true);
        cross.Step();
        Require(!Has(cross, "5000") && Value(cross, "5010") == 88, "Cross-entity REQ stopped yielding its caller or failed native target callback ordering.");
        cross.Step();
        Require(Value(cross, "5000") == 77 && cross.LastFault is null, "Cross-entity REQ did not resume its caller on the next callback.");
        (Jsm.Opcode, int)[] cancelMain = [(Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.PSHI_L, 6), (Jsm.Opcode.POPM_B, 5020), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] intermediate = [(Jsm.Opcode.PSHN_L, 22), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.POPM_B, 5021), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] cancel = [.. Enumerable.Repeat((Jsm.Opcode.NOP, 0), 20), (Jsm.Opcode.RET, 0)];
        EventEngine cancelEngine = Create(cancelMain, intermediate, cancel);
        cancelEngine.Step();
        JsonElement cancelSaved = cancelEngine.Capture();
        Require(Fiber(cancelEngine, 3).GetProperty("NativeResultFrameEntered").GetBoolean() && !Has(cancelEngine, "5020"), "Explicit RET fixture did not retain both entered frames before its cancellation.");

        EventEngine cancelCopy = Create(cancelMain, intermediate, cancel);
        cancelCopy.Restore(cancelSaved);
        for (int callback = 0; callback < 5 && !Has(cancelEngine, "5020"); callback++)
        {
            cancelEngine.Step();
            cancelCopy.Step();
            Require(JsonElement.DeepEquals(cancelEngine.Capture(), cancelCopy.Capture()), "Explicit RET cancellation changed after independent restore.");
        }
        Require(Value(cancelEngine, "5020") == 11 && !Has(cancelEngine, "5021") && !cancelEngine.Capture().GetProperty("Fibers").EnumerateArray().Any(item => !item.GetProperty("Complete").GetBoolean() && item.GetProperty("ScriptId").GetInt32() is 2 or 3), "RET0 did not cancel its intermediate slot, resume the target or restore the first physical frame above the target.");

        (Jsm.Opcode, int)[] targetMain = [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] target = [(Jsm.Opcode.PSHN_L, 22), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.POPM_B, 5022), (Jsm.Opcode.PSHI_L, 6), (Jsm.Opcode.POPM_B, 5023), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        EventEngine targeted = Create(targetMain, target, [(Jsm.Opcode.RET, 1)]);
        for (int callback = 0; callback < 4 && !Has(targeted, "5023"); callback++)
        {
            targeted.Step();
        }
        Require(Value(targeted, "5022") == 99 && Value(targeted, "5023") == 22, "Explicit RET1 lost its lower target's saved operand depth or reversed result bank.");
        EventEngine targetedCopy = Create(targetMain, target, [(Jsm.Opcode.RET, 1)]);
        targetedCopy.Restore(targeted.Capture());
        Require(JsonElement.DeepEquals(targeted.Capture(), targetedCopy.Capture()), "Post-cancellation nondefault target failed exact restore.");
        JsmProgram rescue = JsmProgram.Read(assets.LoadField(144).Jsm);
        Require(rescue.Operations[539].Opcode == Jsm.Opcode.RET && rescue.Operations[539].Parameter == 4, $"Original rescue field144 operation539 is RET{rescue.Operations[539].Parameter}.");
        Console.WriteLine("Native self REQ: shared sixteen-op budget, live LBL bank/reversed RET8 restoration, busy self continuation, lower-slot queue order, mid-preemption restore, cross-entity yield, explicit RET0 cancellation, RET1 retained operands/results and original rescue RET4 operand PASS.");
    }
}
