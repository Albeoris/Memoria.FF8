using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyScrollAndBoundary(string gameDirectory)
    {
        NativePartyCosineScrollContract();
    }

    private static void NativePartyCosineScrollContract()
    {
        EventEngine Create(int slot = 0, int duration = 65540, bool certificate = true, bool knownStart = true)
        {
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] curve = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.CSCROLLP, 0), (Jsm.Opcode.SCROLLSYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] second = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables) { AnimationFrameCount = (_, _) => 1 };
            engine.ResolveNativeNormalBackgroundScrollCenter = () => knownStart ? ((short)40, (short)-80) : null;
            engine.ResolveNativeActorScrollCenter = actor => actor < engine.Scene.NativeActorObjectIds.Count ? ((short)(100 * actor + engine.Frame), (short)-engine.Frame) : null;
            engine.CanSetPartyBackgroundScroll = (partySlot, actor, ticks) => certificate && ticks >= 0 && engine.Scene.NormalBackgroundScrollCenter is not null && engine.Scene.PartyBindingMemory?.At(partySlot) == actor;
            engine.CanRestoreNativeActorScroll = (scene, actors) => scene.ActorBackgroundScroll is NativeActorBackgroundScroll state && actors.Any(actor => actor.ObjectId == state.ObjectId && actor.Active);
            engine.LoadField(600, SyntheticObjects([1, 16 << 7], [init, curve, second]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }

        EventEngine live = Create();
        NativeActorBackgroundScroll? pending = null;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.CSCROLLP))
            {
                pending = live.Scene.ActorBackgroundScroll;
            }
        };
        live.Step();
        Require(live.Scene.FieldPartyNativeActorIds[0] == 255 && live.Scene.PartyBindingMemory!.First == 0 && pending is { Mode: 2, Status: 0, PartySlot: 0, NativeActorId: 0, Duration: 4, X: 40, Y: -80, ProjectedTargetX: null }, "CSCROLLP lost saved B194 despite detached follow alias, lowWORD duration, genuine current center or same-budget entry.");
        Require(live.Scene.ActorBackgroundScroll is { Mode: 2, Status: 1, Counter: 1, X: 40, Y: -80, PreviousX: 40, PreviousY: -80, ProjectedTargetX: 2, ProjectedTargetY: -2 }, "CSCROLLP lost counter0 or actual nonzero callback target.");
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        for (short elapsed = 1; elapsed <= 4; elapsed++)
        {
            live.Step();
            restored.Step();
            NativeActorBackgroundScroll state = live.Scene.ActorBackgroundScroll!;
            Require(state.X == NativeCosineInterpolation.Interpolate(40, (int)live.Frame, 4, elapsed) && state.Y == NativeCosineInterpolation.Interpolate(-80, -(int)live.Frame, 4, elapsed) && state.Counter == Math.Min(elapsed + 1, 4) && state.Status == (elapsed == 4 ? 2 : 1) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "CSCROLLP froze its selected actor, changed cosine arithmetic or diverged after independent midphase restore.");
            Require(!live.Capture().GetProperty("Variables").TryGetProperty("5000", out _), "SCROLLSYNC resumed before the following VM callback.");
        }
        NativeActorBackgroundScroll complete = live.Scene.ActorBackgroundScroll!;
        live.Step();
        restored.Step();
        Require(live.Scene.ActorBackgroundScroll == complete && live.Capture().GetProperty("Variables").GetProperty("5000").GetInt32() == 77 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Completed saved-party cosine followed a later body or added a sync wait.");
        EventEngine secondBody = Create(slot: 1);
        secondBody.Step();
        Require(secondBody.Scene.ActorBackgroundScroll is { PartySlot: 1, NativeActorId: 1, ObjectId: 1, ProjectedTargetX: 102 }, "CSCROLLP substituted the leader for a saved second body.");
        EventEngine zero = Create(duration: 0);
        zero.Step();
        Require(zero.Scene.ActorBackgroundScroll is { Duration: 0, Counter: 0, Status: 2, X: 2, Y: -2 }, "Zero-duration CSCROLLP did not commit the actual callback endpoint.");

        JsonElement valid = restored.Capture();
        foreach ((string name, int value) in new[] { ("PartySlot", 3), ("NativeActorId", 1), ("Counter", 3), ("Duration", -1), ("ProjectedTargetX", 999) })
        {
            JsonNode corrupt = JsonNode.Parse(valid.GetRawText())!;
            corrupt["Scene"]!["ActorBackgroundScroll"]![name] = value;
            EventEngine declined = Create();
            JsonElement before = declined.Capture();
            try
            {
                declined.Restore(JsonSerializer.SerializeToElement(corrupt));
                throw new InvalidOperationException("CSCROLLP accepted a corrupt checkpoint binding or phase.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, declined.Capture()), "Rejected saved-party camera checkpoint mutated its receiver.");
        }
        foreach (EventEngine declined in new[] { Create(slot: 2), Create(slot: 3), Create(duration: -1), Create(certificate: false), Create(knownStart: false) })
        {
            try
            {
                declined.Step();
                throw new InvalidOperationException("CSCROLLP accepted absent saved body, unknown center or uncertified projection.");
            }
            catch (EventScriptException) when (declined.LastFault?.Opcode == nameof(Jsm.Opcode.CSCROLLP))
            {
            }
            JsonElement fiber = declined.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 0 && !item.GetProperty("Initialization").GetBoolean());
            Require(fiber.GetProperty("Position").GetInt32() == 12 && fiber.GetProperty("Stack").GetArrayLength() == 2 && declined.Scene.ActorBackgroundScroll is null, "Refused CSCROLLP consumed operands, advanced PC or created a guessed camera.");
        }
        Console.WriteLine("CSCROLLP: copied saved B194 selector despite FOLLOWOFF, second body, lowWORD duration, dynamic cosine target, inclusive endpoint/zero/sync, independent restore and atomic refusal passed.");
    }
}
