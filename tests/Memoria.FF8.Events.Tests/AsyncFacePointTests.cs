using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeAsyncFacePointContract()
    {
        byte[] Content(int duration, bool replacement, bool consumedUnknown = false)
        {
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.FACEDIRINIT, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.FACEDIRLIMIT, 0), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.EFFECTLOAD, 0), (Jsm.Opcode.RET, 8)];
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, 100 + 65536), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 100)];
            main.Add(consumedUnknown ? (Jsm.Opcode.PSHI_L, 0) : (Jsm.Opcode.PSHN_L, duration));
            main.AddRange([(Jsm.Opcode.RFACEDIR, 0), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0)]);
            if (replacement)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, -100), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -100), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.RFACEDIR, 0), (Jsm.Opcode.PSHN_L, 74), (Jsm.Opcode.POPM_B, 5001)]);
            }
            main.AddRange([(Jsm.Opcode.FACEDIRSYNC, 0), (Jsm.Opcode.PSHN_L, 75), (Jsm.Opcode.POPM_B, 5002), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            return SyntheticObjects([1], [init, main.ToArray()]);
        }
        EventEngine Create(byte[] program)
        {
            GameSession session = GameSession.CreateColdProcess();
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                ReadFaceBaseline = _ => new FaceDirectionBaseline(0, 0),
                ReadFaceAnchorHeight = _ => 0,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY)
            };
            engine.LoadField(600, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        byte[] content = Content(8 + 65536, replacement: true);
        EventEngine live = Create(content);
        int issued = 0;
        live.CommandExecuted += command =>
        {
            if (command.Name == "RFACEDIR")
            {
                issued++;
                Require(live.Actors[0].FaceProgress == 0 && live.Actors[0].FaceDuration == (issued == 1 ? 8 : 2) && command.Arguments.Count == 4, "RFACEDIR did not issue the native WORDs before its ordinary post-body callback.");
            }
        };
        while (live.Actors[0].FaceTargetPoint is null && live.Frame < 8)
        {
            live.Step();
        }
        ActorState actor = live.Actors[0];
        JsonElement first = live.Capture();
        JsonElement source = first.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(issued == 1 && actor.FaceTargetPoint == new Vector3(100, 0, 100) && actor.FaceDuration == 8 && actor.FaceProgress == 1 && first.GetProperty("Variables").GetProperty("5000").GetInt32() == 73 && source.GetProperty("Stack").GetArrayLength() == 0 && source.GetProperty("Results").GetProperty("7").GetProperty("ActorInfoOrigin").GetProperty("NativeActorIndex").GetInt32() == 255 && source.GetProperty("WaitReason").GetString() == "Frames", "RFACEDIR waited, consumed unrelated opaque scalar, interpreted relative angles or lost low signedXYZ/unsignedduration WORDs.");
        EventEngine restored = Create(content);
        restored.Restore(first);
        while (actor.FaceDuration == 8 && live.Frame < 12)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "RFACEDIR pending/replacement phase diverged across independent restore.");
        }
        Vector3 midpoint = actor.FirstControlRotation!.Value;
        Require(issued == 2 && actor.FaceTargetPoint == new Vector3(-100, 0, -100) && actor.FaceProgress == 1 && actor.FaceElapsed == 0 && actor.FaceSecondAngle == 0 && actor.FacePitchTarget == -8 && actor.FaceYawTarget == -10 && Math.Abs(midpoint.Y - 252 * MathF.Tau / 256) < 0.00001 && Math.Abs(midpoint.Z + 251 * MathF.Tau / 256) < 0.00001 && live.Capture().GetProperty("Variables").GetProperty("5001").GetInt32() == 74, "Replacement RFACEDIR reset starts to the intermediate override, applied a relative point or blocked its same-callback successor.");
        live.Step();
        restored.Step();
        Require(actor.FaceProgress == 2 && !live.Capture().GetProperty("Variables").TryGetProperty("5002", out _), "Post-body completion became a premature VM sync result.");
        live.Step();
        restored.Step();
        Require(live.Capture().GetProperty("Variables").GetProperty("5002").GetInt32() == 75 && actor.Direction == 0 && actor.X == 0 && actor.Y == 0 && actor.Z == 0 && actor.FaceElapsed == -8 && actor.FaceSecondAngle == -10 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "RFACEDIR/FACEDIRSYNC completion changed physical pose, final words or continuation.");
        foreach (int duration in new[] { 0, 65535 })
        {
            byte[] edgeContent = Content(duration, replacement: false);
            EventEngine edge = Create(edgeContent);
            while (edge.Actors[0].FaceTargetPoint is null && edge.Frame < 8)
            {
                edge.Step();
            }
            Require(edge.Actors[0].FaceDuration == duration && edge.Actors[0].FaceProgress == (duration == 0 ? 0 : 1) && edge.Capture().GetProperty("Variables").GetProperty("5000").GetInt32() == 73 && (duration != 0 || edge.Actors[0].FirstControlRotation is null), "RFACEDIR zero/full WORD duration invented a wait or interpolation.");
            EventEngine copy = Create(edgeContent);
            copy.Restore(edge.Capture());
            edge.Step();
            copy.Step();
            Require(JsonElement.DeepEquals(edge.Capture(), copy.Capture()), "RFACEDIR duration edge failed independent current-frame restore.");
        }
        EventEngine missing = Create(Content(8, replacement: false));
        missing.ResolveNativeActorFacing = null;
        EventEngine unknown = Create(Content(8, replacement: false, consumedUnknown: true));
        foreach (EventEngine refused in new[] { missing, unknown })
        {
            try
            {
                while (refused.Frame < 8)
                {
                    refused.Step();
                }
                throw new InvalidOperationException("RFACEDIR accepted absent native facing or an unknown consumed WORD.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == "RFACEDIR")
            {
            }
            JsonElement failed = refused.Capture();
            JsonElement fiber = failed.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
            Require(fiber.GetProperty("Stack").GetArrayLength() == 5 && fiber.GetProperty("Position").GetInt32() == refused.LastFault!.Operation && refused.Actors[0].FaceTargetPoint is null && refused.Actors[0].FaceDuration == 0 && refused.Actors[0].FaceProgress == 0 && refused.Actors[0].FirstControlRotation is null && !failed.GetProperty("Variables").TryGetProperty("5000", out _), "Refused RFACEDIR popped known operands before an unknown value or partially published its face phase.");
        }
    }
}
