using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePositiveCosineTurnContract()
    {
        (GameSession Session, EventEngine Engine) Create(int start, int target, int duration, bool native = true)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, start), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, target), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.Unknown9, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: native ? NativeFieldRuntimeProfile.FF8En : null);
            return (session, engine);
        }
        foreach ((int start, int rawTarget, int rawDuration, int target, int duration) in new[] { (234, 124, 35, 380, 35), (234, 35, 124, 291, 124), (10, 200, 4, 200, 4), (234, 65571, 257, 291, 1), (234, -300, 3, -44, 3), (234, 35, 0, 291, 0) })
        {
            var live = Create(start, rawTarget, rawDuration);
            List<int[]> operands = [];
            live.Engine.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.Unknown9))
                {
                    operands.Add(command.Arguments.ToArray());
                }
            };
            live.Engine.Step();
            ActorState actor = live.Engine.Actors[0];
            Require(actor.Direction == start && actor.RotationStart == start && actor.RotationTarget == target && actor.RotationFrames == duration + 1 && actor.RotationTotalFrames == duration && actor.RotationUsesNativeCosine && !live.Session.State.Variables.ContainsKey(5000), "Unknown9 failed signedWORD/+256 target, BYTE duration or its initial return1 yield.");
            Require(operands.Count == 1 && operands[0].SequenceEqual(new[] { rawTarget, rawDuration }), "Unknown9 changed full DWORD command evidence or consumed operands twice.");
            var restored = Create(start, rawTarget, rawDuration);
            restored.Engine.Restore(live.Engine.Capture());
            for (int elapsed = 0; elapsed <= duration; elapsed++)
            {
                live.Engine.Step();
                restored.Engine.Step();
                byte expected = unchecked((byte)NativeCosineInterpolation.Interpolate(start, target, duration, elapsed));
                Require(actor.Direction == expected && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Unknown9 lost its native elapsed0/endpoint cosine or independent restored continuation.");
                Require(live.Session.State.Variables.ContainsKey(5000) == (elapsed == duration), "Unknown9 resumed before BYTE580 completion or inserted an extra return2 budget yield.");
                if (elapsed == duration / 2)
                {
                    restored = Create(start, rawTarget, rawDuration);
                    restored.Engine.Restore(live.Engine.Capture());
                }
            }
            Require(actor.RotationTarget is null && !actor.RotationUsesNativeCosine && live.Session.State.Variables[5000] == 77 && operands.Count == 1, "Unknown9 failed completion cleanup or same-budget next command.");
        }
        var invalid = Create(234, 35, 124, false);
        try
        {
            invalid.Engine.Step();
            throw new InvalidOperationException("Unknown9 accepted an unverified native profile.");
        }
        catch (EventScriptException) when (invalid.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.Unknown9))
        {
        }
        JsonElement refused = invalid.Engine.Capture().GetProperty("Fibers")[0];
        Require(invalid.Engine.Actors[0].Direction == 234 && invalid.Engine.Actors[0].RotationTarget is null && refused.GetProperty("Position").GetInt32() == 5 && refused.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 124, 35 }), "Unknown9 refusal changed its PC, operands or actor turn state.");
        Console.WriteLine("Unknown9: forced direction, signedWORD/BYTE narrowing, cosine elapsed0 and endpoint, return budget, mid-turn restore and atomic native-profile refusal passed.");
    }
}
