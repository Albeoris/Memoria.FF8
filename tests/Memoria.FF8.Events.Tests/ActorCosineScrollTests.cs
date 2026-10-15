using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeActorCosineScrollContract()
    {
        EventEngine Create(int duration = 65540, int rawTarget = 2, bool certificate = true, bool knownStart = true)
        {
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] curve = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, rawTarget), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.CSCROLLA, 0), (Jsm.Opcode.SCROLLSYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] unused = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 0)];
            GameSession session = new(new GameState());
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
            engine.ResolveNativeNormalBackgroundScrollCenter = () => knownStart ? ((short)40, (short)-80) : null;
            engine.ResolveNativeActorScrollCenter = actor => actor < engine.Scene.NativeActorObjectIds.Count ? ((short)(100 * engine.Scene.NativeActorObjectIds[actor] + engine.Frame), (short)(-2 * engine.Frame)) : null;
            engine.CanSetNativeActorCosineBackgroundScroll = (actor, ticks) => certificate && ticks >= 0 && engine.Scene.NormalBackgroundScrollCenter is not null && actor < engine.Scene.NativeActorObjectIds.Count;
            engine.CanRestoreNativeActorScroll = (scene, actors) => scene.ActorBackgroundScroll is NativeActorBackgroundScroll state && actors.Any(actor => actor.ObjectId == state.ObjectId && actor.Active);
            engine.LoadField(600, SyntheticObjects([1, 16 << 7, 32 << 7], [init, curve, unused, target]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }

        EventEngine live = Create();
        NativeActorBackgroundScroll? commandState = null;
        int[]? operands = null;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.CSCROLLA))
            {
                commandState = live.Scene.ActorBackgroundScroll;
                operands = command.Arguments.ToArray();
            }
        };
        live.Step();
        Require(operands is not null && operands.SequenceEqual([2, 65540]) && commandState is { NativeObjectId: 2, ObjectId: 2, NativeActorId: 1, Mode: 2, Status: 0, Duration: 4, X: 40, Y: -80, ProjectedTargetX: null }, "CSCROLLA lost raw-to-retained mapping, fullDWORD evidence, lowWORD duration or pre-driver current center.");
        Require(live.Scene.ActorBackgroundScroll is { Mode: 2, Status: 1, PreviousX: 40, PreviousY: -80, X: 40, Y: -80, Counter: 1, ProjectedTargetX: 202, ProjectedTargetY: -4 } && live.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("WaitReason").GetString() == "BackgroundScroll"), "CSCROLLA did not retain counter0 or immediately poll SCROLLSYNC in the same VM budget.");
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        for (int elapsed = 1; elapsed <= 4; elapsed++)
        {
            live.Step();
            restored.Step();
            NativeActorBackgroundScroll state = live.Scene.ActorBackgroundScroll!;
            Require(state.X == NativeCosineInterpolation.Interpolate(40, 200 + (int)live.Frame, 4, elapsed) && state.Y == NativeCosineInterpolation.Interpolate(-80, -2 * (int)live.Frame, 4, elapsed) && state.ProjectedTargetX == 200 + live.Frame && state.ProjectedTargetY == -2 * live.Frame && state.Status == (elapsed == 4 ? 2 : 1) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Actor cosine camera froze its moving target, changed original cosine arithmetic or diverged after restore.");
            Require(!CapturedVariables(live.Capture()).TryGetProperty("5000", out _), "SCROLLSYNC resumed before the following VM callback.");
        }
        NativeActorBackgroundScroll completed = live.Scene.ActorBackgroundScroll!;
        live.Step();
        restored.Step();
        Require(live.Scene.ActorBackgroundScroll == completed && CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 77 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Completed actor cosine camera followed a later target or inserted an extra synchronization wait.");
        EventEngine zero = Create(0);
        zero.Step();
        Require(zero.Scene.ActorBackgroundScroll is { Mode: 2, Duration: 0, Counter: 0, Status: 2, X: 202, Y: -4 }, "Zero-duration actor cosine did not use the actual first callback target.");

        JsonElement valid = restored.Capture();
        foreach ((string name, int value) in new[] { ("NativeActorId", 0), ("Mode", 3), ("Counter", 3), ("Duration", -1), ("ProjectedTargetX", 207), ("Status", 1) })
        {
            JsonNode corrupt = JsonNode.Parse(valid.GetRawText())!;
            corrupt["Scene"]!["ActorBackgroundScroll"]![name] = value;
            EventEngine declined = Create();
            JsonElement before = declined.Capture();
            try
            {
                declined.Restore(JsonSerializer.SerializeToElement(corrupt));
                throw new InvalidOperationException("Actor cosine accepted a corrupt saved binding or arithmetic phase.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, declined.Capture()), "Rejected actor cosine state mutated its receiver.");
        }
        foreach (EventEngine declined in new[] { Create(-1), Create(rawTarget: 1), Create(rawTarget: 99), Create(certificate: false), Create(knownStart: false) })
        {
            try
            {
                declined.Step();
                throw new InvalidOperationException("CSCROLLA accepted unsupported operands, an unretained target or unknown start history.");
            }
            catch (EventScriptException) when (declined.LastFault?.Opcode == nameof(Jsm.Opcode.CSCROLLA))
            {
            }
            JsonElement fiber = declined.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 0 && !item.GetProperty("Initialization").GetBoolean());
            Require(fiber.GetProperty("Position").GetInt32() == 10 && fiber.GetProperty("Stack").GetArrayLength() == 2 && declined.Scene.ActorBackgroundScroll is null, "Rejected CSCROLLA consumed operands, advanced PC or created a camera state.");
        }
        Console.WriteLine("CSCROLLA: raw actor binding, lowWORD duration, authentic start, dynamic callback target, cosine endpoint/sync, independent restore and atomic refusal passed.");
    }
}
