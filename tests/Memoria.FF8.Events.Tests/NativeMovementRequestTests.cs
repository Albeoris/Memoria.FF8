using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMovementRequestContract()
    {
        foreach ((bool halt, bool transfer) in new[] { (false, false), (true, false), (true, true) })
        {
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] actor = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] lower = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHAC, 0), (Jsm.Opcode.PSHN_L, 48), (Jsm.Opcode.MOVEA, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] upper = [(Jsm.Opcode.LBL, 20), (Jsm.Opcode.PSHN_L, 22), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 2000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 500), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (halt ? Jsm.Opcode.HALT : Jsm.Opcode.NOP, 0), (Jsm.Opcode.RET, 8)];
            List<(Jsm.Opcode, int)> controller = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.REQ, 1)];
            if (transfer)
            {
                controller.AddRange([(Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 21), (Jsm.Opcode.REQSW, 1)]);
            }
            controller.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            byte[] content = SyntheticObjects([0, (16 << 7) | 5, (32 << 7) | 1], [target, actor, lower, [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.RET, 8)], upper, [(Jsm.Opcode.LBL, 21), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.RET, 8)], controller.ToArray()]);
            EventEngine Create()
            {
                var session = new GameSession(new GameState());
                var result = new EventEngine(session, session.State.Variables);
                result.AnimationFrameCount = (_, _) => 4;
                result.CanUseNativeScriptMove = actor => actor.ObjectId == 1;
                result.ResolveNativeScriptDirection = query => query.TargetX == 500 * 4096 && query.FixedX >= 3 * 4096 ? new(NativeScriptDirectionStatus.Snapped, query.TargetX, query.TargetY, query.MovementDirection, query.TurnAccumulator) : new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0);
                result.ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX + 4096;
                    NativeScriptGroundProbe[] fans = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                    return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, fans)]);
                };
                result.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return result;
            }
            EventEngine live = Create();
            while (live.Actors[1].NativeMovementRequest is null && live.Frame < 12)
            {
                live.Step();
            }
            NativeMovementRequestState backup = live.Actors[1].NativeMovementRequest ?? throw new InvalidOperationException("Original REQ did not suspend the held MOVEA.");
            Require(backup.SavedMovement is { TargetNativeObjectId: 0, Speed: 1000, StopDistance: 48 } && live.Actors[1].NativeScriptMovement is { Suspended: true }, "Native request changed the backup target, original speed or mode0 suspension.");
            JsonElement pending = live.Capture();
            EventEngine restored = Create();
            restored.Restore(pending);
            bool replacementObserved = false;
            for (int tick = 0; tick < 12; tick++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Pending native request/held MOVEA continuation diverged after independent restore.");
                if (!replacementObserved && live.Actors[1].NativeScriptMovement is { ScriptId: 20, Active: true })
                {
                    replacementObserved = true;
                    JsonElement during = live.Capture();
                    EventEngine duringCopy = Create();
                    duringCopy.Restore(during);
                    foreach (string property in new[] { "Speed", "TargetX", "Operation" })
                    {
                        JsonNode invalid = JsonNode.Parse(during.GetRawText())!;
                        JsonNode saved = invalid["Actors"]![1]!["NativeMovementRequest"]!["SavedMovement"]!;
                        saved[property] = property == "Speed" ? -1 : property == "TargetX" ? int.MaxValue : saved[property]!.GetValue<int>() + 1;
                        try
                        {
                            duringCopy.Restore(JsonSerializer.SerializeToElement(invalid));
                            throw new InvalidOperationException($"Corrupt saved native movement backup {property} passed validation.");
                        }
                        catch (InvalidDataException)
                        {
                        }
                        Require(JsonElement.DeepEquals(during, duringCopy.Capture()), "Invalid movement backup restore partially changed current motion or fibers.");
                    }
                }
            }
            Require(replacementObserved, "Higher native slot did not issue its own full four-operand MOVE above held MOVEA.");
            JsonElement lowerFiber = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
            Require(lowerFiber.GetProperty("Stack")[0].GetInt32() == 0 && lowerFiber.GetProperty("Results").GetProperty("1").GetInt32() == 11, "Request replaced the lower MOVEA remaining target operand or result bank.");
            if (halt && !transfer)
            {
                JsonElement upperFiber = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 20);
                Require(live.Actors[1].NativeMovementRequest == backup && upperFiber.GetProperty("WaitReason").GetString() == "NativeHalt" && !upperFiber.GetProperty("Complete").GetBoolean() && live.Actors[1].X == 500, "HALT released the higher slot, restored suspended MOVEA or advanced the held body.");
            }
            else
            {
                Require(live.Actors[1].NativeMovementRequest is null && live.Actors[1].NativeScriptMovement is { ScriptId: 17, Active: true, Speed: 1000, TargetNativeObjectId: 0, AnimationFlag40000: false } && live.Actors[1].NativeMovementBaseSpeed == 2000 && live.Actors[1].X > 500 && live.Actors[1].AnimationId == live.Actors[1].BaseAnimationId, $"RET8 failed actual-current-position/lower-speed restoration or incorrectly restored cleared movement-animation flags/base speed: {live.Actors[1].NativeScriptMovement}, backup={live.Actors[1].NativeMovementRequest}, base={live.Actors[1].NativeMovementBaseSpeed}, X={live.Actors[1].X}, animation={live.Actors[1].AnimationId}.");
                if (transfer)
                {
                    JsonElement heldUpper = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 20);
                    Require(heldUpper.GetProperty("WaitReason").GetString() == "NativeHalt" && !heldUpper.GetProperty("Complete").GetBoolean(), "Mode0 higher request transfer released the existing halted slot instead of only restoring the one physical body backup.");
                }
            }
        }
        NativeSingleBackupOverwriteContract();
        NativeTurnMoveCallbackOrderContract();
        NativeHaltedModuleRequestContract();
        Console.WriteLine("Native REQ slot2/MOVE above held default MOVEA: single target/stop/speed backup, independent upper operands/results, current-position RET8 restore, MSPEED current/base distinction, HALT occupied slot and corrupt-backup atomic rejection passed.");
    }

    private static void NativeTurnMoveCallbackOrderContract()
    {
        foreach (bool linear in new[] { false, true })
        {
            (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] actor = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] turn = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, linear ? 0 : 64), (Jsm.Opcode.PSHN_L, 15), (linear ? Jsm.Opcode.PLTURN : Jsm.Opcode.CTURNR, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] move = [(Jsm.Opcode.LBL, 20), (Jsm.Opcode.PSHN_L, 500), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            byte[] content = SyntheticObjects([0, (16 << 7) | 4, (32 << 7) | 1], [player, actor, turn, [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.RET, 8)], move, [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            EventEngine Create()
            {
                var session = new GameSession(new GameState());
                var engine = new EventEngine(session, session.State.Variables)
                {
                    AnimationFrameCount = (_, _) => 4,
                    CanUseNativeScriptMove = actor => actor.ObjectId == 1,
                    ResolveNativeActorFacing = _ => 64,
                    ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
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
            for (int frame = 0; frame < 6; frame++)
            {
                live.Step();
            }
            ActorState body = live.Actors[1];
            Require(body.RotationTarget is not null && body.Direction == 64 && body.NativeScriptMovement is { Active: true, FacingDirection: 64 }, "Post-VM native rotation did not precede mode1 movement's final577 write.");
            EventEngine restored = Create();
            restored.Restore(live.Capture());
            for (int frame = 0; frame < 5; frame++)
            {
                live.Step();
                restored.Step();
                Require(body.RotationTarget is not null && body.Direction == 64 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Latent native turn phase or final MOVE-facing producer diverged after restore.");
            }
        }
    }

    private static void NativeHaltedModuleRequestContract()
    {
        byte[] content = SyntheticObjects([(16 << 7) | 2, 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 18), (Jsm.Opcode.REQEW, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 55), (Jsm.Opcode.POPM_W, 5001), (Jsm.Opcode.RET, 8)]]);
        content[2] = 1;
        content[3] = 1;
        var session = new GameSession(new GameState());
        var engine = new EventEngine(session, session.State.Variables);
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        for (int tick = 0; tick < 6; tick++)
        {
            engine.Step();
        }
        Require(session.State.Variables.GetValueOrDefault(5000) == 77 && session.State.Variables.GetValueOrDefault(5001) == 55, "REQEW slot7 was confused with the background module's occupied default0/HALT.");
        JsonElement defaultFiber = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
        Require(!defaultFiber.GetProperty("Complete").GetBoolean() && defaultFiber.GetProperty("WaitReason").GetString() == "NativeHalt", "Native module default HALT lost its occupied original slot after RET8.");
        var copy = new EventEngine(new GameSession(new GameState()), new Dictionary<int, int>());
        copy.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        copy.Restore(engine.Capture());
        Require(JsonElement.DeepEquals(engine.Capture(), copy.Capture()), "Native non-actor pointer-table request slot did not restore independently.");
    }
}
