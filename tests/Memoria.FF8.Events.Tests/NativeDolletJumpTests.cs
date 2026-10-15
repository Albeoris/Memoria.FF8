using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDolletJumpContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(312);
        JsmProgram original = JsmProgram.Read(field.Jsm);
        EventScript script = original.Objects[1].Scripts.Single(candidate => candidate.Id == 15);
        Require(script.Start == 261 && script.End == 365 && original.Operations[279].Opcode == Jsm.Opcode.RMOVE && original.Operations[348].Opcode == Jsm.Opcode.JUMP3 && original.Operations[348].Parameter == 98 && original.Operations[359].Opcode == Jsm.Opcode.CTURNL, "Dollet regression no longer selects the original312/object1/script15 RMOVE/stall/JUMP3 body.");
        (Jsm.Opcode Opcode, int Parameter)[] main = Enumerable.Repeat((Jsm.Opcode.RET, 0), script.End - script.Start).ToArray();
        main[0] = (Jsm.Opcode.LBL, 1);
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 650), (Jsm.Opcode.PSHN_L, -1800), (Jsm.Opcode.PSHN_L, 11038), (Jsm.Opcode.SET3, 98), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.DIR, 82), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([1], [init, main]);
        // Copy original instruction bytes, including standalone opcodes; only relocate the script label.
        int originalOperationsOffset = BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(6));
        int operationsOffset = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6));
        field.Jsm.AsSpan(originalOperationsOffset + (script.Start + 1) * 4, (main.Length - 1) * 4).CopyTo(content.AsSpan(operationsOffset + (init.Length + 1) * 4));
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        int landingHeight = FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, 98, 705, -1953) ?? throw new InvalidOperationException("Original Dollet triangle98 has no landing plane.");
        EventEngine Create()
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                CanUseNativeScriptMove = actor => actor.ObjectId == 0,
                ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 8, query.TurnAccumulator),
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y),
                // Declared blocked geometry isolates the original script's unchanged-position fallback.
                ResolveNativeScriptMovement = query => new(NativeScriptMovementStatus.Blocked, query.FixedX, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(query.FixedX, query.FixedY, query.Direction, [new NativeScriptGroundProbe(query.FixedX, query.FixedY, query.FixedZ / 4096), new NativeScriptGroundProbe(query.FixedX, query.FixedY, query.FixedZ / 4096), new NativeScriptGroundProbe(query.FixedX, query.FixedY, query.FixedZ / 4096)])])
            };
            engine.LoadField(312, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        ActorState actor = live.Actors[0];
        NativeScriptMovementState? replaced = null;
        ushort? replacedCompletion = null;
        List<int[]> jumps = [];
        bool resumedOriginalTurn = false;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.RANIMELOOP))
            {
                replaced = actor.NativeScriptMovement;
                replacedCompletion = actor.NativeMovementCompletion542;
            }
            if (command.Name == nameof(Jsm.Opcode.JUMP3) && command.Arguments.Count != 0)
            {
                jumps.Add(command.Arguments.ToArray());
                Require(command.Operation - init.Length + script.Start == 348, "Blocked Dollet script reached the timeout jump instead of its actual unchanged-position duration10 fallback.");
            }
            if (command.Name == nameof(Jsm.Opcode.CTURNL))
            {
                resumedOriginalTurn = command.Arguments.SequenceEqual(new[] { 230, 15 });
            }
        };
        for (int callback = 0; callback < 160 && actor.NativeJump is null; callback++)
        {
            live.Step();
        }
        NativeJumpState jump = actor.NativeJump ?? throw new InvalidOperationException("Original Dollet stalled RMOVE did not reach its JUMP3 fallback.");
        Require(replaced is { Active: true, Asynchronous: true, Suspended: false, AutomaticAnimation: true } && replacedCompletion == 1 && replaced.FiberSequence == jump.FiberSequence && replaced.ScriptId == jump.ScriptId && replaced.Operation - init.Length + script.Start == 279 && replaced.FixedX == 650 * 4096 && replaced.FixedY == -1800 * 4096, "Dollet JUMP3 was not issued above its own still-running blocked asynchronous RMOVE body.");
        Require(jumps.Count == 1 && jumps[0].SequenceEqual(new[] { 705, -1953, 11038, 10 }) && jump is { Active: true, Duration: 10, AsyncFlag20000: true, AnimationFlag40000: true } && jump.TargetZ == unchecked(landingHeight << 12) && actor.NativeMovementCompletion542 == 1 && actor.NativeTurnAccumulator474 == 0 && actor.NativeScriptMovement is null && actor.MovementTarget is null && actor.NativeMovementRequest is null && actor.MovementSpeed == 7680 && actor.NativeMotionAuthority is { MovementDirection: 8, FacingHold: 0 }, "Dollet JUMP3 lost original operands, physical flags/scalars, raw setup placement or mode1 replacement ownership.");
        for (int elapsed = 1; elapsed <= 10; elapsed++)
        {
            EventEngine independent = Create();
            independent.Restore(live.Capture());
            live.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Original Dollet RMOVE-to-JUMP3 diverged after independent flight restoration.");
            Require(actor.NativeMovementCompletion542 == 1 && actor.NativeTurnAccumulator474 == elapsed && actor.NativeJump is { Active: true, AsyncFlag20000: true, AnimationFlag40000: true } phase && phase.FixedX == NativeJumpMath.Interpolate(jump.StartX, jump.TargetX, 10, (short)elapsed) && phase.FixedY == NativeJumpMath.Interpolate(jump.StartY, jump.TargetY, 10, (short)elapsed) && phase.FixedZ == NativeJumpMath.Height(jump.StartZ, jump.Velocity, (short)elapsed) && actor.NativeScriptMovement is null && actor.NativeMovementRequest is null, "Original Dollet fallback resumed old grounded movement, lost inherited flags or changed native flight timing.");
        }
        live.Step();
        Require(actor.NativeMovementCompletion542 == 2 && actor.NativeJump is { Active: true, AsyncFlag20000: true, AnimationFlag40000: true } && actor.WalkmeshTriangle == 98, "Dollet fallback missed its delayed triangle/completion update.");
        live.Step();
        Require(actor.NativeJump is { Active: false, AsyncFlag20000: false, AnimationFlag40000: false } && actor.NativeScriptMovement is null && actor.MovementTarget is null && actor.AnimationId == actor.BaseAnimationId, "Dollet fallback did not consume inherited asynchronous/animation completion flags after its VM completion poll.");
        for (int callback = 0; callback < 90 && !resumedOriginalTurn; callback++)
        {
            live.Step();
        }
        Require(resumedOriginalTurn && jumps.Count == 1 && actor.NativeScriptMovement is null && live.LastFault is null, "Dollet script did not continue through its unchanged ANIMEKEEP/RANIME sequence to the original post-jump CTURNL.");
        Console.WriteLine("Original312/script15: blocked asynchronous RMOVE279 reaches unchanged-position JUMP3/348, preserves705/-1953/11038/10 and inherited flags, restores each flight callback, consumes completion and resumes original post-jump animation/turn.");
    }
}
