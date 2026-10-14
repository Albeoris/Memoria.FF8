using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyMenuEnableContract()
    {
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        NativeCardInventory cards = cold.CardInventory!;
        for (int count = 0; count < 100; count++)
        {
            cards = cards.Acquire(76, out _);
        }
        foreach (bool sourceDisabled in new[] { false, true })
        {
            foreach (int argument in new[] { 0, 1, 256, -1, int.MinValue })
            {
                NativeWorldControlState controls = NativeWorldControlState.CreateNewGame().WithPartyPower(sourceDisabled ? 0 : 1) with { Menu209 = 0xAB, EventFlags104 = sourceDisabled ? 0x80000218u : 0x80000018u, MusicKeep207 = 7 };
                byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 76), (Jsm.Opcode.GETCARD, 0), (Jsm.Opcode.PSHN_L, 91), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 57), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.PHSENABLE, 0), (Jsm.Opcode.PSHI_L, 3), (Jsm.Opcode.POPM_L, 1000), (Jsm.Opcode.POPM_L, 1004), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1008), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
                (GameSession session, EventEngine engine) = CreateCardEngine(cards, controls, content);
                session.State.Variables[512] = argument;
                int executions = 0;
                engine.CommandExecuted += command =>
                {
                    if (command.Name == "PHSENABLE")
                    {
                        executions++;
                        Require(command.Arguments.SequenceEqual([argument]), "PHSENABLE narrowed or changed its full DWORD receipt.");
                    }
                };
                engine.Step();
                byte expected = sourceDisabled ? (byte)0xAB : argument == 0 ? (byte)0xA9 : (byte)0xAB;
                Require(executions == 1 && session.WorldControls!.Menu209 == expected && session.WorldControls.EventFlags104 == controls.EventFlags104 && session.WorldControls.MusicKeep207 == 7, "PHSENABLE lost its source-disabled no-write branch or changed another native flag.");
                Require(session.State.Variables[1000] == 91 && session.State.Variables[1004] == 57 && session.State.Variables[1008] == -1, "PHSENABLE changed R0/an unrelated register, popped another operand or yielded before its native return2 successor.");
                JsonElement captured = engine.Capture();
                (GameSession restoredSession, EventEngine restored) = CreateCardEngine(cards, controls, content);
                restored.Restore(captured);
                Require(restoredSession.WorldControls == session.WorldControls && JsonElement.DeepEquals(captured, restored.Capture()), "PHSENABLE current checkpoint lost its canonical native flags.");
                engine.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "PHSENABLE independent restore diverged on its next callback.");
            }
        }
        for (int menu = 0; menu < 256; menu++)
        {
            NativeWorldControlState controls = NativeWorldControlState.CreateNewGame() with { Menu209 = (byte)menu };
            Require(controls.WithPartyMenuEnabled(0).Menu209 == (menu & 0xFD) && controls.WithPartyMenuEnabled(256).Menu209 == (menu | 2), "PHSENABLE did not preserve all unrelated BYTE209 bits.");
            Require(controls.WithSaveEnabled(0).Menu209 == (menu & 0xFE) && controls.WithSaveEnabled(256).Menu209 == (menu | 1), "SAVEENABLE lost its already-canonical bit1 contract.");
        }
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.PHSENABLE, Jsm.Opcode.SAVEENABLE })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 256), (opcode, 0), (Jsm.Opcode.RET, 0));
            (_, EventEngine unknown) = CreateCardEngine(null, null, content);
            bool refused = false;
            try
            {
                unknown.Step();
            }
            catch (EventScriptException)
            {
                refused = true;
            }
            Require(refused && unknown.LastFault?.Opcode == opcode.ToString() && unknown.LastFault.Operation == 2 && unknown.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 256, "Unknown menu/control owner did not refuse atomically at its original command.");

            RejectingMenuControlService service = new(NativeWorldControlState.CreateNewGame());
            EventEngine rejected = new(service, new Dictionary<int, int>());
            rejected.LoadField(100, content, []);
            NativeWorldControlState before = service.WorldControls;
            refused = false;
            try
            {
                rejected.Step();
            }
            catch (EventScriptException)
            {
                refused = true;
            }
            Require(refused && service.Publications == 1 && service.WorldControls == before && rejected.LastFault?.Operation == 2 && rejected.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 256, "Rejected native menu flag publication changed PC/operands/owner.");
        }
        RejectingMenuControlService skipped = new(NativeWorldControlState.CreateNewGame().WithPartyPower(0));
        EventEngine noWriter = new(skipped, new Dictionary<int, int>());
        noWriter.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PHSENABLE, 0), (Jsm.Opcode.RET, 0)), []);
        noWriter.Step();
        Require(noWriter.LastFault is null && skipped.Publications == 0, "Source-disabled PHSENABLE fabricated a native menu-byte write.");
        Console.WriteLine("Native PHSENABLE/SAVEENABLE: full DWORD, exact209 bits, source-disabled no-write, unchangedR0, samecallback, restore and atomic unknown/rejected owner PASS.");
    }

    private sealed class RejectingMenuControlService(NativeWorldControlState controls) : IEventServices
    {
        public NativeWorldControlState WorldControls => controls;
        public int Publications { get; private set; }
        public bool IsMoviePlaying => false;
        public void SetWorldControls(NativeWorldControlState next)
        {
            Publications++;
            throw new NotSupportedException("Deliberately refused control publication.");
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
