using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeNegativeCosineTurnContract()
    {
        using (GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII"))
        {
            JsmProgram original = JsmProgram.Read(assets.LoadField(141).Jsm);
            Require(original.Operations[153].Opcode == Jsm.Opcode.PSHN_L && original.Operations[153].Parameter == 186 && original.Operations[154].Opcode == Jsm.Opcode.PSHN_L && original.Operations[154].Parameter == 23 && original.Operations[155].Opcode == Jsm.Opcode.Unknown8 && original.Operations[155].Parameter == 365, "Original141/Zell turn instruction or DWORD operands changed.");
        }
        (GameSession Session, EventEngine Engine) Create(int start, int target, int duration, bool native = true)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, start), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, target), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.Unknown8, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: native ? NativeFieldRuntimeProfile.FF8En : null);
            return (session, engine);
        }
        foreach ((int start, int rawTarget, int rawDuration, int target, int duration) in new[] { (58, 186, 23, -70, 23), (234, 35, 124, 35, 124), (10, 200, 4, -56, 4), (58, 65722, 257, -70, 1), (58, -300, 3, -300, 3), (58, 186, 0, -70, 0), (58, 58, 3, 58, 3), (255, 32767, 2, 32511, 2) })
        {
            var live = Create(start, rawTarget, rawDuration);
            List<int[]> operands = [];
            live.Engine.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.Unknown8))
                {
                    operands.Add(command.Arguments.ToArray());
                }
            };
            live.Engine.Step();
            ActorState actor = live.Engine.Actors[0];
            Require(actor.Direction == unchecked((byte)NativeCosineInterpolation.Interpolate(start, target, duration, 0)) && actor.RotationStart == start && actor.RotationTarget == (duration == 0 ? null : target) && actor.RotationFrames == duration && actor.RotationTotalFrames == duration && actor.RotationUsesNativeCosine == (duration > 0) && !live.Session.State.Variables.ContainsKey(5000), "Unknown8 failed signedWORD/-256 target, BYTE duration or its same-callback post-VM elapsed0 sample.");
            Require(operands.Count == 1 && operands[0].SequenceEqual(new[] { rawTarget, rawDuration }), "Unknown8 changed full DWORD command evidence or consumed operands twice.");
            var restored = Create(start, rawTarget, rawDuration);
            restored.Engine.Restore(live.Engine.Capture());
            for (int elapsed = 1; elapsed <= duration; elapsed++)
            {
                live.Engine.Step();
                restored.Engine.Step();
                byte expected = unchecked((byte)NativeCosineInterpolation.Interpolate(start, target, duration, elapsed));
                Require(actor.Direction == expected && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Unknown8 lost its native elapsed0/endpoint cosine or independent restored continuation.");
                Require(!live.Session.State.Variables.ContainsKey(5000), "Unknown8 resumed before the next VM visit after post-VM BYTE580 completion.");
                if (elapsed == duration / 2)
                {
                    restored = Create(start, rawTarget, rawDuration);
                    restored.Engine.Restore(live.Engine.Capture());
                }
            }
            live.Engine.Step();
            restored.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Unknown8 completion continuation diverged after restore.");
            Require(actor.RotationTarget is null && !actor.RotationUsesNativeCosine && live.Session.State.Variables[5000] == 77 && operands.Count == 1, "Unknown8 failed completion cleanup or same-budget next command.");
        }
        var invalid = Create(58, 186, 23, false);
        try
        {
            invalid.Engine.Step();
            throw new InvalidOperationException("Unknown8 accepted an unverified native profile.");
        }
        catch (EventScriptException) when (invalid.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.Unknown8))
        {
        }
        JsonElement refused = invalid.Engine.Capture().GetProperty("Fibers")[0];
        Require(invalid.Engine.Actors[0].Direction == 58 && invalid.Engine.Actors[0].RotationTarget is null && refused.GetProperty("Position").GetInt32() == 5 && refused.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 23, 186 }), "Unknown8 refusal changed its PC, operands or actor turn state.");
        Console.WriteLine("Unknown8: forced direction, signedWORD/BYTE narrowing, cosine elapsed0 and endpoint, return budget, mid-turn restore and atomic native-profile refusal passed.");
    }
}
