using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeLayerZeroCosineScrollContract()
    {
        EventEngine Create(int rawDuration = 65540, bool certificate = true)
        {
            GameSession session = new(new GameState());
            EventEngine result = new(session)
            {
                CanSetBackgroundScrollPosition = (_, _, _) => true,
                CanSetCosineBackgroundScroll = (layer, x, y, duration) => certificate && layer == 0 && x == 12 && y == -24 && duration == (short)rawDuration
            };
            result.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 40), (Jsm.Opcode.PSHN_L, -80), (Jsm.Opcode.DSCROLL2, 0), (Jsm.Opcode.PSHN_L, 65548), (Jsm.Opcode.PSHN_L, -65560), (Jsm.Opcode.PSHN_L, rawDuration), (Jsm.Opcode.CSCROLL, 0), (Jsm.Opcode.SCROLLSYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.RET, 0)), []);
            result.Scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 3, 2);
            result.Step();
            return result;
        }

        EventEngine live = Create();
        List<int[]> operands = [];
        BackgroundScrollPosition? packet = null;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.CSCROLL))
            {
                operands.Add(command.Arguments.ToArray());
                packet = live.Scene.BackgroundScrollPositions[0];
            }
        };
        live.Step();
        Require(operands.Count == 1 && operands[0].SequenceEqual(new[] { 65548, -65560, 65540 }), "CSCROLL changed fullDWORD command evidence or consumed a nonexistent layer operand.");
        Require(packet is { Mode: 5, Status: 0, X: 40, Y: -80, TargetX: 12, TargetY: -24, Duration: 4, Counter: 0 }, "CSCROLL failed its layer0 lowWORD packet or changed current coordinates before the post-VM driver.");
        BackgroundScrollPosition first = live.Scene.BackgroundScrollPositions[0];
        JsonElement pending = live.Capture().GetProperty("Fibers")[0];
        Require(first is { Mode: 5, Status: 1, Counter: 1, X: 40, Y: -80, PreviousX: 40, PreviousY: -80 } && pending.GetProperty("Position").GetInt32() == 9 && pending.GetProperty("WaitReason").GetString() == "BackgroundScroll" && pending.GetProperty("Stack").GetArrayLength() == 0, "CSCROLL yielded its own budget, lost counter0 or failed immediate SCROLLSYNC polling.");

        EventEngine restored = Create();
        restored.Restore(live.Capture());
        for (int elapsed = 1; elapsed <= 4; elapsed++)
        {
            live.Step();
            restored.Step();
            BackgroundScrollPosition phase = live.Scene.BackgroundScrollPositions[0];
            Require(phase.X == NativeCosineInterpolation.Interpolate(40, 12, 4, elapsed) && phase.Y == NativeCosineInterpolation.Interpolate(-80, -24, 4, elapsed) && phase.Counter == Math.Min(elapsed + 1, 4) && phase.Status == (elapsed == 4 ? 2 : 1) && !CapturedVariables(live.Capture()).TryGetProperty("5000", out _) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "CSCROLL changed shared cosine arithmetic, endpoint, held synchronization or restored phase.");
            if (elapsed == 2)
            {
                restored = Create();
                restored.Restore(live.Capture());
            }
        }
        live.Step();
        restored.Step();
        Require(CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 77 && operands.Count == 1 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "CSCROLL synchronization failed same-budget continuation on the first VM tick after post-VM completion.");

        EventEngine zero = Create(0);
        zero.Step();
        Require(zero.Scene.BackgroundScrollPositions[0] is { Mode: 5, Duration: 0, Counter: 0, Status: 2, X: 12, Y: -24 } && !CapturedVariables(zero.Capture()).TryGetProperty("5000", out _), "Zero-duration CSCROLL failed its native immediate target and post-VM completion.");
        zero.Step();
        Require(CapturedVariables(zero.Capture()).GetProperty("5000").GetInt32() == 77, "Zero-duration CSCROLL synchronization inserted an extra VM wait.");

        foreach (EventEngine declined in new[] { Create(certificate: false), Create(-1) })
        {
            BackgroundScrollPosition before = declined.Scene.BackgroundScrollPositions[0];
            try
            {
                declined.Step();
                throw new InvalidOperationException("CSCROLL accepted uncertified state or a negative signedWORD duration.");
            }
            catch (EventScriptException) when (declined.LastFault?.Opcode == nameof(Jsm.Opcode.CSCROLL))
            {
            }
            JsonElement refused = declined.Capture().GetProperty("Fibers")[0];
            Require(refused.GetProperty("Position").GetInt32() == 8 && refused.GetProperty("Stack").GetArrayLength() == 3 && refused.GetProperty("Stack")[1].GetInt32() == -65560 && refused.GetProperty("Stack")[2].GetInt32() == 65548 && declined.Scene.BackgroundScrollPositions[0] == before, "Refused CSCROLL changed PC, original operands or scroll history.");
        }
        Console.WriteLine("CSCROLL: implicit layer0, three lowWORD operands, preserved current/post-VM cosine phases, same-budget sync, zero duration, independent restore and atomic refusal passed.");
    }
}
