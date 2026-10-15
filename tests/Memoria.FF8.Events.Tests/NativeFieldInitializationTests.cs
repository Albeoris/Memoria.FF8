using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSerialFieldInitializationContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        List<(Jsm.Opcode, int)> producer = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.PSHM_B, 1025), (Jsm.Opcode.POPM_B, 5001)];
        producer.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), 40));
        producer.AddRange([(Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.LOADSYNC, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 1024), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.RET, 8)]);
        List<(Jsm.Opcode, int)> ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 5000)];
        ordinary.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), 20));
        ordinary.AddRange([(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5008), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        (Jsm.Opcode, int)[] consumer = [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 3), (Jsm.Opcode.PSHM_B, 1024), (Jsm.Opcode.JPF, 6), (Jsm.Opcode.PSHN_L, 496), (Jsm.Opcode.PSHN_L, -314), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 35), (Jsm.Opcode.JMP, 3), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] idle = [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] module = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 1025), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] moduleDefault = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([(16 << 7) | 1, 1, (8 << 7) | 1], [producer.ToArray(), ordinary.ToArray(), consumer, idle, module, moduleDefault]);
        content[2] = 1;
        content[3] = 2;
        EventEngine Create()
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session, session.EventMemory) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        JsonElement allocated = live.Capture();
        List<(int ObjectId, string Name)> commands = [];
        live.CommandExecuted += command => commands.Add((command.ObjectId, command.Name));
        live.Step();
        JsonElement initialized = live.Capture();
        JsonElement values = CapturedVariables(initialized);
        Require(live.Frame == 1 && live.Scene.NativeFieldInitialization?.Stage == NativeFieldInitializationStage.Completed && values.GetProperty("5001").GetInt32() == 7 && values.GetProperty("1024").GetInt32() == 1 && live.Actors[1] is { Active: true, X: 496, Y: -314, WalkmeshTriangle: 35 } && live.Scene.NativeActorObjectIds.SequenceEqual([0, 1]), "Serial initialization exposed a later entity before the prior module/actor producer finished beyond sixteen instructions.");
        Require(commands[0].ObjectId == 2 && commands.All(command => command.Name != "RET") && commands.Count(command => command.Name == "WAIT") == 1 && !values.TryGetProperty("5000", out _) && JsonElement.DeepEquals(allocated.GetProperty("EventRandom"), initialized.GetProperty("EventRandom")) && JsonElement.DeepEquals(allocated.GetProperty("ExamTimer"), initialized.GetProperty("ExamTimer")) && live.Scene.FieldCallbackClock is { OrdinaryCallbacks: 0, MovieCallbacks: 0, InitializationTransactions: 1, TimerCalls: 0 }, "Synchronous initialization called RET/default scripts or advanced callback, RNG or timer ownership while polling WAIT.");
        EventEngine independent = Create();
        independent.Restore(initialized);
        live.Step();
        independent.Step();
        values = CapturedVariables(live.Capture());
        Require(values.GetProperty("5000").GetInt32() == 901 && !values.TryGetProperty("5008", out _) && JsonElement.DeepEquals(live.Capture(), independent.Capture()), "52C960 lost init R0 at default LBL or ordinary callback no longer respected its sixteen-instruction budget.");
        live.Step();
        independent.Step();
        Require(CapturedVariables(live.Capture()).GetProperty("5008").GetInt32() == 77 && JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Ordinary default continuation changed after the serial initialization barrier/independent restore.");
        Console.WriteLine("Native52C650: module-before-actor serial completion beyond16, positive WAIT/LOADSYNC without callbacks, RET precheck, dependent actor retention,52C960 R0 carry and ordinary16-op budget/independent restore PASS.");
    }
}
