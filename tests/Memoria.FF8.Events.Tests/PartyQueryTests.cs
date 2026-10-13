using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyQueryContract()
    {
        EventEngine Create(byte[] content, params int[] party)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(156, content, []);
            engine.Scene.Party.AddRange(party);
            return engine;
        }
        void StepToFault(EventEngine rejected)
        {
            try
            {
                rejected.Step();
                throw new InvalidOperationException("Expected an unsupported party-query boundary.");
            }
            catch (EventScriptException)
            {
            }
        }
        byte[] queries = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 640), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 641), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 642), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine engine = Create(queries, 0, 5);
        List<ScriptCommand> commands = [];
        engine.CommandExecuted += commands.Add;
        engine.Step();
        JsonElement paused = engine.Capture();
        JsonElement values = paused.GetProperty("Variables");
        Require(engine.LastFault is null && values.GetProperty("640").GetInt32() == 0 && values.GetProperty("641").GetInt32() == 5 && values.GetProperty("642").GetInt32() == 255 && engine.Scene.Party.SequenceEqual([0, 5]) && commands.Count(command => command.Name == "GETPARTY") == 3, "GETPARTY did not read canonical character bytes into R0, preserve trailing FF or continue original successor writes.");
        Require(paused.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("0").GetInt32() == 255, "GETPARTY did not persist its zero-extended unsigned BYTE result.");
        EventEngine independent = Create(queries, 7);
        independent.Restore(paused);
        for (int tick = 0; tick < 6; tick++)
        {
            engine.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), independent.Capture()), "Independent party-query result/party restore changed subsequent ordinary waits.");
        }
        byte[] next = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 643), (Jsm.Opcode.RET, 0));
        engine.LoadField(170, next, []);
        engine.Step();
        Require(engine.Capture().GetProperty("Variables").GetProperty("643").GetInt32() == 5 && engine.Scene.Party.SequenceEqual([0, 5]), "Field load lost canonical party query authority.");

        foreach (int slot in new[] { -1, 3, 256, 65536 })
        {
            byte[] invalid = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.RET, 0));
            EventEngine rejected = Create(invalid, 0);
            StepToFault(rejected);
            JsonElement fiber = rejected.Capture().GetProperty("Fibers")[0];
            Require(rejected.LastFault?.Opcode == "GETPARTY" && fiber.GetProperty("Position").GetInt32() == 2 && fiber.GetProperty("Stack")[0].GetInt32() == slot && fiber.GetProperty("Results").EnumerateObject().Count() == 0 && rejected.Scene.Party.SequenceEqual([0]), "An unsupported full DWORD slot changed GETPARTY PC, stack, results or canonical party.");
        }
        foreach (int[] unknown in new[] { Array.Empty<int>(), new[] { 255 }, new[] { -1 }, new[] { 256 }, new[] { 0, 0 }, new[] { 0, 1, 2, 3 } })
        {
            byte[] invalidParty = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.RET, 0));
            EventEngine rejected = Create(invalidParty, unknown);
            StepToFault(rejected);
            JsonElement fiber = rejected.Capture().GetProperty("Fibers")[0];
            Require(rejected.LastFault?.Opcode == "GETPARTY" && fiber.GetProperty("Position").GetInt32() == 2 && fiber.GetProperty("Stack")[0].GetInt32() == 0 && rejected.Scene.Party.SequenceEqual(unknown), "Unknown or malformed canonical party was consumed or invented by GETPARTY.");
        }
        byte[] pending = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.RET, 0));
        EventEngine emptyStack = Create(pending, 0);
        emptyStack.Step();
        JsonNode corrupted = JsonNode.Parse(emptyStack.Capture().GetRawText())!;
        corrupted["Fibers"]![0]!["Position"] = 4;
        emptyStack.Restore(JsonSerializer.SerializeToElement(corrupted));
        StepToFault(emptyStack);
        Require(emptyStack.LastFault?.Opcode == "GETPARTY" && emptyStack.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 4, "Missing GETPARTY operand was not refused at its original PC.");
        foreach (int[] packet in new[] { new[] { 0, 255, 5 }, new[] { 255, 0, 255 }, new[] { 0, 0, 255 }, new[] { 0, 256, 255 } })
        {
            byte[] write = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, packet[0]), (Jsm.Opcode.PSHN_L, packet[1]), (Jsm.Opcode.PSHN_L, packet[2]), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.RET, 0));
            EventEngine rejected = Create(write, 7);
            StepToFault(rejected);
            JsonElement fiber = rejected.Capture().GetProperty("Fibers")[0];
            Require(rejected.LastFault?.Opcode == "SETPARTY" && fiber.GetProperty("Position").GetInt32() == 4 && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(packet.Reverse()) && rejected.Scene.Party.SequenceEqual([7]) && fiber.GetProperty("Results").EnumerateObject().Count() == 0, "SETPARTY consumed an internal FF hole or malformed packet and silently changed subsequent GETPARTY slots.");
        }
        foreach (int[] packet in new[] { new[] { 0, 5, 255 }, new[] { 0, 255, 255 } })
        {
            byte[] write = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, packet[0]), (Jsm.Opcode.PSHN_L, packet[1]), (Jsm.Opcode.PSHN_L, packet[2]), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 644), (Jsm.Opcode.RET, 0));
            EventEngine written = Create(write, 7);
            written.Step();
            Require(written.LastFault is null && written.Scene.Party.SequenceEqual(packet.Where(character => character != 255)) && written.Capture().GetProperty("Variables").GetProperty("644").GetInt32() == packet[1], "Canonical native SETPARTY packets no longer continue into exact GETPARTY slot reads.");
        }
        Console.WriteLine("GETPARTY: canonical character slots and trailing FF, R0/successor persistence and cross-field restore; invalid DWORD slots and unknown party refused before consumption.");
    }
}
