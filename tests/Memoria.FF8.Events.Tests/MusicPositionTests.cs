using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMusicPositionContract(string gameDirectory)
    {
        foreach (bool sentinel in new[] { false, true })
        {
            List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0)];
            if (sentinel)
            {
                operations.Add((Jsm.Opcode.PSHN_L, 123456));
            }
            operations.Add((Jsm.Opcode.Unknown10, 0));
            if (sentinel)
            {
                operations.Add((Jsm.Opcode.POPI_L, 3));
            }
            operations.AddRange([(Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
            byte[] content = SyntheticProgram(operations.ToArray());
            EventEngine Create()
            {
                GameSession session = new(new GameState());
                EventEngine engine = new(session, session.State.Variables);
                engine.LoadField(170, content, []);
                return engine;
            }
            foreach ((int first, int second) in new[] { (int.MinValue, int.MaxValue), (65535, -1), (0, 0) })
            {
                EventEngine engine = Create();
                JsonNode initial = JsonNode.Parse(engine.Capture().GetRawText())!;
                initial["Fibers"]![0]!["Results"]!["0"] = first;
                initial["Fibers"]![0]!["Results"]!["1"] = second;
                initial["Fibers"]![0]!["Results"]!["2"] = 789;
                engine.Restore(JsonSerializer.SerializeToElement(initial));
                uint randomState = engine.Capture().GetProperty("RandomState").GetUInt32();
                List<ScriptCommand> commands = [];
                bool observed = false;
                engine.CommandExecuted += command =>
                {
                    commands.Add(command);
                    if (command.Name != "Unknown10")
                    {
                        return;
                    }
                    JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
                    JsonElement results = fiber.GetProperty("Results");
                    JsonElement stack = fiber.GetProperty("Stack");
                    Require(command.Arguments.Count == 0 && results.GetProperty("0").GetInt32() == 0 && results.GetProperty("1").GetInt32() == 0 && results.GetProperty("2").GetInt32() == 789, "Native music-position query did not overwrite only R0/R1 with zero or fabricated operands.");
                    Require(stack.GetArrayLength() == (sentinel ? 1 : 0) && (!sentinel || stack[0].GetInt32() == 123456), "Native music-position query consumed or fabricated an operand.");
                    observed = true;
                };
                engine.Step();
                JsonElement saved = engine.Capture();
                Require(observed && engine.LastFault is null && engine.Frame == 1 && commands.Any(command => command.Name == "WAIT") && saved.GetProperty("RandomState").GetUInt32() == randomState, "Native music-position query yielded an extra update or changed EventRND.");
                if (sentinel)
                {
                    Require(saved.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 123456, "The immediate instruction following the query did not receive its preserved operand.");
                }
                EventEngine independent = Create();
                independent.Restore(saved);
                Require(JsonElement.DeepEquals(saved, independent.Capture()), "Music-position query results did not restore independently.");
                engine.Step();
                independent.Step();
                Require(JsonElement.DeepEquals(engine.Capture(), independent.Capture()), "Music-position query changed ordinary restored wait continuation.");
            }
        }
        Console.WriteLine("Unknown10/367: installed native music-position zero overwrites R0/R1, retains unrelated registers/operands, continues immediately and restores independently; no playback-time or RNG simulation.");
    }
}
