using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeModelDoorContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldModel model = assets.LoadField(919).Models[13];
        NativeModelPartsDefinition definition = model.NativeParts!;
        EventEngine Create((Jsm.Opcode, int)[] main, bool provider = true, bool peer = false, NativeModelPartsDefinition? originalParts = null)
        {
            List<(Jsm.Opcode, int)[]> scripts = [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 13), (Jsm.Opcode.RET, 8)], main];
            if (peer)
            {
                scripts.Add([(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 13), (Jsm.Opcode.RET, 8)]);
                scripts.Add([(Jsm.Opcode.LBL, 9), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            }
            GameSession session = GameSession.CreateColdProcess();
            EventEngine engine = new(session) { ReadNativeModelParts = provider ? id => id == 13 ? originalParts ?? definition : null : null };
            engine.LoadField(600, SyntheticObjects(peer ? [1, (8 << 7) | 1] : [1], scripts), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        (Jsm.Opcode, int)[] commands = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MODELDOOROPEN, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.MODELDOORCLOSE, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 6000), (Jsm.Opcode.PSHN_L, 30), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MODELDOORCLOSE, 0), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.POPM_L, 6001), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        EventEngine live = Create(commands);
        live.Step();
        Require(live.Scene.ModelParts?.Resources[13].Parts[1][7] == 0, "Normal model registration did not execute flags-only allocation.");
        live.Step();
        Require(CapturedVariables(live.Capture()).GetProperty("6000").GetInt32() == 77 && live.Scene.ModelParts!.Resources[13].Parts[1][12] == 32 && live.Scene.ModelParts.Resources[13].Parts[1][14] == 207, "Model-door return2 or busy request drop changed the original command contract.");
        Require(live.GetNativeModelPartTranslations(0)![1] == new Vector3(1, 0, 6) && live.Scene.ModelParts!.Resources[13].Parts[1][2] == 2, "Command callback did not produce the counter1 skin sample before advancing to2.");
        for (int tick = 0; tick < 9; tick++)
        {
            live.Step();
        }
        JsonElement saved = live.Capture();
        EventEngine restored = Create(commands);
        restored.Restore(saved);
        for (int tick = 0; tick < 50; tick++)
        {
            JsonElement before = live.Capture();
            Vector3[] first = model.Pose(0, 0, skinTranslations: live.GetNativeModelPartTranslations(0));
            Vector3[] second = model.Pose(0, 0, skinTranslations: live.GetNativeModelPartTranslations(0));
            Require(first.SequenceEqual(second) && JsonElement.DeepEquals(before, live.Capture()), "Repeated model rendering mutated native callback state.");
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Native model-part restore changed later callback samples or command completion.");
        }
        Require(live.Scene.ModelParts!.Resources[13].Parts[1][7] == 4 && live.Scene.ModelParts.Resources[13].Parts[1][12] == 0 && live.Scene.ModelParts.Resources[13].Parts[1][14] == 0 && live.GetNativeModelPartTranslations(0)![1] == Vector3.Zero, "Model-door close failed retained target subtraction or persistent terminal placement.");

        (Jsm.Opcode, int)[] open = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MODELDOOROPEN, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        EventEngine shared = Create(open, peer: true);
        shared.Step();
        shared.Step();
        Require(shared.Scene.ModelParts!.Resources.Count == 1 && shared.Scene.ModelParts.Instances.Length == 2 && !shared.Actors[1].Active && shared.GetNativeModelPartTranslations(0)![1] == new Vector3(1, 0, 6) && shared.GetNativeModelPartTranslations(1)![1] == new Vector3(2, 0, 13) && shared.Scene.ModelParts.Resources[13].Parts[1][2] == 3, "Registered shared-resource instances did not tick in order independently of UNUSE.");

        foreach (bool known in new[] { true, false })
        {
            (Jsm.Opcode, int)[] invalid = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, known ? 257 : 1), (Jsm.Opcode.MODELDOOROPEN, 0), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.POPM_L, 6000), (Jsm.Opcode.RET, 8)];
            EventEngine blocked = Create(invalid, provider: known);
            blocked.Step();
            JsonElement before = blocked.Capture().GetProperty("Scene").GetProperty("ModelParts");
            try
            {
                blocked.Step();
                throw new InvalidOperationException("Model-door command accepted an unsupported selector/resource.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement failed = blocked.Capture();
            JsonElement fiber = failed.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
            Require(blocked.LastFault?.Opcode == "MODELDOOROPEN" && fiber.GetProperty("Position").GetInt32() == blocked.LastFault.Operation && fiber.GetProperty("Stack")[0].GetInt32() == (known ? 257 : 1) && !CapturedVariables(failed).TryGetProperty("6000", out _) && JsonElement.DeepEquals(before, failed.GetProperty("Scene").GetProperty("ModelParts")), "Model-door refusal changed PC, operands, memory or physical part rows.");
        }
        JsonNode malformed = JsonNode.Parse(saved.GetRawText())!;
        malformed["Scene"]!.AsObject().Remove("ModelParts");
        EventEngine fresh = Create(commands);
        fresh.Step();
        JsonElement unchanged = fresh.Capture();
        try
        {
            fresh.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Native model restore accepted an old missing owner.");
        }
        catch (JsonException)
        {
        }
        Require(JsonElement.DeepEquals(unchanged, fresh.Capture()), "Missing model-part owner refusal partially mutated the VM.");
        Action<JsonNode>[] corruptions =
        [
            snapshot => snapshot["Scene"]!["ModelParts"]!["Draws"]!.AsObject().Remove("0"),
            snapshot => snapshot["Scene"]!["ModelParts"]!["Draws"]!["0"]!["Frame"] = saved.GetProperty("Frame").GetInt64() - 1,
            snapshot => snapshot["Scene"]!["ModelParts"]!["Draws"]!["0"]!["ModelId"] = 0,
            snapshot => snapshot["Scene"]!["ModelParts"]!["Draws"]!["0"]!["SkinTranslations"]!["1"]!["X"] = 0.5,
            snapshot => snapshot["Scene"]!["ModelParts"]!["Draws"]!["0"]!["SkinTranslations"]!["1"]!["Z"] = 100000,
            snapshot => snapshot["Scene"]!["ModelParts"]!["Draws"]!["0"]!["SkinTranslations"]!.AsObject().Remove("1"),
            snapshot => snapshot["Scene"]!["ModelParts"]!["Instances"]![0] = null,
            snapshot => snapshot["Scene"]!["ModelParts"]!["Resources"]!["13"] = null,
            snapshot => snapshot["Scene"]!["ModelParts"]!["Resources"]!["13"]!["Parts"]![0]![2] = 999,
            snapshot => snapshot["Scene"]!["ModelParts"]!["Resources"]!["13"]!["Parts"]![1]![14] = 100000
        ];
        foreach (Action<JsonNode> corrupt in corruptions)
        {
            JsonNode invalid = JsonNode.Parse(saved.GetRawText())!;
            corrupt(invalid);
            try
            {
                fresh.Restore(JsonSerializer.SerializeToElement(invalid));
                throw new InvalidOperationException("Native model restore accepted a missing, malformed or obsolete skin/body sample.");
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException)
            {
            }
            Require(JsonElement.DeepEquals(unchanged, fresh.Capture()), "Malformed native model restore partially mutated the VM.");
        }
        EventEngine missingDraw = Create(open);
        missingDraw.Step();
        missingDraw.Scene.ModelParts!.Draws.Remove(0);
        try
        {
            missingDraw.GetNativeModelPartTranslations(0);
            throw new InvalidOperationException("Missing executed native skin sample silently rendered base geometry.");
        }
        catch (NotSupportedException)
        {
        }
        short[] overlappingPart = definition.Parts[2].Words.ToArray();
        overlappingPart[0] = 1;
        NativeModelPartsDefinition[] overlappingDefinitions =
        [
            definition with { Parts = [definition.Parts[0], definition.Parts[1], new NativeModelPartDefinition(overlappingPart)] },
            definition with { SkinBindings = [definition.SkinBindings[0], definition.SkinBindings[1], definition.SkinBindings[2] with { FirstVertex = 990 }] }
        ];
        foreach (NativeModelPartsDefinition overlap in overlappingDefinitions)
        {
            EventEngine blocked = Create(open, originalParts: overlap);
            blocked.Step();
            JsonElement before = blocked.Capture().GetProperty("Scene").GetProperty("ModelParts");
            try
            {
                blocked.Step();
                throw new InvalidOperationException("Native model translation silently merged overlapping part geometry.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement failed = blocked.Capture();
            JsonElement fiber = failed.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
            Require(blocked.LastFault?.Opcode == "MODELDOOROPEN" && fiber.GetProperty("Position").GetInt32() == blocked.LastFault.Operation && fiber.GetProperty("Stack")[0].GetInt32() == 1 && JsonElement.DeepEquals(before, failed.GetProperty("Scene").GetProperty("ModelParts")), "Overlapping native geometry refusal changed PC, operands or physical rows.");
        }
        EventEngine changedModel = Create([(Jsm.Opcode.LBL, 1), (Jsm.Opcode.SETMODEL, 14), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        changedModel.Step();
        changedModel.Step();
        Require(changedModel.Scene.ModelParts!.Instances[0].ModelId == 13 && changedModel.Actors[0].ModelId == 14, "SETMODEL silently replaced the retained native instance registration.");
        try
        {
            changedModel.GetNativeModelPartTranslations(0);
            throw new InvalidOperationException("Later SETMODEL silently reused a different retained resource skin sample.");
        }
        catch (NotSupportedException)
        {
        }
    }
}
