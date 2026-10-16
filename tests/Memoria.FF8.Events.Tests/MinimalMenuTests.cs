using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void MinimalMenuCompletionContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        byte[] SyntheticProgram(params (Jsm.Opcode Opcode, int Parameter)[] operations)
        {
            List<(Jsm.Opcode Opcode, int Parameter)> main = [(Jsm.Opcode.LBL, 1)];
            main.AddRange(operations.Skip(1).SkipLast(1));
            main.Add((Jsm.Opcode.HALT, 0));
            main.Add((Jsm.Opcode.RET, 8));
            return SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], main.ToArray()]);
        }
        JsonElement Fiber(JsonElement snapshot) => snapshot.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
        EventEngine Create(byte[] program)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.Restore(session.State with { CharacterVitals = initial.DeepCopy(), WorldControls = NativeWorldControlState.CreateNewGame(), InputConfiguration = assets.ReadNewGameInputConfiguration() });
            EventEngine engine = new(session);
            engine.LoadField(159, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        NativeTutorialContract();
        NativeTutorialOriginalResourceContract(assets);

        byte[] naming = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.PSHN_L, 777), (Jsm.Opcode.POPM_W, 384), (Jsm.Opcode.RET, 0));
        EventEngine gf = Create(naming);
        gf.Scene.Names.Add(0, "Leon");
        gf.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(gf.AcquiredGuardianForces!.SetEquals([0]) && gf.Scene.MinimalMenuCompletions.Count == 0 && gf.CurrentDialogue is { IsNamePrompt: true } && gf.PendingNameSubject == GameNames.GuardianForceSubject(0) && gf.PendingNameDefault == "Quezacotl" && gf.Scene.Names.Count == 2 && gf.Scene.Names[0] == "Leon" && gf.Scene.Names[GameNames.GuardianForceSubject(0)] == "Quezacotl", "MENUNAME3 did not acquire GF0 before its interactive name prompt or changed existing names.");
        EventEngine restoredGf = Create(naming);
        restoredGf.Restore(gf.Capture());
        foreach (PlayerAction action in new[] { new PlayerAction(PlayerActionKind.Wait), new PlayerAction(PlayerActionKind.EnterName, Text: "Storm"), new PlayerAction(PlayerActionKind.Wait), new PlayerAction(PlayerActionKind.EnterName, Text: "Frost"), new PlayerAction(PlayerActionKind.Wait) })
        {
            gf.Apply(action);
            restoredGf.Apply(action);
            Require(JsonElement.DeepEquals(gf.Capture(), restoredGf.Capture()), "Independent GF name-prompt restore changed acquisition, names or native script continuation.");
        }
        Require(gf.AcquiredGuardianForces!.SetEquals([0, 1]) && gf.Scene.MinimalMenuCompletions.Count == 0 && CapturedVariables(gf.Capture()).GetProperty("384").GetInt32() == 777 && gf.Scene.Names[GameNames.GuardianForceSubject(0)] == "Storm" && gf.Scene.Names[GameNames.GuardianForceSubject(1)] == "Frost" && gf.Scene.Names[0] == "Leon", "Consecutive GF naming did not require explicit names before the original successor write or changed the character name.");
        gf.Scene.MinimalMenuCompletions.Add(new MinimalMenuCompletion(1, MinimalMenuKind.GuardianForceName, 159, gf.Capture().GetProperty("ProgramHash").GetString()!, 2, 0, 1, 4, 3, 5, 1, 0));
        restoredGf.Restore(gf.Capture());
        Require(JsonElement.DeepEquals(gf.Capture(), restoredGf.Capture()), "Historical automatic GF naming receipt stopped restoring after interactive naming replaced new receipts.");
        MinimalMenuCompletion[] history = gf.Scene.MinimalMenuCompletions.ToArray();
        byte[] next = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0));
        gf.LoadField(160, next, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(gf.AcquiredGuardianForces!.SetEquals([0, 1]) && gf.Scene.MinimalMenuCompletions.SequenceEqual(history) && gf.Scene.MinimalMenuWaits.Count == 0 && gf.Scene.Names[0] == "Leon", "Field load lost known GF acquisitions/request history or carried an old field's pending yield.");
        EventEngine nextRestore = Create(naming);
        nextRestore.LoadField(160, next, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        nextRestore.Restore(gf.Capture());
        Require(JsonElement.DeepEquals(gf.Capture(), nextRestore.Capture()), "Cross-field completion history did not save/restore independently.");

        int[] guardianForceIds = [0, 1, 2, 3, 4, 6, 5, 7, 8, 9, 10, 12, 11, 13, 14, 15];
        for (int index = 0; index < guardianForceIds.Length; index++)
        {
            int argument = index + 3;
            int id = guardianForceIds[index];
            int subject = GameNames.GuardianForceSubject(id);
            byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, argument), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.PSHN_L, 777), (Jsm.Opcode.POPM_W, 384), (Jsm.Opcode.RET, 0));
            EventEngine named = Create(program);
            JsonNode expectedVitals = JsonNode.Parse(named.Capture().GetProperty("CharacterVitals").GetRawText())!;
            JsonNode expectedGuardianForce = expectedVitals["GuardianForces"]![id]!;
            expectedGuardianForce["Flags"] = expectedGuardianForce["Flags"]!.GetValue<byte>() | 1;
            named.Step();
            JsonElement prompt = named.Capture();
            Require(named.AcquiredGuardianForces!.SetEquals([id]) && named.CurrentDialogue is { IsNamePrompt: true } && named.PendingNameSubject == subject && named.PendingNameDefault == GameNames.DefaultName(subject) && named.Scene.MinimalMenuCompletions.Count == 0 && named.Scene.MinimalMenuWaits.Count == 0 && named.Scene.Names[subject] == GameNames.DefaultName(subject) && !CapturedVariables(prompt).TryGetProperty("384", out _), $"MENUNAME{argument} did not acquire the native GF{id} and block its successor on the correct name subject.");
            Require(JsonNode.DeepEquals(expectedVitals, JsonNode.Parse(prompt.GetProperty("CharacterVitals").GetRawText())), $"MENUNAME{argument} acquisition changed saved HP, stats or other native inputs beyond GF{id} flag1.");
            EventEngine restored = Create(program);
            restored.Restore(prompt);
            Require(restored.CurrentDialogue is { IsNamePrompt: true } && restored.PendingNameSubject == subject && restored.PendingNameDefault == GameNames.DefaultName(subject), $"MENUNAME{argument} lost its pending name subject/default after restore.");
            foreach (EventEngine candidate in new[] { named, restored })
            {
                candidate.Apply(new PlayerAction(PlayerActionKind.Wait));
                Require(candidate.CurrentDialogue is { IsNamePrompt: true } && !CapturedVariables(candidate.Capture()).TryGetProperty("384", out _), $"MENUNAME{argument} accepted an ordinary wait as name confirmation.");
                candidate.Apply(new PlayerAction(PlayerActionKind.EnterName, Text: $"Chosen GF{id}"));
                candidate.Apply(new PlayerAction(PlayerActionKind.Wait));
                Require(!candidate.IsDialogueOpen && candidate.Scene.Names[subject] == $"Chosen GF{id}" && candidate.Scene.MinimalMenuCompletions.Count == 0 && CapturedVariables(candidate.Capture()).GetProperty("384").GetInt32() == 777 && JsonElement.DeepEquals(prompt.GetProperty("CharacterVitals"), candidate.Capture().GetProperty("CharacterVitals")), $"MENUNAME{argument} acceptance changed saved HP/stats, wrote the wrong name or failed original script continuation.");
            }
            Require(JsonElement.DeepEquals(named.Capture(), restored.Capture()), $"MENUNAME{argument} restored prompt diverged after explicit acceptance.");
        }

        foreach (int characterId in new[] { 0, 1, 2 })
        {
            byte[] character = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, characterId), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.RET, 0));
            EventEngine named = Create(character);
            named.Step();
            Require(named.CurrentDialogue is { IsNamePrompt: true } && named.AcquiredGuardianForces!.Count == 0 && named.Scene.MinimalMenuCompletions.Count == 0, "Existing character naming was replaced by automatic GF completion.");
            named.Apply(new PlayerAction(PlayerActionKind.EnterName, Text: "Chosen name"));
            Require(named.Scene.Names[characterId] == "Chosen name" && !named.IsDialogueOpen, "Existing ordinary character naming stopped accepting its normal action.");
        }

        foreach (int argument in new[] { -1, 259 })
        {
            EventEngine refused = Create(SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, argument), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.RET, 0)));
            try
            {
                refused.Step();
                throw new InvalidOperationException("Unsupported MENUNAME target was silently interpreted as a character/GF.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement fiber = Fiber(refused.Capture());
            Require(refused.LastFault is { Opcode: "MENUNAME", Operation: 4 } && fiber.GetProperty("Position").GetInt32() == 4 && fiber.GetProperty("Stack")[0].GetInt32() == argument && refused.AcquiredGuardianForces!.Count == 0 && refused.Scene.MinimalMenuCompletions.Count == 0 && !refused.IsDialogueOpen, "Unsupported naming consumed its original operand/PC or acquired a GF.");
        }
        EventEngine inventory = Create(SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.ADDITEM, 0), (Jsm.Opcode.RET, 0)));
        try
        {
            inventory.Step();
            throw new InvalidOperationException("Inventory grant became a presentation-only completion.");
        }
        catch (EventScriptException)
        {
        }
        Require(inventory.LastFault is { Opcode: "ADDITEM", Operation: 5 } && Fiber(inventory.Capture()).GetProperty("Stack").GetArrayLength() == 2, "Minimal tutorial policy swallowed an original inventory data boundary.");

        EventEngine validation = Create(SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.RET, 0)));
        validation.Step();
        validation.Scene.MinimalMenuCompletions.Add(new MinimalMenuCompletion(1, MinimalMenuKind.GuardianForceName, 159, validation.Capture().GetProperty("ProgramHash").GetString()!, 2, 0, 1, 4, 3, 5, 1, 0));
        JsonElement valid = validation.Capture();
        foreach (string property in new[] { "Version", "MenuId", "Selection", "GuardianForceId", "Operation" })
        {
            JsonNode invalid = JsonNode.Parse(valid.GetRawText())!;
            JsonNode record = invalid["Scene"]!["MinimalMenuCompletions"]![0]!;
            record[property] = property == "Operation" ? -1 : record[property]!.GetValue<int>() + 1;
            try
            {
                validation.Restore(JsonSerializer.SerializeToElement(invalid));
                throw new InvalidOperationException($"Invalid minimal completion {property} passed restore.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(valid, validation.Capture()), "Invalid minimal menu restore partially changed the VM.");
        }
        Require(!nextRestore.Capture().GetProperty("Scene").TryGetProperty("AcquiredGuardianForces", out _) && nextRestore.AcquiredGuardianForces!.SetEquals([0, 1]), "GF acquisition retained a duplicate serialized Scene authority.");
        Console.WriteLine("Minimal menus: native tutorials retain explicit ownership; all sixteen GF targets acquire at issue and require explicit naming, restore pending prompts and preserve HP/stats; historical receipts remain valid and malformed requests are refused.");
    }
}
