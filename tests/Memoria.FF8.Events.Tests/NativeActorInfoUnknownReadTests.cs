using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeActorInfoUnknownReadContract()
    {
        EventEngine Create(int consumer, bool writeBank = true)
        {
            List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -19), (Jsm.Opcode.PSHN_L, 22), (Jsm.Opcode.PSHN_L, -4), (Jsm.Opcode.SET3, 65535), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.DIR, 0)];
            if (writeBank)
            {
                init.AddRange([(Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.EFFECTLOAD, 0)]);
            }
            init.Add((Jsm.Opcode.RET, 8));
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 333), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 777), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0)];
            if (consumer == 0)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PGETINFO, 0)]);
                for (int register = 0; register < 8; register++)
                {
                    main.AddRange([(Jsm.Opcode.PSHI_L, register), (Jsm.Opcode.POPM_L, 5000 + register * 4)]);
                }
            }
            else
            {
                main.Add((Jsm.Opcode.PSHI_L, 0));
                if (consumer == 1)
                {
                    main.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 0)]);
                }
                main.Add((Jsm.Opcode.POPM_L, 5000));
            }
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            GameSession session = GameSession.CreateColdProcess();
            EventEngine engine = new(session) { LoadSoundResource = _ => SoundResourceStatus.Muted };
            engine.Scene.Party.Clear();
            engine.Scene.Party.AddRange([0, 255, 255]);
            engine.LoadField(600, SyntheticObjects([1], [init.ToArray(), main.ToArray()]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        byte[] indexedContent = SyntheticObjects([1, (8 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 257), (Jsm.Opcode.EFFECTLOAD, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 6000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 33), (Jsm.Opcode.PSHN_L, -44), (Jsm.Opcode.PSHN_L, 55), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        GameSession indexedSession = GameSession.CreateColdProcess();
        EventEngine indexed = new(indexedSession) { LoadSoundResource = _ => SoundResourceStatus.Muted };
        indexed.Scene.Party.Clear();
        indexed.Scene.Party.AddRange([0, 255, 255]);
        indexed.LoadField(600, indexedContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        indexed.Step();
        indexed.Step();
        Require(indexed.Scene.MusicProcess?.Bank202 == 1 && indexedSession.State.Variables[6000] == 33 && indexed.PlayerActor?.ObjectId == 0 && indexed.Actors[1].CharacterId == -2, "PGETINFO indexedBYTE202 did not select the actual nonparty NPC body after EFFECTLOAD BYTE wrap.");

        EventEngine live = Create(0);
        live.Step();
        live.Step();
        JsonElement pending = live.Capture();
        JsonElement results = pending.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Results");
        Require(live.Scene.MusicProcess?.Bank202 == 255, "EFFECTLOAD did not write lowBYTE202 independently of backend status.");
        foreach (int register in new[] { 0, 1, 2, 4, 5, 6 })
        {
            NativeScalarValue value = results.GetProperty(register.ToString()).Deserialize<NativeScalarValue>()!;
            Require(value.Values.Length == 0 && !value.IsExact && value.ActorInfoOrigin is { RawOperand: 8, BankAddress: 202, NativeActorIndex: 255, RetainedActorCount: 1 } source && source.ResultRegister == register, "Out-of-retained-table PGETINFO invented a body value or lost actual read provenance.");
        }
        Require(results.GetProperty("3").GetInt32() == 333 && results.GetProperty("7").GetInt32() == 777, "Unknown PGETINFO changed untouched R3/R7.");
        EventEngine resumed = Create(0);
        resumed.Restore(pending);
        for (int callback = 0; callback < 6 && !CapturedVariables(live.Capture()).TryGetProperty("5028", out _); callback++)
        {
            live.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(live.Capture(), resumed.Capture()), "Unknown register bank diverged after genuine component restore and actual overwrite.");
        }
        int[] expected = [-19, 22, -4, 333, 255, 65535, 7929, 777];
        JsonElement variables = CapturedVariables(live.Capture());
        for (int register = 0; register < 8; register++)
        {
            Require(variables.GetProperty((5000 + register * 4).ToString()).GetInt32() == expected[register], "Known follow-up PGETINFO failed to overwrite unknown outputs with native widths.");
        }
        foreach (int consumer in new[] { 1, 2 })
        {
            EventEngine blocked = Create(consumer);
            blocked.Step();
            blocked.Step();
            for (int callback = 0; callback < 6 && blocked.LastFault is null; callback++)
            {
                try
                {
                    blocked.Step();
                }
                catch (EventScriptException)
                {
                    break;
                }
            }
            JsonElement fault = blocked.Capture();
            JsonElement fiber = fault.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
            Require(blocked.LastFault?.Opcode == (consumer == 1 ? "CAL" : "POPM_L") && fiber.GetProperty("Position").GetInt32() == blocked.LastFault.Operation && fiber.GetProperty("Stack").GetArrayLength() == (consumer == 1 ? 2 : 1) && !CapturedVariables(fault).TryGetProperty("5000", out _), "Unknown consumption did not preserve PC, operands and memory before publication.");
        }
        JsonNode malformed = JsonNode.Parse(pending.GetRawText())!;
        JsonNode savedFiber = malformed["Fibers"]!.AsArray().Single(item => item!["ScriptId"]!.GetValue<int>() == 1)!;
        savedFiber["Results"]!["0"]!["ActorInfoOrigin"]!["NativeActorIndex"] = 0;
        JsonElement before = resumed.Capture();
        try
        {
            resumed.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Unknown PGETINFO accepted a forged retained actor index.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, resumed.Capture()), "Malformed unknown origin refusal mutated current VM.");
        EventEngine missing = Create(0, writeBank: false);
        missing.Step();
        try
        {
            missing.Step();
            throw new InvalidOperationException("PGETINFO inferred a missing physicalBYTE202.");
        }
        catch (EventScriptException)
        {
        }
        Require(missing.LastFault is { Opcode: "PGETINFO" } && missing.Scene.MusicProcess is null, "Unknown byte owner did not refuse before query outputs.");
    }
}
