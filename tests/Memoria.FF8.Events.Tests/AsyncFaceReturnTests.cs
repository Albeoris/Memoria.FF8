using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeAsyncFaceReturnContract()
    {
        byte[] Content(int duration, bool asynchronous) => SyntheticObjects([1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.FACEDIRINIT, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.FACEDIRLIMIT, 0), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.EFFECTLOAD, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.RFACEDIR, 0), (Jsm.Opcode.FACEDIRSYNC, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, duration), (asynchronous ? Jsm.Opcode.RFACEDIROFF : Jsm.Opcode.FACEDIROFF, 0), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.FACEDIRSYNC, 0), (Jsm.Opcode.PSHN_L, 78), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create(byte[] content)
        {
            GameSession session = GameSession.CreateColdProcess();
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                ReadFaceBaseline = _ => new FaceDirectionBaseline(7, 8, 6),
                ReadFaceAnchorHeight = _ => 0,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY)
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        foreach (bool asynchronous in new[] { true, false })
        {
            byte[] content = Content(65536 + 2, asynchronous);
            EventEngine live = Create(content);
            while (!live.Actors[0].FaceReturning && live.Frame < 8)
            {
                live.Step();
            }
            ActorState actor = live.Actors[0];
            JsonElement pending = live.Capture();
            Require(actor.FaceDuration == 2 && actor.FaceProgress == 1 && actor.FaceTargetPoint is null && actor.FacePitchTarget == 6 && actor.FacePendingBaseline == 7 && actor.FaceYawTarget == 8 && actor.FaceElapsed == 8 && actor.FaceSecondAngle == 10 && CapturedVariables(pending).TryGetProperty("5000", out _) == asynchronous, "Face return lost low duration WORD, query32 XYZ targets, retained joint starts or native async/blocking return distinction.");
            Vector3 midpoint = actor.FirstControlRotation!.Value;
            Require(Math.Abs(midpoint.Y - 7 * MathF.Tau / 256) < 0.00001 && Math.Abs(midpoint.Z + 9 * MathF.Tau / 256) < 0.00001, "Face return used guessed relative targets or a second interpolation kernel.");
            EventEngine restored = Create(content);
            restored.Restore(pending);
            live.Step();
            restored.Step();
            Require(actor.FaceProgress == 2 && actor.FaceReturning && actor.FirstControlRotation is not null && !CapturedVariables(live.Capture()).TryGetProperty("5001", out _) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Face return released its override or completed source sync before the native closing callback.");
            EventEngine completedCopy = Create(content);
            completedCopy.Restore(live.Capture());
            live.Step();
            restored.Step();
            completedCopy.Step();
            JsonElement complete = live.Capture();
            JsonElement fiber = complete.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
            Require(!actor.FaceReturning && actor.FirstControlRotation is null && actor.FaceElapsed == 6 && actor.FaceFirstAngle == 7 && actor.FaceSecondAngle == 8 && actor.FaceDuration == 2 && actor.FaceProgress == 2 && actor.Direction == 0 && actor.X == 0 && actor.Y == 0 && actor.Z == 0 && CapturedVariables(complete).GetProperty("5000").GetInt32() == 77 && CapturedVariables(complete).GetProperty("5001").GetInt32() == 78 && fiber.GetProperty("Results").GetProperty("7").GetProperty("ActorInfoOrigin").GetProperty("NativeActorIndex").GetInt32() == 255 && JsonElement.DeepEquals(complete, restored.Capture()) && JsonElement.DeepEquals(complete, completedCopy.Capture()), "Completed face return changed physical pose, consumed unrelated unknown scalar, invented another callback or failed exact restore.");
        }
        foreach (int duration in new[] { 0, 65535 })
        {
            byte[] content = Content(duration, asynchronous: true);
            EventEngine edge = Create(content);
            while (edge.Actors[0].FaceDuration == 1 || edge.Actors[0].FaceTargetPoint is null && edge.Frame < 2)
            {
                edge.Step();
            }
            while (!CapturedVariables(edge.Capture()).TryGetProperty("5000", out _) && edge.Frame < 8)
            {
                edge.Step();
            }
            Require(edge.Actors[0].FaceDuration == duration && edge.Actors[0].FaceProgress == (duration == 0 ? 0 : 1) && (duration != 0 || edge.Actors[0].FirstControlRotation is null && !edge.Actors[0].FaceReturning), "RFACEDIROFF zero/full WORD duration introduced a wait, fabricated interpolation or discarded its native release.");
            EventEngine copy = Create(content);
            copy.Restore(edge.Capture());
            edge.Step();
            copy.Step();
            Require(JsonElement.DeepEquals(edge.Capture(), copy.Capture()), "RFACEDIROFF duration edge changed next callback after restore.");
        }
        foreach (bool asynchronous in new[] { true, false })
        {
            foreach (bool missing in new[] { true, false })
            {
                EventEngine refused = Create(Content(2, asynchronous));
                while (refused.Actors[0].FaceProgress != 1 && refused.Frame < 8)
                {
                    refused.Step();
                }
                Vector3? previous = refused.Actors[0].FirstControlRotation;
                refused.ReadFaceBaseline = missing ? null : _ => new FaceDirectionBaseline(256, 0, 0);
                try
                {
                    refused.Step();
                    throw new InvalidOperationException("Native face return accepted missing or invalid query32 targets.");
                }
                catch (EventScriptException) when (refused.LastFault?.Opcode == (asynchronous ? "RFACEDIROFF" : "FACEDIROFF"))
                {
                }
                JsonElement failed = refused.Capture();
                JsonElement fiber = failed.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
                Require(fiber.GetProperty("Position").GetInt32() == refused.LastFault!.Operation && fiber.GetProperty("Stack").GetArrayLength() == 2 && fiber.GetProperty("Stack")[0].GetInt32() == 2 && !refused.Actors[0].FaceReturning && refused.Actors[0].FaceTargetPoint == new Vector3(100, 0, 100) && refused.Actors[0].FaceDuration == 1 && refused.Actors[0].FaceProgress == 1 && refused.Actors[0].FirstControlRotation == previous && !CapturedVariables(failed).TryGetProperty("5000", out _), "Refused face return popped duration or changed point/phase/override before query32 validation.");
            }
        }
    }
}
