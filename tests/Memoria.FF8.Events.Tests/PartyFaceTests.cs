using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyFaceContract()
    {
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        byte[] Content(int slot, int duration, bool moving)
        {
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.FACEDIRINIT, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.FACEDIRLIMIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] face = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 123456), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.FACEDIRP, 0), (Jsm.Opcode.POPM_L, 5002), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.FACEDIRSYNC, 0), (Jsm.Opcode.PSHN_L, 78), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] unused = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -19), (Jsm.Opcode.PSHN_L, -22), (Jsm.Opcode.PSHN_L, 32760), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
            List<(Jsm.Opcode, int)> targetDefault = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 101), (Jsm.Opcode.PSHN_L, -202), (Jsm.Opcode.PSHN_L, 303), (Jsm.Opcode.DOFFSET, 0)];
            if (moving)
            {
                targetDefault.AddRange([(Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, -1000), (Jsm.Opcode.PSHN_L, -22), (Jsm.Opcode.PSHN_L, 32760), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0)]);
            }
            targetDefault.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            return SyntheticObjects([1, 16 << 7, (32 << 7) | 1], [caller, face, unused, target, targetDefault.ToArray()]);
        }
        EventEngine Create(int slot = 0, int duration = 65540, bool moving = false)
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                ReadFaceBaseline = _ => new FaceDirectionBaseline(0, 0),
                ReadFaceAnchorHeight = actor => actor.ObjectId == 2 ? 20 : 0,
                ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY),
                CanUseNativeScriptMove = actor => actor.ObjectId == 2,
                ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 192, 0),
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX - 1377;
                    int y = query.FixedY - 701;
                    NativeScriptGroundProbe[] probes = [new(x, y, 32760), new(x, y, 32760), new(x, y, 32760)];
                    return new(NativeScriptMovementStatus.Accepted, x, y, query.FixedZ, query.Triangle, query.Direction, [new(x, y, query.Direction, probes)]);
                }
            };
            engine.LoadField(74, Content(slot, duration, moving), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        JsonElement CallerFiber(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        bool HasVariable(EventEngine engine, string address) => engine.Capture().GetProperty("Variables").TryGetProperty(address, out _);
        void ReachIssue(EventEngine engine)
        {
            while (engine.Actors[0].FaceTargetPoint is null && engine.Frame < 16)
            {
                engine.Step();
            }
            Require(engine.Actors[0].FaceTargetPoint is not null, "FACEDIRP component did not reach its issue.");
        }

        EventEngine live = Create(moving: true);
        Vector3? sampled = null;
        NativeScriptMovementState? sampledMotion = null;
        int issues = 0;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.FACEDIRP))
            {
                issues++;
                sampledMotion = live.Actors[2].NativeScriptMovement;
                sampled = live.Actors[0].FaceTargetPoint;
                Require(command.Arguments.SequenceEqual([0, 65540]) && live.Actors[0].FaceDuration == 4 && live.Actors[0].FaceProgress == 0, "FACEDIRP changed push-order operands or failed to publish its unsigned duration WORD before the post-VM sample.");
            }
        };
        ReachIssue(live);
        ActorState actor = live.Actors[0];
        Require(issues == 1 && sampledMotion is not null && sampledMotion.FixedX < 0 && (sampledMotion.FixedX & 4095) != 0 && sampledMotion.FixedY < 0 && (sampledMotion.FixedY & 4095) != 0, $"FACEDIRP fixture did not execute negative fractional target motion: issues={issues}, motion={sampledMotion}.");
        Require(live.Scene.NativeActorObjectIds.SequenceEqual([0, 2]) && live.Scene.PartyBindingMemory is { First: 1 } && live.Scene.FieldPartyNativeActorIds.SequenceEqual([255, 255, 255]), "FACEDIRP fixture did not distinguish saved compact body selection from raw objects or detached runtime followers.");
        Vector3 expected = new(sampledMotion!.FixedX / 4096, sampledMotion.FixedY / 4096, -32756);
        Require(sampled == expected && live.Actors[2].DisplayOffset == new Vector3(101, -202, 303) && actor.FaceProgress == 1 && actor.FirstControlRotation is not null && !HasVariable(live, "5000") && CallerFiber(live).GetProperty("Stack")[0].GetInt32() == 123456, "FACEDIRP floored negative target coordinates, included target display offsets, lost final Z+height WORD wrap, consumed an unrelated operand or failed to block after its first post-VM sample.");
        JsonElement issued = live.Capture();
        EventEngine restored = Create(moving: true);
        restored.Restore(issued);
        while (actor.FaceProgress < actor.FaceDuration)
        {
            int progress = actor.FaceProgress;
            live.Step();
            restored.Step();
            Require(actor.FaceTargetPoint == expected && actor.FaceProgress == progress + 1 && !HasVariable(live, "5000") && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "FACEDIRP tracked later target movement, advanced its successor before native equality or diverged after independent pending restore.");
        }
        Require(live.Actors[2].X != sampledMotion.FixedX / 4096f && actor.FaceTargetPoint == expected && issues == 1, "FACEDIRP target was not captured once independently of continued target motion.");
        EventEngine completedCopy = Create(moving: true);
        completedCopy.Restore(live.Capture());
        live.Step();
        restored.Step();
        completedCopy.Step();
        Require(HasVariable(live, "5000") && HasVariable(live, "5001") && live.Capture().GetProperty("Variables").GetProperty("5002").GetInt32() == 123456 && actor.FaceProgress == 4 && actor.FaceTargetPoint == expected && actor.FirstControlRotation is not null && actor.X == 0 && actor.Y == 0 && actor.Z == 0 && actor.Direction == 0 && CallerFiber(live).GetProperty("Stack").GetArrayLength() == 0 && JsonElement.DeepEquals(live.Capture(), restored.Capture()) && JsonElement.DeepEquals(live.Capture(), completedCopy.Capture()), "FACEDIRP equality/FACEDIRSYNC did not continue in the closing VM callback, preserve physical pose and completed joint state or restore independently at equality.");

        foreach (int duration in new[] { 65536, 65535, -1 })
        {
            EventEngine edge = Create(duration: duration);
            ReachIssue(edge);
            int expectedDuration = unchecked((ushort)duration);
            Require(edge.Actors[0].FaceDuration == expectedDuration && edge.Actors[0].FaceProgress == (expectedDuration == 0 ? 0 : 1) && edge.Actors[0].FaceTargetPoint == new Vector3(-19, -22, -32756), "FACEDIRP narrowed duration to a byte/signed WORD or lost the target capture at a duration edge.");
            if (expectedDuration == 0)
            {
                Require(HasVariable(edge, "5000") && HasVariable(edge, "5001") && edge.Actors[0].FirstControlRotation is null, "FACEDIRP zero duration inserted a wait or manufactured a joint override.");
            }
            else
            {
                Require(!HasVariable(edge, "5000"), "FACEDIRP unsigned duration65535 completed immediately.");
            }
            EventEngine copy = Create(duration: duration);
            copy.Restore(edge.Capture());
            edge.Step();
            copy.Step();
            Require(JsonElement.DeepEquals(edge.Capture(), copy.Capture()), "FACEDIRP duration edge diverged after independent restore.");
        }

        foreach ((int slot, bool facing, bool height) in new[] { (1, true, true), (256, true, true), (65536, true, true), (0, false, true), (0, true, false) })
        {
            EventEngine refused = Create(slot: slot, duration: 4);
            if (!facing)
            {
                refused.ResolveNativeActorFacing = null;
            }
            if (!height)
            {
                refused.ReadFaceAnchorHeight = null;
            }
            try
            {
                ReachIssue(refused);
                throw new InvalidOperationException("FACEDIRP accepted an absent binding, wrapped fullDWORD slot or missing native provider.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == nameof(Jsm.Opcode.FACEDIRP))
            {
            }
            JsonElement fiber = CallerFiber(refused);
            ActorState unchanged = refused.Actors[0];
            Require(fiber.GetProperty("Position").GetInt32() == refused.LastFault!.Operation && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([4, slot, 123456]) && unchanged.FaceTargetPoint is null && unchanged.FaceDuration == 0 && unchanged.FaceProgress == 0 && unchanged.FirstControlRotation is null && unchanged.FaceElapsed == 0 && unchanged.FaceSecondAngle == 0 && unchanged.X == 0 && unchanged.Y == 0 && unchanged.Z == 0 && unchanged.Direction == 0 && !HasVariable(refused, "5000"), "FACEDIRP refusal advanced its original PC, consumed operands or partially changed its face/physical state.");
        }
        Console.WriteLine("FACEDIRP: saved compact party ownership, fullDWORD slot refusal, signed fractional target truncation, Z+joint WORD wrap/display-offset exclusion, captured target, unsigned duration edges, callback completion, independent restore and atomic binding/provider refusal passed.");
    }
}
