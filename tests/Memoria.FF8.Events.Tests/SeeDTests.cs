using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSeeDContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initialVitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy initialEconomy = assets.ReadNewGameEconomy();
        NativeSalaryTable table = assets.ReadNativeSalaryTable();
        {
            JsmProgram original = JsmProgram.Read(assets.LoadField(223).Jsm);
            Require(original.Operations[400].Opcode == Jsm.Opcode.PSHSM_B && original.Operations[400].Parameter == 1024 && original.Operations[401].Opcode == Jsm.Opcode.ADDSEEDLEVEL && original.Operations[431].Opcode == Jsm.Opcode.PSHSM_W && original.Operations[431].Parameter == 16 && original.Operations[507].Opcode == Jsm.Opcode.SARALYON, "Original223 rank, signedWORD projection or salary enable instruction changed.");
        }
        void Initialize(GameSession session)
        {
            session.InitializeNewGameCharacterVitals(initialVitals.DeepCopy());
            session.InitializeNewGameEconomy(initialEconomy.DeepCopy());
            session.InitializeNewGameSalaryTable(table);
        }
        (GameSession Session, EventEngine Engine) Create(ushort initial, int operand, bool nativeState = true)
        {
            GameSession session = nativeState ? GameSession.CreateColdProcess() : new GameSession(new GameState());
            if (nativeState)
            {
                session.StartNewGame();
                Initialize(session);
            }
            EventEngine engine = new(session);
            engine.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, initial), (Jsm.Opcode.POPM_W, 16), (Jsm.Opcode.PSHN_L, operand), (Jsm.Opcode.ADDSEEDLEVEL, 0), (Jsm.Opcode.PSHM_W, 16), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDSEEDLEVEL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        foreach ((ushort initial, int operand, ushort expected) in new (ushort, int, ushort)[] { (500, 80, 580), (580, -100, 480), (480, 100, 580), (580, 70, 650), (650, 0, 650), (500, 65536, 500), (500, 32768, 100), (65530, 10, 100), (32760, 20, 100), (50, 0, 100), (3100, 1, 3100) })
        {
            var live = Create(initial, operand);
            List<int[]> evidence = [];
            live.Engine.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.ADDSEEDLEVEL))
                {
                    evidence.Add(command.Arguments.ToArray());
                }
            };
            live.Engine.Step();
            Require(live.Session.SeeD?.RankPoints == expected && live.Session.State.Variables[5000] == expected && !live.Session.State.Variables.ContainsKey(16) && evidence.Count == 1 && evidence[0].SequenceEqual(new[] { operand }), $"ADDSEEDLEVEL initial{initial} operand{operand} expected{expected} actual{live.Session.SeeD?.RankPoints} W5000={live.Session.State.Variables.GetValueOrDefault(5000)} evidence{evidence.Count}: lost WORD wrap/signed clamp, canonical memory or full DWORD evidence.");
            var restored = Create(initial, operand);
            restored.Engine.Restore(live.Engine.Capture());
            live.Engine.Step();
            restored.Engine.Step();
            Require(live.Session.SeeD?.RankPoints == Math.Min(expected + 1, 3100) && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "ADDSEEDLEVEL return2 budget or independent rank restore diverged.");
        }
        GameSession flags = GameSession.CreateColdProcess();
        flags.StartNewGame();
        Initialize(flags);
        Require(flags.SeeD is { RankPoints: 500, SalaryUiEnabled: false, SalaryMovementCounter: 0 } && flags.WorldControls is { SalarySuppressed: true, SalaryDisplaySuppressed: true }, "Executed New Game did not establish native500/mask18/zero movement and actual52CA90 process UIfalse.");
        Require(flags.SeeD!.AddRankPoints(int.MaxValue).RankPoints == 499 && flags.SeeD.AddRankPoints(int.MinValue).RankPoints == 500, "SeeD full DWORD arithmetic narrowed or overflowed before the native WORD destination.");
        EventEngine projected = new(flags);
        projected.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 65530), (Jsm.Opcode.POPM_W, 16), (Jsm.Opcode.PSHSM_W, 16), (Jsm.Opcode.POPM_L, 5001), (Jsm.Opcode.PSHM_B, 16), (Jsm.Opcode.POPM_W, 5002), (Jsm.Opcode.PSHSM_B, 16), (Jsm.Opcode.POPM_L, 5003), (Jsm.Opcode.PSHN_L, 18), (Jsm.Opcode.POPM_B, 17), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.POPM_B, 16), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        projected.Step();
        Require(flags.State.Variables[5001] == -6 && flags.State.Variables[5002] == 250 && flags.State.Variables[5003] == -6 && flags.SeeD!.RankPoints == 0x12FF && !flags.State.Variables.ContainsKey(16) && !flags.State.Variables.ContainsKey(17), "Native SeeD signed/unsigned BYTE/WORD projections or partial BYTE writes lost their owned WORD.");
        const uint unrelated = 0xA5000267;
        flags.SetNativeEventOwners(flags.WorldControls! with { EventFlags104 = unrelated | 0x18 });
        EventEngine salary = new(flags);
        salary.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SARALYON, 0), (Jsm.Opcode.SARALYDISPOFF, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        salary.Step();
        Require(flags.WorldControls is { SalarySuppressed: false, SalaryDisplaySuppressed: true } && flags.SeeD is { SalaryUiEnabled: true }, "SARALYON/DISPOFF failed mask8/mask10 separation or process UI enable.");
        Require(flags.WorldControls!.EventFlags104 == (unrelated | 0x10), "Salary flags overwrote guest, timer, battle, source or unrelated DWORD104 bits.");
        NativeWorldControlState guest = flags.WorldControls!.WithGuestMember(8, false).WithGuestMember(9, false).WithGuestMember(10, false);
        Require(guest.EventFlags104 == ((unrelated | 0x10) & ~7u), "Guest member bits changed salary, timer, battle or source flags.");
        flags.SetNativeEventOwners(flags.WorldControls!.WithSalaryDisplayEnabled(true).WithSalaryEnabled(false), flags.SeeD!.WithSalaryUiEnabled(false));
        Require(flags.WorldControls is { SalarySuppressed: true, SalaryDisplaySuppressed: true } && flags.SeeD is { SalaryUiEnabled: false }, "SARALYOFF failed its two saved masks and process UI write.");
        GameState beforeAtomic = flags.State;
        try
        {
            flags.SetNativeEventOwners(flags.WorldControls!.WithSalaryEnabled(true), flags.SeeD! with { Version = -1 });
            throw new InvalidOperationException("Invalid composite event owners silently committed.");
        }
        catch (InvalidDataException)
        {
            Require(ReferenceEquals(beforeAtomic, flags.State), "Composite event owner validation partially wrote DWORD104 before rejecting the salary state.");
        }
        NativeSalaryResult moving = NativeSalaryCalculator.Sample(flags.SeeD!, flags.Economy, flags.CharacterVitals, 0x5FFF, flags.WorldControls!.WithSalaryEnabled(true).EventFlags104);
        Require(moving.SeeD.SalaryMovementCounter == 0x5FFF && NativeSalaryCalculator.Sample(moving.SeeD, moving.Economy, flags.CharacterVitals, 5, flags.WorldControls.EventFlags104).SeeD.SalaryMovementCounter == 0x5FFF, "Salary movement counter lost its exact native DWORD or disabled hold.");
        Require(NativeSalaryCalculator.Sample(moving.SeeD, moving.Economy, flags.CharacterVitals! with { DreamEnabled = true }, 5, flags.WorldControls.WithSalaryEnabled(true).EventFlags104).SeeD == moving.SeeD, "Dream callbacks changed normal salary rank or movement counter.");
        NativeSalaryResult payment = NativeSalaryCalculator.Sample(moving.SeeD, moving.Economy, flags.CharacterVitals, 1, flags.WorldControls.WithSalaryEnabled(true).EventFlags104);
        Require(payment.SeeD.SalaryMovementCounter == 0 && payment.Economy!.Gil > moving.Economy!.Gil && moving.SeeD.SalaryMovementCounter == 0x5FFF, "Salary payout did not replace the obsolete refusal or changed its prospective source.");
        GameSession warm = GameSession.CreateColdProcess();
        warm.StartNewGame();
        warm.SetNativeEventOwners(warm.WorldControls!.WithSalaryEnabled(true), warm.SeeD!.WithSalaryUiEnabled(true));
        warm.StartNewGame();
        Require(warm.SeeD is { RankPoints: 500, SalaryUiEnabled: false, SalaryMovementCounter: 0 } && warm.WorldControls!.SalarySuppressed && warm.ExamTimer!.ClockCalls == 0, "Warm New Game omitted52CA90 UIfalse .");
        var missing = Create(500, 80, false);
        try
        {
            missing.Engine.Step();
            throw new InvalidOperationException("SeeD WORD16 accepted missing native New Game provenance.");
        }
        catch (EventScriptException)
        {
            JsonElement fiber = missing.Engine.Capture().GetProperty("Fibers")[0];
            Require(missing.Session.SeeD is null && fiber.GetProperty("Position").GetInt32() == 2 && fiber.GetProperty("Stack")[0].GetInt32() == 500 && !missing.Session.State.Variables.ContainsKey(16), "Missing SeeD provenance changed PC, operand or rank state.");
        }
        Console.WriteLine("SeeD: original223 instructions, native New Game500, signedWORD wrap/clamps, canonical memory, return budget, restored continuation, salary mask/UI ownership, warm-root refusal and payment boundary passed.");
    }
}
