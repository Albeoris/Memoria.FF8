using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeAsyncRfmMoveContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(139);
        EventEngine Create()
        {
            GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
            EventEngine engine = new(session)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                CanUseNativeScriptMove = actor => actor.NativeMotionAuthority is { Version: 1, FacingHold: 0 } && actor.CharacterId == -2 && actor.ModelId >= 0 && actor.PushRadius == 48,
                ResolveNativeScriptDirection = query =>
                {
                    FieldNativeScriptDirectionResult native = FieldNativeScriptDirectionProbe.Resolve(new FieldNativeScriptDirectionRequest(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
                    return native.Status == FieldNativeScriptDirectionStatus.Unsupported ? null : new NativeScriptDirectionResult((NativeScriptDirectionStatus)native.Status, native.FixedX, native.FixedY, native.Direction, native.TurnAccumulator);
                }
            };
            engine.ResolveNativeScriptMovement = query =>
            {
                FieldNativeMovementRequest request = new(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, query.Direction, query.Speed, query.Radius, false, true, 0, true);
                FieldNativeMovementResult native = FieldNativeMovementProbe.Resolve(field.Walkmesh, request, engine.Scene.LockedTriangles);
                if (native.Status == FieldNativeMovementStatus.Unsupported || native.Proposals.Any(proposal => proposal.FanProbes.Any(probe => !probe.Supported || probe.NativeHeight is null)))
                {
                    return null;
                }
                return new NativeScriptMovementResult((NativeScriptMovementStatus)native.Status, native.FixedX, native.FixedY, native.FixedZ, native.Triangle, native.Direction, native.Proposals.Select(proposal => new NativeScriptMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction, proposal.FanProbes.Select(probe => new NativeScriptGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value)).ToArray())).ToArray());
            };
            engine.LoadField(139, field.Jsm, field.Messages, session.State.FieldArrival);
            engine.Restore(session.State.EventState!.Value);
            return engine;
        }
        EventEngine first = Create();
        first.Step();
        ActorState actor = first.Actors[11];
        NativeScriptMovementState entered = actor.NativeScriptMovement ?? throw new InvalidOperationException("Original RFMOVE did not create native movement state.");
        JsonElement snapshot = first.Capture();
        JsonElement issuer = snapshot.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 61);
        Require(first.Frame == 8 && entered.Asynchronous && entered.Active && entered.Operation == 1576 && entered.TargetX == -4083 * 4096 && entered.TargetY == -5854 * 4096 && entered.TargetZ == 8341 * 4096 && actor.MovementDirectionLimit == 2 && issuer.GetProperty("Position").GetInt32() == 1577 && issuer.GetProperty("Stack").GetArrayLength() == 0 && issuer.GetProperty("WaitReason").GetString() != "NativeScriptMovement", "RFMOVE lost its original operands, limited direction or advance-and-yield contract.");
        Require(actor.AnimationId == 0 && actor.AnimationFrame == 0 && !actor.AnimationPlaying && !actor.MovementIgnoresWalkmesh, "RFMOVE changed native animation or incorrectly bypassed ground geometry.");
        Require(first.Actors[12].NativeScriptMovement is { Asynchronous: true, AutomaticAnimation: true, Operation: 2484 } && first.Actors[12].AnimationId == 1 && first.Actors[12].AnimationPlaying, "Parallel original RMOVE did not retain its distinct automatic walk/idle behavior.");
        EventEngine resumed = Create();
        resumed.Restore(snapshot);
        for (int tick = 0; tick < 4; tick++)
        {
            first.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Independent asynchronous RFMOVE restore changed raw direction, geometry, results or scheduler progress.");
        }

        EventEngine complete = Create();
        complete.Restore(snapshot);
        complete.ResolveNativeScriptDirection = query => new NativeScriptDirectionResult(NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator);
        complete.Step();
        Require(complete.Actors[11].NativeMovementCompletion542 == 2 && complete.Actors[11].NativeScriptMovement is { Active: true, Asynchronous: true } && complete.Actors[11].MovementTarget is null, "Async stop did not preserve completion until the following actor scheduler pass.");
        int preservedAnimation = complete.Actors[11].AnimationId;
        bool preservedPlaying = complete.Actors[11].AnimationPlaying;
        EventEngine completeRestore = Create();
        completeRestore.Restore(complete.Capture());
        completeRestore.ResolveNativeScriptDirection = complete.ResolveNativeScriptDirection;
        complete.Step();
        completeRestore.Step();
        Require(complete.Actors[11].NativeMovementCompletion542 == 2 && complete.Actors[11].NativeScriptMovement is { Active: false, Asynchronous: true } && complete.Actors[11].AnimationPlaying == preservedPlaying && complete.Actors[11].AnimationId == preservedAnimation && complete.Actors[12].NativeMovementCompletion542 == 2 && complete.Actors[12].NativeScriptMovement is { Active: false, AutomaticAnimation: true } && complete.Actors[12].AnimationId == 0 && complete.Actors[12].AnimationPlaying && JsonElement.DeepEquals(complete.Capture(), completeRestore.Capture()), "Async completion lost original RFMOVE script animation/RMOVE idle reset or diverged after restore.");

        EventEngine refused = Create();
        refused.ResolveNativeScriptMovement = _ => null;
        try
        {
            refused.Step();
            throw new InvalidOperationException("Uncertified RFMOVE consumed its four original operands.");
        }
        catch (EventScriptException) when (refused.LastFault is { Opcode: "RFMOVE", Operation: 1576 })
        {
        }
        JsonElement held = refused.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 61);
        Require(held.GetProperty("Position").GetInt32() == 1576 && held.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([0, 8341, -5854, -4083]) && refused.Actors[11].NativeScriptMovement is null && refused.Actors[11].X == 10 && refused.Actors[11].Y == -6770, "RFMOVE refusal changed original PC, operands or actor placement.");
        JsonNode malformed = JsonNode.Parse(snapshot.GetRawText())!;
        malformed["Actors"]![11]!["NativeScriptMovement"]!["Asynchronous"] = false;
        try
        {
            Create().Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("RFMOVE checkpoint accepted a synchronous held-PC variant.");
        }
        catch (InvalidDataException)
        {
        }
        Console.WriteLine($"Original139 RFMOVE: frame7ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢8 consumes4/PC1577, animation retained, first raw({entered.FixedX},{entered.FixedY},{entered.FixedZ})/dir{entered.MovementDirection}/turn{first.Actors[11].NativeTurnAccumulator474}; four native updates restore exactly, next-scheduler completion and preconsume refusal verified.");
    }
}
