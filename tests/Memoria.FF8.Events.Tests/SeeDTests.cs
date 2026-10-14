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
        using (GameAssets assets = new(gameDirectory))
        {
            JsmProgram original = JsmProgram.Read(assets.LoadField(223).Jsm);
            Require(original.Operations[400].Opcode == Jsm.Opcode.PSHSM_B && original.Operations[400].Parameter == 1024 && original.Operations[401].Opcode == Jsm.Opcode.ADDSEEDLEVEL && original.Operations[431].Opcode == Jsm.Opcode.PSHSM_W && original.Operations[431].Parameter == 16 && original.Operations[507].Opcode == Jsm.Opcode.SARALYON, "Original223 rank, signedWORD projection or salary enable instruction changed.");
        }
        (GameSession Session, EventEngine Engine) Create(ushort initial, int operand, bool nativeState = true)
        {
            GameSession session = nativeState ? GameSession.CreateColdProcess() : new GameSession(new GameState());
            if (nativeState)
            {
                session.StartNewGame();
            }
            EventEngine engine = new(session, session.State.Variables);
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
        Require(flags.SeeD is { RankPoints: 500, SalarySuppressed: true, SalaryDisplaySuppressed: true, SalaryUiEnabled: null, SalaryMovementCounter: 0 } && flags.ReplayJournal!.IsComplete, "Executed New Game did not establish native500/mask18/zero movement without inferring process salary UI.");
        Require(flags.SeeD!.AddRankPoints(int.MaxValue).RankPoints == 499 && flags.SeeD.AddRankPoints(int.MinValue).RankPoints == 500, "SeeD full DWORD arithmetic narrowed or overflowed before the native WORD destination.");
        EventEngine projected = new(flags, flags.State.Variables);
        projected.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 65530), (Jsm.Opcode.POPM_W, 16), (Jsm.Opcode.PSHSM_W, 16), (Jsm.Opcode.POPM_L, 5001), (Jsm.Opcode.PSHM_B, 16), (Jsm.Opcode.POPM_W, 5002), (Jsm.Opcode.PSHSM_B, 16), (Jsm.Opcode.POPM_L, 5003), (Jsm.Opcode.PSHN_L, 18), (Jsm.Opcode.POPM_B, 17), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.POPM_B, 16), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        projected.Step();
        Require(flags.State.Variables[5001] == -6 && flags.State.Variables[5002] == 250 && flags.State.Variables[5003] == -6 && flags.SeeD!.RankPoints == 0x12FF && !flags.State.Variables.ContainsKey(16) && !flags.State.Variables.ContainsKey(17), "Native SeeD signed/unsigned BYTE/WORD projections or partial BYTE writes lost their owned WORD.");
        EventEngine salary = new(flags, flags.State.Variables);
        salary.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SARALYON, 0), (Jsm.Opcode.SARALYDISPOFF, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        salary.Step();
        Require(flags.SeeD is { SalarySuppressed: false, SalaryDisplaySuppressed: true, SalaryUiEnabled: true }, "SARALYON/DISPOFF failed mask8/mask10 separation or process UI enable.");
        flags.SetSeeD(flags.SeeD!.WithSalaryDisplayEnabled(true).WithSalaryEnabled(false));
        Require(flags.SeeD is { SalarySuppressed: true, SalaryDisplaySuppressed: true, SalaryUiEnabled: false }, "SARALYOFF failed its two saved masks and process UI write.");
        NativeSeeDState moving = flags.SeeD!.WithSalaryEnabled(true).WithMovementUnits(0x5FFF);
        Require(moving.SalaryMovementCounter == 0x5FFF && moving.WithSalaryEnabled(false).WithMovementUnits(5).SalaryMovementCounter == 0x5FFF, "Salary movement counter lost its exact native DWORD or disabled hold.");
        try
        {
            _ = moving.WithMovementUnits(1);
            throw new InvalidOperationException("Salary payment silently passed its native unsupported boundary.");
        }
        catch (NotSupportedException)
        {
            Require(moving.SalaryMovementCounter == 0x5FFF, "Unsupported salary payment modified its source counter.");
        }
        GameSession warm = GameSession.CreateColdProcess();
        warm.StartNewGame();
        warm.SetSeeD(warm.SeeD!.WithSalaryEnabled(true));
        warm.StartNewGame();
        Require(warm.SeeD is { RankPoints: 500, SalarySuppressed: true, SalaryUiEnabled: true, SalaryMovementCounter: 0 } && warm.ExamTimer!.ClockCalls == 0 && !warm.ReplayJournal!.IsComplete, "Warm New Game reset process salary UI or published an unsupported fresh journal root.");
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
