using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static (EventEngine Engine, List<NativePlayerMovementQuery> PlayerCalls, List<NativeJoinMovementQuery> JoinCalls, GameSession Session) CreateNativeFadeRegression(Jsm.Opcode release, bool join = false, bool blackAfterRelease = false)
    {
        (Jsm.Opcode, int)[] Initialize(int slot)
        {
            List<(Jsm.Opcode, int)> instructions = [(Jsm.Opcode.LBL, slot * 16)];
            if (slot == 0)
            {
                instructions.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.SETPARTY, 0)]);
            }
            instructions.AddRange([(Jsm.Opcode.SETMODEL, slot), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 0)]);
            return instructions.ToArray();
        }
        List<(Jsm.Opcode, int)> leader = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.INITTRACE, 0)];
        if (release == Jsm.Opcode.FADEIN)
        {
            leader.Add((Jsm.Opcode.FADEBLACK, 0));
        }
        leader.AddRange([(Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.POPM_B, 6000), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0)]);
        if (join)
        {
            leader.AddRange([(Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.JOIN, 0)]);
        }
        else
        {
            leader.Add((release, 0));
        }
        leader.AddRange([(Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 6003)]);
        if (blackAfterRelease)
        {
            leader.AddRange([(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.FADEBLACK, 0)]);
        }
        leader.AddRange([(Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
        List<(Jsm.Opcode, int)[]> scripts = [Initialize(0), leader.ToArray()];
        foreach (int slot in new[] { 1, 2 })
        {
            scripts.Add(Initialize(slot));
            scripts.Add([(Jsm.Opcode.LBL, slot * 16 + 1), (Jsm.Opcode.PSHN_L, slot * (join ? 120 : 12)), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 7), (Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.POPM_B, 6000 + slot), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
        }
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
        List<NativePlayerMovementQuery> playerCalls = [];
        List<NativeJoinMovementQuery> joinCalls = [];
        EventEngine engine = new(session)
        {
            AnimationFrameCount = (_, _) => 4,
            ResolveNativeTriangleHeight = (_, _, _) => 3,
            ResolvePartyTraceTriangle = (_, _, _) => 7,
            ResolveWalkmesh = (x, y) => new WalkmeshPosition(new Vector3(x, y, 3), 7),
            CanUseNativePlayerMovement = _ => true,
            CanUseNativeJoinMovement = _ => true,
            ResolveNativeScriptDirection = query =>
            {
                FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
                return result.Status == FieldNativeScriptDirectionStatus.Unsupported ? null : new NativeScriptDirectionResult((NativeScriptDirectionStatus)result.Status, result.FixedX, result.FixedY, result.Direction, result.TurnAccumulator);
            }
        };
        // Declared component ground certificates isolate callback admission from walkmesh geometry.
        engine.ResolveNativePlayerMovement = query =>
        {
            playerCalls.Add(query);
            int x = query.FixedX + 4096;
            NativePlayerGroundProbe probe = new(x, query.FixedY, query.FixedZ >> 12);
            return new NativePlayerMovementResult(x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeJoinMovementProposal(x, query.FixedY, query.Direction)], [[probe, probe, probe]]);
        };
        engine.ResolveNativeJoinMovement = query =>
        {
            joinCalls.Add(query);
            int x = query.FixedX - 100 * 4096;
            NativePlayerGroundProbe probe = new(x, query.FixedY, query.FixedZ >> 12);
            return new NativeJoinMovementResult(true, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeJoinMovementProposal(x, query.FixedY, query.Direction)], [[probe, probe, probe]]);
        };
        engine.LoadField(600, SyntheticObjects([1, (16 << 7) | 1, (32 << 7) | 1], scripts), [], new FieldTransition(600, 1, 2, 7, 0), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        for (int update = 0; update < 10 && !CapturedVariables(engine.Capture()).TryGetProperty("6002", out _); update++)
        {
            engine.Step();
        }
        Require(engine.FieldFade is { Physical.Mode: 4 } && engine.Actors[1].X == (join ? 120 : 12) && engine.Actors[2].X == (join ? 240 : 24) && engine.Scene.NativePartyTraceVersion == 1, "Black-mode fixture lost explicit default-script placement before deferred UCOFF or failed authentic field initialization.");
        return (engine, playerCalls, joinCalls, session);
    }

    private static JsonElement CaptureFadePartyPose(EventEngine engine)
    {
        return JsonSerializer.SerializeToElement(engine.Actors.Select(actor => new { actor.X, actor.Y, actor.Z, actor.WalkmeshTriangle, actor.Direction, actor.AnimationId, actor.AnimationFrame, actor.AnimationFirst, actor.AnimationLast, actor.AnimationPlaying, actor.AnimationLoop }).ToArray());
    }

    private static void NativeFadeBlackPlayerGateContract()
    {
        foreach (Jsm.Opcode release in new[] { Jsm.Opcode.FADENONE, Jsm.Opcode.FADEIN })
        {
            var live = CreateNativeFadeRegression(release);
            EventEngine engine = live.Engine;
            Require(engine.FieldFade!.Saved.Mode == (release == Jsm.Opcode.FADEIN ? 4 : 0), "Fade fixture confused loader physical mode4 with the independently saved fade bank.");
            JsonElement pose = CaptureFadePartyPose(engine);
            JsonElement trace = JsonSerializer.SerializeToElement(engine.Scene.PartyTrace);
            FieldFixedPosition position = engine.Scene.NativePlayerPosition!;
            var independent = CreateNativeFadeRegression(release);
            independent.Engine.Restore(engine.Capture());
            foreach (PlayerAction action in new[] { new PlayerAction(PlayerActionKind.Wait), new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 2), new PlayerAction(PlayerActionKind.Wait) })
            {
                long frame = engine.Frame;
                Require(engine.Apply(action).Accepted && independent.Engine.Apply(action).Accepted, "Physical mode4 refused a sampled Wait/Move callback instead of gating only its ordinary motor.");
                Require(engine.Frame == frame + action.Frames && engine.FieldFade is { Physical.Mode: 4 } && engine.Scene.NativePlayerPosition == position && engine.Scene.NativeSalaryMovementUnits == 0 && live.PlayerCalls.Count == 0, "Physical mode4 displaced the player, sampled movement salary units or executed its queued normal motor.");
                Require(JsonElement.DeepEquals(pose, CaptureFadePartyPose(engine)) && JsonElement.DeepEquals(trace, JsonSerializer.SerializeToElement(engine.Scene.PartyTrace)), "Black-mode Wait/Move replaced explicit party poses, idle/follower animations or dual trace state.");
                Require(JsonElement.DeepEquals(engine.Capture(), independent.Engine.Capture()), "Independent black-mode restore changed a retained pose, animation or callback admission.");
            }
            for (int update = 0; update < 10 && engine.FieldFade!.Physical.Mode == 4; update++)
            {
                engine.Step();
                independent.Engine.Step();
            }
            Require(engine.FieldFade!.Physical.Mode == (release == Jsm.Opcode.FADEIN ? 2 : 0) && CapturedVariables(engine.Capture()).GetProperty("6003").GetInt32() == 73, "Executed FADENONE/FADEIN failed to release physical mode4 while continuing the VM.");
            Require(engine.Actors[1].X == 1 && engine.Actors[2].X == 1, "Leaving black mode did not resume ordinary delayed follower placement.");
            Require(engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted && independent.Engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted, "Released field callback did not admit normal movement.");
            Require(live.PlayerCalls.Count == 1 && engine.Scene.NativePlayerPosition == position with { X = position.X + 4096 } && engine.Scene.NativeSalaryMovementUnits == 5 && JsonElement.DeepEquals(engine.Capture(), independent.Engine.Capture()), "Normal motor, movement salary scalar or independent continuation failed after the actual fade producer left mode4.");
        }
        var resampled = CreateNativeFadeRegression(Jsm.Opcode.FADENONE, blackAfterRelease: true);
        for (int update = 0; update < 10 && resampled.Engine.FieldFade!.Physical.Mode == 4; update++)
        {
            resampled.Engine.Step();
        }
        Require(resampled.Engine.FieldFade is { Physical.Mode: 0 } && resampled.Engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted && resampled.Engine.Scene.NativeSalaryMovementUnits == 5, "Fade salary-reset fixture did not produce its nonzero previous-body scalar through an actual admitted Move.");
        byte? unitsAtBlackIssue = null;
        resampled.Engine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.FADEBLACK))
            {
                unitsAtBlackIssue = resampled.Engine.Scene.NativeSalaryMovementUnits;
            }
        };
        JsonElement movingPose = CaptureFadePartyPose(resampled.Engine);
        JsonElement movingTrace = JsonSerializer.SerializeToElement(resampled.Engine.Scene.PartyTrace);
        Require(resampled.Engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted && unitsAtBlackIssue == 5 && resampled.Engine.FieldFade is { Physical.Mode: 4 } && resampled.Engine.Scene.NativeSalaryMovementUnits == 0 && resampled.PlayerCalls.Count == 1, "Physical mode4 did not clear the genuine previous movement scalar before suppressing the newly queued normal motor.");
        Require(JsonElement.DeepEquals(movingPose, CaptureFadePartyPose(resampled.Engine)) && JsonElement.DeepEquals(movingTrace, JsonSerializer.SerializeToElement(resampled.Engine.Scene.PartyTrace)), "Returning to black mode changed party placement, animation or trace while zeroing its previous salary scalar.");

        var missing = CreateNativeFadeRegression(Jsm.Opcode.FADENONE);
        for (int update = 0; update < 10 && missing.Engine.FieldFade!.Physical.Mode == 4; update++)
        {
            missing.Engine.Step();
        }
        Require(missing.Engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted && missing.Engine.Scene.NativeSalaryMovementUnits == 5, "Missing-fade refusal fixture did not execute the nonzero movement producer.");
        // Deliberate component fault injection removes only the required owner;
        // no missing mode, salary value or historical snapshot is synthesized.
        missing.Session.Restore(missing.Session.State with { FieldFade = null });
        JsonElement beforeMissingOwner = missing.Engine.Capture();
        int actionsBeforeMissingOwner = missing.Engine.ActionLog.Count;
        try
        {
            missing.Engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
            throw new InvalidOperationException("Move admitted an unknown physical fade owner.");
        }
        catch (NotSupportedException)
        {
        }
        Require(JsonElement.DeepEquals(beforeMissingOwner, missing.Engine.Capture()) && missing.Engine.Scene.NativeSalaryMovementUnits == 5 && missing.Engine.ActionLog.Count == actionsBeforeMissingOwner && missing.PlayerCalls.Count == 1, "Missing physical fade owner refusal advanced the VM/input, cleared the previous salary scalar or invoked the normal motor.");
        Console.WriteLine("Native physical fadeWORD4: loader/saved bank distinction, Wait/Move pose/animation/trace retention, genuine movement scalar5 reset to0, atomic missing-owner Move refusal, independent restore and FADENONE/FADEIN release passed.");
    }

    private static void NativeFadeBlackScriptJoinContract()
    {
        var live = CreateNativeFadeRegression(Jsm.Opcode.FADENONE, join: true);
        var independent = CreateNativeFadeRegression(Jsm.Opcode.FADENONE, join: true);
        independent.Engine.Restore(live.Engine.Capture());
        for (int update = 0; update < 24 && !CapturedVariables(live.Engine.Capture()).TryGetProperty("6003", out _); update++)
        {
            live.Engine.Step();
            independent.Engine.Step();
            Require(live.Engine.FieldFade is { Physical.Mode: 4 } && JsonElement.DeepEquals(live.Engine.Capture(), independent.Engine.Capture()), "Physical black mode changed the scripted UCOFF/JOIN callback or its independent restore.");
        }
        Require(!live.Engine.IsUserControlEnabled && live.JoinCalls.Count > 0 && live.PlayerCalls.Count == 0 && live.Engine.Actors[1].X < 120 && live.Engine.Actors[2].X < 240 && CapturedVariables(live.Engine.Capture()).GetProperty("6003").GetInt32() == 73 && live.Engine.FieldFade is { Physical.Mode: 4 }, "Black-mode normal-motor gate froze UCOFF/JOIN, blocked explicit companion mode1 movement or prevented its successor.");
        Console.WriteLine("Native black mode: VM UCOFF, explicit JOIN companion movement/completion and same-black successor execute with independent restoration; ordinary player motor remains unused.");
    }
}
