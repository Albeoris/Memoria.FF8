using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyLinearScrollContract()
    {
        EventEngine Create(int slot = 0, int duration = 65540)
        {
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.LSCROLLP, 0), (Jsm.Opcode.SCROLLSYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0)]]);
            GameSession session = new(new GameState());
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
            engine.ResolveNativeNormalBackgroundScrollCenter = () => ((short)40, (short)-80);
            engine.ResolveNativeActorScrollCenter = actor => actor == 0 ? ((short)engine.Frame, (short)-engine.Frame) : null;
            engine.CanSetNativeActorLinearBackgroundScroll = (actor, ticks) => actor == 0 && ticks >= 0 && engine.Scene.NormalBackgroundScrollCenter is not null;
            engine.CanRestoreNativeActorScroll = (scene, _) => scene.ActorBackgroundScroll is { NativeActorId: 0, PartySlot: 0 };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        Require(live.Scene.ActorBackgroundScroll is { Mode: 1, PartySlot: 0, NativeActorId: 0, NativeObjectId: 0, ObjectId: 0, Duration: 4, Counter: 1, X: 40, Y: -80, ProjectedTargetX: 2, ProjectedTargetY: -2 }, "LSCROLLP lost saved B194 selector, lowWORD duration, genuine start or first callback endpoint.");
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        for (short elapsed = 1; elapsed <= 4; elapsed++)
        {
            live.Step();
            restored.Step();
            NativeActorBackgroundScroll state = live.Scene.ActorBackgroundScroll!;
            Require(state.X == NativeLinearScrollPhase.Interpolate(40, (short)live.Frame, 4, elapsed) && state.Y == NativeLinearScrollPhase.Interpolate(-80, (short)-live.Frame, 4, elapsed) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Restored party linear camera changed selected saved body or native moving-target interpolation.");
        }
        foreach (EventEngine declined in new[] { Create(1), Create(3), Create(duration: -1) })
        {
            try
            {
                declined.Step();
                throw new InvalidOperationException("LSCROLLP accepted absent/out-of-range saved slot or negative duration.");
            }
            catch (EventScriptException) when (declined.LastFault?.Opcode == nameof(Jsm.Opcode.LSCROLLP))
            {
            }
            JsonElement fiber = declined.Capture().GetProperty("Fibers").EnumerateArray().Single(item => !item.GetProperty("Initialization").GetBoolean());
            Require(fiber.GetProperty("Stack").GetArrayLength() == 2 && declined.Scene.ActorBackgroundScroll is null, "Refused LSCROLLP consumed operands or substituted a leader.");
        }
        Console.WriteLine("LSCROLLP: saved-party BYTE selector, shared dynamic linear curve, lowWORD duration, independent restore and atomic absent-slot refusal passed.");
    }
}
