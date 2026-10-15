using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeCardAndSaveEnableContract(string gameDirectory)
    {
        GameSession fresh = GameSession.CreateColdProcess();
        fresh.StartNewGame();
        NativeCardInventory initial = fresh.CardInventory!;
        Require(initial.Cards.Length == 110 && initial.Cards.All(value => value == 0) && initial.ObtainedRareCards.Length == 5 && initial.ObtainedRareCards.All(value => value == 0), "Native New Game did not retain the authentic card-region clear.");

        foreach (int card in new[] { 0, 76, 77, 84, 85, 109 })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 57), (Jsm.Opcode.PSHN_L, card), (Jsm.Opcode.GETCARD, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1000), (Jsm.Opcode.POPM_L, 1004), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            (GameSession session, EventEngine engine) = CreateCardEngine(initial, NativeWorldControlState.CreateNewGame(), content);
            int executions = 0;
            engine.CommandExecuted += command =>
            {
                if (command.Name == "GETCARD")
                {
                    executions++;
                    Require(command.Arguments.SequenceEqual([card]) && engine.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 57, "GETCARD changed its DWORD argument or popped an unrelated operand.");
                }
            };
            engine.Step();
            NativeCardInventory acquired = session.CardInventory!;
            Require(executions == 1 && session.State.Variables[1000] == 0 && session.State.Variables[1004] == 57 && acquired.Cards[card] == (card < 77 ? 0x81 : 0xF0) && acquired.Cards.Where((_, index) => index != card).All(value => value == 0), "GETCARD lost saved bytes, result0 or same-callback completion.");
            byte[] expectedRare = new byte[5];
            if (card >= 77)
            {
                expectedRare[(card - 77) / 8] = (byte)(1 << ((card - 77) & 7));
            }
            Require(acquired.ObtainedRareCards.SequenceEqual(expectedRare) && initial.Cards.All(value => value == 0), "GETCARD changed unrelated rare acquisition bits or mutated its source inventory.");
            JsonElement captured = engine.Capture();
            (GameSession restoredSession, EventEngine restored) = CreateCardEngine(initial, NativeWorldControlState.CreateNewGame(), content);
            restored.Restore(captured);
            Require(JsonElement.DeepEquals(captured, restored.Capture()) && CardsEqual(acquired, restoredSession.CardInventory), "Independent VM restore lost card bytes.");
            engine.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Ordinary callback after GETCARD restore diverged.");
            engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
            Require(CardsEqual(acquired, session.CardInventory), "Fresh field allocation reset card inventory.");
        }

        NativeCardInventory capped = initial.DeepCopy();
        capped.Cards[76] = 99;
        capped = capped.Acquire(76, out int at100);
        Require(at100 == 0 && capped.Cards[76] == 0xE4, "Original common-card grant from99 to100 changed.");
        byte[] overflowProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 76), (Jsm.Opcode.GETCARD, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1000), (Jsm.Opcode.RET, 0));
        (GameSession overflowSession, EventEngine overflow) = CreateCardEngine(capped, NativeWorldControlState.CreateNewGame(), overflowProgram);
        overflow.Step();
        Require(overflowSession.CardInventory!.Cards[76] == 0xE4 && overflowSession.State.Variables[1000] == -1, "Native common-card cap100 did not report result-1 without incrementing.");
        NativeCardInventory unmarkedCap = initial.DeepCopy();
        unmarkedCap.Cards[0] = 100;
        NativeCardInventory failedGrant = unmarkedCap.Acquire(0, out int capResult);
        Require(capResult == -1 && failedGrant.Cards[0] == 0xE4, "Native failed grant must still set common encountered bit80.");
        NativeCardInventory rare = initial.Acquire(109, out _).Acquire(77, out _).Acquire(109, out int repeated);
        Require(repeated == 0 && rare.Cards[109] == 0xF0 && rare.Cards[77] == 0xF0 && rare.ObtainedRareCards.SequenceEqual(new byte[] { 1, 0, 0, 0, 1 }), "Repeated rare grants lost obtained history or assigned a quantity.");

        foreach (int argument in new[] { 0, 1, 256, -1, int.MinValue })
        {
            NativeWorldControlState controls = NativeWorldControlState.CreateNewGame().WithPartyPower(0) with { Menu209 = 0xAA };
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 76), (Jsm.Opcode.GETCARD, 0), (Jsm.Opcode.PSHN_L, 91), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 57), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.SAVEENABLE, 0), (Jsm.Opcode.PSHI_L, 3), (Jsm.Opcode.POPM_L, 1000), (Jsm.Opcode.POPM_L, 1004), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1008), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            (GameSession session, EventEngine engine) = CreateCardEngine(capped, controls, content);
            session.SetEventMemory(session.State.EventMemory.Write(512, 4, NativeScalarValue.Exact(argument)));
            engine.Step();
            Require(session.WorldControls == controls.WithSaveEnabled(argument) && session.WorldControls.Menu209 == (argument == 0 ? 0xAA : 0xAB) && session.State.Variables[1000] == 91 && session.State.Variables[1004] == 57 && session.State.Variables[1008] == -1, "SAVEENABLE narrowed its DWORD, changed another menu bit/result or failed same-callback completion.");
            JsonElement captured = engine.Capture();
            (GameSession restoredSession, EventEngine restored) = CreateCardEngine(initial, controls, content);
            restored.Restore(captured);
            Require(JsonElement.DeepEquals(captured, restored.Capture()) && restoredSession.WorldControls == session.WorldControls, "SAVEENABLE restore lost canonical menu bytes.");
            session.RequestField(new FieldTransition(101, 0, 0, 0, 0));
            Require(session.WorldControls == controls.WithSaveEnabled(argument).WithNormalFieldLoad(), "Native normal field load failed to clear save/menu low bits after SAVEENABLE.");
        }
        for (int menu = 0; menu <= 255; menu++)
        {
            NativeWorldControlState controls = NativeWorldControlState.CreateNewGame() with { Menu209 = (byte)menu };
            Require(controls.WithSaveEnabled(0).Menu209 == (menu & 0xFE) && controls.WithSaveEnabled(256).Menu209 == (menu | 1), "SAVEENABLE did not preserve all unrelated menu bits.");
        }

        foreach ((NativeCardInventory? Cards, NativeWorldControlState? Controls, Jsm.Opcode Opcode, int? Argument) boundary in new (NativeCardInventory?, NativeWorldControlState?, Jsm.Opcode, int?)[]
        {
            (null, null, Jsm.Opcode.GETCARD, 0),
            (initial, null, Jsm.Opcode.GETCARD, -1),
            (initial, null, Jsm.Opcode.GETCARD, 110),
            (initial, null, Jsm.Opcode.GETCARD, 256),
            (initial, null, Jsm.Opcode.GETCARD, null),
            (initial, null, Jsm.Opcode.SAVEENABLE, 1),
            (initial, NativeWorldControlState.CreateNewGame(), Jsm.Opcode.SAVEENABLE, null)
        })
        {
            byte[] content = boundary.Argument is int value ? SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, value), (boundary.Opcode, 0), (Jsm.Opcode.RET, 0)) : SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.JPF, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.NOP, 0), (boundary.Opcode, 0), (Jsm.Opcode.RET, 0));
            (GameSession session, EventEngine engine) = CreateCardEngine(boundary.Cards, boundary.Controls, content);
            ExpectWorldControlFault(engine, boundary.Opcode, boundary.Argument is null ? 5 : 2, boundary.Argument is int operand ? [operand] : []);
            Require(CardsEqual(session.CardInventory, boundary.Cards) && session.WorldControls == boundary.Controls, "Refused card/save contract mutated persistent bytes.");
        }
        (GameSession aliasSession, EventEngine aliasEngine) = CreateCardEngine(initial, NativeWorldControlState.CreateNewGame(), SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SAVEENABLE, 0), (Jsm.Opcode.RET, 0)));
        ExpectEventMemoryPublicationFault(aliasSession, aliasSession.State.EventMemory.Write(208, 4, NativeScalarValue.Exact(255)));
        foreach ((Jsm.Opcode Opcode, int Address) access in new[] { (Jsm.Opcode.PSHM_B, 1408), (Jsm.Opcode.PSHM_W, 1407), (Jsm.Opcode.POPM_L, 1405), (Jsm.Opcode.POPM_B, 1522) })
        {
            byte[] content = access.Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHM_W ? SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 57), (access.Opcode, access.Address), (Jsm.Opcode.POPM_L, 1000), (Jsm.Opcode.POPM_L, 1004), (Jsm.Opcode.RET, 0)) : SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 57), (access.Opcode, access.Address), (Jsm.Opcode.RET, 0));
            (GameSession session, EventEngine engine) = CreateCardEngine(initial, NativeWorldControlState.CreateNewGame(), content);
            ExpectWorldControlFault(engine, access.Opcode, 2, [57]);
            Require(session.State.Variables.Count == 0 && CardsEqual(session.CardInventory, initial), "Direct card-byte alias refusal mutated native inventory or created a duplicate memory record.");
        }
        (GameSession cardAliasSession, EventEngine cardAliasEngine) = CreateCardEngine(initial, NativeWorldControlState.CreateNewGame(), SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.GETCARD, 0), (Jsm.Opcode.RET, 0)));
        ExpectEventMemoryPublicationFault(cardAliasSession, cardAliasSession.State.EventMemory.Write(1405, 4, NativeScalarValue.Exact(77)));
        RejectingCardService refusingService = new(initial);
        EventEngine refusedService = new(refusingService, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        refusedService.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 76), (Jsm.Opcode.GETCARD, 0), (Jsm.Opcode.RET, 0)), []);
        try
        {
            refusedService.Step();
            throw new InvalidOperationException("Rejecting inventory service completed GETCARD.");
        }
        catch (EventScriptException) when (refusedService.LastFault?.Opcode == "GETCARD")
        {
        }
        JsonElement refusedFiber = refusedService.Capture().GetProperty("Fibers")[0];
        Require(refusedFiber.GetProperty("Position").GetInt32() == 2 && refusedFiber.GetProperty("Stack")[0].GetInt32() == 76 && CardsEqual(refusingService.CardInventory, initial), "Inventory service rejection lost the original PC/operand or mutated inventory.");

        NativeCardInventorySaveContract(initial, overflowProgram, overflowSession, overflow);
        using GameAssets assets = new(gameDirectory);
        foreach ((int Field, int Object, int Script, int Operation, Jsm.Opcode Opcode) native in new[] { (137, 11, 78, 1012, Jsm.Opcode.GETCARD), (165, 33, 187, 3657, Jsm.Opcode.SAVEENABLE) })
        {
            JsmProgram program = JsmProgram.Read(assets.LoadField(native.Field).Jsm);
            var script = program.Objects[native.Object].Scripts.Single(script => script.Id == native.Script);
            Require(script.Start <= native.Operation && script.End > native.Operation && program.Operations[native.Operation].Opcode == native.Opcode && program.Operations[native.Operation - 1].Opcode == Jsm.Opcode.PSHN_L, "Reported original card/save command or script ownership changed.");
            int argument = program.Operations[native.Operation - 1].Parameter;
            Console.WriteLine($"Original {native.Field}/{native.Object}/{native.Script}/{native.Operation}: {native.Opcode} DWORD{argument}; previous/successor: {string.Join(", ", program.Operations.Skip(native.Operation - 2).Take(6).Select(operation => $"{operation.Index}:{operation.Opcode}/{operation.Parameter}"))}");
            (GameSession selectedSession, EventEngine selected) = CreateCardEngine(initial, NativeWorldControlState.CreateNewGame(), SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, argument), (native.Opcode, 0), (Jsm.Opcode.RET, 0)));
            selected.Step();
            Require(selected.LastFault is null && (native.Opcode == Jsm.Opcode.GETCARD ? selectedSession.CardInventory!.Cards[argument] != 0 : selectedSession.WorldControls!.Menu209 == 1), "Original reached operand did not complete under the shared native contract.");
        }
        Console.WriteLine("Native GETCARD/SAVEENABLE: saved card bytes and rare bits, cap100/result0/-1, full DWORD operands, same-callback completion, menu bit preservation, independent restore/save and fail-fast boundaries PASS.");
    }

    private static (GameSession Session, EventEngine Engine) CreateCardEngine(NativeCardInventory? cards, NativeWorldControlState? controls, byte[] content)
    {
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 100, CardInventory = cards, WorldControls = controls });
        EventEngine engine = new(session);
        engine.LoadField(100, content, []);
        return (session, engine);
    }

    private static bool CardsEqual(NativeCardInventory? left, NativeCardInventory? right)
    {
        return JsonElement.DeepEquals(JsonSerializer.SerializeToElement(left), JsonSerializer.SerializeToElement(right));
    }

    private sealed class RejectingCardService(NativeCardInventory cards) : IEventServices
    {
        public NativeCardInventory CardInventory => cards;
        public bool CanStoreCardInventory => true;
        public bool IsMoviePlaying => false;
        public void SetCardInventory(NativeCardInventory inventory)
        {
            throw new NotSupportedException("Deliberately rejected inventory transaction.");
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

    private static void NativeCardInventorySaveContract(NativeCardInventory initial, byte[] content, GameSession session, EventEngine engine)
    {
        string path = Path.Combine(Path.GetTempPath(), $"memoria-native-cards-{Guid.NewGuid():N}.json");
        try
        {
            JsonElement captured = engine.Capture();
            GameSession saved = new(session.State with { EventState = captured });
            saved.Save(path);
            GameState loaded = GameSession.Load(path);
            Require(CardsEqual(loaded.CardInventory, session.CardInventory) && JsonElement.DeepEquals(loaded.EventState!.Value, captured), "Independent disk save/load changed native cards.");
            NativeCardInventory mismatched = initial.Acquire(1, out _);
            GameSession mismatch = new(saved.State with { CardInventory = mismatched });
            mismatch.Save(path);
            try
            {
                GameSession.Load(path);
                throw new InvalidOperationException("Root/event card-byte disagreement was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            JsonObject oldVm = JsonNode.Parse(captured.GetRawText())!.AsObject();
            oldVm.Remove("CardInventory");
            (GameSession _, EventEngine restored) = CreateCardEngine(initial, session.WorldControls, content);
            try
            {
                restored.Restore(JsonSerializer.SerializeToElement(oldVm));
                throw new InvalidOperationException("Missing card history was silently supplied from current session state.");
            }
            catch (InvalidDataException)
            {
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
