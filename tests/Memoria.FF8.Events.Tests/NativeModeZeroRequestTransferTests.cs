using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeModeZeroRequestTransferContract()
    {
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        foreach (Jsm.Opcode request in new[] { Jsm.Opcode.REQ, Jsm.Opcode.REQSW, Jsm.Opcode.REQEW })
        {
            foreach (bool queueLower in new[] { false, true })
            {
                int upperSlot = queueLower ? 4 : 3;
                (Jsm.Opcode, int)[] empty = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
                (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
                (Jsm.Opcode, int)[] lower = [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.RET, 8)];
                (Jsm.Opcode, int)[] middle = [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.PSHN_L, 22), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.ANIME, 10), (Jsm.Opcode.PSHN_L, 44), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.RET, 8)];
                (Jsm.Opcode, int)[] upper = [(Jsm.Opcode.LBL, 20), (Jsm.Opcode.PSHN_L, 33), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 2000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)];
                (Jsm.Opcode, int)[] queued = [(Jsm.Opcode.LBL, 21), (Jsm.Opcode.PSHN_L, 55), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.RET, 8)];
                (Jsm.Opcode, int)[] controller = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 18), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 19), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, upperSlot), (Jsm.Opcode.PSHN_L, 20), (request, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
                (Jsm.Opcode, int)[] other = queueLower ? [(Jsm.Opcode.LBL, 49), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 21), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)] : [(Jsm.Opcode.LBL, 49), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
                byte[] content = SyntheticObjects([0, (16 << 7) | 5, (32 << 7) | 1, (48 << 7) | 1], [empty, init, [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], lower, middle, upper, queued, [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)], controller, [(Jsm.Opcode.LBL, 48), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)], other]);

                EventEngine Create()
                {
                    GameSession session = GameSession.CreateColdProcess();
                    session.StartNewGame();
                    session.SetCharacterVitals(assets.ReadNewGameCharacterVitals());
                    session.MarkReplayUnavailable("Declared native request component; ground/direction/animation providers are bounded test adapters.");
                    EventEngine engine = new(session, session.State.Variables)
                    {
                        AnimationFrameCount = (_, _) => 64,
                        CanUseNativeScriptMove = actor => actor.ObjectId == 1,
                        ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, query.TurnAccumulator),
                        ResolveNativeScriptMovement = query =>
                        {
                            int x = query.FixedX + 4096;
                            NativeScriptGroundProbe[] fan = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                            return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, fan)]);
                        }
                    };
                    engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                    return engine;
                }

                EventEngine live = Create();
                while (live.Actors[1].NativeMovementRequest?.RequestScriptId != 19 && live.Frame < 20)
                {
                    live.Step();
                }
                NativeMovementRequestState original = live.Actors[1].NativeMovementRequest ?? throw new InvalidOperationException("Middle animation request failed to suspend the actual lower MOVE.");
                Require(original.RequestScriptId == 19 && original.SavedMovement.ScriptId == 18 && live.Actors[1].NativeScriptMovement is { Active: true, Suspended: true }, "Native initial mode1 backup or mode0 suspension is wrong.");
                live.Step();
                JsonElement beforeHigher = live.Capture();
                Require(beforeHigher.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 19).GetProperty("WaitReason").GetString() == nameof(WaitReason.Animation), "The reached-family regression did not actually preempt an animation wait.");
                EventEngine restored = Create();
                restored.Restore(beforeHigher);
                bool middleReturnObserved = false;
                live.CommandExecuted += command =>
                {
                    if (command.Name == nameof(Jsm.Opcode.RET) && command.ObjectId == 1 && command.ScriptId == 19)
                    {
                        JsonElement middle = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 19);
                        Require(middle.GetProperty("Results").GetProperty("1").GetInt32() == 44 && live.Actors[1].NativeMovementRequest is null && live.Actors[1].NativeScriptMovement!.ScriptId == 18, "Middle RET resurrected a consumed backup or lost its separate result bank.");
                        middleReturnObserved = true;
                    }
                };
                bool transferred = false;
                bool queuedObserved = false;
                bool consumed = false;
                for (int callback = 0; callback < 24; callback++)
                {
                    live.Step();
                    restored.Step();
                    JsonElement current = live.Capture();
                    Require(JsonElement.DeepEquals(current, restored.Capture()), "Animation-held nested request diverged after independent restore.");
                    NativeMovementRequestState? retained = live.Actors[1].NativeMovementRequest;
                    if (retained?.RequestScriptId == 20)
                    {
                        transferred = true;
                        Require(retained.RequestSlot == upperSlot && retained.SavedMovement == original.SavedMovement && live.Actors[1].NativeScriptMovement is { Suspended: true }, "Mode0 request overwrote its sole physical backup or restarted movement.");
                        EventEngine copy = Create();
                        copy.Restore(current);
                        Require(JsonElement.DeepEquals(current, copy.Capture()), "Transferred single backup failed current checkpoint validation.");
                        if (queueLower && current.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 21 && !fiber.GetProperty("Complete").GetBoolean()))
                        {
                            queuedObserved = true;
                            Require(retained.RequestSlot == 4, "A lower queued slot stole the current slot4 physical backup.");
                        }
                    }
                    if (transferred && retained is null)
                    {
                        consumed = true;
                        Require(live.Actors[1].NativeScriptMovement is { Active: true, Suspended: false, ScriptId: 18, Speed: 1000 } && live.Actors[1].NativeMovementBaseSpeed == 2000, "Highest RET8 did not consume the original physical backup while retaining current base speed.");
                    }
                }
                Require(transferred && consumed && (!queueLower || queuedObserved), "Required higher transfer, lower queue or single RET consumption was not exercised.");
                JsonElement final = live.Capture();
                JsonElement lowerFiber = final.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 18);
                Require(lowerFiber.GetProperty("Results").GetProperty("1").GetInt32() == 11 && middleReturnObserved && live.Actors[1].NativeMovementRequest is null && live.Actors[1].NativeScriptMovement!.ScriptId == 18, "Middle RET resurrected a consumed backup or replaced separate operand/result contexts.");
            }
        }
        NativeCompletedAsyncRequestRefusal(assets);
        Console.WriteLine("Native REQ/REQSW/REQEW: MOVE1 -> held ANIME2 -> mode0 higher request, sole backup transfer/RET8 consumption, lower queued request, current/base speed, independent restore and completed-async-owner atomic refusal passed.");
    }

    private static void NativeCompletedAsyncRequestRefusal(GameAssets assets)
    {
        byte[] content = SyntheticObjects([1, (16 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.REQSW, 0), (Jsm.Opcode.RET, 8)]]);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.MarkReplayUnavailable("Declared completed asynchronous movement component with bounded test geometry.");
        EventEngine engine = new(session, session.State.Variables)
        {
            AnimationFrameCount = (_, _) => 4,
            CanUseNativeScriptMove = _ => true,
            ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, query.TurnAccumulator),
            ResolveNativeScriptMovement = query =>
            {
                int x = query.FixedX + 4096;
                NativeScriptGroundProbe[] fan = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, fan)]);
            }
        };
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        engine.Step();
        Require(engine.Actors[0].NativeScriptMovement is { Active: true, Asynchronous: true } && !engine.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1), "The physical-only boundary did not actually retain movement after its VM owner completed.");
        while (engine.LastFault is null && engine.Frame < 12)
        {
            NativeScriptMovementState before = engine.Actors[0].NativeScriptMovement!;
            try
            {
                engine.Step();
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == nameof(Jsm.Opcode.REQSW))
            {
                JsonElement caller = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
                Require(caller.GetProperty("Position").GetInt32() == engine.LastFault.Operation && caller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([0, 2]) && engine.Actors[0].NativeScriptMovement == before && engine.Actors[0].NativeMovementRequest is null && !engine.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && !fiber.GetProperty("Initialization").GetBoolean()), "Unproved physical-only request consumed operands, advanced PC, changed movement or invented a replacement VM fiber.");
            }
        }
        Require(engine.LastFault is not null, "A completed-VM asynchronous body silently skipped its native request backup.");
    }
}
