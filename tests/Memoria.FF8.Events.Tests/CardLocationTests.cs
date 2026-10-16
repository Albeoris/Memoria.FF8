using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeCardLocationContract(string gameDirectory)
    {
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        NativeCardInventory initial = cold.CardInventory!;
        int[] locations = [-1, int.MinValue, 0, 17, 239, 240, 241, 496, 511, int.MaxValue];
        foreach (int id in new[] { 0, 76 })
        {
            for (int raw = 0; raw <= 255; raw++)
            {
                NativeCardInventory cards = initial.DeepCopy();
                cards.Cards[id] = (byte)raw;
                NativeCardInventory before = cards.DeepCopy();
                Require(cards.Location(id) == 0, "WHERECARD common signed boundary must return zero independently of its BYTE.");
                foreach (int location in locations)
                {
                    int quantity = raw & 0x7F;
                    bool success = location == 240 ? quantity < 100 : quantity > 0;
                    int expected = success ? raw + (location == 240 ? 1 : -1) : raw;
                    NativeCardInventory next = cards.Transfer(id, location, out int result);
                    Require(next.Cards[id] == expected && result == (success ? 0 : -1) && next.ObtainedRareCards.SequenceEqual(cards.ObtainedRareCards) && CardsEqual(cards, before), "SETCARD common full-DWORD location comparison, saturation, acquisition-bit preservation or immutable preparation changed.");
                    Require(next.Cards.Where((_, index) => index != id).SequenceEqual(cards.Cards.Where((_, index) => index != id)), "SETCARD changed another card record.");
                }
            }
        }
        foreach (int id in new[] { 77, 84, 85, 109 })
        {
            for (int raw = 0; raw <= 255; raw++)
            {
                foreach (bool obtained in new[] { false, true })
                {
                    NativeCardInventory cards = initial.DeepCopy();
                    cards.Cards[id] = (byte)raw;
                    int rare = id - 77;
                    cards.ObtainedRareCards[rare / 8] = obtained ? (byte)(1 << (rare & 7)) : (byte)0;
                    Require(cards.Location(id) == raw, "WHERECARD must zero-extend every rare location BYTE without testing obtained history.");
                    foreach (int location in locations)
                    {
                        NativeCardInventory next = cards.Transfer(id, location, out int result);
                        Require(result == 0 && next.Cards[id] == unchecked((byte)location) && next.ObtainedRareCards.SequenceEqual(cards.ObtainedRareCards) && cards.Cards[id] == raw, "SETCARD rare low-BYTE location write must preserve acquired history and original arrays.");
                    }
                }
            }
        }
        foreach (int id in new[] { int.MinValue, -1, 0, 76, 77, 109 })
        {
            NativeCardInventory cards = initial.DeepCopy();
            cards.Cards[109] = 0xFF;
            byte[] content = CardLocationQueryProgram(id);
            (GameSession session, EventEngine engine) = CreateCardEngine(cards, null, content);
            session.SetEventMemory(session.State.EventMemory.Write(512, 4, NativeScalarValue.Exact(id)));
            engine.Step();
            Require(session.State.Variables[1000] == cards.Location(id) && session.State.Variables[1004] == 57 && session.State.Variables[1008] == 91 && CardsEqual(cards, session.CardInventory), "WHERECARD lost full signed DWORD/R0, unrelated stack/result or read purity.");
        }
        foreach (int id in new[] { 0, 76, 77, 109 })
        {
            foreach (int location in locations)
            {
                NativeCardInventory cards = initial.Acquire(id, out _);
                NativeCardInventory expected = cards.Transfer(id, location, out int result);
                byte[] content = CardLocationTransferProgram(id, location);
                (GameSession session, EventEngine engine) = CreateCardEngine(cards, null, content);
                session.SetEventMemory(session.State.EventMemory.Write(516, 4, NativeScalarValue.Exact(location)));
                engine.Step();
                Require(CardsEqual(expected, session.CardInventory) && session.State.Variables[1000] == result && session.State.Variables[1004] == 57 && session.State.Variables[1008] == 91, "SETCARD reversed DWORD operands, yielded, lost R0 or consumed unrelated stack/results.");
                JsonElement saved = engine.Capture();
                (GameSession restoredSession, EventEngine restored) = CreateCardEngine(initial, null, content);
                restored.Restore(saved);
                Require(JsonElement.DeepEquals(saved, restored.Capture()) && CardsEqual(expected, restoredSession.CardInventory), "SETCARD restore lost its canonical saved card owner.");
                engine.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "SETCARD independent post-WAIT restore diverged.");
            }
        }
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.WHERECARD, Jsm.Opcode.SETCARD })
        {
            foreach (int id in new[] { int.MinValue, -1, 110, 256 })
            {
                if (opcode == Jsm.Opcode.WHERECARD && id < 77)
                {
                    continue;
                }
                (GameSession session, EventEngine engine) = CreateCardEngine(initial, null, CardLocationFaultProgram(opcode, id));
                session.SetEventMemory(session.State.EventMemory.Write(512, 4, NativeScalarValue.Exact(id)));
                ExpectWorldControlFault(engine, opcode, 4, [id, 17, 57]);
            }
            (GameSession missing, EventEngine missingEngine) = CreateCardEngine(null, null, CardLocationFaultProgram(opcode, 77));
            missing.SetEventMemory(missing.State.EventMemory.Write(512, 4, NativeScalarValue.Exact(77)));
            ExpectWorldControlFault(missingEngine, opcode, 4, [77, 17, 57]);
            Require(missing.CardInventory is null, "Missing card location owner refusal created data.");
            foreach (int address in new[] { 1405, 1408, 1522 })
            {
                (GameSession session, EventEngine engine) = CreateCardEngine(initial, null, CardLocationFaultProgram(opcode, 77));
                ExpectEventMemoryPublicationFault(session, session.State.EventMemory.Write(address, 4, NativeScalarValue.Exact(7)));
                Require(CardsEqual(initial, session.CardInventory), "Card location publication refusal changed the inventory.");
            }
            byte[] underflow = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.JPF, 2), (Jsm.Opcode.PSHN_L, 57), (Jsm.Opcode.NOP, 0), (opcode, 0), (Jsm.Opcode.RET, 0));
            (_, EventEngine empty) = CreateCardEngine(initial, null, underflow);
            ExpectWorldControlFault(empty, opcode, 5, []);
        }
        ReadOnlyCardService readOnly = new(initial);
        Dictionary<int, int> memory = new() { [512] = 109 };
        EventEngine query = new(readOnly, NativeEventMemory.FromDWords(memory));
        query.LoadField(100, CardLocationQueryProgram(109), []);
        query.Step();
        Require(query.Variables[1000] == 232 && readOnly.Writes == 0, "WHERECARD lost its native initialized rare owner232 or incorrectly requires writer capability.");
        EventEngine readOnlyTransfer = new(readOnly, NativeEventMemory.FromDWords(new Dictionary<int, int> { [512] = 77 }));
        readOnlyTransfer.LoadField(100, CardLocationFaultProgram(Jsm.Opcode.SETCARD, 77), []);
        ExpectCardLocationServiceFault(readOnlyTransfer);
        Require(readOnly.Writes == 0, "SETCARD called a read-only setter before refusing.");
        RejectingCardService rejecting = new(initial);
        EventEngine rejected = new(rejecting, NativeEventMemory.FromDWords(new Dictionary<int, int> { [512] = 77 }));
        rejected.LoadField(100, CardLocationFaultProgram(Jsm.Opcode.SETCARD, 77), []);
        ExpectCardLocationServiceFault(rejected);
        Require(CardsEqual(initial, rejecting.CardInventory), "SETCARD rejected service changed native card arrays.");
        (_, EventEngine oneOperand) = CreateCardEngine(initial, null, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.SETCARD, 0), (Jsm.Opcode.RET, 0)));
        ExpectWorldControlFault(oneOperand, Jsm.Opcode.SETCARD, 2, [77]);
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.WHERECARD, Jsm.Opcode.SETCARD })
        {
            GameSession session = new(new GameState { CardInventory = initial, ExamTimer = NativeExamTimerState.FromReachedSet(60).AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 1) });
            EventEngine engine = new(session);
            engine.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 17), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (opcode, 0), (Jsm.Opcode.RET, 0)), []);
            try
            {
                engine.Step();
                throw new InvalidOperationException("Card location operation chose an uncertain consumed DWORD.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == opcode.ToString())
            {
            }
            JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
            Require(fiber.GetProperty("Position").GetInt32() == 4 && fiber.GetProperty("Stack")[0].GetProperty("Values").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([59, 60]) && CardsEqual(initial, session.CardInventory), "Card location uncertainty refusal changed the native alternatives, PC or inventory.");
        }

        using GameAssets assets = new(gameDirectory);
        JsmProgram original = JsmProgram.Read(assets.LoadField(95).Jsm);
        EventScript site = original.Objects[10].Scripts.Single(script => script.Id == 44);
        Require(site.Start <= 595 && site.End > 595 && original.Operations[595].Opcode == Jsm.Opcode.WHERECARD && original.Operations.Skip(site.Start).Take(site.End - site.Start).Any(operation => operation.Opcode == Jsm.Opcode.SETCARD), "Original95/10/44 location-transfer script or WHERECARD595 boundary changed.");
        Console.WriteLine("Native WHERECARD/SETCARD: full BYTE domains, signed/full DWORD operands, common transfer and rare location semantics, R0/stack, restore and atomic ownership refusal PASS.");
    }

    private static byte[] CardLocationQueryProgram(int id)
    {
        return SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 91), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 57), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.WHERECARD, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1000), (Jsm.Opcode.POPM_L, 1004), (Jsm.Opcode.PSHI_L, 3), (Jsm.Opcode.POPM_L, 1008), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
    }

    private static byte[] CardLocationTransferProgram(int id, int location)
    {
        return SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 91), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 57), (Jsm.Opcode.PSHM_L, 516), (Jsm.Opcode.PSHN_L, id), (Jsm.Opcode.SETCARD, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1000), (Jsm.Opcode.POPM_L, 1004), (Jsm.Opcode.PSHI_L, 3), (Jsm.Opcode.POPM_L, 1008), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
    }

    private static byte[] CardLocationFaultProgram(Jsm.Opcode opcode, int id)
    {
        return SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 57), (Jsm.Opcode.PSHN_L, 17), (Jsm.Opcode.PSHM_L, 512), (opcode, 0), (Jsm.Opcode.RET, 0));
    }

    private static void ExpectCardLocationServiceFault(EventEngine engine)
    {
        try
        {
            engine.Step();
            throw new InvalidOperationException("SETCARD completed against a refusing inventory service.");
        }
        catch (EventScriptException) when (engine.LastFault?.Opcode == "SETCARD")
        {
        }
        JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
        Require(fiber.GetProperty("Position").GetInt32() == 4 && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([77, 17, 57]), "SETCARD service refusal changed the original PC/operands.");
    }
}
