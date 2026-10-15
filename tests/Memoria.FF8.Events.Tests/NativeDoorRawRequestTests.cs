using System.Reflection;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDoorRawRequestContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        foreach (Jsm.Opcode busy in new[] { Jsm.Opcode.REQ, Jsm.Opcode.REQSW, Jsm.Opcode.REQEW })
        {
            NativeDoorRawRequestInterleaving(vitals, economy, busy);
        }
        EventEngine early = CreateNativeDoorRawRequest(vitals, economy, beforeInitialization: true);
        NativeDoorRawRequestRefusal(early, 0, "REQ", "request before physical V2 initialization");
        EventEngine explicitReturn = CreateNativeDoorRawRequest(vitals, economy, explicitReturn: true);
        NativeDoorRawRequestRefusal(explicitReturn, 24, "RET", "door explicit RET0 cancellation");
        Console.WriteLine("Native door raw slot1/local8, direct slot6, raw slot7/local9: typed suspended operands/results, entered LBL frames, highest-slot order, same-budget RET8 resume, independent restores, busy REQ/SW/EW drop and atomic pre-V2/RET0 refusals passed (declared scheduler component).");
    }

    private static void NativeDoorRawRequestInterleaving(NativeCharacterVitals vitals, NativeEconomy economy, Jsm.Opcode busy)
    {
        EventEngine live = CreateNativeDoorRawRequest(vitals, economy, busy);
        List<int> labels = [];
        List<int> returns = [];
        List<long> returnFrames = [];
        Dictionary<int, NativeScalarValue> completedResults = [];
        HashSet<int> resumedInReturnCallback = [];
        bool sawUnenteredHigh = false;
        int busyCount = 0;
        live.CommandExecuted += command =>
        {
            if (command.ObjectId == 1 && command.Name == "LBL")
            {
                labels.Add(command.ScriptId);
            }
            if (command.ObjectId == 1 && command.Name == "RET" && command.ScriptId >= 19)
            {
                returns.Add(command.ScriptId);
                returnFrames.Add(live.Frame);
                completedResults[command.ScriptId] = NativeDoorRawFiber(live, command.ScriptId).GetProperty("Results").GetProperty("7").Deserialize<NativeScalarValue>()!;
            }
            if (command.ObjectId == 1 && returns.Count > 0 && returnFrames[^1] == live.Frame && command.ScriptId != returns[^1])
            {
                resumedInReturnCallback.Add(command.ScriptId);
            }
            if (command.ObjectId == 0 && command.Name == busy.ToString() && command.Arguments.SequenceEqual(new[] { 1, 65535 }))
            {
                busyCount++;
            }
            if (command.ObjectId == 0 && command.Name == "REQ" && command.Arguments.SequenceEqual(new[] { 7, 25 }))
            {
                JsonElement pending = NativeDoorRawFiber(live, 25);
                sawUnenteredHigh = pending.GetProperty("NativeDoorSlot").GetInt32() == 7 && pending.GetProperty("Priority").GetInt32() == 0 && !pending.GetProperty("NativeResultFrameEntered").GetBoolean() && pending.GetProperty("Stack").GetArrayLength() == 0;
            }
        };
        for (int tick = 0; tick < 12 && !HasNativeDoorRawFiber(live, 24); tick++)
        {
            live.Step();
        }
        JsonElement raw = NativeDoorRawFiber(live, 24);
        Require(live.Scene.NativeDoorDriverVersion == 2 && raw.GetProperty("NativeDoorSlot").GetInt32() == 1 && raw.GetProperty("Priority").GetInt32() == 6 && raw.GetProperty("NativeResultFrameEntered").GetBoolean(), "Raw priority1/local8 did not acquire physical slot1 independently of its script-local identity.");
        Require(raw.GetProperty("Stack").GetArrayLength() == 1 && !raw.GetProperty("Stack")[0].Deserialize<NativeScalarValue>()!.IsExact && raw.GetProperty("Results").GetProperty("1").GetInt32() == 11, "Raw door expression did not retain its typed operand and independent eight-register result frame.");

        // This is the declared direct-event Queue/slot boundary used by the
        // existing door suspension component. Raw requests above and below it
        // are actual executed REQ instructions, not fabricated journal entries.
        MethodInfo queue = typeof(EventEngine).GetMethod("Queue", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object direct = queue.Invoke(live, [1, 19, 1, false])!;
        direct.GetType().GetField("NativeDoorSlot")!.SetValue(direct, (byte)6);
        JsonElement pendingCapture = live.Capture();
        EventEngine pendingRestore = CreateNativeDoorRawRequest(vitals, economy, busy);
        pendingRestore.Restore(pendingCapture);
        live.Step();
        pendingRestore.Step();
        JsonElement directCapture = live.Capture();
        Require(JsonElement.DeepEquals(raw, NativeDoorRawFiber(live, 24)), "Direct physical slot6 changed the lower raw slot1 PC, stack, result frame or wait owner.");
        Require(NativeDoorRawFiber(live, 19).GetProperty("Stack").GetArrayLength() == 1 && NativeDoorRawFiber(live, 19).GetProperty("Results").GetProperty("1").GetInt32() == 22 && JsonElement.DeepEquals(directCapture, pendingRestore.Capture()), "Direct slot6 failed to suspend raw local8 with an independent typed expression or pending-request restore.");
        EventEngine midRestore = CreateNativeDoorRawRequest(vitals, economy, busy);
        midRestore.Restore(directCapture);
        JsonElement? directBeforeHigh = null;
        for (int tick = 0; tick < 24 && !live.Capture().GetProperty("Variables").TryGetProperty("6000", out _); tick++)
        {
            bool highBefore = HasNativeDoorRawFiber(live, 25);
            JsonElement? directBefore = HasNativeDoorRawFiber(live, 19) ? NativeDoorRawFiber(live, 19) : null;
            live.Step();
            pendingRestore.Step();
            midRestore.Step();
            Require(JsonElement.DeepEquals(live.Capture(), pendingRestore.Capture()) && JsonElement.DeepEquals(live.Capture(), midRestore.Capture()), "Door raw/direct interleaving diverged after independent pending and mid-expression restores.");
            if (!highBefore && HasNativeDoorRawFiber(live, 25))
            {
                directBeforeHigh = directBefore;
                Require(directBefore is JsonElement held && JsonElement.DeepEquals(held, NativeDoorRawFiber(live, 19)), "Higher raw slot7 changed suspended direct slot6 before its RET8.");
            }
        }
        JsonElement variables = live.Capture().GetProperty("Variables");
        Require(sawUnenteredHigh && directBeforeHigh is not null && busyCount == 1 && labels.Where(script => script >= 19).SequenceEqual(new[] { 24, 19, 25 }) && returns.SequenceEqual(new[] { 25, 19, 24 }), "Native raw/direct occupancy, actual LBL admission or highest physical slot7→6→1 return order changed.");
        Require(variables.GetProperty("6002").GetInt32() == 33 && variables.GetProperty("6001").GetInt32() == 22 && variables.GetProperty("6000").GetInt32() == 11 && variables.GetProperty("6100").GetInt32() == 77, "Busy raw request looked up its invalid script, lost its successor or overwrote another slot's results.");
        Require(resumedInReturnCallback.SetEquals(new[] { 19, 24 }), "Door RET8 failed to resume retained lower requests within the same sixteen-op callback budget.");
        foreach (int script in new[] { 24, 19, 25 })
        {
            NativeScalarValue retained = completedResults[script];
            Require(!retained.IsExact && retained.ActorInfoOrigin is { NativeActorIndex: 255, RetainedActorCount: 1 }, "Door RET8 consumed or replaced the suspended typed actor-info provenance.");
        }
    }

    private static EventEngine CreateNativeDoorRawRequest(NativeCharacterVitals vitals, NativeEconomy economy, Jsm.Opcode busy = Jsm.Opcode.REQ, Jsm.Opcode firstRequest = Jsm.Opcode.REQ, bool beforeInitialization = false, bool explicitReturn = false)
    {
        (Jsm.Opcode, int)[] Empty(int label) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] Expression(int label, int result, int noOperations, int destination)
        {
            List<(Jsm.Opcode, int)> expression = [(Jsm.Opcode.LBL, label), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, result), (Jsm.Opcode.POPI_L, 1)];
            expression.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), noOperations));
            expression.AddRange([(Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_W, destination), (Jsm.Opcode.RET, 8)]);
            return expression.ToArray();
        }
        List<(Jsm.Opcode, int)> initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.EFFECTLOAD, 0)];
        if (beforeInitialization)
        {
            initialize.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.REQ, 1)]);
        }
        initialize.Add((Jsm.Opcode.RET, 8));
        (Jsm.Opcode, int)[] issuer = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.NOP, 0), (firstRequest, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 65535), (busy, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 25), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 6100), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] line = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)[]> scripts = [initialize.ToArray(), issuer, line, Empty(17), Empty(18), Expression(19, 22, 60, 6001), Empty(20), Empty(21), Empty(22), Empty(23), explicitReturn ? [(Jsm.Opcode.LBL, 24), (Jsm.Opcode.RET, 0)] : Expression(24, 11, 40, 6000), Expression(25, 33, 24, 6002)];
        byte[] content = SyntheticObjects([(16 << 7) | 9, 1], scripts);
        content[1] = 1;
        content[3] = 1;
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        session.MarkReplayUnavailable("Declared native door raw/direct scheduler component; direct event is staged at the existing Queue boundary.");
        EventEngine engine = new(session, session.State.Variables) { AnimationFrameCount = (_, _) => 4, LoadSoundResource = _ => SoundResourceStatus.Muted };
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static void NativeDoorRawRequestRefusal(EventEngine engine, int script, string opcode, string description)
    {
        JsonElement? before = null;
        engine.CommandExecuted += command =>
        {
            if (command.ScriptId == script && (opcode == "RET" ? command.Name == "LBL" : command.Name == "NOP"))
            {
                before = NativeDoorRawFiber(engine, script);
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
        JsonElement after = NativeDoorRawFiber(engine, script);
        Require(before is JsonElement saved && engine.LastFault?.Opcode == opcode && JsonElement.DeepEquals(saved, after) && after.GetProperty("Position").GetInt32() == engine.LastFault.Operation && !engine.Capture().GetProperty("Variables").TryGetProperty("6100", out _), $"Unsupported {description} changed its original PC, operands, entered frame or successor before refusal.");
        if (opcode != "RET")
        {
            Require(!HasNativeDoorRawFiber(engine, 24), $"Unsupported {description} queued a door request before refusal.");
        }
    }

    private static bool HasNativeDoorRawFiber(EventEngine engine, int script) => engine.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == script);

    private static JsonElement NativeDoorRawFiber(EventEngine engine, int script) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == script);
}
