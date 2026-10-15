using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBackgroundShadeStopContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        foreach ((int delay, int rawDuration, Vector3 endpoint) in new[] { (3, 65538, new Vector3(0, 10, 20)), (5, 65536, new Vector3(90, 80, 70)) })
        {
            List<(Jsm.Opcode, int)> first = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.BGDRAW, 0)];
            first.AddRange(new[] { 2, 3, 90, 80, 70, 0, 10, 20, 0, 0 }.Select(value => (Jsm.Opcode.PSHN_L, value)));
            first.AddRange([(Jsm.Opcode.RBGSHADELOOP, 0), (Jsm.Opcode.PSHN_L, delay), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.BGSHADESTOP, 0), (Jsm.Opcode.POPM_B, 6100), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0)]);
            first.AddRange(new[] { rawDuration, 346, 336, 326, 256, 266, 276 }.Select(value => (Jsm.Opcode.PSHN_L, value)));
            first.AddRange([(Jsm.Opcode.BGSHADE, 0), (Jsm.Opcode.PSHN_L, 85), (Jsm.Opcode.POPM_B, 6102), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            (Jsm.Opcode, int)[] second = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.PSHN_L, 120), (Jsm.Opcode.PSHN_L, 110), (Jsm.Opcode.PSHN_L, 80), (Jsm.Opcode.PSHN_L, 70), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.BGSHADE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            byte[] content = SyntheticObjects([0, 16 << 7], [first.ToArray(), second]);
            content[2] = 2;
            content[3] = 0;
            EventEngine Create()
            {
                GameSession session = GameSession.CreateColdProcess();
                session.StartNewGame();
                session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
                session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
                session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
                session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
                EventEngine engine = new(session, session.State.Variables);
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            EventEngine live = Create();
            long loopFrame = -1;
            long stopFrame = -1;
            long shadeFrame = -1;
            int issues = 0;
            live.CommandExecuted += command =>
            {
                if (command.ObjectId != 0)
                {
                    return;
                }
                if (command.Name == nameof(Jsm.Opcode.RBGSHADELOOP))
                {
                    loopFrame = live.Frame;
                }
                if (command.Name == nameof(Jsm.Opcode.BGSHADESTOP))
                {
                    stopFrame = live.Frame;
                    JsonElement fiber = live.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 0);
                    Require(command.Arguments.Count == 0 && fiber.GetProperty("Stack").EnumerateArray().Single().GetInt32() == 77 && live.GetBackgroundModuleShades()[0] == endpoint, "BGSHADESTOP consumed an operand or retained the interpolated color instead of its current target.");
                    Require(!live.Scene.BackgroundShadeLoops.ContainsKey(0) && live.Scene.BackgroundShades[0].FadeFrames == 0 && live.BackgroundStates[0] == 3 && live.Scene.BackgroundShades[1].SecondColor == new Vector3(80, 70, 60), "BGSHADESTOP changed another module, background frame or retained its loop.");
                }
                if (command.Name == nameof(Jsm.Opcode.BGSHADE) && command.Arguments.Count == 7)
                {
                    issues++;
                    shadeFrame = live.Frame;
                    Require(command.Arguments[0] == rawDuration && live.Scene.BackgroundShades[0] is { FadeFrames: var duration, FirstColor: var from, SecondColor: var to } && duration == unchecked((ushort)rawDuration) && from == new Vector3(90, 80, 70) && to == new Vector3(0, 10, 20), "BGSHADE narrowed its command evidence or lost its WORD duration/BYTE endpoints.");
                }
            };
            for (int tick = 0; tick < 16 && stopFrame < 0; tick++)
            {
                live.Step();
            }
            Require(stopFrame == loopFrame + delay && live.Capture().GetProperty("Variables").GetProperty("6100").GetInt32() == 77 && live.GetBackgroundModuleShades()[0] == endpoint, "BGSHADESTOP did not return2 in the same VM budget or lost the phase-boundary target.");
            EventEngine restored = Create();
            restored.Restore(live.Capture());
            for (int tick = 0; tick < 16 && !live.Capture().GetProperty("Variables").TryGetProperty("6102", out _); tick++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Stopped shade or subsequent blocking BGSHADE diverged after independent restore.");
                if (shadeFrame == live.Frame)
                {
                    JsonElement fiber = live.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 0);
                    Require(fiber.GetProperty("WaitReason").GetString() == "NativeBackgroundShade" && fiber.GetProperty("NativeShadeOperation").GetInt32() == fiber.GetProperty("Position").GetInt32() && !live.Capture().GetProperty("Variables").TryGetProperty("6102", out _), "BGSHADE did not retain its issued PC and return1, including duration0.");
                }
            }
            Require(issues == 1 && live.Frame == shadeFrame + unchecked((ushort)rawDuration) + 1 && live.Capture().GetProperty("Variables").GetProperty("6102").GetInt32() == 85 && live.GetBackgroundModuleShades()[0] == new Vector3(0, 10, 20), "BGSHADE did not poll through its inclusive renderer endpoint and then return2 without consuming operands again.");
        }
        Console.WriteLine("BGSHADESTOP: forward phase-boundary/reverse target snap, no operands, same-budget return2, module/frame isolation, subsequent BYTE/WORD BGSHADE issue/poll timing including duration0 and independent restore passed.");
    }
}
