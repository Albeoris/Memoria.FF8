using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeTrainControlByteContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeSalaryTable salary = assets.ReadNativeSalaryTable();
        GameSession CreateSession()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(vitals.DeepCopy());
            session.InitializeNewGameEconomy(economy.DeepCopy());
            session.InitializeNewGameSalaryTable(salary);
            return session;
        }
        for (int value = 0; value <= byte.MaxValue; value++)
        {
            GameSession session = CreateSession();
            NativeWorldControlState original = session.WorldControls!;
            byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, value + 256), (Jsm.Opcode.POPM_B, 103), (Jsm.Opcode.PSHM_B, 103), (Jsm.Opcode.POPM_L, 1000), (Jsm.Opcode.PSHSM_B, 103), (Jsm.Opcode.POPM_L, 1004), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(600, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            Require(session.WorldControls!.EventFlags103 == value && session.WorldControls.EventFlags104 == original.EventFlags104 && session.State.Variables[1000] == value && session.State.Variables[1004] == unchecked((sbyte)value), "Native BYTE103 writer lost truncation, unsigned/signed query or adjacent DWORD104.");
            Require(!session.State.Variables.ContainsKey(103) && session.NativeVariableValues?.ContainsKey(103) != true, "Native train flags acquired a duplicate sparse memory owner.");
            if (value == 0xC2)
            {
                JsonElement captured = engine.Capture();
                GameSession restoredSession = CreateSession();
                EventEngine restored = new(restoredSession, restoredSession.State.Variables);
                restored.LoadField(600, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                restored.Restore(captured);
                Require(JsonElement.DeepEquals(captured, restored.Capture()) && restoredSession.WorldControls == session.WorldControls, "Independent native train-flag restore lost its authentic BYTE103 owner.");
                engine.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Native train flags diverged on the next ordinary callback after restore.");
            }
        }
        GameSession crossed = CreateSession();
        EventEngine crossEngine = new(crossed, crossed.State.Variables);
        crossEngine.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0x654321), (Jsm.Opcode.POPM_L, 103), (Jsm.Opcode.PSHM_L, 103), (Jsm.Opcode.POPM_L, 1000), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.POPM_W, 103), (Jsm.Opcode.PSHM_W, 103), (Jsm.Opcode.POPM_L, 1004), (Jsm.Opcode.PSHSM_W, 103), (Jsm.Opcode.POPM_L, 1008), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        crossEngine.Step();
        Require(crossed.WorldControls is { EventFlags103: 255, EventFlags104: 0x65FF } && crossed.State.Variables[1000] == 0x654321 && crossed.State.Variables[1004] == 65535 && crossed.State.Variables[1008] == -1, "Native WORD/DWORD103 overlap lost one physical bank, truncation or signed query.");
        foreach ((Jsm.Opcode opcode, int address) in new[] { (Jsm.Opcode.PSHM_W, 102), (Jsm.Opcode.POPM_W, 102), (Jsm.Opcode.PSHM_L, 102), (Jsm.Opcode.POPM_L, 102), (Jsm.Opcode.PSHM_W, 107), (Jsm.Opcode.POPM_L, 105) })
        {
            GameSession session = CreateSession();
            NativeWorldControlState original = session.WorldControls!;
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 123), (opcode, address), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            ExpectWorldControlFault(engine, opcode, 2, [123]);
            Require(session.WorldControls == original && session.State.Variables.Count == 0, "Refused surrounding native train memory changed the owner or operands.");
        }
        foreach (int alias in new[] { 100, 102, 103, 104 })
        {
            GameSession session = CreateSession();
            session.State.Variables[alias] = 77;
            NativeWorldControlState original = session.WorldControls!;
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.POPM_B, 103), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            ExpectWorldControlFault(engine, Jsm.Opcode.POPM_B, 2, [123]);
            Require(session.WorldControls == original && session.State.Variables[alias] == 77, "Unproved train-control alias was silently adopted or overwritten.");
        }
        Console.WriteLine("Native train BYTE103: all256 signed/unsigned queries, exact byte truncation, independent restore/next callback and atomic unknown-neighbor/alias refusals pass.");
    }
}
