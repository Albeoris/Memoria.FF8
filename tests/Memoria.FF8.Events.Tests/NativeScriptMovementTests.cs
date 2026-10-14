using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeScriptMovementAndBoundary(string gameDirectory)
    {
        NativeMotionAuthorityContract();
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(165);
        EventEngine Create()
        {
            GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                CanUseNativeScriptMove = actor => actor.ObjectId == 20 && actor.ModelId == 19 && actor.MovementDirectionLimit == 4 && actor.MovementSpeed == 2500,
                ResolveNativeScriptDirection = query =>
                {
                    FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(new FieldNativeScriptDirectionRequest(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
                    return result.Status == FieldNativeScriptDirectionStatus.Unsupported ? null : new NativeScriptDirectionResult((NativeScriptDirectionStatus)result.Status, result.FixedX, result.FixedY, result.Direction, result.TurnAccumulator);
                }
            };
            engine.ResolveNativeScriptMovement = query =>
            {
                FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(field.Walkmesh, new FieldNativeMovementRequest(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, query.Direction, query.Speed, query.Radius, false, true, 0, true), engine.Scene.LockedTriangles);
                if (result.Status == FieldNativeMovementStatus.Unsupported || result.Proposals.Any(proposal => proposal.FanProbes.Any(probe => probe.NativeHeight is null)))
                {
                    return null;
                }
                return new NativeScriptMovementResult((NativeScriptMovementStatus)result.Status, result.FixedX, result.FixedY, result.FixedZ, result.Triangle, result.Direction, result.Proposals.Select(proposal => new NativeScriptMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction, proposal.FanProbes.Select(probe => new NativeScriptGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value)).ToArray())).ToArray());
            };
            engine.LoadField(165, field.Jsm, field.Messages, session.State.FieldArrival);
            engine.Restore(session.State.EventState!.Value);
            return engine;
        }
        EventEngine actual = Create();
        actual.Apply(new PlayerAction(PlayerActionKind.Wait));
        ActorState actor = actual.Actors[20];
        NativeScriptMovementState state = actor.NativeScriptMovement ?? throw new InvalidOperationException("Actual native MOVE did not preserve its raw state.");
        Require(actual.Frame == 6 && state.Operation == 1861 && state.ScriptId == 103 && state.FixedX == -2864 * 4096 && state.FixedY == -5474 * 4096 && state.FixedZ == 567 * 4096 && state.Triangle == 212 && actor.NativeMovementCompletion542 == 1 && state.MovementDirection == 4 && state.FacingDirection == 4 && state.TurnAccumulator == 4 && actor.AnimationId == actor.WalkAnimationId && actor.AnimationFrame == 0 && actor.MovementTarget is not null, $"Actual third-branch blocked MOVE changed native initial raw placement, limited turn, animation or completion: {state}, animation{actor.AnimationId}/{actor.AnimationFrame}.");
        JsonElement saved = actual.Capture();
        JsonElement owner = saved.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Sequence").GetInt64() == state.FiberSequence);
        Require(owner.GetProperty("Position").GetInt32() == 1861 && owner.GetProperty("Stack").GetArrayLength() == 0 && owner.GetProperty("WaitReason").GetString() == "NativeScriptMovement", "Native MOVE consumed its operands more than once or advanced its held original instruction.");
        EventEngine independent = Create();
        independent.Restore(saved);
        for (int tick = 0; tick < 12; tick++)
        {
            actual.Apply(new PlayerAction(PlayerActionKind.Wait));
            independent.Apply(new PlayerAction(PlayerActionKind.Wait));
            Require(JsonElement.DeepEquals(actual.Capture(), independent.Capture()), "Independent mid-MOVE restore changed raw turn, blocked/accepted geometry, animation or native PC wait.");
        }

        EventEngine missing = Create();
        missing.ResolveNativeScriptMovement = _ => null;
        try
        {
            missing.Apply(new PlayerAction(PlayerActionKind.Wait));
            throw new InvalidOperationException("Missing native MOVE geometry consumed the four original operands.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement faultFiber = missing.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 20 && fiber.GetProperty("ScriptId").GetInt32() == 103);
        Require(missing.LastFault is { Opcode: "MOVE", Operation: 1861 } && faultFiber.GetProperty("Position").GetInt32() == 1861 && faultFiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 0, 567, -9146, 413 }) && missing.Actors[20].NativeScriptMovement is null, "Unsupported native MOVE mutated its PC, operand stack or raw state.");

        foreach (int fanHeight in new[] { 567, 1000 })
        {
            EventEngine contact = Create();
            Func<NativeScriptMovementQuery, NativeScriptMovementResult?> original = contact.ResolveNativeScriptMovement!;
            contact.ResolveNativeScriptMovement = query =>
            {
                NativeScriptMovementResult result = original(query)!;
                NativeScriptMovementProposal first = result.Proposals[0];
                NativeScriptGroundProbe[] fans = first.FanProbes.ToArray();
                // Explicit synthetic contact query: hidden actor19 remains a
                // collision peer, and its test uses each fan's ground height.
                fans[0] = new NativeScriptGroundProbe((int)contact.Actors[19].X * 4096, (int)contact.Actors[19].Y * 4096, fanHeight);
                NativeScriptMovementProposal[] proposals = result.Proposals.ToArray();
                proposals[0] = first with { FanProbes = fans };
                return result with { Proposals = proposals };
            };
            try
            {
                contact.Apply(new PlayerAction(PlayerActionKind.Wait));
            }
            catch (EventScriptException) when (fanHeight == 567)
            {
            }
            Require(fanHeight == 567 ? contact.LastFault is { Opcode: "MOVE", Operation: 1861 } && contact.Actors[20].NativeScriptMovement is null : contact.LastFault is null && contact.Actors[20].NativeScriptMovement is not null, "Hidden peer contact ignored visibility or used actor current Z in place of native per-fan plane height.");
        }

        // Exercise completion semantics independently: geometry is never called
        // for a stop/snap, and native completion preserves Z, triangle and577.
        foreach (NativeScriptDirectionStatus status in new[] { NativeScriptDirectionStatus.Stopped, NativeScriptDirectionStatus.Snapped })
        {
            EventEngine completed = Create();
            completed.Restore(saved);
            NativeScriptMovementState initial = completed.Actors[20].NativeScriptMovement!;
            completed.ResolveNativeScriptDirection = query => new NativeScriptDirectionResult(status, status == NativeScriptDirectionStatus.Snapped ? query.TargetX : query.FixedX, status == NativeScriptDirectionStatus.Snapped ? query.TargetY : query.FixedY, query.MovementDirection, query.TurnAccumulator);
            completed.ResolveNativeScriptMovement = _ => throw new InvalidOperationException("Stopped/snap MOVE unnecessarily queried ground geometry.");
            completed.Apply(new PlayerAction(PlayerActionKind.Wait));
            NativeScriptMovementState finish = completed.Actors[20].NativeScriptMovement!;
            Require(completed.Actors[20].NativeMovementCompletion542 == 2 && finish is { Active: true } && completed.Actors[20].MovementTarget is null && finish.FixedZ == initial.FixedZ && finish.Triangle == initial.Triangle && finish.FacingDirection == initial.FacingDirection, "Native stop/snap changed height, triangle or body direction.");
            EventEngine restored = Create();
            restored.Restore(completed.Capture());
            restored.ResolveNativeScriptDirection = completed.ResolveNativeScriptDirection;
            restored.ResolveNativeScriptMovement = completed.ResolveNativeScriptMovement;
            bool resumedIdle = false;
            completed.CommandExecuted += command =>
            {
                if (command.ObjectId == 20 && command.Operation == initial.Operation)
                {
                    resumedIdle = completed.Actors[20].NativeMovementCompletion542 == 2 && completed.Actors[20].NativeScriptMovement is { Active: false } && completed.Actors[20].AnimationId == completed.Actors[20].BaseAnimationId && completed.Actors[20].AnimationFrame == 0;
                }
            };
            completed.Apply(new PlayerAction(PlayerActionKind.Wait));
            restored.Apply(new PlayerAction(PlayerActionKind.Wait));
            Require(JsonElement.DeepEquals(completed.Capture(), restored.Capture()) && resumedIdle, "Native MOVE completion did not reset full idle and resume its original PC on the next VM update.");
        }

        JsonElement valid = actual.Capture();
        foreach (string property in new[] { "FixedX", "Operation", "Version" })
        {
            JsonNode invalid = JsonNode.Parse(valid.GetRawText())!;
            JsonNode motion = invalid["Actors"]![20]!["NativeScriptMovement"]!;
            motion[property] = motion[property]!.GetValue<int>() + 1;
            try
            {
                actual.Restore(JsonSerializer.SerializeToElement(invalid));
                throw new InvalidOperationException($"Invalid native MOVE {property} passed checkpoint validation.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(valid, actual.Capture()), "Invalid native MOVE restore partially changed the VM.");
        }
        Console.WriteLine("Original165 native MOVE: third branch212 first step blocked, rawXYZ unchanged/575=577=4/474=4/completion1/PC1861 held; independent repeated blocked/accepted restore, stop/snap XY-only and next-update idle completion; unsupported first proposal preserves four operands.");
    }
}
