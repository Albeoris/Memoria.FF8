using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDirectedLinearTurnContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.LTURNR, Jsm.Opcode.LTURNL, Jsm.Opcode.Unknown6, Jsm.Opcode.Unknown7 })
        {
            (int, int, int, int)[] cases = opcode == Jsm.Opcode.Unknown6 ? [(20, 159, 30, -97), (56, 248, 15, -8), (200, 10, 4, 10), (5, 65530, 65540, -6), (0, 128, 4, -128), (5, 5, 2, 5), (5, 10, 0, -246), (195, 32767, 4, 32511), (255, -32768, 4, -32768)] : opcode == Jsm.Opcode.Unknown7 ? [(248, 56, 15, 312), (10, 200, 4, 200), (234, 65541, 65540, 261), (0, 128, 4, 128), (5, 5, 2, 5), (10, 5, 0, 261), (195, 32767, 4, 32767), (255, -32768, 4, -32512)] : [(195, 184, 30, 184), (234, 65541, 65540, 261), (0, 128, 4, 128), (5, 5, 2, 5), (10, 5, 0, 5), (195, 32767, 4, 32511), (255, -32768, 4, -32512)];
            foreach ((int start, int rawTarget, int rawDuration, int target) in cases)
            {
                byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, start), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, rawTarget), (Jsm.Opcode.PSHN_L, rawDuration), (opcode, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
                (GameSession Session, EventEngine Engine) Create()
                {
                    GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
                    EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
                    engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                    return (session, engine);
                }
                var live = Create();
                List<int[]> consumed = [];
                live.Engine.CommandExecuted += command =>
                {
                    if (command.Name == opcode.ToString())
                    {
                        consumed.Add(command.Arguments.ToArray());
                    }
                };
                live.Engine.Step();
                live.Engine.Step();
                ActorState actor = live.Engine.Actors[0];
                int duration = unchecked((byte)rawDuration);
                Require(consumed.Count == 1 && consumed[0].SequenceEqual(new[] { rawTarget, rawDuration }) && actor.Direction == start && actor.RotationStart == start && actor.RotationTotalFrames == duration && actor.RotationTarget == (duration == 0 ? null : target) && actor.RotationFrames == duration && actor.RotationUsesNativeLinear == (duration != 0) && !live.Session.State.Variables.ContainsKey(5000), "Directed linear turn lost original operands, signed target, BYTE duration, elapsed0 sample or always-wait issue semantics.");
                Require(actor.NativeMotionAuthority == new NativeMotionAuthority(1, 0, 0) && actor.NativeTurnAccumulator474 == 0 && actor.NativeMovementCompletion542 == 0 && actor.CollisionEnabled, "Linear turn changed unrelated575/576,474,542 or588 ownership.");
                var independent = Create();
                independent.Engine.Restore(live.Engine.Capture());
                for (int elapsed = 1; elapsed <= duration; elapsed++)
                {
                    live.Engine.Step();
                    independent.Engine.Step();
                    byte expected = unchecked((byte)NativeJumpMath.Interpolate(start, target, (short)duration, (short)elapsed));
                    Require(actor.Direction == expected && actor.Direction == MathF.Truncate(actor.Direction) && !live.Session.State.Variables.ContainsKey(5000) && JsonElement.DeepEquals(live.Engine.Capture(), independent.Engine.Capture()), "Native LTURN lost integer signed division, lowBYTE577, inclusive endpoint or independent restoration.");
                }
                Require(actor.RotationTarget is null && !actor.RotationUsesNativeLinear, "Directed linear turn did not finish after duration+1 physical samples.");
                live.Engine.Step();
                independent.Engine.Step();
                Require(live.Session.State.Variables.GetValueOrDefault(5000) == 77 && consumed.Count == 1 && JsonElement.DeepEquals(live.Engine.Capture(), independent.Engine.Capture()), "Directed linear completion did not resume its successor on the following VM poll without another operand pop.");
            }
        }
        NativeDirectedLinearTurnMoveConcurrency(vitals, economy);
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.LTURNR, Jsm.Opcode.LTURNL, Jsm.Opcode.Unknown6, Jsm.Opcode.Unknown7 })
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine unknown = new(session);
            unknown.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 184), (Jsm.Opcode.PSHN_L, 30), (opcode, 0), (Jsm.Opcode.RET, 8)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            JsonNode fractional = JsonNode.Parse(unknown.Capture().GetRawText())!;
            fractional["Actors"]![0]!["Direction"] = 193.9f;
            unknown.Restore(JsonSerializer.SerializeToElement(fractional));
            try
            {
                unknown.Step();
                throw new InvalidOperationException("Native linear turn rounded an unproved fractional body direction.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement fiber = unknown.Capture().GetProperty("Fibers")[0];
            Require(unknown.LastFault?.Operation == 3 && fiber.GetProperty("Position").GetInt32() == 3 && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 30, 184 }) && unknown.Actors[0].RotationTarget is null && unknown.Actors[0].Direction == 193.9f, "Unproved linear turn changed original PC, operands or facing state.");
        }
        Console.WriteLine("Native LTURNR/LTURNL/opcode363/364: signed WORD targets, BYTE durations, nearest and negative/positive-wrap selection, equal/zero issue waits, integer inclusive samples, MOVE concurrency and independent restoration pass.");
    }

    private static GameSession CreateDirectedLinearTurnSession(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(vitals.DeepCopy());
        session.InitializeNewGameEconomy(economy.DeepCopy());
        return session;
    }

    private static void NativeDirectedLinearTurnMoveConcurrency(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.LTURNR, Jsm.Opcode.LTURNL, Jsm.Opcode.Unknown6, Jsm.Opcode.Unknown7 })
        {
            int start = opcode == Jsm.Opcode.Unknown6 ? 56 : opcode == Jsm.Opcode.Unknown7 ? 248 : 195;
            int rawTarget = opcode == Jsm.Opcode.Unknown6 ? 248 : opcode == Jsm.Opcode.Unknown7 ? 56 : 184;
            int target = opcode == Jsm.Opcode.Unknown6 ? -8 : opcode == Jsm.Opcode.Unknown7 ? 312 : 184;
            short duration = opcode == Jsm.Opcode.Unknown7 ? (short)15 : (short)30;
            (Jsm.Opcode, int)[] peer = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] actor = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, start), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] lower = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, rawTarget), (Jsm.Opcode.PSHN_L, duration), (opcode, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] upper = [(Jsm.Opcode.LBL, 20), (Jsm.Opcode.PSHN_L, 500), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            byte[] content = SyntheticObjects([0, (16 << 7) | 4, (32 << 7) | 1], [peer, actor, lower, [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.RET, 8)], upper, [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            EventEngine Create()
            {
                GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
                EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4, CanUseNativeScriptMove = body => body.ObjectId == 1 };
                engine.ResolveNativeScriptDirection = query =>
                {
                    ActorState body = engine.Actors[1];
                    if (body.NativeScriptMovement is not null && body.RotationTarget is not null)
                    {
                        int elapsed = body.RotationTotalFrames - body.RotationFrames;
                        byte expected = unchecked((byte)NativeJumpMath.Interpolate(start, target, duration, (short)elapsed));
                        Require(query.FacingDirection == expected, "Concurrent MOVE did not read the same callback's native linear BYTE577 sample before overwriting facing.");
                    }
                    return new NativeScriptDirectionResult(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0);
                };
                // Declared blocked geometry isolates shared577 and turn/movement callback ownership.
                engine.ResolveNativeScriptMovement = query => new NativeScriptMovementResult(NativeScriptMovementStatus.Blocked, query.FixedX, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(query.FixedX, query.FixedY, query.Direction, [new NativeScriptGroundProbe(query.FixedX, query.FixedY, 0), new NativeScriptGroundProbe(query.FixedX, query.FixedY, 0), new NativeScriptGroundProbe(query.FixedX, query.FixedY, 0)])]);
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            EventEngine live = Create();
            for (int callback = 0; callback < 6; callback++)
            {
                live.Step();
            }
            ActorState body = live.Actors[1];
            Require(body.RotationUsesNativeLinear && body.RotationTarget == target && body.NativeScriptMovement is { Active: true, FacingDirection: 64, MovementDirection: 64 } && body.Direction == 64 && body.NativeMovementCompletion542 == 1, "Higher requested MOVE failed to coexist with the lower native linear turn or lost its final body577 write.");
            EventEngine independent = Create();
            independent.Restore(live.Capture());
            for (int callback = 0; callback < 32; callback++)
            {
                live.Step();
                independent.Step();
                Require(body.Direction == 64 && body.NativeScriptMovement is { Active: true } && JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Concurrent turn/MOVE changed facing ownership or diverged after independent pending-request restore.");
            }
            Require(body.RotationTarget is null && !body.RotationUsesNativeLinear, "MOVE cancelled or stalled the independent lower linear-turn clock.");
            JsonElement valid = live.Capture();
            JsonNode malformed = JsonNode.Parse(valid.GetRawText())!;
            malformed["Actors"]![1]!["RotationUsesNativeLinear"] = true;
            malformed["Actors"]![1]!["RotationTarget"] = 32768;
            malformed["Actors"]![1]!["RotationFrames"] = 1;
            try
            {
                live.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Out-of-WORD native linear turn restored above MOVE.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(valid, live.Capture()), "Rejected turn restoration changed the current MOVE or request slots.");
        }
    }
}
