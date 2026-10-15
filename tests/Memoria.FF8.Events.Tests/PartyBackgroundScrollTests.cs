using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyBackgroundScrollContract()
    {
        (GameSession Session, EventEngine Engine) Create(int slot = 0)
        {
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.DSCROLLP, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.SCROLLSYNC, 0), (Jsm.Opcode.RET, 0)]]);
            GameSession session = new(new GameState());
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
            engine.ResolveNativeActorScrollCenter = actor => actor == 0 ? ((short)engine.Frame, (short)-engine.Frame) : null;
            engine.CanRestoreNativeActorScroll = (scene, _) => scene.ActorBackgroundScroll is { NativeActorId: 0 };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var live = Create();
        live.Engine.Step();
        live.Engine.Step();
        Require(live.Engine.Scene.ActorBackgroundScroll is { PartySlot: 0, NativeActorId: 0, ObjectId: 0, NativeObjectId: 0, Status: 1, X: 2, Y: -2 } && !live.Session.State.Variables.ContainsKey(5000), "DSCROLLP lost saved-party selector, actual current center or return3 yield.");
        JsonElement saved = live.Engine.Capture();
        var copy = Create();
        copy.Engine.Restore(saved);
        for (int frame = 0; frame < 4; frame++)
        {
            live.Engine.Step();
            copy.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), copy.Engine.Capture()) && live.Engine.Scene.ActorBackgroundScroll!.X == live.Engine.Frame && live.Engine.Scene.ActorBackgroundScroll.Y == -live.Engine.Frame, "Restored DSCROLLP lost continuous tracking or exact VM state.");
        }
        Require(live.Session.State.Variables[5000] == 9 && live.Engine.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("WaitReason").GetString() == nameof(WaitReason.BackgroundScroll)), "Continuous party-follow camera completed SCROLLSYNC instead of holding status1.");
        JsonNode invalid = JsonNode.Parse(saved.GetRawText())!;
        invalid["Scene"]!["ActorBackgroundScroll"]!["PartySlot"] = 3;
        string before = copy.Engine.Capture().GetRawText();
        try
        {
            copy.Engine.Restore(JsonSerializer.SerializeToElement(invalid));
            throw new InvalidOperationException("DSCROLLP restored an invalid saved-party selector.");
        }
        catch (InvalidDataException)
        {
        }
        Require(copy.Engine.Capture().GetRawText() == before, "Malformed party camera restore mutated the VM.");
        var absent = Create(1);
        absent.Engine.Step();
        try
        {
            absent.Engine.Step();
            throw new InvalidOperationException("DSCROLLP substituted a leader for an absent saved party slot.");
        }
        catch (EventScriptException) when (absent.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.DSCROLLP))
        {
        }
        Require(absent.Engine.Scene.ActorBackgroundScroll is null && absent.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean()).GetProperty("Stack")[0].GetInt32() == 1, "Absent party camera request consumed its operand or invented a body.");
    }
}
