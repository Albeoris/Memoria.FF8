using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeActorInfoContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initialVitals = assets.ReadNewGameCharacterVitals();
        EventEngine Create(bool fractional)
        {
            List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -19), (Jsm.Opcode.PSHN_L, 22), (Jsm.Opcode.PSHN_L, -4), (Jsm.Opcode.SET3, fractional ? 0 : 65535), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)];
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 333), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 777), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHN_L, fractional ? 1000 : -321), (Jsm.Opcode.MSPEED, 0)];
            if (fractional)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, -50), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, -4), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0)]);
            }
            main.Add((Jsm.Opcode.GETINFO, 0));
            for (int register = 0; register < 8; register++)
            {
                main.AddRange([(Jsm.Opcode.PSHI_L, register), (Jsm.Opcode.POPM_L, 5000 + register * 4)]);
            }
            main.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PGETINFO, 0)]);
            for (int register = 0; register < 8; register++)
            {
                main.AddRange([(Jsm.Opcode.PSHI_L, register), (Jsm.Opcode.POPM_L, 5040 + register * 4)]);
            }
            main.Add((Jsm.Opcode.RET, 8));
            var session = new GameSession(new GameState { CharacterVitals = initialVitals.DeepCopy(), WorldControls = NativeWorldControlState.CreateNewGame() });
            var engine = new EventEngine(session)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = _ => true,
                ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX - 3072;
                    int y = query.FixedY + 2048;
                    NativeScriptGroundProbe[] probes = [new(x, y, -4), new(x, y, -4), new(x, y, -4)];
                    return new(NativeScriptMovementStatus.Accepted, x, y, query.FixedZ, query.Triangle, query.Direction, [new(x, y, query.Direction, probes)]);
                }
            };
            engine.LoadField(600, SyntheticObjects([1], [init.ToArray(), main.ToArray()]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create(false);
        live.Step();
        live.Step();
        Require(live.Scene.FieldPartyNativeActorIds.SequenceEqual([255, 255, 255]) && live.Scene.PartyBindingMemory is { First: 0 }, "Actor-info fixture did not retain the saved body while FOLLOWOFF cleared runtime mapping.");
        EventEngine restored = Create(false);
        restored.Restore(live.Capture());
        for (int tick = 0; tick < 4; tick++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "GETINFO/PGETINFO register bank diverged after independent restore.");
        }
        JsonElement variables = CapturedVariables(live.Capture());
        int[] expected = [-19, 22, -4, 333, 255, 65535, -321, 777];
        for (int register = 0; register < 8; register++)
        {
            Require(variables.GetProperty((5000 + register * 4).ToString()).GetInt32() == expected[register] && variables.GetProperty((5040 + register * 4).ToString()).GetInt32() == expected[register], "Actor information lost unsigned triangle, signed speed, saved detached body or preserved R3/R7.");
        }
        EventEngine fractional = Create(true);
        fractional.Step();
        fractional.Step();
        Require(fractional.Actors[0].NativeScriptMovement is { FixedX: -80896, FixedY: 92160 }, "Fractional info fixture did not execute its declared native movement adapter.");
        EventEngine fractionalCopy = Create(true);
        fractionalCopy.Restore(fractional.Capture());
        for (int tick = 0; tick < 4; tick++)
        {
            fractional.Step();
            fractionalCopy.Step();
            Require(JsonElement.DeepEquals(fractional.Capture(), fractionalCopy.Capture()), "Fractional actor info or active asynchronous motion diverged after restore.");
        }
        JsonElement fractionalVariables = CapturedVariables(fractional.Capture());
        Require(fractionalVariables.GetProperty("5000").GetInt32() == -19 && fractionalVariables.GetProperty("5004").GetInt32() == 22 && fractionalVariables.GetProperty("5012").GetInt32() == 333 && fractionalVariables.GetProperty("5028").GetInt32() == 777, "Native signed fixed-point division was replaced by flooring or changed untouched result registers.");
        Console.WriteLine("Native GETINFO/PGETINFO: actual allocated body/saved detached party selection, R0/1/2 truncation, R4 BYTE, R5 unsigned WORD sentinel, R6 signed WORD, preserved R3/R7, executed fractional native motion and independent restore passed (synthetic component geometry).");
    }
}
