using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDyingContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        Require(initial.CombatParty.SequenceEqual(new byte[] { 255, 0, 255 }) && initial.Records.All(record => record.Status == 0 && record.HitPoints.IsPositive) && initial.Records[0].HitPoints == new NativeHpWord(1, 9999) && initial.Records[3].HitPoints == new NativeHpWord(9999, 9999), "Authentic template/menu initialization did not preserve raw combat slots, zero status and unknown exact clamped HP.");
        Require(initial.InitOutSha256 == "4D0D23473AB54AC078B0C77A1672D970BF51E7824BAC1724027B5766B706C9EA", "Selected init.out provenance changed.");
        NativeCharacterVital[] records = initial.Records.ToArray();
        records[0] = records[0] with { HitPoints = new NativeHpWord(0, 0), Status = 0xA5A5 };
        records[2] = records[2] with { HitPoints = new NativeHpWord(0, 0), Status = 0x1111 };
        records[3] = records[3] with { HitPoints = new NativeHpWord(2, 100), Status = 0xFFFF };
        NativeCharacterVitals source = initial with { Records = records, CombatParty = [0, 255, 3] };
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.DYING, 0), (Jsm.Opcode.POPM_B, 1000), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        (GameSession Session, EventEngine Engine) Create(NativeCharacterVitals? state)
        {
            GameSession session = new(new GameState { CharacterVitals = state });
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, content, []);
            return (session, engine);
        }
        (GameSession session, EventEngine engine) = Create(source);
        bool observed = false;
        engine.CommandExecuted += command =>
        {
            if (command.Name == "DYING")
            {
                Require(command.Arguments.Count == 0 && engine.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 77, "DYING consumed an operand or added a result.");
                observed = true;
            }
        };
        engine.Step();
        NativeCharacterVitals result = session.CharacterVitals!;
        Require(observed && result.Records[0].HitPoints == new NativeHpWord(1, 1) && result.Records[0].Status == 0xA5A4 && result.Records[2] == records[2] && result.Records[3] == records[3] && result.CombatParty.SequenceEqual(source.CombatParty), "DYING did not repair only zero-HP combat members while preserving positive status and inactive records.");
        JsonElement snapshot = engine.Capture();
        (GameSession independentSession, EventEngine independent) = Create(initial);
        independent.Restore(snapshot);
        Require(JsonElement.DeepEquals(snapshot, independent.Capture()) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(result), JsonSerializer.SerializeToElement(independentSession.CharacterVitals)), "Independent VM restore lost raw combat slots or HP/status domains.");
        engine.Step();
        independent.Step();
        Require(JsonElement.DeepEquals(engine.Capture(), independent.Capture()), "Ordinary post-DYING wait diverged after independent restore.");

        foreach (NativeCharacterVitals? invalidState in new NativeCharacterVitals?[] { null, source with { Records = records.Select((record, index) => index == 0 ? record with { HitPoints = new NativeHpWord(0, 10) } : record).ToArray() } })
        {
            (GameSession invalidSession, EventEngine invalid) = Create(invalidState);
            try
            {
                invalid.Step();
                throw new InvalidOperationException("Unknown or mixed zero/nonzero native HP was accepted.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement failed = invalid.Capture();
            Require(invalid.LastFault?.Opcode == "DYING" && failed.GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2 && failed.GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 77 && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(invalidState), JsonSerializer.SerializeToElement(invalidSession.CharacterVitals)), "DYING refusal changed the PC, stack or HP/status state.");
        }
        NativeCharacterVital[] permuted = initial.Records.ToArray();
        (permuted[0], permuted[1]) = (permuted[1], permuted[0]);
        try
        {
            (initial with { Records = permuted }).Validate();
            throw new InvalidOperationException("Permuted native character records were accepted.");
        }
        catch (InvalidDataException)
        {
        }

        byte[] partyContent = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SUBPARTY, 0), (Jsm.Opcode.DYING, 0), (Jsm.Opcode.RET, 0));
        GameSession partySession = new(new GameState { CharacterVitals = initial });
        EventEngine partyEngine = new(partySession, partySession.State.Variables);
        partyEngine.LoadField(100, partyContent, []);
        List<byte[]> combat = [];
        partyEngine.CommandExecuted += command =>
        {
            if (command.Name is "SETPARTY" or "ADDPARTY" or "SUBPARTY")
            {
                combat.Add(partySession.CharacterVitals!.CombatParty.ToArray());
            }
        };
        partyEngine.Step();
        Require(combat.Count == 3 && combat[0].SequenceEqual(new byte[] { 255, 0, 255 }) && combat[1].SequenceEqual(new byte[] { 3, 0, 255 }) && combat[2].SequenceEqual(new byte[] { 255, 0, 255 }), "Party commands lost native swapped slots, first-empty insertion or exact combat-slot removal.");
        GameSession clearSession = new(new GameState { CharacterVitals = initial, Variables = new Dictionary<int, int> { [255] = 5, [256] = 6, [1279] = 7, [1280] = 8 } });
        EventEngine clear = new(clearSession, clearSession.State.Variables);
        clear.Scene.Party.Clear();
        clear.Scene.Party.AddRange([0, 255, 255]);
        clear.Scene.Members.Add(0);
        clear.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.CLEAR, 0), (Jsm.Opcode.RET, 0)), []);
        clear.Step();
        Require(clearSession.State.Variables.Count == 2 && clearSession.State.Variables[255] == 5 && clearSession.State.Variables[1280] == 8 && clear.Scene.Party.SequenceEqual([0, 255, 255]) && clear.Scene.Members.SetEquals([0]) && clearSession.CharacterVitals == initial, "Native CLEAR exceeded its256..1279 range or changed party, membership or character vitals.");
        foreach (int character in new[] { 8, 259 })
        {
            GameSession invalidPartySession = new(new GameState { CharacterVitals = initial });
            EventEngine invalidParty = new(invalidPartySession, invalidPartySession.State.Variables);
            invalidParty.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.RET, 0)), []);
            try
            {
                invalidParty.Step();
                throw new InvalidOperationException("Unmapped native character operand was accepted.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement failed = invalidParty.Capture();
            Require(invalidParty.LastFault?.Opcode == "ADDPARTY" && failed.GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2 && failed.GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == character && invalidPartySession.CharacterVitals == initial, "Unmapped ADDPARTY consumed its operand or changed native vitals.");
        }
        string mismatchPath = Path.Combine("../fixtures/external-fixture.json");
        Directory.CreateDirectory(Path.GetDirectoryName(mismatchPath)!);
        System.Text.Json.Nodes.JsonNode missing = System.Text.Json.Nodes.JsonNode.Parse(snapshot.GetRawText())!;
        missing.AsObject().Remove("CharacterVitals");
        File.WriteAllText(mismatchPath, JsonSerializer.Serialize(session.State with { EventState = JsonSerializer.SerializeToElement(missing) }));
        try
        {
            GameSession.Load(mismatchPath);
            throw new InvalidOperationException("Root vitals without active VM history were accepted.");
        }
        catch (InvalidDataException)
        {
        }
        Console.WriteLine("DYING: authentic init/kernel HP positivity domains, raw combat slots, exact zero-HP1/status-bit0 repair, positive/inactive preservation, independent complete VM restore, atomic unknown/mixed-HP refusal and party binding checks passed.");
    }
}
