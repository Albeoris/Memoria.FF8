using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private enum MovementAnimationSample
    {
        Stopped,
        Blocked,
        NoDisplacement,
        Moving
    }

    private static void NativeMovementAnimationDisplacementContract()
    {
        byte[] Content(Jsm.Opcode opcode, int speed, bool repeated, bool forced)
        {
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 4), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 1), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)];
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.RANIMELOOP, forced ? 3 : 4), (Jsm.Opcode.PSHN_L, speed), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 20), (opcode, 0)];
            if (repeated)
            {
                main.Add((Jsm.Opcode.JMP, -5));
            }
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            return SyntheticObjects([1], [init, main.ToArray()]);
        }
        void Configure(EventEngine engine, MovementAnimationSample sample)
        {
            engine.ResolveNativeScriptDirection = query => sample == MovementAnimationSample.Stopped ? new NativeScriptDirectionResult(NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator) : new NativeScriptDirectionResult(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0);
            engine.ResolveNativeScriptMovement = query =>
            {
                if (sample == MovementAnimationSample.Stopped)
                {
                    throw new InvalidOperationException("Immediate stop unnecessarily queried physical ground displacement.");
                }
                int x = query.FixedX + (sample == MovementAnimationSample.Moving ? 4096 : 0);
                NativeScriptGroundProbe[] probes = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                return new NativeScriptMovementResult(sample == MovementAnimationSample.Blocked ? NativeScriptMovementStatus.Blocked : NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(x, query.FixedY, query.Direction, probes)]);
            };
        }
        EventEngine Create(MovementAnimationSample sample, Jsm.Opcode opcode = Jsm.Opcode.RMOVE, int speed = 2000, bool repeated = false, bool forced = false)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.SetWorldControls(NativeWorldControlState.CreateNewGame());
            session.SetEventRandom(session.EventRandom!.ClearSavedStateForNewGame());
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (model, animation) => model == 2 && animation is >= 1 and <= 4 ? 8 : throw new InvalidOperationException("Movement animation selected an unknown synthetic model clip."),
                CanUseNativeScriptMove = actor => actor.ObjectId == 0
            };
            Configure(engine, sample);
            engine.LoadField(74, Content(opcode, speed, repeated, forced), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        void ReachMovement(EventEngine engine)
        {
            while (engine.Actors[0].NativeScriptMovement is null && engine.Frame < 10)
            {
                engine.Step();
            }
            Require(engine.Actors[0].NativeScriptMovement is not null, "Movement-animation fixture did not issue its movement request.");
        }
        void StepPair(EventEngine live, EventEngine restored)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Movement animation, raw body or scheduler diverged after independent pending restore.");
        }

        foreach (MovementAnimationSample sample in new[] { MovementAnimationSample.Stopped, MovementAnimationSample.Blocked, MovementAnimationSample.NoDisplacement })
        {
            EventEngine stationary = Create(sample, repeated: true);
            int issued = 0;
            stationary.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.RMOVE))
                {
                    issued++;
                    Require(stationary.Actors[0].AnimationId == 4, "Repeated stationary RMOVE flashed a locomotion clip while issuing an intention before physical displacement.");
                }
            };
            ReachMovement(stationary);
            ActorState idle = stationary.Actors[0];
            Require(idle.AnimationId == 4 && idle.AnimationFrame == 1 && idle.X == 0 && idle.Y == 200 && idle.Z == 0 && idle.NativeMovementCompletion542 == (sample == MovementAnimationSample.Stopped ? 2 : 1), "Stationary RMOVE reset the already advancing idle clip or changed stop/body completion semantics.");
            EventEngine copy = Create(sample, repeated: true);
            copy.Restore(stationary.Capture());
            for (int tick = 0; tick < 12; tick++)
            {
                float expectedFrame = (idle.AnimationFrame + 1) % 8;
                StepPair(stationary, copy);
                Require(idle.AnimationId == 4 && idle.AnimationFrame == expectedFrame && idle.AnimationPlaying && idle.AnimationLoop && idle.X == 0 && idle.Y == 200 && idle.Z == 0, "Blocked, accepted-zero-delta or immediately stopped RMOVE restarted idle instead of advancing its complete clip.");
            }
            Require(issued >= 10, "Stationary animation regression did not exercise repeated asynchronous movement issues.");
        }

        EventEngine live = Create(MovementAnimationSample.Blocked);
        ReachMovement(live);
        EventEngine restored = Create(MovementAnimationSample.Blocked);
        restored.Restore(live.Capture());
        ActorState actor = live.Actors[0];
        StepPair(live, restored);
        Require(actor.AnimationId == 4 && actor.AnimationFrame == 2 && actor.NativeScriptMovement is { Active: true, Asynchronous: true } && actor.NativeMovementCompletion542 == 1, "Blocked pending movement did not retain an advancing idle clip and its original asynchronous body owner.");
        Configure(live, MovementAnimationSample.Moving);
        Configure(restored, MovementAnimationSample.Moving);
        StepPair(live, restored);
        Require(actor.X == 1 && actor.AnimationId == 1 && actor.AnimationFrame == 0 && actor.AnimationFirst == 0 && actor.AnimationLast == 7 && actor.AnimationPlaying && actor.AnimationLoop, "First committed displacement did not begin the complete walk clip at frame0.");
        for (int tick = 1; tick <= 8; tick++)
        {
            StepPair(live, restored);
            Require(actor.X == tick + 1 && actor.AnimationId == 1 && actor.AnimationFrame == tick % 8, "Continued physical displacement restarted the locomotion clip each callback or failed to play its complete loop.");
        }
        Configure(live, MovementAnimationSample.Blocked);
        Configure(restored, MovementAnimationSample.Blocked);
        float stoppedX = actor.X;
        StepPair(live, restored);
        Require(actor.X == stoppedX && actor.AnimationId == 4 && actor.AnimationFrame == 0 && actor.NativeMovementCompletion542 == 1, "A blocked physical step retained walking or changed native pending completion.");
        StepPair(live, restored);
        Require(actor.AnimationId == 4 && actor.AnimationFrame == 1, "Repeated blocked movement reset idle after the block transition.");
        Configure(live, MovementAnimationSample.NoDisplacement);
        Configure(restored, MovementAnimationSample.NoDisplacement);
        StepPair(live, restored);
        Require(actor.X == stoppedX && actor.AnimationId == 4 && actor.AnimationFrame == 2, "Accepted geometry without a coordinate delta was treated as physical movement.");
        Configure(live, MovementAnimationSample.Moving);
        Configure(restored, MovementAnimationSample.Moving);
        StepPair(live, restored);
        Require(actor.X == stoppedX + 1 && actor.AnimationId == 1 && actor.AnimationFrame == 0, "Resuming physical movement after a block failed to restart locomotion once.");

        foreach ((int speed, int expectedAnimation) in new[] { (2695, 1), (2696, 2) })
        {
            EventEngine threshold = Create(MovementAnimationSample.Moving, speed: speed);
            ReachMovement(threshold);
            Require(threshold.Actors[0].X == 1 && threshold.Actors[0].AnimationId == expectedAnimation && threshold.Actors[0].AnimationFrame == 0 && threshold.Actors[0].MovementSpeed == speed, "Committed locomotion lost the walk/run speed boundary or changed physical movement speed.");
        }

        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.FMOVE, Jsm.Opcode.RFMOVE })
        {
            EventEngine forced = Create(MovementAnimationSample.Moving, opcode, forced: true);
            ReachMovement(forced);
            EventEngine copy = Create(MovementAnimationSample.Moving, opcode, forced: true);
            copy.Restore(forced.Capture());
            ActorState explicitAnimation = forced.Actors[0];
            Require(explicitAnimation.AnimationId == 3 && explicitAnimation.AnimationFrame == 1 && explicitAnimation.X == 1 && explicitAnimation.NativeScriptMovement is { AutomaticAnimation: false }, "Forced movement replaced the script's explicit animation on physical displacement.");
            Configure(forced, MovementAnimationSample.Blocked);
            Configure(copy, MovementAnimationSample.Blocked);
            StepPair(forced, copy);
            Require(explicitAnimation.AnimationId == 3 && explicitAnimation.AnimationFrame == 2 && explicitAnimation.X == 1, "Blocked forced movement replaced or restarted the script's explicit animation.");
            Configure(forced, MovementAnimationSample.Stopped);
            Configure(copy, MovementAnimationSample.Stopped);
            StepPair(forced, copy);
            StepPair(forced, copy);
            Require(explicitAnimation.AnimationId == 3 && explicitAnimation.AnimationFrame == 4 && explicitAnimation.NativeMovementCompletion542 == 2 && explicitAnimation.NativeScriptMovement is { Active: false }, "Forced movement completion changed the script clip or its original completion/next-callback ownership.");
        }
        Console.WriteLine("Movement animation policy: repeated stop/blocked/accepted-zero-delta requests preserve advancing idle; committed displacement selects full walk/run clips once, block/resume transitions and speed boundary pass; forced FMOVE/RFMOVE clips and independent pending restores remain intact.");
    }
}
