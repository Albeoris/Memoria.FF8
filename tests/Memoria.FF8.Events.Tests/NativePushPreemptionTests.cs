using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeEmptyPushPreemptionContract()
    {
        (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] peer = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([0, (16 << 7) | 3], [player, peer, main, [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            GameSession session = new(new GameState());
            EventEngine result = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = actor => actor.ObjectId == 1,
                ResolveNativeScriptDirection = query =>
                {
                    FieldNativeScriptDirectionResult direction = FieldNativeScriptDirectionProbe.Resolve(new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
                    return new((NativeScriptDirectionStatus)direction.Status, direction.FixedX, direction.FixedY, direction.Direction, direction.TurnAccumulator);
                },
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX + 4096;
                    NativeScriptGroundProbe[] probes = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                    return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, probes)]);
                }
            };
            result.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return result;
        }
        EventEngine live = Create();
        live.Step();
        live.Step();
        ActorState actor = live.Actors[1];
        NativeScriptMovementState movement = actor.NativeScriptMovement!;
        Require(movement is { Active: true, Completion: 1 } && actor.AnimationId == 1, $"Synthetic native MOVE failed to establish its genuine held instruction and full walk animation: {movement}, animation{actor.AnimationId}");
        typeof(ActorState).GetProperty(nameof(ActorState.NativePushContactPending))!.SetValue(actor, true);
        JsonElement beforePush = live.Capture();
        EventEngine restored = Create();
        restored.Restore(beforePush);
        bool suspensionObserved = false;
        live.CommandExecuted += command =>
        {
            if (command.ScriptId == 19 && command.Name == "LBL")
            {
                NativeScriptMovementState suspended = actor.NativeScriptMovement!;
                Require(suspended == movement with { Suspended = true }, "Empty native push changed the held movement's target, raw pose, direction, owner or operands during suspension.");
                JsonElement paused = live.Capture();
                JsonElement owner = paused.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Sequence").GetInt64() == movement.FiberSequence);
                Require(owner.GetProperty("Position").GetInt32() == movement.Operation && owner.GetProperty("WaitReason").GetString() == nameof(WaitReason.NativeScriptMovement) && owner.GetProperty("Stack").GetArrayLength() == 0, "Native push altered the suspended lower slot's original PC or operand depth.");
                EventEngine pausedCopy = Create();
                pausedCopy.Restore(paused);
                Require(JsonElement.DeepEquals(paused, pausedCopy.Capture()), "Native contact slot/suspension failed an exact independent checkpoint validation.");
                suspensionObserved = true;
            }
        };
        PlayerAction wait = new(PlayerActionKind.Wait);
        Require(live.Apply(wait) == restored.Apply(wait) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Native MOVE push/resume diverged after an independent ordinary-boundary restore.");
        Require(suspensionObserved && !actor.NativePushContactPending && actor.NativeScriptMovement is { Active: true, Suspended: false, Completion: 1 } resumed && resumed.FiberSequence == movement.FiberSequence && resumed.Operation == movement.Operation && resumed.TargetX == movement.TargetX && resumed.FixedX == movement.FixedX + 4096, "Empty native push failed same-budget RET/resume and actual subsequent native movement.");
        Console.WriteLine("Native empty local3/slot6 push preemption: authentic MOVE target/speed/raw state retained, operand owner/PC preserved, mode0→RET mode1/completion0, same callback lower-slot resumption and independent restore passed (synthetic component geometry).");
    }
}
