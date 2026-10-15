using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDoorRequestWaitContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeDoorWaitSelectedBeforeLabel(vitals, economy);
        NativeDoorWaitLowerStart(vitals, economy);
        NativeDoorWaitEndWithSlotReuse(vitals, economy);
        foreach ((Jsm.Opcode opcode, int raw, bool completes) in new[] { (Jsm.Opcode.REQSW, 257, false), (Jsm.Opcode.REQEW, 257, true), (Jsm.Opcode.REQEW, -255, false) })
        {
            NativeDoorWaitWidth(vitals, economy, opcode, raw, completes);
        }
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.REQSW, Jsm.Opcode.REQEW })
        {
            NativeDoorWaitBeforeHalt(vitals, economy, opcode);
            EventEngine self = CreateNativeDoorWait(vitals, economy, opcode, self: true);
            self.Step();
            self.Step();
            QueueNativeDoorWaitComponent(self, 19, 6);
            NativeDoorWaitExpectRefusal(self, 19, opcode);
        }
        Console.WriteLine("Native door REQSW/EW: actual slot selection before LBL, lower-slot polling, end wait across higher slots and slot reuse, retained DWORD operands/PC, signed widths257/−255, one-time queueing, completion yield, independent restores and atomic malformed/self-transfer refusals passed.");
    }

    private static void NativeDoorWaitBeforeHalt(NativeCharacterVitals vitals, NativeEconomy economy, Jsm.Opcode opcode)
    {
        EventEngine engine = CreateNativeDoorWait(vitals, economy, opcode, beforeHalt: true);
        EventEngine restored = CreateNativeDoorWait(vitals, economy, opcode, beforeHalt: true);
        bool issuedBeforeHalt = false;
        engine.CommandExecuted += command =>
        {
            if (command.ScriptId == 1 && command.Name == "NOP")
            {
                JsonElement actualDefault = NativeDoorRawFiber(engine, 17);
                issuedBeforeHalt = actualDefault.GetProperty("WaitReason").GetString() == "None" && actualDefault.GetProperty("NativeDoorDefaultFrame").GetBoolean() && !actualDefault.GetProperty("NativeResultFrameEntered").GetBoolean();
            }
        };
        for (int callback = 0; callback < 12 && !CapturedVariables(engine.Capture()).TryGetProperty("6200", out _); callback++)
        {
            restored.Restore(engine.Capture());
            engine.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Native current-slot wait before the default reached HALT diverged after independent restore.");
        }
        Require(issuedBeforeHalt && !engine.Actors[1].LineEnabled && CapturedVariables(engine.Capture()).GetProperty("6200").GetInt32() == 77, "Native door wait invented a HALT floor requirement instead of reading the executed52C960 current0 owner.");
    }

    private static void NativeDoorWaitSelectedBeforeLabel(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        EventEngine engine = CreateNativeDoorWait(vitals, economy, Jsm.Opcode.REQSW);
        bool completedBeforeLabel = false;
        long completionFrame = -1;
        engine.CommandExecuted += command =>
        {
            if (command.ScriptId == 1 && command.Name == "REQSW")
            {
                JsonElement target = NativeDoorRawFiber(engine, 24);
                JsonElement caller = NativeDoorRawFiber(engine, 1);
                completedBeforeLabel = !target.GetProperty("Started").GetBoolean() && !target.GetProperty("NativeResultFrameEntered").GetBoolean() && target.GetProperty("NativeDoorSlot").GetInt32() == 3 && caller.GetProperty("Stack").GetArrayLength() == 0 && caller.GetProperty("WaitReason").GetString() == "None";
                completionFrame = engine.Frame;
                Require(!CapturedVariables(engine.Capture()).TryGetProperty("6200", out _), "Selected SW ran its successor before native return3 yield.");
            }
        };
        ReachNativeDoorWait(engine, () => completionFrame >= 0);
        Require(completedBeforeLabel && !engine.Actors[1].LineEnabled && !CapturedVariables(engine.Capture()).TryGetProperty("6200", out _), "SW used host Started rather than actual selected slot3 or failed the reached LINEOFF/RET8 target.");
        engine.Step();
        Require(engine.Frame == completionFrame + 1 && CapturedVariables(engine.Capture()).GetProperty("6200").GetInt32() == 77, "SW completion failed to yield until the following ordinary callback.");
    }

    private static void NativeDoorWaitLowerStart(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        EventEngine engine = CreateNativeDoorWait(vitals, economy, Jsm.Opcode.REQSW);
        engine.Step();
        engine.Step();
        QueueNativeDoorWaitComponent(engine, 19, 6);
        ReachNativeDoorWait(engine, () => IsNativeDoorWaitHeld(engine));
        JsonElement pending = engine.Capture();
        JsonElement target = NativeDoorRawFiber(engine, 24);
        Require(!target.GetProperty("Started").GetBoolean() && !target.GetProperty("NativeResultFrameEntered").GetBoolean(), "Lower SW target entered LBL while physical slot6 was current.");
        AssertNativeDoorWaitHeld(engine, 3);
        EventEngine restored = CreateNativeDoorWait(vitals, economy, Jsm.Opcode.REQSW);
        restored.Restore(pending);
        bool selectedUnstarted = false;
        int completions = 0;
        engine.CommandExecuted += command =>
        {
            if (command.ScriptId == 1 && command.Name == "REQSW")
            {
                completions++;
                selectedUnstarted = !NativeDoorRawFiber(engine, 24).GetProperty("Started").GetBoolean();
            }
        };
        for (int tick = 0; tick < 16 && !CapturedVariables(engine.Capture()).TryGetProperty("6200", out _); tick++)
        {
            engine.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Lower SW wait diverged after independent pending-slot restore.");
        }
        Require(completions == 1 && selectedUnstarted && !engine.Actors[1].LineEnabled && CapturedVariables(engine.Capture()).GetProperty("6200").GetInt32() == 77, "SW did not release exactly when lower slot3 became current before LBL, or reissued the request on a poll.");
    }

    private static void NativeDoorWaitEndWithSlotReuse(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        EventEngine engine = CreateNativeDoorWait(vitals, economy, Jsm.Opcode.REQEW);
        ReachNativeDoorWait(engine, () => IsNativeDoorWaitHeld(engine));
        Require(!engine.Actors[1].LineEnabled && NativeDoorRawFiber(engine, 24).GetProperty("Complete").GetBoolean(), "Reached EW target did not execute LINEOFF/RET8 while caller kept its original operands.");
        AssertNativeDoorWaitHeld(engine, 3);
        JsonElement pending = engine.Capture();
        NativeDoorWaitMalformedRestore(vitals, economy, pending);
        QueueNativeDoorWaitComponent(engine, 19, 6);
        QueueNativeDoorWaitComponent(engine, 25, 3);
        EventEngine restored = CreateNativeDoorWait(vitals, economy, Jsm.Opcode.REQEW);
        restored.Restore(engine.Capture());
        long sequence = NativeDoorRawFiber(engine, 1).GetProperty("NativeDoorRequestWait").GetProperty("RequestSequence").GetInt64();
        long completedAt = -1;
        engine.CommandExecuted += command =>
        {
            if (command.ScriptId == 1 && command.Name == "REQEW")
            {
                completedAt = engine.Frame;
            }
        };
        engine.Step();
        restored.Step();
        AssertNativeDoorWaitHeld(engine, 3);
        Require(NativeDoorRawFiber(engine, 1).GetProperty("NativeDoorRequestWait").GetProperty("RequestSequence").GetInt64() == sequence && !NativeDoorRawFiber(engine, 25).GetProperty("Started").GetBoolean(), "EW followed its completed request instead of current higher slot6 or overwrote its one-time issue receipt.");
        bool waitedOnReusedSlot = false;
        for (int tick = 0; tick < 20 && completedAt < 0; tick++)
        {
            engine.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "EW higher-slot/reused-slot wait diverged after independent restore.");
            if (IsNativeDoorWaitHeld(engine) && HasNativeDoorRawFiber(engine, 25) && NativeDoorRawFiber(engine, 25).GetProperty("Started").GetBoolean())
            {
                waitedOnReusedSlot = true;
                AssertNativeDoorWaitHeld(engine, 3);
            }
        }
        Require(waitedOnReusedSlot && completedAt >= 0 && !CapturedVariables(engine.Capture()).TryGetProperty("6200", out _) && CapturedVariables(engine.Capture()).GetProperty("6201").GetInt32() == 91, "EW released before reused slot3 retired or executed its successor in the completion callback.");
        engine.Step();
        restored.Step();
        Require(engine.Frame == completedAt + 1 && CapturedVariables(engine.Capture()).GetProperty("6200").GetInt32() == 77 && JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "EW completion failed native pop2/return3/following-callback continuation.");
    }

    private static void NativeDoorWaitWidth(NativeCharacterVitals vitals, NativeEconomy economy, Jsm.Opcode opcode, int raw, bool completes)
    {
        EventEngine engine = CreateNativeDoorWait(vitals, economy, opcode, raw);
        bool selectedBeforeLabel = false;
        engine.CommandExecuted += command =>
        {
            if (command.ScriptId == 1 && command.Name == opcode.ToString())
            {
                selectedBeforeLabel = !NativeDoorRawFiber(engine, 24).GetProperty("Started").GetBoolean();
            }
        };
        ReachNativeDoorWait(engine, () => IsNativeDoorWaitHeld(engine) || selectedBeforeLabel);
        if (completes)
        {
            Require(selectedBeforeLabel && !IsNativeDoorWaitHeld(engine), "EW raw257 was narrowed before its signed fullDWORD comparison.");
            engine.Step();
            Require(CapturedVariables(engine.Capture()).GetProperty("6200").GetInt32() == 77, "Immediate signed EW completion did not preserve native yield.");
            return;
        }
        AssertNativeDoorWaitHeld(engine, raw);
        EventEngine restored = CreateNativeDoorWait(vitals, economy, opcode, raw);
        restored.Restore(engine.Capture());
        for (int tick = 0; tick < 3; tick++)
        {
            engine.Step();
            restored.Step();
            AssertNativeDoorWaitHeld(engine, raw);
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "FullDWORD native wait diverged after restored pending or completed target.");
        }
    }

    private static EventEngine CreateNativeDoorWait(NativeCharacterVitals vitals, NativeEconomy economy, Jsm.Opcode opcode, int rawPriority = 3, bool self = false, bool beforeHalt = false)
    {
        (Jsm.Opcode, int)[] Empty(int label) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] actor = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] issuer = self ? [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)] : [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, rawPriority), (Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.NOP, 0), (opcode, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 6200), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] line = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> direct = [(Jsm.Opcode.LBL, 19)];
        if (self)
        {
            direct.AddRange([(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.NOP, 0), (opcode, 1)]);
        }
        else
        {
            // LBL +62NOP +RET exactly fills four native16-op callbacks.
            // RET8 selects slot3 with no remaining budget to enter its LBL.
            direct.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), 62));
        }
        direct.Add((Jsm.Opcode.RET, 8));
        List<(Jsm.Opcode, int)> reused = [(Jsm.Opcode.LBL, 25)];
        reused.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), 32));
        reused.AddRange([(Jsm.Opcode.PSHN_L, 91), (Jsm.Opcode.POPM_W, 6201), (Jsm.Opcode.RET, 8)]);
        List<(Jsm.Opcode, int)> defaultBody = [(Jsm.Opcode.LBL, 17)];
        if (beforeHalt)
        {
            defaultBody.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), 80));
        }
        defaultBody.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        List<(Jsm.Opcode, int)[]> scripts = [actor, issuer, line, defaultBody.ToArray(), Empty(18), direct.ToArray(), Empty(20), Empty(21), Empty(22), Empty(23), [(Jsm.Opcode.LBL, 24), (Jsm.Opcode.LINEOFF, 0), (Jsm.Opcode.RET, 8)], reused.ToArray()];
        byte[] content = SyntheticObjects([(16 << 7) | 9, 1], scripts);
        content[1] = 1;
        content[3] = 1;
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        session.MarkReplayUnavailable("Declared door wait component; original request executes normally, direct/reused slots are staged through current Queue boundary.");
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static void QueueNativeDoorWaitComponent(EventEngine engine, int script, byte slot)
    {
        MethodInfo queue = typeof(EventEngine).GetMethod("Queue", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object fiber = queue.Invoke(engine, [1, script, 7 - slot, false])!;
        fiber.GetType().GetField("NativeDoorSlot")!.SetValue(fiber, slot);
        typeof(EventEngine).GetMethod("SelectNativeDoorRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, [fiber]);
    }

    private static bool IsNativeDoorWaitHeld(EventEngine engine) => HasNativeDoorRawFiber(engine, 1) && NativeDoorRawFiber(engine, 1).GetProperty("WaitReason").GetString() == "NativeDoorRequest";

    private static void AssertNativeDoorWaitHeld(EventEngine engine, int raw)
    {
        JsonElement caller = NativeDoorRawFiber(engine, 1);
        JsonElement owner = caller.GetProperty("NativeDoorRequestWait");
        Require(caller.GetProperty("WaitReason").GetString() == "NativeDoorRequest" && caller.GetProperty("Position").GetInt32() == owner.GetProperty("Operation").GetInt32() && caller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 24, raw }) && owner.GetProperty("RawPriority").GetInt32() == raw && owner.GetProperty("RawScriptId").GetInt32() == 24 && owner.GetProperty("TargetNativeObjectId").GetInt32() == 1 && owner.GetProperty("TargetObjectId").GetInt32() == 1 && !CapturedVariables(engine.Capture()).TryGetProperty("6200", out _), "Native door polling changed its held PC, original DWORD operands, target receipt or successor.");
    }

    private static void ReachNativeDoorWait(EventEngine engine, Func<bool> predicate)
    {
        for (int callback = 0; callback < 16 && !predicate(); callback++)
        {
            engine.Step();
        }
        Require(predicate(), "Declared door wait component did not reach its expected bounded control point.");
    }

    private static void NativeDoorWaitMalformedRestore(NativeCharacterVitals vitals, NativeEconomy economy, JsonElement pending)
    {
        foreach (string member in new[] { "RawPriority", "RequestSequence", "DefaultSequence", "Operation", "MissingVersion", "MissingOperation", "MissingTargetNativeObjectId", "MissingTargetObjectId", "MissingRawPriority", "MissingRawScriptId", "MissingRequestSequence", "MissingDefaultSequence" })
        {
            EventEngine target = CreateNativeDoorWait(vitals, economy, Jsm.Opcode.REQEW);
            JsonElement before = target.Capture();
            JsonNode bad = JsonNode.Parse(pending.GetRawText())!;
            JsonNode owner = bad["Fibers"]!.AsArray().Single(fiber => fiber!["ScriptId"]!.GetValue<int>() == 1)!["NativeDoorRequestWait"]!;
            if (member.StartsWith("Missing", StringComparison.Ordinal))
            {
                owner.AsObject().Remove(member[7..]);
            }
            else
            {
                owner[member] = member == "RawPriority" ? 257 : 999999;
            }
            try
            {
                target.Restore(JsonSerializer.SerializeToElement(bad));
                throw new InvalidOperationException($"Native door wait accepted forged {member}.");
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException)
            {
            }
            Require(JsonElement.DeepEquals(before, target.Capture()), "Malformed native door wait restore mutated current state.");
        }
    }

    private static void NativeDoorWaitExpectRefusal(EventEngine engine, int script, Jsm.Opcode opcode)
    {
        JsonElement? before = null;
        int targetCount = -1;
        engine.CommandExecuted += command =>
        {
            if (command.ScriptId == script && command.Name == "NOP")
            {
                before = NativeDoorRawFiber(engine, script);
                targetCount = engine.Capture().GetProperty("Fibers").GetArrayLength();
            }
        };
        for (int callback = 0; callback < 12 && engine.LastFault is null; callback++)
        {
            try
            {
                engine.Step();
            }
            catch (EventScriptException)
            {
                break;
            }
        }
        Require(engine.LastFault?.Opcode == opcode.ToString() && before is JsonElement saved && JsonElement.DeepEquals(saved, NativeDoorRawFiber(engine, script)) && targetCount == engine.Capture().GetProperty("Fibers").GetArrayLength() && !HasNativeDoorRawFiber(engine, 24), "Unsupported native door wait mutated PC, stack, wait owner or queued target before refusal.");
    }
}
