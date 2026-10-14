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
    private static void NativePolygonColorContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeModelPartsDefinition definition = assets.LoadField(895).Models[6].NativeParts!;
        List<(Jsm.Opcode, int)[]> scripts =
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 257), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.PSHN_L, 768), (Jsm.Opcode.POLYCOLORALL, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.PSHN_L, 65), (Jsm.Opcode.PSHN_L, 66), (Jsm.Opcode.POLYCOLORALL, 0), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.POPM_L, 5001), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 6), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.PSHN_L, 17), (Jsm.Opcode.PSHN_L, 34), (Jsm.Opcode.PSHN_L, 51), (Jsm.Opcode.POLYCOLOR, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 6), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]
        ];
        byte[] content = SyntheticObjects([1, (8 << 7) | 1, (16 << 7) | 1], scripts);
        content[0] = 1;
        content[3] = 2;
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            EventEngine engine = new(session, session.State.Variables) { ReadNativeModelParts = id => id == 6 ? definition : null };
            engine.LoadField(600, content, [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
            return engine;
        }
        EventEngine live = Create();
        Require(live.Actors[0].PolygonColor is null && live.Actors[1].PolygonColor == new Vector3(128) && live.Actors[2].PolygonColor == new Vector3(128), "Native allocation invented script RGB or omitted its actual actor128 producer.");
        live.Step();
        Require(live.Actors[0].PolygonColor is null && live.Actors[1].PolygonColor == new Vector3(1, 255, 0) && live.Actors[2].PolygonColor == new Vector3(1, 255, 0), "Init POLYCOLORALL failed BYTE narrowing or wrote a nonactor record.");
        live.Step();
        Require(live.Actors[1].PolygonColor == new Vector3(17, 34, 51) && live.Actors[2].PolygonColor == new Vector3(1, 255, 0) && !live.Actors[2].Active && live.Scene.ModelParts!.Resources.Count == 1 && live.Capture().GetProperty("Variables").GetProperty("5000").GetInt32() == 77, "Caller-only RGB leaked through shared model resource or failed same-budget successor.");
        JsonElement saved = live.Capture();
        EventEngine restored = Create();
        restored.Restore(saved);
        for (int callback = 0; callback < 4; callback++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "RGB state changed after independent model-instance restore.");
        }
        Require(live.Actors[0].PolygonColor is null && live.Actors[1].PolygonColor == new Vector3(64, 65, 66) && live.Actors[2].PolygonColor == new Vector3(64, 65, 66) && live.Capture().GetProperty("Variables").GetProperty("5001").GetInt32() == 88, "POLYCOLORALL skipped a retained UNUSE body, tinted only the caller or wrote the script record.");

        foreach (int failure in new[] { 0, 1, 2 })
        {
            EventEngine blocked;
            if (failure == 0)
            {
                blocked = new EventEngine(GameSession.CreateColdProcess(), new Dictionary<int, int>());
                blocked.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.POLYCOLOR, 0), (Jsm.Opcode.RET, 8)), []);
            }
            else
            {
                byte[] invalid = SyntheticObjects([1, 8 << 7], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.POLYCOLOR, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.RET, 8)]]);
                invalid[0] = 1;
                invalid[3] = 1;
                blocked = new EventEngine(GameSession.CreateColdProcess(), new Dictionary<int, int>());
                blocked.LoadField(600, invalid, [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
                if (failure == 2)
                {
                    JsonNode malformed = JsonNode.Parse(blocked.Capture().GetRawText())!;
                    malformed["Actors"]![1]!["PolygonColor"]!["X"] = 1.5;
                    JsonElement unchanged = blocked.Capture();
                    try
                    {
                        blocked.Restore(JsonSerializer.SerializeToElement(malformed));
                        throw new InvalidOperationException("Native RGB restore accepted a fractional byte.");
                    }
                    catch (InvalidDataException)
                    {
                    }
                    Require(JsonElement.DeepEquals(unchanged, blocked.Capture()), "Malformed RGB restore partially changed native owners.");
                    continue;
                }
            }
            Vector3?[] before = blocked.Actors.Select(actor => actor.PolygonColor).ToArray();
            try
            {
                blocked.Step();
                throw new InvalidOperationException("Native RGB command accepted an unproved allocation or nonactor caller.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement failed = blocked.Capture();
            JsonElement caller = failed.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 0 && item.GetProperty("Initialization").GetBoolean());
            Require(blocked.LastFault?.Opcode == "POLYCOLOR" && caller.GetProperty("Position").GetInt32() == blocked.LastFault.Operation && caller.GetProperty("Stack").GetArrayLength() == 3 && before.SequenceEqual(blocked.Actors.Select(actor => actor.PolygonColor)), "Native RGB refusal changed PC, operands or actor bytes.");
        }
    }
}
