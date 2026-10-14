using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMovementAccelerationContract()
    {
        Require(NativeMovementAcceleration.CalculateSpeed(1000, 3000, 0, 0, 100 << 12, 0) == 1400, "MACCEL did not apply clock20 and current remaining distance.");
        Require(NativeMovementAcceleration.CalculateSpeed(100, 200, 8191, 0, 0, 0) == 2100, "MACCEL rounded a negative fractional delta away from zero.");
        Require(NativeMovementAcceleration.CalculateSpeed(100, 200, 0, 0, 0, 0) == 2100, "MACCEL zero distance did not add its entire numerator.");
        Require(NativeMovementAcceleration.CalculateSpeed(32000, -32000, 0, 0, 4096, 0) == -2816, "MACCEL lost native signed WORD wrapping.");
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.MLIMIT, 0), (Jsm.Opcode.PSHN_L, 0x100000 + 100), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0x10000), (Jsm.Opcode.PSHN_L, 0x10000 + 3000), (Jsm.Opcode.MACCEL, 0), (Jsm.Opcode.PSHN_L, 19), (Jsm.Opcode.POPM_B, 1100), (Jsm.Opcode.PSHN_L, 150), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.PSHN_L, 23), (Jsm.Opcode.POPM_B, 1101), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([1], [init, main]);
        Dictionary<EventEngine, Dictionary<int, int>> memory = [];
        int Variable(EventEngine engine, int address) => memory[engine].GetValueOrDefault(address);
        EventEngine Create()
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolveWalkmesh = (x, y) => new(new(x, y, 200), 7),
                CanUseNativeScriptMove = _ => true,
                ResolveNativeScriptDirection = query =>
                {
                    FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
                    NativeScriptDirectionStatus status = result.Status switch
                    {
                        FieldNativeScriptDirectionStatus.Moving => NativeScriptDirectionStatus.Moving,
                        FieldNativeScriptDirectionStatus.Stopped => NativeScriptDirectionStatus.Stopped,
                        FieldNativeScriptDirectionStatus.Snapped => NativeScriptDirectionStatus.Snapped,
                        _ => throw new InvalidOperationException("Synthetic MACCEL direction exceeded the native component domain.")
                    };
                    return new NativeScriptDirectionResult(status, result.FixedX, result.FixedY, result.Direction, result.TurnAccumulator);
                },
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX + 4096;
                    NativeScriptGroundProbe[] fans = [new(x, query.FixedY, 200), new(x, query.FixedY, 200), new(x, query.FixedY, 200)];
                    return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, fans)]);
                }
            };
            memory.Add(engine, session.State.Variables);
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        live.Step();
        ActorState actor = live.Actors[0];
        Require(actor.NativeScriptMovement is { Active: true, Accelerated: true, Speed: 1400, DirectionLimit: 0, HoldsFacing: true } && actor.NativeMovementAccelerationTarget == 3000 && actor.NativeMovementBaseSpeed == 1000 && actor.MovementSpeed == 1400 && actor.Direction == 77 && actor.MovementDirectionLimit == 0 && actor.AnimationId == actor.WalkAnimationId, "MACCEL first issue lost operand width, base/current/target words or held facing.");
        JsonElement saved = live.Capture();
        EventEngine restored = Create();
        restored.Restore(saved);
        NativeScriptMovementState before = actor.NativeScriptMovement!;
        short expected = NativeMovementAcceleration.CalculateSpeed(before.Speed, 3000, before.FixedX, before.FixedY, before.TargetX, before.TargetY);
        live.Step();
        restored.Step();
        Require(actor.MovementSpeed == expected && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "MACCEL retry used initial distance/speed or diverged after independent restore.");
        bool sawRun = false;
        bool completedBeforeSuccessor = false;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.MACCEL) && Variable(live, 1100) == 0 && actor.NativeScriptMovement is { Active: false, Completion: 2 })
            {
                completedBeforeSuccessor = actor.NativeMotionAuthority?.FacingHold == 0 && actor.AnimationId == actor.BaseAnimationId && actor.NativeMovementBaseSpeed == 1000;
            }
        };
        for (int callback = 0; callback < 200 && Variable(live, 1101) != 23; callback++)
        {
            live.Step();
            restored.Step();
            sawRun |= actor.NativeScriptMovement is { Accelerated: true } && actor.AnimationId == actor.RunAnimationId;
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "MACCEL or following ordinary MOVE diverged after restore.");
            if (actor.NativeScriptMovement is { Active: true, Accelerated: false })
            {
                Require(actor.MovementSpeed == 1000 && actor.NativeScriptMovement.Speed == 1000, "MOVE reused MACCEL terminal510 instead of original base512.");
            }
        }
        Require(sawRun && completedBeforeSuccessor && Variable(live, 1100) == 19 && Variable(live, 1101) == 23, "MACCEL did not switch animation, complete then execute its successor in native VM order.");
        EventEngine refused = Create();
        refused.Step();
        refused.ResolveNativeScriptMovement = _ => null;
        try
        {
            refused.Step();
            throw new InvalidOperationException("Unknown MACCEL geometry was admitted.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement fault = refused.Capture();
        JsonElement fiber = fault.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
        Require(refused.LastFault?.Opcode == "MACCEL" && fiber.GetProperty("Stack").GetArrayLength() == 5 && refused.Actors[0].NativeScriptMovement is null && refused.Actors[0].NativeMovementAccelerationTarget is null && refused.Actors[0].MovementSpeed == 1000 && refused.Actors[0].MovementDirectionLimit == 9, "MACCEL preflight changed operands or physical words on refusal.");
        JsonNode malformed = JsonNode.Parse(saved.GetRawText())!;
        malformed["Actors"]![0]!.AsObject().Remove(nameof(ActorState.NativeMovementAccelerationTarget));
        JsonElement current = live.Capture();
        try
        {
            live.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Missing current WORD516 shape was inferred.");
        }
        catch (JsonException)
        {
        }
        Require(JsonElement.DeepEquals(current, live.Capture()), "Malformed MACCEL restore mutated the VM.");
        NativeAccelerationRequestContract();
    }

    private static void NativeAccelerationRequestContract()
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] lower = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 3000), (Jsm.Opcode.MACCEL, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] upper = [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHN_L, 2000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 500), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 2500), (Jsm.Opcode.MACCEL, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] controllerInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] controller = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([2, (16 << 7) | 1], [init, lower, upper, controllerInit, controller]);
        EventEngine Create()
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = _ => true,
                ResolveNativeScriptDirection = query => query.TargetX == 500 * 4096 && query.FixedX >= 3 * 4096 ? new(NativeScriptDirectionStatus.Snapped, query.TargetX, query.TargetY, query.MovementDirection, query.TurnAccumulator) : new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX + 4096;
                    NativeScriptGroundProbe[] fans = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                    return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, fans)]);
                }
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        while (live.Actors[0].NativeMovementRequest is null && live.Frame < 12)
        {
            live.Step();
        }
        NativeMovementRequestState original = live.Actors[0].NativeMovementRequest ?? throw new InvalidOperationException("REQ did not suspend lower MACCEL.");
        Require(original.SavedMovement.Accelerated && live.Actors[0].NativeMovementAccelerationTarget == 3000, "REQ copied the wrong native movement or changed516.");
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        short? upperTerminal = null;
        bool terminalBackupObserved = false;
        live.CommandExecuted += command =>
        {
            ActorState body = live.Actors[0];
            if (command.ScriptId == 2 && command.Name == "MACCEL" && body.NativeScriptMovement is { Active: false, Completion: 2 } finished)
            {
                upperTerminal = finished.Speed;
                terminalBackupObserved = body.NativeMovementRequest?.SavedMovement.Speed == finished.Speed && body.NativeMovementBaseSpeed == 2000;
            }
        };
        for (int callback = 0; callback < 12 && live.Actors[0].NativeMovementRequest is not null; callback++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Nested MACCEL request diverged after independent restore.");
        }
        ActorState actor = live.Actors[0];
        Require(terminalBackupObserved && upperTerminal is not null && actor.NativeMovementRequest is null && actor.NativeScriptMovement is { ScriptId: 1, TargetX: 4096000, Accelerated: true } && actor.NativeMovementAccelerationTarget == 2500 && actor.NativeMovementBaseSpeed == 2000 && actor.NativeScriptMovement.Speed == NativeMovementAcceleration.CalculateSpeed(upperTerminal.Value, 2500, 500 << 12, 0, 1000 << 12, 0), $"RET8 state: terminal={upperTerminal}, observed={terminalBackupObserved}, target={actor.NativeMovementAccelerationTarget}, base={actor.NativeMovementBaseSpeed}, movement={actor.NativeScriptMovement}, backup={actor.NativeMovementRequest}.");
        NativeScriptMovementState before = actor.NativeScriptMovement!;
        short expected = NativeMovementAcceleration.CalculateSpeed(before.Speed, 2500, before.FixedX, before.FixedY, before.TargetX, before.TargetY);
        live.Step();
        restored.Step();
        Require(actor.MovementSpeed == expected && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Resumed lower MACCEL did not consume the actual retained higher target516.");
    }
}
