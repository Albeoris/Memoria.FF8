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
        EventEngine Create(byte[] program)
        {
            GameSession session = new(new GameState { CharacterVitals = initial.DeepCopy(), FieldButtonInput = NativeFieldButtonInput.FromColdProcess() });
            session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
            EventEngine engine = new(session);
            engine.LoadField(159, NativeTutorialOrdinaryProgram(program), []);
            engine.Step();
            return engine;
        }
        NativeTutorialContract();
        NativeTutorialOriginalResourceContract(assets);

        byte[] naming = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.PSHN_L, 777), (Jsm.Opcode.POPM_W, 384), (Jsm.Opcode.RET, 0));
        EventEngine gf = Create(naming);
        gf.Scene.Names.Add(0, "Leon");
        gf.Scene.Names.Add(3, "Legacy name value");
        gf.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(gf.AcquiredGuardianForces!.SetEquals([0]) && gf.Scene.MinimalMenuCompletions.Single() is { Kind: MinimalMenuKind.GuardianForceName, RawArgument: 3, MenuId: 5, Selection: 1, GuardianForceId: 0 } && !gf.IsDialogueOpen && gf.Scene.Names.Count == 2 && gf.Scene.Names[0] == "Leon" && gf.Scene.Names[3] == "Legacy name value", "MENUNAME3 did not acquire GF0 positively or changed existing character/default names.");
        EventEngine restoredGf = Create(naming);
        restoredGf.Restore(gf.Capture());
        for (int tick = 0; tick < 2; tick++)
        {
            gf.Apply(new PlayerAction(PlayerActionKind.Wait));
            restoredGf.Apply(new PlayerAction(PlayerActionKind.Wait));
            Require(JsonElement.DeepEquals(gf.Capture(), restoredGf.Capture()), "Independent GF naming-yield restore changed acquisition, request provenance or native script continuation.");
        }
        Require(gf.Frame == 4 && gf.AcquiredGuardianForces!.SetEquals([0, 1]) && gf.Scene.MinimalMenuCompletions[1] is { RawArgument: 4, MenuId: 6, GuardianForceId: 1 } && CapturedVariables(gf.Capture()).GetProperty("384").GetInt32() == 777 && !gf.Scene.Names.ContainsKey(4), "MENUNAME4 did not acquire GF1 before the original successor write or invented a name.");
        MinimalMenuCompletion[] history = gf.Scene.MinimalMenuCompletions.ToArray();
        byte[] next = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0));
        gf.LoadField(160, next, []);
        Require(gf.AcquiredGuardianForces!.SetEquals([0, 1]) && gf.Scene.MinimalMenuCompletions.SequenceEqual(history) && gf.Scene.MinimalMenuWaits.Count == 0 && gf.Scene.Names[0] == "Leon", "Field load lost known GF acquisitions/request history or carried an old field's pending yield.");
        EventEngine nextRestore = Create(naming);
        nextRestore.LoadField(160, next, []);
        nextRestore.Restore(gf.Capture());
        Require(JsonElement.DeepEquals(gf.Capture(), nextRestore.Capture()), "Cross-field completion history did not save/restore independently.");

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
            JsonElement fiber = refused.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean());
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
        Require(inventory.LastFault is { Opcode: "ADDITEM", Operation: 5 } && inventory.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean()).GetProperty("Stack").GetArrayLength() == 2, "Minimal tutorial policy swallowed an original inventory data boundary.");

        EventEngine validation = Create(naming);
        validation.Step();
        JsonElement valid = validation.Capture();
        foreach (string property in new[] { "Version", "MenuId", "Selection", "GuardianForceId", "Operation" })
        {
            JsonNode invalid = JsonNode.Parse(valid.GetRawText())!;
            JsonNode record = invalid["Scene"]!["MinimalMenuCompletions"]![0]!;
            record[property] = record[property]!.GetValue<int>() + 1;
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
        Console.WriteLine("Minimal menus: GF3/4 acquisition retains its next-update yield, names and legacy completion history; malformed requests and inventory grants refused.");
    }
}
