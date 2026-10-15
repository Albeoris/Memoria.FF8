using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeHowManyCardContract(string gameDirectory)
    {
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        NativeCardInventory initial = cold.CardInventory!;
        foreach (int id in new[] { 0, 76 })
        {
            for (int raw = 0; raw <= 255; raw++)
            {
                NativeCardInventory cards = initial.DeepCopy();
                cards.Cards[id] = (byte)raw;
                byte[] before = cards.Cards.ToArray();
                int expected = unchecked((sbyte)raw) < 0 ? raw & 0x7F : -1;
                Require(cards.AvailableCount(id) == expected && cards.Cards.SequenceEqual(before) && cards.ObtainedRareCards.All(value => value == 0), "HOWMANYCARD common signed BYTE/low-seven-bit quantity or read purity changed.");
            }
        }
        foreach (int id in new[] { 77, 84, 85, 109 })
        {
            for (int raw = 0; raw <= 255; raw++)
            {
                NativeCardInventory cards = initial.DeepCopy();
                cards.Cards[id] = (byte)raw;
                Require(cards.AvailableCount(id) == -1, "HOWMANYCARD inferred rare acquisition from location without the saved bitmap.");
                int rare = id - 77;
                cards.ObtainedRareCards[rare / 8] = (byte)(1 << (rare & 7));
                NativeCardInventory before = cards.DeepCopy();
                Require(cards.AvailableCount(id) == (raw == 0xF0 ? 1 : 0) && CardsEqual(cards, before), "HOWMANYCARD changed rare acquisition-bit boundaries, locationF0 equality or its saved inputs.");
            }
        }

        foreach (int id in new[] { 0, 76, 77, 84, 85, 109 })
        {
            NativeCardInventory cards = initial.Acquire(id, out _);
            if (id < 77)
            {
                cards.Cards[id] = 0xE4;
            }
            NativeCardInventory before = cards.DeepCopy();
            byte[] content = HowManyCardProgram(id);
            (GameSession session, EventEngine engine) = CreateCardEngine(cards, null, content);
            int executions = 0;
            engine.CommandExecuted += command =>
            {
                if (command.Name == "HOWMANYCARD")
                {
                    executions++;
                    JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
                    Require(command.Arguments.SequenceEqual([id]) && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([57]), "HOWMANYCARD narrowed the card DWORD or consumed an unrelated operand.");
                }
            };
            engine.Step();
            Require(executions == 1 && session.State.Variables[1000] == cards.AvailableCount(id) && session.State.Variables[1004] == 57 && session.State.Variables[1008] == 91 && CardsEqual(cards, before) && CardsEqual(session.CardInventory, before), "HOWMANYCARD lost same-callback R0, another result/stack slot or changed the saved inventory.");
            JsonElement captured = engine.Capture();
            (GameSession restoredSession, EventEngine restored) = CreateCardEngine(initial, null, content);
            restored.Restore(captured);
            Require(JsonElement.DeepEquals(captured, restored.Capture()) && CardsEqual(restoredSession.CardInventory, cards), "Independent HOWMANYCARD restore lost saved card bytes or the native result.");
            engine.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Ordinary post-WAIT callback diverged after independent HOWMANYCARD restore.");
        }

        ReadOnlyCardService readOnly = new(initial.Acquire(12, out _));
        Dictionary<int, int> memory = [];
        EventEngine read = new(readOnly, NativeEventMemory.FromDWords(memory));
        read.LoadField(100, HowManyCardProgram(12), []);
        read.Step();
        Require(!readOnly.CanStoreCardInventory && readOnly.Writes == 0 && read.Variables[1000] == 1 && readOnly.CardInventory.Cards[12] == 0x81, "HOWMANYCARD incorrectly requires a writer or updates a read-only service.");
        EventEngine cannotGrant = new(readOnly, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        cannotGrant.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.GETCARD, 0), (Jsm.Opcode.RET, 0)), []);
        try
        {
            cannotGrant.Step();
            throw new InvalidOperationException("Read-only card service completed GETCARD.");
        }
        catch (EventScriptException) when (cannotGrant.LastFault?.Opcode == "GETCARD")
        {
        }
        JsonElement refusedGrant = cannotGrant.Capture().GetProperty("Fibers")[0];
        Require(refusedGrant.GetProperty("Position").GetInt32() == 2 && refusedGrant.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([12]), "Shared GETCARD capability refusal changed its original PC or card DWORD.");
        Require(readOnly.Writes == 0 && readOnly.CardInventory.Cards[12] == 0x81, "Shared GETCARD guard mutated a read-only owner before capability refusal.");

        foreach (int id in new[] { -1, int.MinValue, 110, 256 })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 57), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.HOWMANYCARD, 0), (Jsm.Opcode.RET, 0));
            (GameSession session, EventEngine engine) = CreateCardEngine(initial, null, content);
            session.SetEventMemory(session.State.EventMemory.Write(512, 4, NativeScalarValue.Exact(id)));
            ExpectWorldControlFault(engine, Jsm.Opcode.HOWMANYCARD, 3, [id, 57]);
            Require(session.State.Variables.Count == 1 && session.State.Variables[512] == id && CardsEqual(initial, session.CardInventory), "Invalid full DWORD card ID changed memory, narrowed into an accepted ID or changed the owner.");
        }
        (GameSession missing, EventEngine missingEngine) = CreateCardEngine(null, null, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.HOWMANYCARD, 0), (Jsm.Opcode.RET, 0)));
        ExpectWorldControlFault(missingEngine, Jsm.Opcode.HOWMANYCARD, 2, [12]);
        Require(missing.CardInventory is null && missing.State.Variables.Count == 0, "Missing card owner refusal created persistent data.");
        byte[] underflow = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.JPF, 2), (Jsm.Opcode.PSHN_L, 57), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.HOWMANYCARD, 0), (Jsm.Opcode.RET, 0));
        (_, EventEngine empty) = CreateCardEngine(initial, null, underflow);
        ExpectWorldControlFault(empty, Jsm.Opcode.HOWMANYCARD, 5, []);
        foreach (int address in new[] { 1405, 1408, 1518, 1522 })
        {
            (GameSession session, EventEngine engine) = CreateCardEngine(initial, null, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.HOWMANYCARD, 0), (Jsm.Opcode.RET, 0)));
            ExpectEventMemoryPublicationFault(session, session.State.EventMemory.Write(address, 4, NativeScalarValue.Exact(7)));
            Require(CardsEqual(initial, session.CardInventory), "Card-memory publication refusal changed the inventory.");
        }
        foreach ((int address, int width) in new[] { (1405, 4), (1407, 2), (1522, 1) })
        {
            NativeMemoryValue alias = new(width, NativeScalarValue.Exact(7));
            GameSession session = new(new GameState { CardInventory = initial });
            ExpectEventMemoryPublicationFault(session, new NativeEventMemory(1, new Dictionary<int, NativeMemoryValue> { [address] = alias }));
            Require(CardsEqual(initial, session.CardInventory), "Typed partial card alias changed the inventory before publication refusal.");
        }
        GameSession uncertainSession = new(new GameState { CardInventory = initial, ExamTimer = NativeExamTimerState.FromReachedSet(0) });
        EventEngine uncertain = new(uncertainSession);
        uncertain.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.HOWMANYCARD, 0), (Jsm.Opcode.RET, 0)), []);
        uncertain.Step();
        try
        {
            uncertain.Step();
            throw new InvalidOperationException("HOWMANYCARD chose an uncertain native DWORD card ID.");
        }
        catch (EventScriptException) when (uncertain.LastFault?.Opcode == "HOWMANYCARD")
        {
        }
        JsonElement uncertainFiber = uncertain.Capture().GetProperty("Fibers")[0];
        Require(uncertainFiber.GetProperty("Position").GetInt32() == 7 && uncertainFiber.GetProperty("Stack")[0].GetProperty("Values").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([59, 60]) && CardsEqual(initial, uncertainSession.CardInventory), "Uncertain card operand refusal changed the exact executed timer alternatives, PC or owner.");

        using GameAssets assets = new(gameDirectory);
        JsmProgram original = JsmProgram.Read(assets.LoadField(912).Jsm);
        EventScript site = original.Objects[11].Scripts.Single(script => script.Id == 77);
        Require(site.Start <= 1237 && site.End > 1237 && original.Operations[1237].Opcode == Jsm.Opcode.HOWMANYCARD && original.Operations[1236].Opcode == Jsm.Opcode.PSHN_L && original.Operations[1236].Parameter == 12, "The actual912/11/77/1237 HOWMANYCARD DWORD12 site changed.");
        Console.WriteLine("Native HOWMANYCARD: full common BYTE domain, rare history/location boundaries, read-only service, same-callback DWORD/R0/stack, independent restore/post-WAIT and atomic refusal PASS.");
    }

    private static byte[] HowManyCardProgram(int id)
    {
        return SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 91), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 57), (Jsm.Opcode.PSHN_L, id), (Jsm.Opcode.HOWMANYCARD, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1000), (Jsm.Opcode.POPM_L, 1004), (Jsm.Opcode.PSHI_L, 3), (Jsm.Opcode.POPM_L, 1008), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
    }

    private sealed class ReadOnlyCardService(NativeCardInventory cards) : IEventServices
    {
        public NativeCardInventory CardInventory => cards;
        public bool CanStoreCardInventory => false;
        public bool IsMoviePlaying => false;
        public int Writes { get; private set; }
        public void SetCardInventory(NativeCardInventory inventory)
        {
            Writes++;
            throw new NotSupportedException("Read-only card owner cannot grant cards.");
        }
        public void RequestMovie(int index)
        {
            throw new NotSupportedException();
        }
        public void RequestField(int fieldId, int entryPoint)
        {
            throw new NotSupportedException();
        }
        public void ShowDialog(string text)
        {
        }
    }
}
