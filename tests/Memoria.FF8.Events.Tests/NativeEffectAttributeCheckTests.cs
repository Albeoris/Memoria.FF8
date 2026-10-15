using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeEffectAttributeCheckContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        EventEngine Create(int argument, bool missingArgument = false)
        {
            List<(Jsm.Opcode, int)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 903), (Jsm.Opcode.POPI_L, 1)];
            if (!missingArgument)
            {
                operations.AddRange([(Jsm.Opcode.PSHN_L, 12345), (Jsm.Opcode.PSHN_L, argument)]);
            }
            operations.AddRange([(Jsm.Opcode.Unknown13, 0), (Jsm.Opcode.POPM_L, 6000), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 6004), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_L, 6008), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.POPM_B, 6012), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(600, SyntheticProgram(operations.ToArray()), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        foreach (int argument in new[] { 1, -1, 0x123456 })
        {
            EventEngine live = Create(argument);
            int executions = 0;
            live.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.Unknown13))
                {
                    executions++;
                    JsonElement fiber = live.Capture().GetProperty("Fibers").EnumerateArray().Single();
                    Require(command.Arguments.SequenceEqual(new[] { argument }) && fiber.GetProperty("Stack").EnumerateArray().Single().GetInt32() == 12345 && fiber.GetProperty("Results").GetProperty("0").GetInt32() == 0 && fiber.GetProperty("Results").GetProperty("1").GetInt32() == 903 && fiber.GetProperty("WaitReason").GetString() == "None", "Native520200 lost its full DWORD forwarding, single pop, R0-only zero result or immediate completion.");
                }
            };
            live.Step();
            JsonElement current = live.Capture();
            Require(executions == 1 && current.GetProperty("Variables").GetProperty("6000").GetInt32() == 12345 && current.GetProperty("Variables").GetProperty("6004").GetInt32() == 0 && current.GetProperty("Variables").GetProperty("6008").GetInt32() == 903 && current.GetProperty("Variables").GetProperty("6012").GetInt32() == 99 && live.Frame == 1, "Native520200 did not preserve the lower operand/R1 or return2 to its ordinary successor in the same callback.");
            EventEngine restored = Create(argument);
            restored.Restore(current);
            for (int tick = 0; tick < 3; tick++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Native520200 result/stack continuation diverged after independent restore.");
            }
        }
        EventEngine refused = Create(1, missingArgument: true);
        try
        {
            refused.Step();
            throw new InvalidOperationException("Native520200 admitted an absent operand.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == nameof(Jsm.Opcode.Unknown13))
        {
        }
        JsonElement refusedFiber = refused.Capture().GetProperty("Fibers").EnumerateArray().Single();
        Require(refusedFiber.GetProperty("Position").GetInt32() == 5 && refusedFiber.GetProperty("Results").GetProperty("0").GetInt32() == 901 && refusedFiber.GetProperty("Results").GetProperty("1").GetInt32() == 903 && refusedFiber.GetProperty("Stack").GetArrayLength() == 0, "Native520200 refusal changed its original PC, operands or result bank.");
        Console.WriteLine("Native520200/Unknown13: full DWORD forwarded once, original empty SdEffectAttrCheck returns R0 zero, R1/lower stack preserved, same-callback return2, independent restore and atomic missing-operand refusal passed.");
    }
}
