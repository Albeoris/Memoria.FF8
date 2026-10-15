using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBackgroundShadeOffContract(string gameDirectory)
    {
        foreach (bool loop in new[] { false, true })
        {
            List<(Jsm.Opcode, int)> first = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.BGDRAW, 0)];
            int[] colors = loop ? [25, 22, 126, 100, 90, 10, 20, 30, 1, 1] : [20, 126, 100, 90, 10, 20, 30];
            first.AddRange(colors.Select(value => (Jsm.Opcode.PSHN_L, value)));
            first.Add((loop ? Jsm.Opcode.RBGSHADELOOP : Jsm.Opcode.BGSHADE, 0));
            first.AddRange([(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.BGSHADEOFF, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0)]);
            first.AddRange(colors.Select(value => (Jsm.Opcode.PSHN_L, value)));
            first.Add((loop ? Jsm.Opcode.RBGSHADELOOP : Jsm.Opcode.BGSHADE, 0));
            first.AddRange([(Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
            (Jsm.Opcode, int)[] second = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.PSHN_L, 120), (Jsm.Opcode.PSHN_L, 110), (Jsm.Opcode.PSHN_L, 80), (Jsm.Opcode.PSHN_L, 70), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.BGSHADE, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)];
            byte[] content = SyntheticObjects([0, 16 << 7], [first.ToArray(), second]);
            content[2] = 2;
            content[3] = 0;
            EventEngine Create()
            {
                GameSession session = new(new GameState());
                EventEngine engine = new(session);
                engine.LoadField(100, content, []);
                return engine;
            }
            EventEngine engine = Create();
            bool reset = false;
            engine.CommandExecuted += command =>
            {
                if (command.Name == "BGSHADEOFF")
                {
                    Require(command.Arguments.Count == 0 && engine.GetBackgroundModuleShades()[0] == Vector3.Zero, "BGSHADEOFF must consume no operands and immediately present the native zero RGB endpoint.");
                    Require(!engine.Scene.BackgroundShadeLoops.ContainsKey(0) && engine.Scene.BackgroundShades[0] is { FadeFrames: 0, FirstColor: var firstColor, SecondColor: var secondColor } && firstColor == Vector3.Zero && secondColor == Vector3.Zero, "BGSHADEOFF retained a loop, duration or color endpoint.");
                    Require(engine.BackgroundStates[0] == 3 && engine.Scene.BackgroundShades[1].FirstColor == new Vector3(128, 120, 110), "BGSHADEOFF changed module frame selection or another module's color.");
                    reset = true;
                }
            };
            for (int tick = 0; tick < 10 && !reset; tick++)
            {
                engine.Step();
            }
            Require(reset, "Source module never executed BGSHADEOFF.");
            JsonElement saved = engine.Capture();
            EventEngine independent = Create();
            independent.Restore(saved);
            Require(JsonElement.DeepEquals(saved, independent.Capture()), "Shade reset and complete VM did not restore independently.");
            for (int tick = 0; tick < 12; tick++)
            {
                engine.Step();
                independent.Step();
                Require(JsonElement.DeepEquals(engine.Capture(), independent.Capture()), "Independent shade reset/restart progression diverged.");
            }
            Require(engine.GetBackgroundModuleShades()[0] != Vector3.Zero && (!loop || engine.Scene.BackgroundShadeLoops.ContainsKey(0)), "A later original shade command failed to restart after BGSHADEOFF.");
        }
        byte[] nonModule = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.BGSHADEOFF, 0), (Jsm.Opcode.POPM_B, 1000), (Jsm.Opcode.RET, 0));
        GameSession invalidSession = new(new GameState());
        EventEngine invalid = new(invalidSession);
        invalid.LoadField(100, nonModule, []);
        try
        {
            invalid.Step();
            throw new InvalidOperationException("Nonmodule shade fields were silently mapped to module0.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement fault = invalid.Capture();
        Require(invalid.LastFault?.Opcode == "BGSHADEOFF" && fault.GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2 && fault.GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 77 && invalid.Scene.BackgroundShades.Count == 0, "Nonmodule refusal changed the issuing PC, operand or background shades.");

        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BGSHADE_SOURCE");
        if (source is not null)
        {
            using GameAssets assets = new(gameDirectory);
            GameSession session = new(GameSession.Load(source));
            FieldAssets field = assets.LoadField(session.State.FieldId);
            EventEngine actual = new(session) { AnimationFrameCount = field.GetAnimationFrameCount };
            actual.LoadField(field.Id, field.Jsm, field.Messages, session.State.FieldArrival, nativeRuntimeProfile: assets.NativeRuntimeProfile);
            actual.Restore(session.State.EventState!.Value);
            bool crossed = false;
            actual.CommandExecuted += command =>
            {
                if (command.Name == "BGSHADEOFF")
                {
                    int module = JsmProgram.Read(field.Jsm).Objects[command.ObjectId].BackgroundModuleIndex!.Value;
                    Require(actual.GetBackgroundModuleShades()[module] == Vector3.Zero, "Reached original shade-off did not reset its actual source module.");
                    crossed = true;
                }
            };
            actual.Step();
            Require(crossed && actual.LastFault is null, "Reached original unsupported shade-off failed to execute under the shared module contract.");
        }
        Console.WriteLine("BGSHADEOFF: zero RGB reset cancels one-shot/loop state, preserves other module/frame selection, restarts and independently restores; nonmodule context refuses before PC/operand changes.");
    }
}
