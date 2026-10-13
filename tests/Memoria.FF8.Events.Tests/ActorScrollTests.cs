using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeActorScrollContract()
    {
        byte[] Content(int rawTarget)
        {
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] follow = [(Jsm.Opcode.LBL, 1), (rawTarget < 0 ? Jsm.Opcode.PSHN_L : Jsm.Opcode.PSHAC, rawTarget), (Jsm.Opcode.DSCROLLA, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHAC, 0), (Jsm.Opcode.DSCROLLA, 0), (Jsm.Opcode.SCROLLSYNC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] unused = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] replacement = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, -3), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.DSCROLL, 0), (Jsm.Opcode.RET, 0)];
            return SyntheticObjects([1, 16 << 7, (32 << 7) | 1], [init, follow, unused, target, replacement]);
        }
        (GameSession Session, EventEngine Engine) Create(int rawTarget = 2)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables) { AnimationFrameCount = (_, _) => 1, CanSetBackgroundScrollPosition = (layer, _, _) => layer == 0 };
            // The synthetic host supplies a unique endpoint that changes each actual callback.
            // App projection tests separately exercise the real native camera/clamp certificate.
            engine.ResolveNativeActorScrollCenter = actor => actor < engine.Scene.NativeActorObjectIds.Count ? ((short)(100 * engine.Scene.NativeActorObjectIds[actor] + engine.Frame), (short)(-engine.Frame)) : null;
            engine.CanRestoreNativeActorScroll = (scene, actors) => scene.ActorBackgroundScroll is NativeActorBackgroundScroll state && actors.Any(actor => actor.ObjectId == state.ObjectId && actor.Active);
            engine.LoadField(600, Content(rawTarget), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var live = Create();
        live.Engine.Step();
        Require(live.Engine.Scene.NativeActorObjectIds.SequenceEqual([0, 2]), "Synthetic native compaction did not remove raw slot1.");
        live.Engine.Step();
        Require(live.Engine.Scene.ActorBackgroundScroll is { NativeObjectId: 2, ObjectId: 2, NativeActorId: 1, Status: 1, X: 202, Y: -2, PreviousX: null, PreviousY: null } && !live.Session.State.Variables.ContainsKey(5000), "DSCROLLA lost raw-to-retained selection, same-callback endpoint, unknown previous center or return3 yield.");
        JsonElement saved = live.Engine.Capture();
        JsonNode corrupt = JsonNode.Parse(saved.GetRawText())!;
        corrupt["Scene"]!["ActorBackgroundScroll"]!["NativeActorId"] = 0;
        var rejected = Create();
        JsonElement untouched = rejected.Engine.Capture();
        try
        {
            rejected.Engine.Restore(JsonSerializer.SerializeToElement(corrupt));
            throw new InvalidOperationException("Saved actor scroll accepted a mismatched compact binding.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(untouched, rejected.Engine.Capture()), "Rejected saved actor binding mutated the receiving engine.");
        var restored = Create();
        restored.Engine.Restore(saved);
        bool switched = false;
        bool waited = false;
        bool replaced = false;
        for (int frame = 0; frame < 20; frame++)
        {
            live.Engine.Step();
            restored.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Actor-follow camera diverged after independent typed restore.");
            if (live.Engine.Scene.ActorBackgroundScroll is NativeActorBackgroundScroll state)
            {
                Require(state.Status == 1 && state.X == 100 * state.ObjectId + live.Engine.Frame && state.Y == -live.Engine.Frame, "Continuous follow stopped updating or fabricated completed status2.");
                if (state.ObjectId == 0)
                {
                    switched = true;
                    Require(state.PreviousX > 200 && state.PreviousY < 0, "DSCROLLA replacement lost the actual preceding current center.");
                }
                if (live.Engine.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("WaitReason").GetString() == nameof(WaitReason.BackgroundScroll)))
                {
                    waited = true;
                    Require(!live.Session.State.Variables.ContainsKey(5001), "SCROLLSYNC completed a continuous status1 actor camera.");
                }
            }
            else if (live.Engine.Scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? direct))
            {
                replaced = true;
                Require(direct is { Mode: 3, Status: 2, X: -3, Y: 4 } && direct.PreviousX > 0, "Direct replacement kept competing follow state or inferred a zero previous center.");
            }
        }
        Require(switched && waited && replaced && live.Session.State.Variables[5000] == 9 && live.Session.State.Variables[5001] == 7, "Actor replacement/synchronization did not complete after the real direct mode replacement.");
        foreach (int raw in new[] { -1, 1, 99 })
        {
            var invalid = Create(raw);
            invalid.Engine.Step();
            try
            {
                invalid.Engine.Step();
                throw new InvalidOperationException("DSCROLLA admitted an invalid or nonretained source object.");
            }
            catch (EventScriptException) when (invalid.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.DSCROLLA))
            {
            }
            JsonElement fiber = invalid.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 0 && !item.GetProperty("Initialization").GetBoolean());
            Require(fiber.GetProperty("Stack")[0].GetInt32() == raw && invalid.Engine.Scene.ActorBackgroundScroll is null, "DSCROLLA preflight consumed its raw operand or changed scene state.");
        }
        Console.WriteLine("Actor scroll: raw-to-compacted mapping, continuous moving endpoint/status1, native yield, replacement/SCROLLSYNC and independent JSON restore plus atomic invalid targets passed.");
    }
}
