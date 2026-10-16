using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeTutorialContract()
    {
        EventEngine Create(byte[] program)
        {
            EventEngine engine = new(new GameSession(new GameState()));
            engine.LoadField(159, NativeTutorialOrdinaryProgram(program), []);
            engine.Step();
            return engine;
        }
        foreach (int argument in new[] { 0, 255, 256, -1, 0x12345 })
        {
            byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, argument), (Jsm.Opcode.TUTO, 0), (Jsm.Opcode.PSHN_L, argument), (Jsm.Opcode.MENUTIPS, 0), (Jsm.Opcode.PSHN_L, 33), (Jsm.Opcode.POPM_W, 256), (Jsm.Opcode.RET, 0));
            EventEngine engine = Create(program);
            List<ScriptCommand> commands = [];
            engine.CommandExecuted += commands.Add;
            engine.Apply(new PlayerAction(PlayerActionKind.Wait));
            NativeTutorialRequest request = engine.Scene.NativeTutorial ?? throw new InvalidOperationException("TUTO did not request its native UI.");
            Require(engine.NextTrigger.Kind == PlayerTriggerKind.Tutorial, "Pending native tutorial did not publish its own input trigger.");
            JsonElement paused = engine.Capture();
            JsonElement owner = paused.GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean());
            Require(request is { Version: 1, FieldId: 159, Frame: 2, ObjectId: 0, ScriptId: 1, Operation: 4, MenuId: 29 } && request.RawArgument == argument && request.TutorialId == unchecked((byte)argument) && engine.Scene.MinimalMenuCompletions.Count == 0 && !engine.IsDialogueOpen && engine.Scene.UnlockedTutorials.Count == 0 && !CapturedVariables(paused).TryGetProperty("256", out _) && owner.GetProperty("Position").GetInt32() == 5 && owner.GetProperty("Stack").GetArrayLength() == 0 && owner.GetProperty("WaitReason").GetString() == "NativeTutorial" && owner.GetProperty("WaitUntil").GetInt64() == 0, "TUTO did not preserve its low BYTE, consumed stack and advanced PC without completing or writing story state.");
            engine.Step();
            engine.Update(TimeSpan.FromSeconds(2));
            Require(!engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 10)).Accepted && !engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && !engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted && !engine.CanAcceptMovementInput && !engine.IsUserControlEnabled && JsonElement.DeepEquals(paused, engine.Capture()), "Ordinary ticks or field input resumed a pending tutorial.");
            EventEngine independent = Create(program);
            independent.Restore(paused);
            Require(JsonElement.DeepEquals(paused, independent.Capture()), "Pending tutorial failed an independent snapshot round trip.");
            foreach (string property in new[] { "Version", "FieldId", "Frame", "ObjectId", "ScriptId", "Operation", "FiberSequence", "RawArgument", "MenuId", "TutorialId" })
            {
                JsonNode invalid = JsonNode.Parse(paused.GetRawText())!;
                JsonNode record = invalid["Scene"]!["NativeTutorial"]!;
                record[property] = property == "TutorialId" ? (record[property]!.GetValue<long>() + 1) & 255 : record[property]!.GetValue<long>() + 1;
                Reject(invalid, property);
            }
            foreach (string property in new[] { "Position", "WaitUntil", "Sequence" })
            {
                JsonNode invalid = JsonNode.Parse(paused.GetRawText())!;
                JsonNode fiber = invalid["Fibers"]![1]!;
                fiber[property] = fiber[property]!.GetValue<long>() + 1;
                Reject(invalid, property);
            }
            JsonNode absentRaw = JsonNode.Parse(paused.GetRawText())!;
            absentRaw["Scene"]!["NativeTutorial"]!.AsObject().Remove("RawArgument");
            try
            {
                independent.Restore(JsonSerializer.SerializeToElement(absentRaw));
                throw new InvalidOperationException("Tutorial with absent original raw operand passed restore.");
            }
            catch (JsonException)
            {
            }
            Require(JsonElement.DeepEquals(paused, independent.Capture()), "Absent tutorial provenance partially changed field state.");
            JsonNode invalidPlayback = JsonNode.Parse(paused.GetRawText())!;
            invalidPlayback["Scene"]!["NativeTutorialPlayback"] = 3;
            Reject(invalidPlayback, "non-object playback");
            JsonNode withPlayback = JsonNode.Parse(paused.GetRawText())!;
            withPlayback["Scene"]!["NativeTutorialPlayback"] = new JsonObject { ["Cursor"] = 17 };
            independent.Restore(JsonSerializer.SerializeToElement(withPlayback));
            Require(independent.Scene.NativeTutorialPlayback?.GetProperty("Cursor").GetInt32() == 17, "Pending tutorial lost its host playback state.");
            independent.Restore(paused);
            JsonNode missing = JsonNode.Parse(paused.GetRawText())!;
            missing["Scene"]!.AsObject().Remove("NativeTutorial");
            Reject(missing, "missing request");
            JsonNode orphan = JsonNode.Parse(paused.GetRawText())!;
            orphan["Fibers"]![1]!["WaitReason"] = "None";
            Reject(orphan, "missing owner wait");
            engine.CompleteNativeTutorial();
            independent.CompleteNativeTutorial();
            Require(JsonElement.DeepEquals(engine.Capture(), independent.Capture()) && engine.Frame == 3 && engine.Scene.NativeTutorial is null && engine.Scene.UnlockedTutorials.SetEquals([argument & 127]) && CapturedVariables(engine.Capture()).GetProperty("256").GetInt32() == 33 && commands.Count(command => command.Name == "TUTO") == 1 && commands.Any(command => command.Name == "MENUTIPS"), "Explicit tutorial close replayed the request, added a frame, changed MENUTIPS masking or lost original continuation.");
            try
            {
                engine.CompleteNativeTutorial();
                throw new InvalidOperationException("Duplicate tutorial close was accepted.");
            }
            catch (InvalidOperationException exception) when (exception.Message.StartsWith("No native tutorial", StringComparison.Ordinal))
            {
            }

            // Historical automatic receipts remain historical completed requests.
            JsonNode legacy = JsonNode.Parse(paused.GetRawText())!;
            legacy["Scene"]!.AsObject().Remove("NativeTutorial");
            legacy["Scene"]!["MinimalMenuCompletions"] = JsonSerializer.SerializeToNode(new[] { new MinimalMenuCompletion(1, MinimalMenuKind.Tutorial, 159, request.ProgramHash, 2, 0, 1, 4, argument, 29, unchecked((byte)argument), null) });
            legacy["Scene"]!["MinimalMenuWaits"] = JsonSerializer.SerializeToNode(new Dictionary<long, int> { [request.FiberSequence] = 0 });
            legacy["Scene"]!["Names"]!["0"] = "Historical Squall";
            legacy["Fibers"]![1]!["WaitReason"] = "MinimalMenu";
            legacy["Fibers"]![1]!["WaitUntil"] = 3;
            EventEngine historical = Create(program);
            historical.Restore(JsonSerializer.SerializeToElement(legacy));
            historical.Step();
            Require(historical.Scene.NativeTutorial is null && historical.Scene.Names[0] == "Historical Squall" && historical.Scene.MinimalMenuCompletions.Single().Kind == MinimalMenuKind.Tutorial && CapturedVariables(historical.Capture()).GetProperty("256").GetInt32() == 33, "Historical tutorial receipt was replayed or its original name/story continuation changed.");

            void Reject(JsonNode invalid, string property)
            {
                try
                {
                    independent.Restore(JsonSerializer.SerializeToElement(invalid));
                    throw new InvalidOperationException($"Invalid tutorial {property} passed restore.");
                }
                catch (InvalidDataException)
                {
                }
                Require(JsonElement.DeepEquals(paused, independent.Capture()), "Invalid tutorial restore partially changed the VM.");
            }
        }
        byte[] browserProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.MENUTUTO, 0), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0));
        EventEngine browser = Create(browserProgram);
        browser.Step();
        Require(browser.Scene.NativeTutorial is { RawArgument: 0, MenuId: 26, TutorialId: null } && browser.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean()).GetProperty("Stack")[0].GetInt32() == 77, "MENUTUTO consumed an operand or selected an invented tutorial ID.");
        EventEngine restoredBrowser = Create(browserProgram);
        restoredBrowser.Restore(browser.Capture());
        restoredBrowser.Step();
        Require(restoredBrowser.Scene.NativeTutorial == browser.Scene.NativeTutorial && restoredBrowser.Frame == 2, "Tutorial browser escaped its restored pending hold.");
        browser.CompleteNativeTutorial();
        restoredBrowser.CompleteNativeTutorial();
        Require(JsonElement.DeepEquals(browser.Capture(), restoredBrowser.Capture()) && CapturedVariables(browser.Capture()).GetProperty("5000").GetInt32() == 77 && browser.Scene.UnlockedTutorials.Count == 0, "Tutorial browser lost its untouched stack or unlocked an unrequested tutorial.");
        JsonNode legacyBrowser = JsonNode.Parse(restoredBrowser.Capture().GetRawText())!;
        NativeTutorialRequest browserRequest = new(1, 159, legacyBrowser["ProgramHash"]!.GetValue<string>(), 2, 0, 1, 4, 2, 0, 26, null);
        //The legacy browser receipt already returned; it must never reopen a tutorial.
        legacyBrowser["Scene"]!["MinimalMenuCompletions"] = JsonSerializer.SerializeToNode(new[] { new MinimalMenuCompletion(1, MinimalMenuKind.TutorialBrowser, browserRequest.FieldId, browserRequest.ProgramHash, browserRequest.Frame, browserRequest.ObjectId, browserRequest.ScriptId, browserRequest.Operation, 0, 26, 1, null) });
        EventEngine historicalBrowser = Create(browserProgram);
        historicalBrowser.Restore(JsonSerializer.SerializeToElement(legacyBrowser));
        Require(historicalBrowser.Scene.NativeTutorial is null && historicalBrowser.Scene.MinimalMenuCompletions.Single().Kind == MinimalMenuKind.TutorialBrowser, "Historical completed MENUTUTO receipt reopened its native menu.");
        JsonNode detachedPlayback = JsonNode.Parse(browser.Capture().GetRawText())!;
        detachedPlayback["Scene"]!["NativeTutorialPlayback"] = new JsonObject();
        try
        {
            browser.Restore(JsonSerializer.SerializeToElement(detachedPlayback));
            throw new InvalidOperationException("Detached tutorial playback passed restore.");
        }
        catch (InvalidDataException)
        {
        }
        foreach (bool nested in new[] { false, true })
        {
            byte[] parent = nested ? browserProgram : SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8));
            EventEngine child = Create(parent);
            child.Step();
            NativeTutorialRequest? nativeParent = child.Scene.NativeTutorial;
            long childFrame = child.Frame;
            child.BeginMenuTutorial(4);
            Require(child.Scene.NativeMenuTutorial is { Version: 1, FieldId: 159, TutorialId: 4 } && child.Scene.NativeMenuTutorial.Frame == childFrame && child.NextTrigger.Kind == PlayerTriggerKind.Tutorial, "Browser lesson did not preserve its explicit field owner.");
            JsonElement saved = child.Capture();
            child.Step();
            child.Update(TimeSpan.FromSeconds(1));
            Require(!child.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && JsonElement.DeepEquals(saved, child.Capture()), "Browser lesson did not freeze ordinary engine callbacks/input.");
            EventEngine restoredChild = Create(parent);
            restoredChild.Restore(saved);
            Require(JsonElement.DeepEquals(saved, restoredChild.Capture()), "Browser lesson failed an independent exact snapshot restore.");
            foreach (string property in new[] { "Version", "FieldId", "Frame", "TutorialId" })
            {
                JsonNode invalid = JsonNode.Parse(saved.GetRawText())!;
                JsonNode record = invalid["Scene"]!["NativeMenuTutorial"]!;
                record[property] = property == "TutorialId" ? 9 : record[property]!.GetValue<int>() + 1;
                try
                {
                    restoredChild.Restore(JsonSerializer.SerializeToElement(invalid));
                    throw new InvalidOperationException($"Invalid browser tutorial {property} passed restore.");
                }
                catch (InvalidDataException)
                {
                }
                Require(JsonElement.DeepEquals(saved, restoredChild.Capture()), "Invalid browser tutorial restore partially changed field state.");
            }
            if (nested)
            {
                try
                {
                    child.CompleteNativeTutorial();
                    throw new InvalidOperationException("MENUTUTO returned while its child lesson remained active.");
                }
                catch (InvalidOperationException exception) when (exception.Message.StartsWith("Close the browser-selected", StringComparison.Ordinal))
                {
                }
            }
            child.EndMenuTutorial();
            restoredChild.EndMenuTutorial();
            Require(child.Frame == childFrame && child.Scene.NativeMenuTutorial is null && child.Scene.NativeTutorial == nativeParent && JsonElement.DeepEquals(child.Capture(), restoredChild.Capture()), "Browser lesson close advanced the field or released its parent MENUTUTO.");
        }
        Console.WriteLine("Native tutorials: low-byte TUTO and zero-operand MENUTUTO suspend field input/ticks, round-trip exact pending owners, explicitly resume one budget, reject malformed snapshots and preserve historical completions/MENUTIPS.");
    }
    private static void NativeTutorialOriginalResourceContract(GameAssets assets)
    {
        FieldAssets field = assets.LoadField(159);
        JsmProgram program = JsmProgram.Read(field.Jsm);
        JsmOperation operation = program.Operations.First(item => item.Opcode == Jsm.Opcode.TUTO && item.Index > 0 && program.Operations[item.Index - 1].Opcode == Jsm.Opcode.PSHN_L);
        EventObject obj = program.Objects.Single(item => item.Scripts.Any(script => script.ContainsOperation(operation.Index)));
        EventScript script = obj.Scripts.Single(item => item.ContainsOperation(operation.Index));
        int raw = program.Operations[operation.Index - 1].Parameter;
        EventEngine Create()
        {
            EventEngine engine = new(new GameSession(new GameState()));
            engine.LoadField(field.Id, field.Jsm, field.Messages);
            return engine;
        }
        EventEngine engine = Create();
        JsonNode focused = JsonNode.Parse(engine.Capture().GetRawText())!;
        JsonNode owner = focused["Fibers"]!.AsArray().Single(item => item!["ObjectId"]!.GetValue<int>() == obj.Index)!.DeepClone();
        owner["ScriptId"] = script.Id;
        owner["Position"] = operation.Index - 1;
        owner["Initialization"] = false;
        owner["Started"] = true;
        focused["Fibers"] = new JsonArray(owner);
        focused["DefaultsStarted"] = true;
        focused["Frame"] = 1;
        focused["Scene"]!["FieldCallbackClock"]!["InitializationTransactions"] = 1;
        engine.Restore(JsonSerializer.SerializeToElement(focused));
        engine.Step();
        Require(engine.Scene.NativeTutorial is { MenuId: 29 } request && request.ObjectId == obj.Index && request.ScriptId == script.Id && request.Operation == operation.Index && request.RawArgument == raw && request.TutorialId == unchecked((byte)raw), "Original field TUTO did not retain the issuing resource request and low BYTE.");
        JsonElement pending = engine.Capture();
        EventEngine restored = Create();
        restored.Restore(pending);
        restored.Step();
        Require(JsonElement.DeepEquals(pending, restored.Capture()), "Original-resource tutorial pending request did not restore/freeze independently.");
        Console.WriteLine($"Original field TUTO: field{field.Id}/object{obj.Index}/script{script.Id}/operation{operation.Index}, raw{raw}, exact pending restore verified without route replay (field {field.Name}; module={obj.BackgroundModuleIndex}; area={obj.IsArea}; door={obj.IsDoor}).");
    }

    private static byte[] NativeTutorialOrdinaryProgram(byte[] singleScript)
    {
        int sourceOffset = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(singleScript.AsSpan(6));
        int count = (singleScript.Length - sourceOffset) / 4;
        byte[] ordinary = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], Enumerable.Repeat((Jsm.Opcode.RET, 0), count).ToArray()]);
        int targetOffset = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(ordinary.AsSpan(6)) + 8;
        // Menu ownership belongs to a field module, without inventing physical actor allocation.
        ordinary[2] = 1;
        ordinary[3] = 0;
        singleScript.AsSpan(sourceOffset).CopyTo(ordinary.AsSpan(targetOffset));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(ordinary.AsSpan(targetOffset), (int)Jsm.Opcode.LBL << 24 | 1);
        return ordinary;
    }

}
