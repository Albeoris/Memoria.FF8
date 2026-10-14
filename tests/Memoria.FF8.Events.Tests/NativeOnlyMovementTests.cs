using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeOnlyMovementContract()
    {
        byte[] unsupportedProgram = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 30), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create(byte[] content)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine refused = Create(unsupportedProgram);
        refused.Step();
        try
        {
            refused.Step();
            throw new InvalidOperationException("MOVE without a native adapter used obsolete managed interpolation.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "MOVE")
        {
        }
        JsonElement stopped = refused.Capture();
        JsonElement caller = stopped.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(caller.GetProperty("Stack").GetArrayLength() == 4 && refused.Actors[0].MovementTarget is null && refused.Actors[0].X == 0 && refused.Actors[0].Y == 0, "Native MOVE refusal consumed operands or installed a managed movement target.");
        EventEngine independent = Create(unsupportedProgram);
        independent.Restore(stopped);
        Require(JsonElement.DeepEquals(stopped, independent.Capture()), "Native MOVE refusal lost the preserved original operation and operands after restore.");
        JsonNode legacy = JsonNode.Parse(stopped.GetRawText())!;
        legacy["Actors"]![0]!["MovementTarget"] = JsonSerializer.SerializeToNode(new System.Numerics.Vector3(10, 20, 30), new JsonSerializerOptions { IncludeFields = true });
        try
        {
            independent.Restore(JsonSerializer.SerializeToElement(legacy));
            throw new InvalidOperationException("Obsolete managed movement snapshot was accepted.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(stopped, independent.Capture()), "Obsolete managed movement restore mutated the current native VM.");

        foreach (string property in new[] { "FaceStateVersion", "NativeBaseAnimationVersion" })
        {
            JsonNode missing = JsonNode.Parse(stopped.GetRawText())!;
            missing["Actors"]![0]!.AsObject().Remove(property);
            try
            {
                independent.Restore(JsonSerializer.SerializeToElement(missing));
                throw new InvalidOperationException("Missing current native actor provenance was inferred during restore.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(stopped, independent.Capture()), "Missing actor provenance restore changed the current native VM or services.");
        }

        foreach (string property in new[] { "FaceStateVersion", "NativeBaseAnimationVersion" })
        {
            JsonNode oldVersion = JsonNode.Parse(stopped.GetRawText())!;
            oldVersion["Actors"]![0]![property] = 0;
            try
            {
                independent.Restore(JsonSerializer.SerializeToElement(oldVersion));
                throw new InvalidOperationException("Obsolete actor version0 was admitted by current restore.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(stopped, independent.Capture()), "Obsolete actor version restore changed current VM or services.");
        }
        JsonNode missingControls = JsonNode.Parse(stopped.GetRawText())!;
        missingControls.AsObject().Remove("WorldControls");
        try
        {
            independent.Restore(JsonSerializer.SerializeToElement(missingControls));
            throw new InvalidOperationException("Missing canonical world-control field was accepted.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(stopped, independent.Capture()), "Missing world-control field restore mutated the current unknown-state VM.");
        GameSession knownControlsSession = new(new GameState { WorldControls = NativeWorldControlState.CreateNewGame() });
        EventEngine knownControls = new(knownControlsSession, knownControlsSession.State.Variables) { AnimationFrameCount = (_, _) => 4 };
        knownControls.LoadField(600, unsupportedProgram, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        JsonElement knownBefore = knownControls.Capture();
        try
        {
            knownControls.Restore(stopped);
            throw new InvalidOperationException("Unknown saved controls silently retained a different known service state.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(knownBefore, knownControls.Capture()) && knownControlsSession.State.WorldControls == NativeWorldControlState.CreateNewGame(), "World-control mismatch refusal mutated VM or canonical service state.");

        byte[] splitProgram = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SPLIT, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine split = Create(splitProgram);
        split.CanUseNativeJoinMovement = _ => true;
        split.ResolveNativeScriptDirection = query =>
        {
            FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
            return result.Status == FieldNativeScriptDirectionStatus.Unsupported ? null : new NativeScriptDirectionResult((NativeScriptDirectionStatus)result.Status, result.FixedX, result.FixedY, result.Direction, result.TurnAccumulator);
        };
        split.ResolveNativeJoinMovement = query =>
        {
            int y = query.FixedY + 20 * 4096 + 123;
            NativePlayerGroundProbe probe = new(query.FixedX, y, query.FixedZ >> 12);
            return new NativeJoinMovementResult(true, query.FixedX, y, query.FixedZ, query.Triangle, query.Direction, [new(query.FixedX, y, query.Direction)], [[probe, probe, probe]]);
        };
        for (int frame = 0; frame < 30 && split.Actors[0].NativeSplitPosition is null; frame++)
        {
            split.Step();
        }
        NativeJoinedPosition raw = split.Actors[0].NativeSplitPosition ?? throw new InvalidOperationException("Completed SPLIT did not retain its executed native raw coordinates.");
        Require(raw.FixedY % 4096 != 0 && split.Actors[0].Y == raw.FixedY / 4096f && split.Actors[0].NativeMotionAuthority == raw.Authority, "Completed SPLIT rounded fractional raw position or discarded physical575/576.");
        JsonElement completed = split.Capture();
        EventEngine restoredSplit = Create(splitProgram);
        restoredSplit.Restore(completed);
        Require(JsonElement.DeepEquals(completed, restoredSplit.Capture()), "Completed SPLIT raw physical body failed independent native restore.");
        byte[] dialogueProgram = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.RAMESW, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        GameSession dialogueSession = new(new GameState());
        EventEngine dialogueEngine = new(dialogueSession, dialogueSession.State.Variables)
        {
            AnimationFrameCount = (_, _) => 4,
            ResolveNativeTriangleHeight = (_, _, _) => 0,
            ResolvePartyTraceTriangle = (_, _, _) => 0,
            ResolveWalkmesh = (x, y) => new WalkmeshPosition(new System.Numerics.Vector3(x, y, 0), 0),
            CanUseNativePlayerMovement = _ => true,
            ResolveNativePlayerMovement = query =>
            {
                int y = query.FixedY + 4096;
                NativePlayerGroundProbe probe = new(query.FixedX, y, 0);
                return new NativePlayerMovementResult(query.FixedX, y, query.FixedZ, query.Triangle, query.Direction, [new(query.FixedX, y, query.Direction)], [[probe, probe, probe]]);
            }
        };
        dialogueEngine.LoadField(600, dialogueProgram, ["Ambient native text"], new FieldTransition(600, 0, 0, 0, 0), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        dialogueEngine.Step();
        dialogueEngine.Step();
        Require(dialogueEngine.CurrentDialogue is { CanConfirm: true } && !dialogueEngine.IsMovementBlockedByDialogue, "Plain automatic text incorrectly claimed physical movement ownership.");
        PlayerActionResult moved = dialogueEngine.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: 1));
        Require(moved.Accepted && moved.FramesAdvanced == 1 && dialogueEngine.Actors[0].Y == 1 && dialogueEngine.CurrentDialogue is { CanConfirm: true }, "Native held directions stopped merely because confirmable plain text was open.");
        Console.WriteLine("Native-only movement: missing adapter freezes MOVE operands/PC, obsolete managed target restore refuses atomically, completed SPLIT retains fractional raw placement and physical575/576 across restore.");
    }
}
