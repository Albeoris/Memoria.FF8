using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeEconomyContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeEconomy initial = assets.ReadNewGameEconomy();
        Require(initial.InitOutSha256 == "4D0D23473AB54AC078B0C77A1672D970BF51E7824BAC1724027B5766B706C9EA" && initial.Gil == 5000 && initial.PastGil == 3000 && initial.EventBankGil72 == 0 && initial.EventBankPastGil76 == 0 && initial.Items.Take(4).SequenceEqual(new NativeItemStock[] { new(1, 6), new(7, 4), new(16, 2), new(33, 5) }) && initial.Items.Skip(4).All(item => item == new NativeItemStock(0, 0)), "Actual init.out pairs/gil and executed save/event-bank clear differ from native provenance.");
        NativeEconomy lamp = initial.AddItem(168, 1, out int lampResult);
        Require(lamp.Items[4] == new NativeItemStock(168, 1) && initial.Items[4].ItemId == 0 && lampResult == 0, "ADDITEM did not preserve the original owner or fill the first native empty pair.");
        NativeItemStock[] rows = initial.Items.ToArray();
        rows[0] = new NativeItemStock(168, 99);
        rows[1] = new NativeItemStock(168, 3);
        rows[4] = new NativeItemStock(0, 250);
        NativeEconomy ordered = initial with { Items = rows };
        NativeEconomy wrapped = ordered.AddItem(168, 257, out int capped);
        Require(wrapped.Items[0].Count == 100 && capped == 1 && wrapped.Items[1].Count == 3 && ordered.Items[0].Count == 99, "Native first-match count, lowBYTE quantity or100 ceiling was replaced with aggregate semantics.");
        wrapped = ordered.AddItem(168, 200, out int wrappedResult);
        Require(wrapped.Items[0].Count == 43 && wrappedResult == 0, "Native BYTE addition did not wrap before the unsigned100 clamp.");
        wrapped = ordered.AddItem(65536 + 2, 7, out int inserted);
        Require(wrapped.Items[4] == new NativeItemStock(2, 1) && inserted == 0, "FullDWORD ID comparison, lowBYTE insertion or retained empty-row count was lost.");
        Require(ordered.AddItem(0, int.MaxValue, out int zeroResult).Items.SequenceEqual(ordered.Items) && zeroResult == 0, "Exact item0 was not native no-op.");
        NativeEconomy full = initial with { Items = Enumerable.Range(0, 198).Select(_ => new NativeItemStock(1, 1)).ToArray() };
        Require(full.AddItem(168, 1, out int fullResult).Items.SequenceEqual(full.Items) && fullResult == 1, "Full native inventory changed saved pairs.");
        Require(initial.AddGil(-3000, false).Gil == 2000 && initial.AddGil(-5001, false).Gil == NativeEconomy.GilCeiling && initial.AddGil(int.MaxValue, true).PastGil == NativeEconomy.GilCeiling && (initial with { Gil = uint.MaxValue }).AddGil(2, false).Gil == 1, "Gil native uncheckedDWORD addition/unsigned ceiling differs at subtraction or overflow.");

        (GameSession Session, EventEngine Engine) Create(byte[] program, NativeEconomy? economy)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            if (economy is not null)
            {
                session.InitializeNewGameEconomy(economy);
            }
            EventEngine engine = new(session);
            engine.LoadField(600, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return (session, engine);
        }
        static byte[] EconomyProgram(params (Jsm.Opcode Opcode, int Parameter)[] operations)
        {
            operations[0] = (Jsm.Opcode.LBL, 1);
            return SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], operations]);
        }
        byte[] grant = EconomyProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 168), (Jsm.Opcode.ADDITEM, 0), (Jsm.Opcode.PSHN_L, -3000), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.PSHM_L, 72), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.PSHN_L, -1000), (Jsm.Opcode.ADDPASTGIL, 0), (Jsm.Opcode.PSHM_L, 76), (Jsm.Opcode.POPM_L, 1104), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0));
        (GameSession liveSession, EventEngine live) = Create(grant, initial);
        live.Step();
        Require(liveSession.Economy!.Items[4] == new NativeItemStock(168, 1) && liveSession.Economy.Gil == 2000 && liveSession.Economy.PastGil == 2000 && liveSession.State.Variables[1100] == 2000 && liveSession.State.Variables[1104] == 2000 && live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Results").EnumerateObject().Count() == 0, "Native economy opcode order, no-yield successor, immediate bank copies or discarded item result differs.");
        (GameSession copySession, EventEngine copy) = Create(grant, initial);
        copy.Restore(live.Capture());
        live.Step();
        copy.Step();
        Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()) && copySession.Economy!.Items.SequenceEqual(liveSession.Economy.Items), "Current economy restore/nextcallback changed canonical inventory or VM.");

        byte[] bankProgram = EconomyProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, unchecked((int)0xFFABCDEF)), (Jsm.Opcode.POPM_L, 72), (Jsm.Opcode.PSHSM_W, 74), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.PSHM_L, 72), (Jsm.Opcode.POPM_L, 1104), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHM_L, 72), (Jsm.Opcode.POPM_L, 1108), (Jsm.Opcode.PSHM_L, 76), (Jsm.Opcode.POPM_L, 1112), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0));
        (GameSession bankSession, EventEngine bank) = Create(bankProgram, initial);
        bank.Step();
        Require(bankSession.Economy!.Gil == 5000 && bankSession.Economy.EventBankGil72 == 0xFFABCDEF && bankSession.State.Variables[1100] == unchecked((short)0xFFAB) && bankSession.State.Variables[1104] == unchecked((int)0xFFABCDEF), "Direct event-bank write incorrectly changed live gil or lost signed nativeWORD read in the same budget.");
        (GameSession bankCopySession, EventEngine bankCopy) = Create(bankProgram, initial);
        bankCopy.Restore(bank.Capture());
        bank.Step();
        bankCopy.Step();
        Require(bankSession.State.Variables[1108] == 5000 && bankSession.State.Variables[1112] == 3000 && JsonElement.DeepEquals(bank.Capture(), bankCopy.Capture()), "Next529FF0 callback did not replace bank-only mutation with live gil or restored an inferred copy.");
        NativeEconomy crossing = initial.WriteEventBank(74, 4, unchecked((int)0xAABBCCDD));
        Require(crossing.EventBankGil72 == 0xCCDD0000 && crossing.EventBankPastGil76 == 0x0000AABB && crossing.ReadEventBank(74, 4) == unchecked((int)0xAABBCCDD) && crossing.Gil == initial.Gil, "Unaligned native bank projection did not preserve both distinct physicalDWORDs and surrounding bytes.");
        foreach ((Jsm.Opcode write, Jsm.Opcode read, int address, int raw, int expected) in new[] { (Jsm.Opcode.POPM_B, Jsm.Opcode.PSHSM_B, 79, 511, -1), (Jsm.Opcode.POPM_W, Jsm.Opcode.PSHSM_W, 73, 131071, -1), (Jsm.Opcode.POPM_L, Jsm.Opcode.PSHM_L, 74, -1, -1) })
        {
            byte[] widthProgram = EconomyProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, raw), (write, address), (read, address), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0));
            (GameSession widthSession, EventEngine widthEngine) = Create(widthProgram, initial);
            widthEngine.Step();
            Require(widthSession.State.Variables[1100] == expected && widthSession.Economy!.Gil == 5000 && widthSession.Economy.PastGil == 3000, "Native BYTE/WORD/unalignedDWORD bank projection lost truncation/sign or changed live currency.");
        }

        JsonElement valid = bank.Capture();
        foreach (string property in new[] { "Items", "Gil", "PastGil", "EventBankGil72", "EventBankPastGil76", "InitOutSha256" })
        {
            JsonNode missing = JsonNode.Parse(valid.GetRawText())!;
            missing["Economy"]!.AsObject().Remove(property);
            try
            {
                bank.Restore(JsonSerializer.SerializeToElement(missing));
                throw new InvalidOperationException($"Missing required economy {property} was accepted.");
            }
            catch (JsonException)
            {
            }
            Require(JsonElement.DeepEquals(valid, bank.Capture()), "Malformed economy restore partially changed VM or services.");
        }
        JsonNode aliased = JsonNode.Parse(valid.GetRawText())!;
        SetCapturedCell(aliased, 72, 4, 5000);
        try
        {
            bank.Restore(JsonSerializer.SerializeToElement(aliased));
            throw new InvalidOperationException("Separate VM gil alias was accepted.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(valid, bank.Capture()), "Rejected economy alias changed current VM or owner.");
        (GameSession unknownSession, EventEngine unknown) = Create(grant, null);
        try
        {
            unknown.Step();
            throw new InvalidOperationException("Missing native inventory initialization was guessed.");
        }
        catch (EventScriptException) when (unknown.LastFault?.Opcode == "ADDITEM")
        {
        }
        Require(unknownSession.Economy is null && unknown.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([168, 1]), "Unknown economy refusal consumed original operands or published defaults.");

        RejectingEconomyServices rejected = new(initial.CopyGilToEventBank());
        EventEngine refused = new(rejected, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        refused.LoadField(600, grant, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        refused.Step();
        try
        {
            refused.Step();
            throw new InvalidOperationException("Rejected inventory service publication was ignored.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "ADDITEM")
        {
        }
        JsonElement held = refused.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(held.GetProperty("Position").GetInt32() == 5 && held.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([168, 1]) && rejected.Economy!.Items[4].ItemId == 0, "Rejected economy publication advancedPC, consumed operands or partially changed inventory.");
        RejectingEconomyServices rejectedCopy = new(initial);
        EventEngine refusedCopy = new(rejectedCopy, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        refusedCopy.LoadField(600, grant, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        refusedCopy.Step();
        JsonElement before = refusedCopy.Capture();
        try
        {
            refusedCopy.Step();
            throw new InvalidOperationException("Rejected preVM bank copy was ignored.");
        }
        catch (NotSupportedException)
        {
        }
        Require(JsonElement.DeepEquals(before, refusedCopy.Capture()), "Rejected preVM copy changedFrame or VM before publication.");
        CopyingEconomyServices movie = new(initial);
        EventEngine movieEngine = new(movie, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        movieEngine.LoadField(600, EconomyProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        movieEngine.Step();
        movieEngine.Step();
        Require(movie.Economy!.EventBankGil72 == 5000 && movie.Economy.EventBankPastGil76 == 3000 && movieEngine.Scene.FieldCallbackClock is { MovieCallbacks: 1 }, "Movie field callback omitted its shared native529FF0 gil copy.");
        string loadPath = Path.Combine(Path.GetTempPath(), "ff8-economy-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            bankSession.SetEventState(bank.Capture());
            File.WriteAllText(loadPath, JsonSerializer.Serialize(bankSession.State with { Economy = bankSession.Economy! with { Gil = 1 } }));
            try
            {
                _ = GameSession.Load(loadPath);
                throw new InvalidOperationException("Different root and current VM economy owners were accepted.");
            }
            catch (InvalidDataException)
            {
            }
        }
        finally
        {
            File.Delete(loadPath);
        }
        Console.WriteLine("Native economy: authentic198pairs/5000/3000/initialbank0; item rawwidth/order/BYTEwrap; gilDWORDwrap/unsignedceiling; bank-only writes and nextcallback copy; independentrestore, missing-state/alias/publisher refusals pass.");
    }

    private sealed class RejectingEconomyServices(NativeEconomy economy) : IEventServices
    {
        public NativeEconomy? Economy => economy;
        public bool CanStoreEconomy => true;
        public NativeEventRandomState? EventRandom { get; private set; } = NativeEventRandomState.FromColdProcess().ClearSavedStateForNewGame();
        public bool CanStoreEventRandom => true;
        public NativeWorldControlState? WorldControls { get; private set; } = NativeWorldControlState.CreateNewGame();
        public void SetWorldControls(NativeWorldControlState controls)
        {
            WorldControls = controls;
        }
        public bool CanStoreNativeEventOwners => true;
        public void SetNativeEventOwners(NativeWorldControlState controls, NativeSeeDState? seeD = null, NativeExamTimerState? timer = null, NativeCharacterVitals? characterVitals = null, NativeEconomy? economy = null, NativeGlobalSoundProcess? globalSoundProcess = null, NativeSalaryPresentation? salaryPresentation = null, NativeEventRandomState? eventRandom = null, NativeVibrationProcessState? vibrationProcess = null, NativeFieldFadeState? fieldFade = null, NativeEventMemory? eventMemory = null)
        {
            if (economy is not null && !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(economy), JsonSerializer.SerializeToElement(Economy)))
            {
                SetEconomy(economy);
            }
            EventRandom = eventRandom ?? EventRandom;
            WorldControls = controls;
        }

        public bool IsMoviePlaying => false;

        public void SetEconomy(NativeEconomy value)
        {
            throw new NotSupportedException("Component economy publisher rejection.");
        }

        public void RequestMovie(int index)
        {
        }

        public void RequestField(int fieldId, int entryPoint)
        {
        }

        public void ShowDialog(string text)
        {
        }
    }

    private sealed class CopyingEconomyServices(NativeEconomy economy) : IEventServices
    {
        public NativeEconomy? Economy { get; private set; } = economy;
        public bool CanStoreEconomy => true;
        public NativeEventRandomState? EventRandom { get; private set; } = NativeEventRandomState.FromColdProcess().ClearSavedStateForNewGame();
        public bool CanStoreEventRandom => true;
        public NativeWorldControlState? WorldControls { get; private set; } = NativeWorldControlState.CreateNewGame();
        public void SetWorldControls(NativeWorldControlState controls)
        {
            WorldControls = controls;
        }
        public bool CanStoreNativeEventOwners => true;
        public void SetNativeEventOwners(NativeWorldControlState controls, NativeSeeDState? seeD = null, NativeExamTimerState? timer = null, NativeCharacterVitals? characterVitals = null, NativeEconomy? economy = null, NativeGlobalSoundProcess? globalSoundProcess = null, NativeSalaryPresentation? salaryPresentation = null, NativeEventRandomState? eventRandom = null, NativeVibrationProcessState? vibrationProcess = null, NativeFieldFadeState? fieldFade = null, NativeEventMemory? eventMemory = null)
        {
            if (economy is not null && !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(economy), JsonSerializer.SerializeToElement(Economy)))
            {
                SetEconomy(economy);
            }
            EventRandom = eventRandom ?? EventRandom;
            WorldControls = controls;
        }

        public bool IsMoviePlaying => true;
        public bool? NativeMovieClockFlag => true;

        public void SetEconomy(NativeEconomy value)
        {
            Economy = value.DeepCopy();
        }

        public void RequestMovie(int index)
        {
        }

        public void RequestField(int fieldId, int entryPoint)
        {
        }

        public void ShowDialog(string text)
        {
        }
    }
}
