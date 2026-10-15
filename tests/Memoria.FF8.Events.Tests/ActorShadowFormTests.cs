using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeActorShadowFormContract()
    {
        EventEngine Create(byte[] content, bool authenticated = true)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session);
            engine.LoadField(100, content, [], nativeRuntimeProfile: authenticated ? NativeFieldRuntimeProfile.FF8En : null);
            return engine;
        }
        foreach (int[] sizes in new int[][] { [50, 200, 50, 50, 50, 100, 50, 50], [0, 1, 3, -1, -3, 4, -4, 300], [1024, -1024, 1027, -1027, 1028, -1028, 0x7fffff, -0x800000] })
        {
            List<(Jsm.Opcode, int)> instructions = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.SHADELEVEL, 0), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.SHADESET, 0)];
            instructions.AddRange(sizes.Select(size => (Jsm.Opcode.PSHN_L, size)));
            instructions.AddRange([(Jsm.Opcode.SHADEFORM, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.SHADESET, 0), (Jsm.Opcode.RET, 0)]);
            byte[] content = SyntheticProgram(instructions.ToArray());
            EventEngine engine = Create(content);
            Require(engine.Actors[0].NativeShadowForm is null, "Allocation invented an executed SHADEFORM receipt.");
            engine.Step();
            ActorState actor = engine.Actors[0];
            byte[] expected = Enumerable.Range(0, 8).Select(index => unchecked((byte)(sizes[(index + 4) & 7] / 4))).ToArray();
            Require(actor.NativeShadowSet is null && actor.NativeShadowForm is { } form && form.RawSizes.SequenceEqual(sizes) && form.Radii601To608.SequenceEqual(expected) && actor.NativeShadowLevel is { Level609: 255 } && actor.ShadeLevel == 255 && actor.PolygonColor == new Vector3(128), "SHADEFORM changed native division, BYTE order, current-shape replacement or independent color.");
            Require(CapturedVariables(engine.Capture()).GetProperty("500").GetInt32() == 7, "SHADEFORM did not return immediately to the following script store.");
            JsonElement saved = engine.Capture();
            EventEngine restored = Create(content);
            restored.Restore(saved);
            Require(JsonElement.DeepEquals(saved, restored.Capture()), "Asymmetric shadow geometry did not capture/restore independently.");
            foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
            {
                node => node["Actors"]![0]!["NativeShadowForm"]!["Radii601To608"]![0] = unchecked((byte)(expected[0] + 1)),
                node => node["Actors"]![0]!["NativeShadowForm"]!["RawSizes"] = new JsonArray(0),
                node => node["Actors"]![0]!["NativeShadowForm"]!["Operation"] = 4,
                node => node["Actors"]![0]!["NativeShadowForm"]!["Frame"] = saved.GetProperty("Frame").GetInt64() + 1
            })
            {
                JsonNode malformed = JsonNode.Parse(saved.GetRawText())!;
                corrupt(malformed);
                string before = restored.Capture().GetRawText();
                try
                {
                    restored.Restore(JsonSerializer.SerializeToElement(malformed));
                    throw new InvalidOperationException("Malformed SHADEFORM authority restored.");
                }
                catch (InvalidDataException)
                {
                }
                Require(before == restored.Capture().GetRawText(), "Malformed SHADEFORM refusal mutated the VM.");
            }
            for (int frame = 0; frame < 5; frame++)
            {
                engine.Step();
                restored.Step();
            }
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()) && actor.NativeShadowForm is null && actor.NativeShadowSet is { Radius601To608: 4 }, "Subsequent SHADESET did not replace all eight current form bytes after restore.");
            engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            Require(engine.Actors[0].NativeShadowForm is null && engine.Actors[0].NativeShadowSet is null, "New allocation retained prior field shadow geometry.");
        }
        foreach (bool authenticated in new[] { false, true })
        {
            List<(Jsm.Opcode, int)> instructions = [(Jsm.Opcode.LBL, 0)];
            instructions.AddRange(Enumerable.Range(0, 8).Select(index => (Jsm.Opcode.PSHN_L, index * 4)));
            if (authenticated)
            {
                instructions.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0)]);
            }
            instructions.AddRange([(Jsm.Opcode.SHADEFORM, 0), (Jsm.Opcode.RET, 0)]);
            EventEngine engine = Create(SyntheticProgram(instructions.ToArray()), authenticated);
            if (authenticated)
            {
                engine.Step();
                // A deliberately malformed synthetic checkpoint verifies runtime
                // underflow refusal; the JSM expression parser requires eight pushes.
                JsonNode incomplete = JsonNode.Parse(engine.Capture().GetRawText())!;
                incomplete["Fibers"]![0]!["Stack"]!.AsArray().RemoveAt(0);
                incomplete["Fibers"]![0]!["StackExpressions"]!.AsArray().RemoveAt(0);
                engine.Restore(JsonSerializer.SerializeToElement(incomplete));
            }
            try
            {
                engine.Step();
                throw new InvalidOperationException("Unproved or incomplete SHADEFORM operands acquired geometry authority.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == "SHADEFORM")
            {
            }
            JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
            Require(engine.Actors[0].NativeShadowForm is null && fiber.GetProperty("Position").GetInt32() == (authenticated ? 11 : 9) && fiber.GetProperty("Stack").GetArrayLength() == (authenticated ? 7 : 8), "SHADEFORM preflight refusal consumed the original PC or operands.");
        }
    }
}
