using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSalaryPayoutContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeSalaryTable table = assets.ReadNativeSalaryTable();
        Require(table.Entries.SequenceEqual(new ushort[] { 0, 50, 100, 150, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1250, 1300, 1350, 1400, 1450, 1500, 1550, 1600, 1650, 1700, 1750, 1800, 1850, 1900, 1950, 2000, 3000 }) && vitals.Records.All(row => row.KillCount == 0), "Authenticated native salary table or init.out physical kill words changed.");
        NativeSeeDState fresh = NativeSeeDState.CreateNewGame();
        Require(fresh.SalaryUiEnabled == false && fresh.KillBaseline18 == 0 && fresh.SalaryDisplayCountdown184 == 0 && fresh.PreviousRank186 == 0, "Executed52CA90 NewGame did not clear saved words and derive UIfalse from104mask8.");
        NativeSeeDState seed = fresh with { SalaryTable = table, RankPoints = 650, SalaryMovementCounter = 24575, SalaryDisplayCountdown184 = ushort.MaxValue };
        NativeSalaryResult paid = NativeSalaryCalculator.Sample(seed, economy, vitals, 1, 0);
        Require(paid.SeeD is { RankPoints: 640, PreviousRank186: 650, KillBaseline18: 0, SalaryMovementCounter: 0, SalaryDisplayCountdown184: 149 } && paid.Economy is { Gil: 9000, PastGil: 3000, EventBankGil72: 0, EventBankPastGil76: 0 } && paid.Payment == new NativeSalaryPayment(6, 6, 4000, 4000) && !paid.CloseDisplay, "Native payout lost signed rank adjustment, raw saved words, counter reset, live-only gil or immediate150â†’149 countdown.");
        Require(seed.RankPoints == 650 && seed.SalaryMovementCounter == 24575 && economy.Gil == 5000 && vitals.Records.All(row => row.KillCount == 0), "Prospective payout mutated the source owner or fabricated combat kills.");
        NativeCharacterVital[] rows = vitals.Records.ToArray();
        rows[7] = rows[7] with { KillCount = 200 };
        NativeSalaryResult allPhysical = NativeSalaryCalculator.Sample(seed, economy, vitals with { Records = rows }, 1, 0);
        Require(allPhysical.SeeD.RankPoints == 840 && allPhysical.SeeD.KillBaseline18 == 200 && allPhysical.Economy!.Gil == 11000 && allPhysical.Payment == new NativeSalaryPayment(6, 8, 4000, 6000), "Salary omitted a nonparty/unregistered physical kill record.");
        rows[0] = rows[0] with { KillCount = ushort.MaxValue };
        rows[1] = rows[1] with { KillCount = 2 };
        rows[7] = rows[7] with { KillCount = 0 };
        NativeSalaryResult wrapped = NativeSalaryCalculator.Sample(seed with { KillBaseline18 = 65530 }, economy, vitals with { Records = rows }, 1, 0);
        Require(wrapped.SeeD.KillBaseline18 == 1 && wrapped.SeeD.RankPoints == 647, "Salary kill sum/baseline/rank arithmetic did not wrap at WORD width.");
        NativeSalaryResult negative = NativeSalaryCalculator.Sample(seed with { RankPoints = ushort.MaxValue }, economy, vitals, 1, 0);
        Require(negative.SeeD.RankPoints == 100 && negative.Payment == new NativeSalaryPayment(0, 1, 0, 500), "Salary old rank signed division or signedWORD clamp was replaced by unsigned arithmetic.");
        foreach (uint suppressedDisplay in new uint[] { 0x10, 0x1000 })
        {
            NativeSalaryResult hidden = NativeSalaryCalculator.Sample(seed with { RankPoints = 32767 }, economy, vitals, 1, suppressedDisplay);
            Require(hidden.SeeD.RankPoints == 3100 && hidden.Payment is null && hidden.Economy!.Gil == 35000, "Suppressed native salary display accessed an unused out-of-table old rank or skipped payment.");
        }
        try
        {
            _ = NativeSalaryCalculator.Sample(seed with { RankPoints = 32767 }, economy, vitals, 1, 0);
            throw new InvalidOperationException("Displayed salary invented an out-of-table old rank amount.");
        }
        catch (NotSupportedException)
        {
        }
        NativeSalaryResult oversized = NativeSalaryCalculator.Sample(seed, economy, vitals, 5792, 0);
        Require(oversized.SeeD.SalaryMovementCounter == 0, "Salary preserved overshoot instead of native counter reset0.");
        NativeSalaryResult next = NativeSalaryCalculator.Sample(oversized.SeeD, oversized.Economy, vitals, 5792, 0);
        Require(next.SeeD.SalaryMovementCounter == 5792 && next.Payment is null, "Next actual callback consumed the discarded prior overshoot.");
        NativeSalaryResult clamped = NativeSalaryCalculator.Sample(seed, economy with { Gil = 99998000 }, vitals, 1, 0);
        Require(clamped.Economy!.Gil == NativeEconomy.GilCeiling, "Native payout did not apply unsigned gil ceiling.");
        NativeSalaryResult gilWrapped = NativeSalaryCalculator.Sample(seed, economy with { Gil = uint.MaxValue }, vitals, 1, 0);
        Require(gilWrapped.Economy!.Gil == 3999, "Native payout did not wrapDWORD gil before applying its unsigned ceiling.");
        foreach ((NativeSeeDState state, NativeEconomy? cash, NativeCharacterVitals? characters) in new[] { (seed with { SalaryTable = null }, (NativeEconomy?)economy, (NativeCharacterVitals?)vitals), (seed, (NativeEconomy?)null, (NativeCharacterVitals?)vitals), (seed, (NativeEconomy?)economy, (NativeCharacterVitals?)null) })
        {
            try
            {
                _ = NativeSalaryCalculator.Sample(state, cash, characters, 1, 0);
                throw new InvalidOperationException("Salary payout supplied an unproved native table, live gil or kill/dream owner.");
            }
            catch (NotSupportedException)
            {
            }
        }
        NativeSalaryResult overflow = NativeSalaryCalculator.Sample(seed with { SalaryMovementCounter = 1 }, economy, vitals, uint.MaxValue, 0);
        Require(overflow.SeeD.SalaryMovementCounter == 0 && overflow.Payment is null, "Native DWORD counter wrap was replaced with checked or saturated addition.");
        NativeSalaryResult firstClose = NativeSalaryCalculator.Sample(fresh, economy, vitals, 0, 8);
        Require(firstClose.CloseDisplay && firstClose.Payment is null && firstClose.SeeD.SalaryDisplayCountdown184 == ushort.MaxValue, "Native initial countdown0 did not close despite salary suppression.");
        NativeSalaryResult dream = NativeSalaryCalculator.Sample(seed with { SalaryDisplayCountdown184 = 150 }, economy, vitals with { DreamEnabled = true }, 1, 0);
        Require(dream.SeeD.SalaryDisplayCountdown184 == 150 && dream.SeeD.SalaryMovementCounter == 24575 && dream.Payment is null && !dream.CloseDisplay, "Dream flag failed to skip both salary and display-countdown aging.");
        NativeSalaryResult repeated = paid;
        for (int tick = 0; tick < 149; tick++)
        {
            repeated = NativeSalaryCalculator.Sample(repeated.SeeD, repeated.Economy, vitals, 0, 8);
        }
        Require(repeated.SeeD.SalaryDisplayCountdown184 == 0 && !repeated.CloseDisplay, "Countdown closed before the native zero-reading callback.");
        repeated = NativeSalaryCalculator.Sample(repeated.SeeD, repeated.Economy, vitals, 0, 8);
        Require(repeated.CloseDisplay && repeated.SeeD.SalaryDisplayCountdown184 == ushort.MaxValue, "Countdown zero callback did not close then decrement toFFFF.");
        Require(seed.WriteEventWords(16, 4, unchecked((int)0xABCDFFFF)) is { RankPoints: 65535, KillBaseline18: 0xABCD } && seed.WriteEventWords(185, 2, 0x1234).ReadEventWords(185, 2) == 0x1234, "Canonical rank/baseline/display words lost unaligned native projection or surrounding preservation.");
        NativeSalaryTable restoredTable = JsonSerializer.Deserialize<NativeSalaryTable>(JsonSerializer.Serialize(table))!;
        Require(restoredTable == table, "Authenticated table content equality failed after independent serialization.");
        (GameSession Session, EventEngine Engine) Counter()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(vitals.DeepCopy());
            session.InitializeNewGameEconomy(economy.DeepCopy());
            session.InitializeNewGameSalaryTable(table);
            session.SetWorldControls(session.WorldControls!.WithSalaryEnabled(true).WithSalaryDisplayEnabled(false));
            EventEngine engine = new(session);
            engine.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0x6000), (Jsm.Opcode.POPM_L, 8), (Jsm.Opcode.PSHM_L, 8), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHM_L, 8), (Jsm.Opcode.POPM_L, 1104), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var counter = Counter();
        counter.Engine.Step();
        Require(counter.Session.SeeD!.SalaryMovementCounter == 0x6000 && counter.Session.State.Variables[1100] == 0x6000 && !counter.Session.State.Variables.ContainsKey(8) && counter.Session.Economy!.Gil == 5000, "Same-budget POPM8/PSHM8 lost the native raw pending counter or caused an early payment.");
        JsonElement pendingCounter = counter.Engine.Capture();
        var restoredCounter = Counter();
        restoredCounter.Engine.Restore(pendingCounter);
        counter.Engine.Step();
        restoredCounter.Engine.Step();
        Require(counter.Session.SeeD is { SalaryMovementCounter: 0, RankPoints: 490, PreviousRank186: 500 } && counter.Session.State.Variables[1104] == 0 && counter.Session.Economy is { Gil: 7000, EventBankGil72: 7000 } && JsonElement.DeepEquals(counter.Engine.Capture(), restoredCounter.Engine.Capture()), "Next callback did not consume the saved raw counter with native payout/reset and exact independent restore.");
        Require(seed.WriteEventWords(9, 2, 0xABCD).ReadEventWords(9, 2) == 0xABCD && seed.WriteEventWords(8, 4, -1).SalaryMovementCounter == uint.MaxValue, "Native counter BYTE/WORD/DWORD projection restricted legitimate raw bits.");
        // Explicit unknown-clock component input; no journal or research snapshot is fabricated.
        NativeExamTimerState unknownClock = NativeExamTimerState.FromReachedSet(10).AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 1);
        NativeScalarValue uncertainByte = NativeScalarValue.FromTimer(new NativeTimerReadOrigin(1, 600, new string('A', 64), 0, 0, 1, 1, unknownClock)).ToWidth(1);
        Require(!uncertainByte.IsExact, "Overlap fixture did not retain genuine timer alternatives.");
        GameSession aliasSession = GameSession.CreateColdProcess();
        aliasSession.StartNewGame();
        GameState beforeAlias = aliasSession.State;
        try
        {
            aliasSession.SetNativeEventOwners(aliasSession.WorldControls!.WithSalaryEnabled(true), seeD: aliasSession.SeeD! with { RankPoints = 600 }, economy: economy.DeepCopy(), eventMemory: new NativeEventMemory(1, new Dictionary<int, NativeMemoryValue> { [9] = new(1, uncertainByte) }));
            throw new InvalidOperationException("Typed counter overlap was admitted by atomic native-owner publication.");
        }
        catch (InvalidDataException)
        {
        }
        Require(ReferenceEquals(beforeAlias, aliasSession.State) && aliasSession.WorldControls!.SalarySuppressed && aliasSession.SeeD!.RankPoints == 500 && aliasSession.Economy is null, "Typed SeeD alias rejection partially changed controls, rank or economy.");
        aliasSession.SetEventMemory(new NativeEventMemory(1, new Dictionary<int, NativeMemoryValue> { [1200] = new(1, uncertainByte) }));
        aliasSession.SetNativeEventOwners(aliasSession.WorldControls!.WithSalaryEnabled(true), seeD: aliasSession.SeeD! with { RankPoints = 600 }, economy: economy.DeepCopy());
        Require(!aliasSession.WorldControls!.SalarySuppressed && aliasSession.SeeD!.RankPoints == 600 && aliasSession.Economy!.Gil == 5000 && aliasSession.State.NativeVariableValues!.ContainsKey(1200), "Nonoverlapping typed native input was incorrectly rejected or lost at atomic publication.");
        JsonNode missingKills = JsonNode.Parse(JsonSerializer.Serialize(vitals))!;
        missingKills["Records"]![0]!.AsObject().Remove("KillCount");
        try
        {
            _ = JsonSerializer.Deserialize<NativeCharacterVitals>(missingKills.ToJsonString());
            throw new InvalidOperationException("Missing actual kill word was padded from a cached stat owner.");
        }
        catch (JsonException)
        {
        }
        Console.WriteLine("Native salary: authenticated32-entry table/eight physical kill words; signedWORD wrap, rank/pay bounds, counter overshoot discard, world live-only gil, countdown suppression/dream/zero edges and required current state pass.");
    }
}
