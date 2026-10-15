using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDoorDefaultFrameContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        foreach (bool twoRequests in new[] { false, true })
        {
            NativeDoorDefaultInheritedBank(vitals, economy, twoRequests);
        }
        NativeDoorDefaultOperandCapacity(vitals, economy, 80);
        NativeDoorDefaultOperandCapacity(vitals, economy, 72);
        EventEngine noFrameReturn = CreateNativeDoorDefaultComponent(vitals, economy, defaultReturn: true);
        ReachNativeDoorDefault(noFrameReturn);
        JsonElement before = NativeDoorRawFiber(noFrameReturn, 17);
        ExpectNativeDoorDefaultFault(noFrameReturn, "RET");
        JsonNode dispatched = JsonNode.Parse(before.GetRawText())!;
        // Run marks first dispatch before opcode preflight; all native opcode
        // owners, PC, operands and the inherited result bank must stay intact.
        dispatched["Started"] = true;
        Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(dispatched), NativeDoorRawFiber(noFrameReturn, 17)), "Default RET8 changed its PC, inherited bank or zero saved-frame owner before refusing the negative alias.");
        Console.WriteLine("Native door default52C960: current0 before body/HALT, skipped LBL and live init-result inheritance, zero saved cells,80-cell operand limit, requestedLBL8 capacity, asymmetric RET8 reversal/double reversal, exact independent restores and malformed/default-RET alias refusals passed.");
    }

    private static void NativeDoorDefaultInheritedBank(NativeCharacterVitals vitals, NativeEconomy economy, bool twoRequests)
    {
        int defaultStart = -1;
        EventEngine engine = CreateNativeDoorDefaultComponent(vitals, economy, onDefaultStart: start => defaultStart = start);
        ReachNativeDoorDefault(engine);
        JsonElement initial = engine.Capture();
        JsonElement defaultFiber = NativeDoorRawFiber(engine, 17);
        JsonElement init = NativeDoorRawFiber(engine, 16);
        JsonElement scheduler = initial.GetProperty("Actors")[1].GetProperty("NativeDoorScheduler");
        Require(defaultFiber.GetProperty("NativeDoorDefaultFrame").GetBoolean() && !defaultFiber.GetProperty("NativeResultFrameEntered").GetBoolean() && defaultFiber.GetProperty("Position").GetInt32() == defaultStart + 1 && defaultFiber.GetProperty("Stack").GetArrayLength() == 0, "Fresh default executed LBL or invented saved results instead of native start+1/depthFF.");
        Require(scheduler.GetProperty("CurrentSlot").GetInt32() == 0 && scheduler.GetProperty("CurrentSequence").GetInt64() == defaultFiber.GetProperty("Sequence").GetInt64() && scheduler.GetProperty("DefaultSequence").GetInt64() == defaultFiber.GetProperty("Sequence").GetInt64() && scheduler.GetProperty("InitializationSequence").GetInt64() == init.GetProperty("Sequence").GetInt64() && scheduler.GetProperty("RetiredOperation").ValueKind == JsonValueKind.Null, "Fresh native current0/default identity was inferred from HALT or lost its actual completed initialization source.");
        Require(NativeDoorResultBank(defaultFiber).SequenceEqual(new[] { 11, 22, 33, 44, 55, 66, 77, 88 }), "Default did not inherit the actual live init result bank unchanged.");
        EventEngine preBody = CreateNativeDoorDefaultComponent(vitals, economy);
        preBody.Restore(initial);
        for (int callback = 0; callback < 4 && NativeDoorRawFiber(engine, 17).GetProperty("WaitReason").GetString() != "NativeHalt"; callback++)
        {
            engine.Step();
            preBody.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), preBody.Capture()), "Default-before-HALT state diverged after independent restore.");
        }
        JsonElement variables = CapturedVariables(engine.Capture());
        Require(Enumerable.Range(0, 8).All(register => variables.GetProperty((6300 + register).ToString()).GetInt32() == (register + 1) * 11), "Skipped default LBL cleared live init values before actual PSHI reads.");
        QueueNativeDoorWaitComponent(engine, 24, 3);
        if (twoRequests)
        {
            QueueNativeDoorWaitComponent(engine, 19, 6);
        }
        JsonElement pending = engine.Capture();
        NativeDoorDefaultMalformedRestore(vitals, economy, pending);
        EventEngine restored = CreateNativeDoorDefaultComponent(vitals, economy);
        restored.Restore(pending);
        int firstScript = twoRequests ? 19 : 24;
        engine.Step();
        restored.Step();
        JsonElement entered = NativeDoorRawFiber(engine, firstScript);
        Require(entered.GetProperty("NativeResultFrameEntered").GetBoolean() && !entered.GetProperty("NativeDoorDefaultFrame").GetBoolean() && entered.GetProperty("NativeDoorSavedResults").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 11, 22, 33, 44, 55, 66, 77, 88 }) && NativeDoorResultBank(entered).All(value => value == 0), "Requested LBL did not save the current asymmetric eight-cell bank in native ascending order and clear the live bank.");
        Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Requested default-bank suspension diverged after independent pending restore.");
        NativeDoorDefaultSavedBankRefusal(vitals, economy, engine.Capture(), firstScript);
        EventEngine enteredRestore = CreateNativeDoorDefaultComponent(vitals, economy);
        enteredRestore.Restore(engine.Capture());
        for (int callback = 0; callback < 8 && engine.Capture().GetProperty("Actors")[1].GetProperty("NativeDoorScheduler").GetProperty("CurrentSlot").GetInt32() != 0; callback++)
        {
            engine.Step();
            restored.Step();
            enteredRestore.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()) && JsonElement.DeepEquals(engine.Capture(), enteredRestore.Capture()), "Native saved-bank pop order diverged after pending or entered-frame restore.");
        }
        int[] expected = twoRequests ? [11, 22, 33, 44, 55, 66, 77, 88] : [88, 77, 66, 55, 44, 33, 22, 11];
        Require(NativeDoorResultBank(NativeDoorRawFiber(engine, 17)).SequenceEqual(expected) && engine.Capture().GetProperty("Actors")[1].GetProperty("NativeDoorScheduler").GetProperty("CurrentSequence").GetInt64() == defaultFiber.GetProperty("Sequence").GetInt64(), "Native RET8 failed reversed saved-bank restoration or double reversal through two requested contexts before resuming the actual default.");
    }

    private static void NativeDoorDefaultOperandCapacity(NativeCharacterVitals vitals, NativeEconomy economy, int count)
    {
        EventEngine engine = CreateNativeDoorDefaultComponent(vitals, economy, operandCount: count);
        ReachNativeDoorDefault(engine);
        ReachNativeDoorWait(engine, () => NativeDoorRawFiber(engine, 17).GetProperty("Stack").GetArrayLength() == count);
        JsonElement defaultFiber = NativeDoorRawFiber(engine, 17);
        Require(defaultFiber.GetProperty("NativeDoorDefaultFrame").GetBoolean() && !defaultFiber.GetProperty("NativeResultFrameEntered").GetBoolean(), "Native default operands were charged an invented eight-result LBL frame.");
        if (count == 72)
        {
            QueueNativeDoorWaitComponent(engine, 24, 3);
            engine.Step();
            JsonElement request = NativeDoorRawFiber(engine, 24);
            Require(request.GetProperty("NativeResultFrameEntered").GetBoolean() && request.GetProperty("Stack").GetArrayLength() == 0 && request.GetProperty("NativeDoorSavedResults").GetArrayLength() == 8 && NativeDoorRawFiber(engine, 17).GetProperty("Stack").GetArrayLength() == 72, "Default72 plus requested LBL8 did not fit the original80-DWORD bank exactly.");
        }
        JsonElement pending = engine.Capture();
        EventEngine restored = CreateNativeDoorDefaultComponent(vitals, economy, operandCount: count);
        restored.Restore(pending);
        int script = count == 72 ? 24 : 17;
        JsonElement? before = count == 72 ? NativeDoorRawFiber(engine, script) : null;
        engine.CommandExecuted += command =>
        {
            if (command.ScriptId == script && command.Name == "NOP")
            {
                before = NativeDoorRawFiber(engine, script);
            }
        };
        ExpectNativeDoorDefaultFault(engine, "PSHN_L");
        ExpectNativeDoorDefaultFault(restored, "PSHN_L");
        Require(before is JsonElement saved && JsonElement.DeepEquals(saved, NativeDoorRawFiber(engine, script)) && JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Operand81 alias refusal changed default/request PC, operands, result frame or independently restored state.");
    }

    private static EventEngine CreateNativeDoorDefaultComponent(NativeCharacterVitals vitals, NativeEconomy economy, int operandCount = 0, bool defaultReturn = false, Action<int>? onDefaultStart = null)
    {
        (Jsm.Opcode, int)[] Empty(int label) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, 16)];
        for (int register = 0; register < 8; register++)
        {
            init.AddRange([(Jsm.Opcode.PSHN_L, (register + 1) * 11), (Jsm.Opcode.POPI_L, register)]);
        }
        init.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 8)]);
        List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 17)];
        if (operandCount > 0)
        {
            main.AddRange(Enumerable.Range(1, operandCount).Select(value => (Jsm.Opcode.PSHN_L, value)));
            if (operandCount == 80)
            {
                main.AddRange([(Jsm.Opcode.NOP, 0), (Jsm.Opcode.PSHN_L, 999)]);
            }
        }
        else if (!defaultReturn)
        {
            for (int register = 0; register < 8; register++)
            {
                main.AddRange([(Jsm.Opcode.PSHI_L, register), (Jsm.Opcode.POPM_L, 6300 + register)]);
            }
        }
        if (!defaultReturn)
        {
            main.Add((Jsm.Opcode.HALT, 0));
        }
        main.Add((Jsm.Opcode.RET, 8));
        (Jsm.Opcode, int)[] Request(int label)
        {
            List<(Jsm.Opcode, int)> request = [(Jsm.Opcode.LBL, label)];
            request.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), operandCount == 72 ? 15 : 16));
            if (operandCount == 72)
            {
                request.Add((Jsm.Opcode.PSHN_L, 999));
            }
            request.Add((Jsm.Opcode.RET, 8));
            return request.ToArray();
        }
        byte[] content = SyntheticObjects([(16 << 7) | 8, 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], init.ToArray(), main.ToArray(), Empty(18), Request(19), Empty(20), Empty(21), Empty(22), Empty(23), Request(24)]);
        content[1] = 1;
        content[3] = 1;
        // SyntheticObjects' short encoding treats a zero POPI parameter as
        // the opcode word itself. Preserve the actual result-register0 operand.
        int operationsOffset = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6));
        for (int offset = operationsOffset; offset < content.Length; offset += 4)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(offset)) == (int)Jsm.Opcode.POPI_L)
            {
                BinaryPrimitives.WriteInt32LittleEndian(content.AsSpan(offset), (int)Jsm.Opcode.POPI_L << 24);
            }
        }
        onDefaultStart?.Invoke(JsmProgram.Read(content).Objects[1].Scripts[1].Start);
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        session.MarkReplayUnavailable("Declared door default/result-bank component; requested contexts use current native Queue/selection producers.");
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static int[] NativeDoorResultBank(JsonElement fiber) => Enumerable.Range(0, 8).Select(register => fiber.GetProperty("Results").TryGetProperty(register.ToString(), out JsonElement value) ? value.GetInt32() : 0).ToArray();

    private static void ReachNativeDoorDefault(EventEngine engine) => ReachNativeDoorWait(engine, () => engine.Capture().GetProperty("DefaultsStarted").GetBoolean());

    private static void ExpectNativeDoorDefaultFault(EventEngine engine, string opcode)
    {
        try
        {
            engine.Step();
            throw new InvalidOperationException($"Native door accepted unsupported {opcode} alias.");
        }
        catch (EventScriptException)
        {
        }
        Require(engine.LastFault?.Opcode == opcode, $"Native door refused before the intended {opcode} boundary.");
    }

    private static void NativeDoorDefaultMalformedRestore(NativeCharacterVitals vitals, NativeEconomy economy, JsonElement pending)
    {
        foreach (string member in new[] { "CurrentSequence", "DefaultSequence", "InitializationSequence", "CurrentSlot", "RetiredOperation", "DefaultFrame", "MissingCurrentSlot", "MissingRetiredOperation" })
        {
            EventEngine target = CreateNativeDoorDefaultComponent(vitals, economy);
            JsonElement before = target.Capture();
            JsonNode malformed = JsonNode.Parse(pending.GetRawText())!;
            if (member == "DefaultFrame")
            {
                malformed["Fibers"]!.AsArray().Single(fiber => fiber!["ScriptId"]!.GetValue<int>() == 17)!["NativeDoorDefaultFrame"] = false;
            }
            else if (member.StartsWith("Missing", StringComparison.Ordinal))
            {
                malformed["Actors"]![1]!["NativeDoorScheduler"]!.AsObject().Remove(member[7..]);
            }
            else
            {
                malformed["Actors"]![1]!["NativeDoorScheduler"]![member] = member == "CurrentSlot" ? 2 : 999999;
            }
            try
            {
                target.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException($"Native door accepted forged {member} owner.");
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException)
            {
            }
            Require(JsonElement.DeepEquals(before, target.Capture()), "Malformed native door scheduler restore mutated the current VM.");
        }
    }

    private static void NativeDoorDefaultSavedBankRefusal(NativeCharacterVitals vitals, NativeEconomy economy, JsonElement pending, int script)
    {
        EventEngine target = CreateNativeDoorDefaultComponent(vitals, economy);
        JsonElement before = target.Capture();
        JsonNode malformed = JsonNode.Parse(pending.GetRawText())!;
        malformed["Fibers"]!.AsArray().Single(fiber => fiber!["ScriptId"]!.GetValue<int>() == script)!["NativeDoorSavedResults"]!.AsArray().RemoveAt(7);
        try
        {
            target.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Native door restored a requested LBL with only seven saved results.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, target.Capture()), "Malformed native saved-bank restore mutated the current VM.");
    }
}
