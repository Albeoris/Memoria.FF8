using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeJoinMoveTakeoverContract()
    {
        NativeJoinMoveTakeoverCase(false);
        NativeJoinMoveTakeoverCase(true);
    }

    private static void NativeJoinMoveTakeoverCase(bool asynchronous)
    {
        using GameAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        (Jsm.Opcode, int)[] Empty(int label) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] Body(int label, int character, int x) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, character), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> leader = Body(8, 0, 0).ToList();
        leader.InsertRange(2, [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SUBPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDPARTY, 0)]);
        byte[] content = SyntheticObjects([1, (8 << 7) | 4, (16 << 7) | 4, (24 << 7) | 1], [Empty(0), [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], leader.ToArray(), [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Empty(10), Empty(11), [(Jsm.Opcode.LBL, 12), (Jsm.Opcode.JOIN, 0), (Jsm.Opcode.RET, 8)], Body(16, 1, 300), [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (asynchronous ? Jsm.Opcode.RMOVE : Jsm.Opcode.MOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Empty(18), Empty(19), [(Jsm.Opcode.LBL, 20), (Jsm.Opcode.PSHN_L, 192), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.CTURNR, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.REQEW, 1), (Jsm.Opcode.FOLLOWON, 0), (Jsm.Opcode.RET, 8)], Empty(24), [(Jsm.Opcode.LBL, 25), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.REQ, 2), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(vitals.DeepCopy());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = actor => actor.ObjectId == 2,
                CanUseNativeJoinMovement = actor => actor.ObjectId == 2,
                ResolvePartyTraceTriangle = (_, _, _) => 0,
                ResolveNativeScriptDirection = query =>
                {
                    FieldNativeScriptDirectionResult native = FieldNativeScriptDirectionProbe.Resolve(new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit, query.HoldsFacing ? (byte)1 : (byte)0));
                    return native.Status == FieldNativeScriptDirectionStatus.Unsupported ? null : new((NativeScriptDirectionStatus)native.Status, native.FixedX, native.FixedY, native.Direction, native.TurnAccumulator);
                },
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX + 4096;
                    return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)])]);
                },
                ResolveNativeJoinMovement = query =>
                {
                    int x = query.FixedX - 20 * 4096;
                    return new(true, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction)], [[new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)]]);
                }
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        bool suspendedTurn = false;
        while (live.Actors[2].NativeScriptMovement is not { SupersededByPartyJoin: true } && live.Frame < 30)
        {
            live.Step();
            if (!suspendedTurn && live.Actors[2].NativeScriptMovement is { Suspended: true } movement && live.Actors[2].Direction != movement.FacingDirection)
            {
                suspendedTurn = true;
                EventEngine suspendedCopy = Create();
                suspendedCopy.Restore(live.Capture());
                Require(JsonElement.DeepEquals(live.Capture(), suspendedCopy.Capture()), "Suspended MOVE could not retain an upper request's independent facing change.");
            }
        }
        Require(suspendedTurn, "Fixture did not execute upper-facing change while lower MOVE was suspended.");
        ActorState companion = live.Actors[2];
        Require(companion.NativeScriptMovement is { Active: false, Cancelled: true, Suspended: false, SupersededByPartyJoin: true } && companion.NativeMovementRequest is { SavedMovement.TargetX: 4096000, SavedMovement.Active: true }, "JOIN failed to replace the physical target while retaining the independent issued MOVE and original backup.");
        NativeMovementRequestState backup = companion.NativeMovementRequest!;
        JsonElement pending = live.Capture();
        EventEngine copy = Create();
        copy.Restore(pending);
        JsonNode broken = JsonNode.Parse(pending.GetRawText())!;
        broken["PartyJoin"] = null;
        try
        {
            copy.Restore(JsonSerializer.SerializeToElement(broken));
            throw new InvalidOperationException("Unbound displaced MOVE marker passed restore.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(pending, copy.Capture()), "Invalid displaced MOVE restore mutated the active engine.");
        JsonNode invalidBackup = JsonNode.Parse(pending.GetRawText())!;
        invalidBackup["Actors"]![2]!["NativeMovementRequest"]!["SavedMovement"]!["SupersededByPartyJoin"] = true;
        try
        {
            copy.Restore(JsonSerializer.SerializeToElement(invalidBackup));
            throw new InvalidOperationException("JOIN marker was accepted in the independent original request backup.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(pending, copy.Capture()), "Invalid backup marker restore mutated the active engine.");
        bool resumed = false;
        bool completedJoin = false;
        live.CommandExecuted += command =>
        {
            if (command.Name == "JOIN" && live.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null)
            {
                completedJoin = true;
                EventEngine completedCopy = Create();
                completedCopy.Restore(live.Capture());
            }
        };
        for (int tick = 0; tick < 30; tick++)
        {
            live.Step();
            copy.Step();
            Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()), "JOIN takeover/RET8 continuation diverged after independent restore.");
            if (!resumed && companion.NativeMovementRequest is null && companion.NativeScriptMovement is { Active: true, SupersededByPartyJoin: false })
            {
                resumed = true;
                Require(companion.NativeScriptMovement.TargetX == backup.SavedMovement.TargetX && companion.NativeScriptMovement.Speed == backup.SavedMovement.Speed && companion.X < 150 && companion.NativeJoinedPosition is null, $"RET8 failed to resume the old target/speed from JOIN-final placement: X={companion.X}, move={companion.NativeScriptMovement}, backup={backup.SavedMovement}.");
                EventEngine afterReturn = Create();
                afterReturn.Restore(live.Capture());
                afterReturn.Step();
                live.Step();
                copy.Step();
                Require(JsonElement.DeepEquals(live.Capture(), afterReturn.Capture()) && JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Post-RET8 restored MOVE failed its next ordinary callback.");
            }
        }
        Require(resumed && completedJoin, "JOIN did not naturally complete and release the higher request's RET8.");
        Console.WriteLine("JOIN active MOVE takeover: physical target ownership, untouched request backup, marker binding, mid-JOIN restore and post-RET8 continuation passed.");
    }
}
