using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeTripleTriadContract()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        foreach (int card in Enumerable.Range(0, 5))
        {
            cards = cards.Acquire(card, out _);
        }
        int[] raw = [377, 0xDF, 257, 306, 263, 260, 383];
        List<(Jsm.Opcode, int)> talk = [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 900)];
        talk.AddRange(raw.Select(value => (Jsm.Opcode.PSHN_L, value)));
        talk.AddRange([(Jsm.Opcode.CARDGAME, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 904), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_L, 908), (Jsm.Opcode.RET, 8)]);
        byte[] content = SyntheticObjects([2], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], talk.ToArray()]);
        (GameSession Session, EventEngine Engine) Create(NativeCardInventory inventory, bool host)
        {
            NativeWorldControlState controls = NativeWorldControlState.CreateNewGame();
            GameSession fresh = GameSession.CreateColdProcess();
            fresh.StartNewGame();
            GameSession session = new(fresh.State with { Scene = SceneKind.Field, FieldId = 600, CardInventory = inventory, SeeD = null, WorldControls = controls with { EventFlags104 = controls.EventFlags104 | 0x10 } });
            EventEngine engine = new(session);
            if (host)
            {
                engine.ValidateTripleTriad = _ => null;
            }
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return (session, engine);
        }
        foreach (int nativeResult in new[] { -1, 0, 1, 2 })
        {
            (GameSession session, EventEngine engine) = Create(cards, true);
            Require(engine.TalkTo(0, true), "Native card challenge did not queue talk slot7.");
            engine.Step();
            NativeTripleTriadRequest request = engine.PendingTripleTriad ?? throw new InvalidOperationException("CARDGAME did not suspend for its installed host.");
            Require(session.State.Variables[900] == 1 && request.RawOperands.SequenceEqual(raw) && request.OpponentOwner == 121 && request.NativeRuleMask == 0xDF && request.TradeRule == 1 && request.RareChance == 50 && request.Argument4 == 7 && request.Argument5 == 4 && request.CommonLevelMask == 127 && request.AvailableCards == 5, "CARDGAME changed source operand ordering, cast widths, challenge R0 or deck count.");
            Require(request.Rules.HasFlag(TripleTriadRules.Random | TripleTriadRules.SuddenDeath | TripleTriadRules.Elemental), "Native CARDGAME flags were confused with the pure match enum.");
            long frame = engine.Frame;
            engine.Step();
            Require(engine.Frame == frame && engine.NextTrigger.Kind == PlayerTriggerKind.TripleTriad && !engine.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted, "CARDGAME did not pause the field clock and ordinary input.");
            JsonElement snapshot = engine.Capture();
            (GameSession restoredSession, EventEngine restored) = Create(cards, true);
            restored.Restore(snapshot);
            Require(JsonElement.DeepEquals(snapshot, restored.Capture()), "Independent CARDGAME restore changed its exact pending owner.");
            JsonObject corrupt = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
            corrupt["Scene"]!["TripleTriadRequest"]!["AvailableCards"] = 6;
            (GameSession _, EventEngine rejected) = Create(cards, true);
            try
            {
                rejected.Restore(JsonSerializer.SerializeToElement(corrupt));
                throw new InvalidOperationException("Changed CARDGAME count was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            int published = 0;
            restored.ReturnFromTripleTriad(nativeResult, () => published++);
            Require(published == 1 && restored.PendingTripleTriad is null && restored.Scene.TripleTriadRequest is null, "CARDGAME did not return exactly once.");
            Require(restoredSession.State.Variables[904] == 5 && restoredSession.State.Variables[908] == nativeResult, "CARDGAME completion lost native R0=count or R1=win/loss/draw/cancel.");
            Require(restored.Capture().GetProperty("EventMemory").ValueKind != JsonValueKind.Null, "CARDGAME lost canonical event memory.");
        }
        (GameSession ordinarySession, EventEngine ordinary) = Create(cards, true);
        Require(ordinary.TalkTo(0), "Ordinary talk request failed after the card challenge extension.");
        ordinary.Step();
        Require(ordinarySession.State.Variables[900] == 0, "Ordinary talk inherited the card challenge result bit.");
        ordinary.ReturnFromTripleTriad(-1);
        (GameSession insufficientSession, EventEngine insufficient) = Create(cards.Transfer(0, 121, out _), false);
        Require(insufficient.TalkTo(0, true), "Insufficient deck did not enter the original talk request.");
        insufficient.Step();
        Require(insufficient.PendingTripleTriad is null && insufficientSession.State.Variables[904] == 4 && insufficientSession.State.Variables[908] == 0, "Native fewer-than-five path did not return R0=count/R1=0 without a host.");
        (GameSession _, EventEngine missing) = Create(cards, false);
        Require(missing.TalkTo(0, true), "Missing-host fixture did not queue talk.");
        try
        {
            missing.Step();
            throw new InvalidOperationException("CARDGAME without a host was admitted.");
        }
        catch (EventScriptException)
        {
            JsonElement fiber = missing.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 2);
            Require(fiber.GetProperty("Position").GetInt32() == missing.LastFault!.Operation && fiber.GetProperty("Stack").GetArrayLength() == 7, "Missing CARDGAME host changed its PC or operands before rejection.");
        }
        Console.WriteLine("Native CARDGAME: seven DWORDs and BYTE casts, rule adapter, challenge entry, min deck, host suspension, result/cancel, independent restore and rejection PASS.");
    }
}
