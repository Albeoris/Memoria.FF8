using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeActorLinearScrollContract()
    {
        EventEngine Create(int duration = 65540, int rawTarget = 2, bool certificate = true, bool knownStart = true)
        {
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] curve = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, rawTarget), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.LSCROLLA, 0), (Jsm.Opcode.SCROLLSYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] unused = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 0)];
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables) { AnimationFrameCount = (_, _) => 1 };
            engine.ResolveNativeNormalBackgroundScrollCenter = () => knownStart ? ((short)40, (short)-80) : null;
            engine.ResolveNativeActorScrollCenter = actor => actor < engine.Scene.NativeActorObjectIds.Count ? ((short)(100 * engine.Scene.NativeActorObjectIds[actor] + engine.Frame), (short)(-2 * engine.Frame)) : null;
            engine.CanSetNativeActorLinearBackgroundScroll = (actor, ticks) => certificate && ticks >= 0 && engine.Scene.NormalBackgroundScrollCenter is not null && actor < engine.Scene.NativeActorObjectIds.Count;
            engine.CanRestoreNativeActorScroll = (scene, actors) => scene.ActorBackgroundScroll is NativeActorBackgroundScroll state && actors.Any(actor => actor.ObjectId == state.ObjectId && actor.Active);
            engine.LoadField(600, SyntheticObjects([1, 16 << 7, 32 << 7], [init, curve, unused, target]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        EventEngine live = Create();
        NativeActorBackgroundScroll? packet = null;
        int[]? operands = null;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.LSCROLLA))
            {
                packet = live.Scene.ActorBackgroundScroll;
                operands = command.Arguments.ToArray();
            }
        };
        live.Step();
        Require(operands is not null && operands.SequenceEqual([2, 65540]) && packet is { NativeObjectId: 2, ObjectId: 2, NativeActorId: 1, Mode: 1, Status: 0, Duration: 4, X: 40, Y: -80 }, "LSCROLLA lost raw-to-retained binding, fullDWORD evidence, lowWORD duration or prior camera center.");
        Require(live.Scene.ActorBackgroundScroll is { Mode: 1, Status: 1, Counter: 1, X: 40, Y: -80, PreviousX: 40, PreviousY: -80, ProjectedTargetX: 202, ProjectedTargetY: -4 } && live.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("WaitReason").GetString() == "BackgroundScroll"), "LSCROLLA lost first counter0 or immediate same-budget synchronization polling.");
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        for (short elapsed = 1; elapsed <= 4; elapsed++)
        {
            live.Step();
            restored.Step();
            NativeActorBackgroundScroll state = live.Scene.ActorBackgroundScroll!;
            Require(state.X == NativeLinearScrollPhase.Interpolate(40, (short)(200 + live.Frame), 4, elapsed) && state.Y == NativeLinearScrollPhase.Interpolate(-80, (short)(-2 * live.Frame), 4, elapsed) && state.Status == (elapsed == 4 ? 2 : 1) && JsonElement.DeepEquals(live.Capture(), restored.Capture()) && !live.Capture().GetProperty("Variables").TryGetProperty("5000", out _), "LSCROLLA froze its moving endpoint, changed signed native linear arithmetic or lost restore/synchronization ordering.");
        }
        NativeActorBackgroundScroll completed = live.Scene.ActorBackgroundScroll!;
        live.Step();
        Require(live.Scene.ActorBackgroundScroll == completed && live.Capture().GetProperty("Variables").GetProperty("5000").GetInt32() == 77, "Completed actor linear camera kept following or delayed synchronization.");
        EventEngine zero = Create(0);
        zero.Step();
        Require(zero.Scene.ActorBackgroundScroll is { Mode: 1, Status: 2, Counter: 0, X: 40, Y: -80, ProjectedTargetX: 202, ProjectedTargetY: -4 }, "Zero-duration actor linear camera must retain start while recording the actual callback target.");
        JsonElement valid = restored.Capture();
        foreach ((string name, int value) in new[] { ("NativeActorId", 0), ("Mode", 3), ("Counter", 3), ("Duration", -1), ("ProjectedTargetX", 207) })
        {
            JsonNode corrupt = JsonNode.Parse(valid.GetRawText())!;
            corrupt["Scene"]!["ActorBackgroundScroll"]![name] = value;
            EventEngine declined = Create();
            JsonElement before = declined.Capture();
            try
            {
                declined.Restore(JsonSerializer.SerializeToElement(corrupt));
                throw new InvalidOperationException("LSCROLLA accepted a corrupt retained binding or native phase.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, declined.Capture()), "Rejected actor linear snapshot mutated the receiver.");
        }
        foreach (EventEngine declined in new[] { Create(-1), Create(rawTarget: 1), Create(rawTarget: 99), Create(certificate: false), Create(knownStart: false) })
        {
            try
            {
                declined.Step();
                throw new InvalidOperationException("LSCROLLA accepted unsupported operands, absent target or unknown current history.");
            }
            catch (EventScriptException) when (declined.LastFault?.Opcode == nameof(Jsm.Opcode.LSCROLLA))
            {
            }
            JsonElement fiber = declined.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 0 && !item.GetProperty("Initialization").GetBoolean());
            Require(fiber.GetProperty("Position").GetInt32() == 10 && fiber.GetProperty("Stack").GetArrayLength() == 2 && declined.Scene.ActorBackgroundScroll is null, "Refused LSCROLLA consumed operands, advanced PC or created camera state.");
        }
        Console.WriteLine("LSCROLLA: raw binding, shared dynamic linear endpoint, native duration/current/counter/sync, zero-duration prior center and atomic restore/refusal passed.");
    }
}
