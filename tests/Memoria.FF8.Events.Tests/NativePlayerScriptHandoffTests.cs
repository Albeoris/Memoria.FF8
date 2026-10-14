using System.Reflection;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePlayerScriptHandoffContract()
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 212), (Jsm.Opcode.PSHN_L, 554), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 133), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 216), (Jsm.Opcode.PSHN_L, 553), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([1], [init, main]);
        EventEngine Create(bool moving = false)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolveWalkmesh = (x, y) => new(new(x, y, 200), 7),
                CanUseNativeScriptMove = actor => actor.NativeMotionAuthority is { Version: 1, FacingHold: 0 },
                ResolveNativeScriptDirection = query =>
                {
                    FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
                    NativeScriptDirectionStatus status = result.Status switch
                    {
                        FieldNativeScriptDirectionStatus.Stopped => NativeScriptDirectionStatus.Stopped,
                        FieldNativeScriptDirectionStatus.Snapped => NativeScriptDirectionStatus.Snapped,
                        FieldNativeScriptDirectionStatus.Moving when moving => NativeScriptDirectionStatus.Moving,
                        _ => throw new InvalidOperationException("Synthetic handoff must reach the original479970 near-target snap.")
                    };
                    return new NativeScriptDirectionResult(status, result.FixedX, result.FixedY, result.Direction, result.TurnAccumulator);
                },
                ResolveNativeScriptMovement = query =>
                {
                    if (!moving)
                    {
                        throw new InvalidOperationException("Native479970 snap must not query ground or fabricate a new triangle.");
                    }
                    int x = query.FixedX + 4096;
                    NativeScriptGroundProbe[] fans = [new(x, query.FixedY, 200), new(x, query.FixedY, 200), new(x, query.FixedY, 200)];
                    return new NativeScriptMovementResult(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(x, query.FixedY, query.Direction, fans)]);
                }
            };
            (Jsm.Opcode, int)[] movingMain = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 554), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RMOVE, 0), (Jsm.Opcode.RET, 0)];
            engine.LoadField(600, moving ? SyntheticObjects([1], [init, movingMain]) : content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        void Set(object target, string property, object value) => target.GetType().GetProperty(property)!.SetValue(target, value);
        void PrepareSyntheticTrace(EventEngine engine)
        {
            PartyTraceSample sample = new(212, 554, 200, 7, 133);
            Set(engine.Scene, nameof(SceneEventState.PartyTrace), new PartyTraceState(0, 0, 0, Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray(), Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray()));
            Set(engine.Scene, nameof(SceneEventState.NativePartyTraceVersion), 1);
            Set(engine.Scene, nameof(SceneEventState.NativePlayerMovementVersion), 1);
            Set(engine.Scene, nameof(SceneEventState.NativePlayerPosition), new FieldFixedPosition(212 << 12, 554 << 12, 200 << 12));
            Set(engine.Actors[0], nameof(ActorState.WalkmeshTriangle), 7);
        }
        void CommitSyntheticNativeProposal(EventEngine engine, FieldFixedPosition position, int direction)
        {
            // Component test only: inject an already certified native proposal at
            // the same commit boundary used by ordinary Move.
            // No research checkpoint or journal is fabricated by this fixture.
            MethodInfo commit = typeof(EventEngine).GetMethod("MovePlayerCore", BindingFlags.Instance | BindingFlags.NonPublic)!;
            commit.Invoke(engine, [position.X / 4096f, position.Y / 4096f, position.Z / 4096f, 7, position, direction, true, null]);
        }
        EventEngine live = Create();
        live.Step();
        live.Step();
        PrepareSyntheticTrace(live);
        FieldFixedPosition fractional = new(872254, 2270827, 819200);
        CommitSyntheticNativeProposal(live, fractional, 133);
        Require(live.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 133, 0) && live.Scene.NativePlayerPosition == fractional && live.Actors[0].X == fractional.X / 4096f, "Certified player commit discarded575/576 or rounded its fractional native origin.");
        JsonElement saved = live.Capture();
        EventEngine restored = Create();
        restored.Restore(saved);
        for (int callback = 0; callback < 3; callback++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Fractional player-to-script handoff diverged after independent restore.");
        }
        Require(live.Scene.FieldPartyNativeActorIds[0] == 255 && live.Scene.PartyBindingMemory is { First: 0 } && live.Actors[0].NativeScriptMovement is { Active: false, Completion: 2, FixedX: 884736, FixedY: 2265088, FixedZ: 819200, Triangle: 7 } && live.Scene.NativePlayerPosition == new FieldFixedPosition(884736, 2265088, 819200) && live.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 133, 0), "FOLLOWOFF/native MOVE fell back to float movement, lost saved leader binding or changed snap triangle authority.");
        EventEngine held = Create();
        held.Step();
        held.Step();
        PrepareSyntheticTrace(held);
        Set(held.Actors[0], nameof(ActorState.NativeMotionAuthority), new NativeMotionAuthority(1, 16, 1));
        CommitSyntheticNativeProposal(held, fractional, 64);
        Require(held.Actors[0].Direction == 133 && held.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 64, 1), "Native input commit failed to distinguish575 from held577 or changed576.");
        EventEngine moving = Create(true);
        moving.Step();
        moving.Step();
        PrepareSyntheticTrace(moving);
        CommitSyntheticNativeProposal(moving, fractional, 133);
        moving.Step();
        moving.Step();
        NativeScriptMovementState owner = moving.Actors[0].NativeScriptMovement!;
        Require(owner is { Active: true, Completion: 1 } && moving.IsUserControlEnabled, "Synthetic active script must retain global control to exercise native body572 input suppression.");
        EventEngine movingRestored = Create(true);
        movingRestored.Restore(moving.Capture());
        PlayerAction input = new(PlayerActionKind.Move, Horizontal: 1);
        Require(moving.Apply(input) is { Accepted: true, FramesAdvanced: 1 } && movingRestored.Apply(input) is { Accepted: true, FramesAdvanced: 1 }, "Active native movement input did not advance its ordinary callback.");
        Require(moving.Actors[0].NativeScriptMovement is { Active: true, Completion: 1 } next && next.FiberSequence == owner.FiberSequence && next.FixedX == owner.FixedX + 4096 && moving.Scene.NativePlayerPosition == new FieldFixedPosition(next.FixedX, next.FixedY, next.FixedZ) && JsonElement.DeepEquals(moving.Capture(), movingRestored.Capture()), "Ordinary player input replaced active native MOVE ownership, ran legacy locomotion or diverged after restore.");
        GameSession movingSession = (GameSession)typeof(EventEngine).GetField("services", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(moving)!;
        movingSession.SetSeeD(NativeSeeDState.CreateNewGame().WithSalaryEnabled(true));
        Set(moving.Scene, nameof(SceneEventState.NativeSalaryMovementUnits), (byte)5);
        moving.Apply(input);
        Require(movingSession.SeeD!.SalaryMovementCounter == 5 && moving.Scene.NativeSalaryMovementUnits == 5, "Native mode572=1 incorrectly reset the retained previous-run salary scalar.");
        moving.Apply(input);
        Require(movingSession.SeeD!.SalaryMovementCounter == 10 && moving.Scene.NativeSalaryMovementUnits == 5, "Repeated native scripted body callback lost the held previous salary scalar.");
        Console.WriteLine("Player/script handoff: certified575/576 retention, fractional saved-leader origin, FOLLOWOFF, original479970 snap/triangle retention, held577 and ordinary input suppression/continued native motion with independent restore passed.");
    }
}
