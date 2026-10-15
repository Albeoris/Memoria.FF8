using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
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

    private static void NativeMovementAnimationContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        GameSession NewSession()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(vitals.DeepCopy());
            session.InitializeNewGameEconomy(economy.DeepCopy());
            return session;
        }
        void Configure(EventEngine engine, MovementAnimationSample sample)
        {
            engine.ResolveNativeScriptDirection = query => new(sample == MovementAnimationSample.Stopped ? NativeScriptDirectionStatus.Stopped : NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, sample == MovementAnimationSample.Stopped ? query.MovementDirection : (byte)64, query.TurnAccumulator);
            engine.ResolveNativeScriptMovement = query =>
            {
                int x = query.FixedX + (sample == MovementAnimationSample.Moving ? 4096 : 0);
                NativeScriptGroundProbe[] probes = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                return new(sample == MovementAnimationSample.Blocked ? NativeScriptMovementStatus.Blocked : NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, probes)]);
            };
        }
        EventEngine Create(MovementAnimationSample sample, int speed = 2000, Jsm.Opcode opcode = Jsm.Opcode.RMOVE)
        {
            GameSession session = NewSession();
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 8,
                CanUseNativeScriptMove = _ => true
            };
            Configure(engine, sample);
            byte[] content = SyntheticObjects([1], [
                [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 4), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.RANIMELOOP, 3), (Jsm.Opcode.PSHN_L, speed), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 20), (opcode, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            while (engine.Actors[0].NativeScriptMovement is null && engine.Frame < 10)
            {
                engine.Step();
            }
            Require(engine.Actors[0].NativeScriptMovement is not null, "Native movement animation fixture did not issue.");
            return engine;
        }
        foreach (MovementAnimationSample sample in Enum.GetValues<MovementAnimationSample>())
        {
            EventEngine live = Create(sample);
            ActorState actor = live.Actors[0];
            Require(actor.AnimationId == 1 && actor.AnimationFrame == 0, "Native RMOVE524030 must select full walk on issue, including stopped or blocked bodies; script ISTOUCH owns the corridor's idle decision.");
            EventEngine restored = Create(sample);
            restored.Restore(live.Capture());
            for (int callback = 0; callback < 12; callback++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Native movement/animation body diverged after independent restore.");
                if (sample == MovementAnimationSample.Stopped)
                {
                    Require(actor.AnimationId == 4 && actor.AnimationFrame == callback % 8 && actor.NativeScriptMovement is { Active: false }, "Native async completion must select idle on the following VM turn.");
                }
                else
                {
                    Require(actor.AnimationId == 1 && actor.AnimationFrame == (callback + 1) % 8 && actor.NativeScriptMovement is { Active: true }, "Blocked or moving native RMOVE must retain its full progressing walk clip without an invented displacement policy.");
                }
            }
        }
        foreach ((int speed, int animation) in new[] { (2695, 1), (2696, 2) })
        {
            Require(Create(MovementAnimationSample.Moving, speed).Actors[0].AnimationId == animation, "Native walk/run issue threshold changed.");
        }
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.FMOVE, Jsm.Opcode.RFMOVE })
        {
            EventEngine forced = Create(MovementAnimationSample.Moving, opcode: opcode);
            Require(forced.Actors[0].AnimationId == 3 && forced.Actors[0].AnimationFrame == 1, "Native forced movement replaced the explicit script clip.");
            Configure(forced, MovementAnimationSample.Blocked);
            forced.Step();
            Require(forced.Actors[0].AnimationId == 3 && forced.Actors[0].AnimationFrame == 2, "Blocked native forced movement reset the script animation.");
        }
        foreach ((int x, int z, int expected) in new[] { (180, 0, 1), (196, 0, 0), (195, 0, 1), (180, -127, 0), (180, -126, 1), (180, 127, 1), (180, 128, 0) })
        {
            (Jsm.Opcode, int)[] Init(int label, int position, int height, int push, int talk) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.PSHN_L, position), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, height), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, push), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, talk), (Jsm.Opcode.TALKRADIUS, 0), (Jsm.Opcode.RET, 8)];
            GameSession session = NewSession();
            EventEngine touch = new(session) { AnimationFrameCount = (_, _) => 8 };
            int touchCommands = 0;
            touch.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.ISTOUCH))
                {
                    touchCommands++;
                    Require(command.Arguments.SequenceEqual([1]), "Native ISTOUCH must consume its original DWORD native-object operand once.");
                }
            };
            byte[] content = SyntheticObjects([1, 32 << 7, 16 << 7], [Init(0, 0, 0, 48, 148), [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHAC, 1), (Jsm.Opcode.ISTOUCH, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Init(32, 1000, 0, 500, 500), Init(16, x, z, 48, 128)]);
            touch.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            for (int callback = 0; callback < 6 && !session.State.Variables.ContainsKey(5000); callback++)
            {
                touch.Step();
            }
            Require(touchCommands == 1 && session.State.Variables.GetValueOrDefault(5000) == expected, $"Native ISTOUCH51ED80/47B460 lost native-object lookup, target push502 + caller talk504, strict XY radius or Z bounds at ({x},{z}).");
        }
        Console.WriteLine("Native movement animation: issue-time walk/run, blocked clip progression, next-VM stop idle, forced clips, independent restore; ISTOUCH asymmetric owner radii, native object mapping, strict XY and Z boundaries pass.");
    }
}

