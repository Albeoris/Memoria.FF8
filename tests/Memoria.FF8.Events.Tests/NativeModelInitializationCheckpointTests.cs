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
    private static void NativeModelInitializationCheckpointContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(919);
        NativeModelPartsDefinition definition = field.Models[13].NativeParts!;
        byte[] content = SyntheticObjects([1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 13), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MODELDOOROPEN, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            EventEngine engine = new(session)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                ReadNativeModelParts = model => model == 13 ? definition : null
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        Require(live.Frame == 1 && live.Scene.FieldCallbackClock is { InitializationTransactions: 1, OrdinaryCallbacks: 0, MovieCallbacks: 0 } && live.Scene.ModelParts is { Draws.Count: 0 } && live.Scene.ModelParts.Resources.Values.All(resource => resource.Parts.All(row => row[7] == 0)), "Initial model checkpoint did not stop after registration and before the first actual skin callback.");
        JsonElement initial = live.Capture();
        EventEngine restored = Create();
        restored.Restore(initial);
        Require(JsonElement.DeepEquals(initial, restored.Capture()), "Initial model registration changed during independent restore.");
        live.Step();
        restored.Step();
        Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()) && live.Scene.FieldCallbackClock is { OrdinaryCallbacks: 1 } && live.Scene.ModelParts!.Draws[0].Frame == live.Frame && live.GetNativeModelPartTranslations(0)![1] == new Vector3(1, 0, 6), "First ordinary callback failed to produce the same actual model skin sample after initial restore.");
        JsonElement before = restored.Capture();
        JsonNode missing = JsonNode.Parse(before.GetRawText())!;
        missing["Scene"]!["ModelParts"]!["Draws"]!.AsObject().Remove("0");
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(missing));
            throw new InvalidOperationException("A completed ordinary model callback accepted a missing executed sample.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, restored.Capture()), "Missing post-callback model sample refusal changed the live VM.");
        JsonNode premature = JsonNode.Parse(initial.GetRawText())!;
        premature["Scene"]!["ModelParts"]!["Draws"] = JsonNode.Parse(before.GetProperty("Scene").GetProperty("ModelParts").GetProperty("Draws").GetRawText());
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(premature));
            throw new InvalidOperationException("Initial model registration accepted an unexecuted skin sample.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, restored.Capture()), "Premature model sample refusal changed the live VM.");
    }
}
