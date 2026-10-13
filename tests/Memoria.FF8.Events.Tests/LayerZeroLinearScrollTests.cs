using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeLayerZeroLinearScrollContract()
    {
        EventEngine Create(int rawDuration = 65540, bool certificate = true, bool knownStart = true)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables) { CanSetLinearBackgroundScroll = query => certificate && query.Layer == 0 && query.StartX == 40 && query.StartY == -80 && query.TargetX == 12 && query.TargetY == -24 && query.Duration >= 0, CanRestoreLinearBackgroundScroll = _ => true };
            engine.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 40), (Jsm.Opcode.PSHN_L, -80), (Jsm.Opcode.DSCROLL2, 0), (Jsm.Opcode.PSHN_L, 65548), (Jsm.Opcode.PSHN_L, -65560), (Jsm.Opcode.PSHN_L, rawDuration), (Jsm.Opcode.LSCROLL, 0), (Jsm.Opcode.SCROLLSYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0)), []);
            engine.CanSetBackgroundScrollPosition = (_, _, _) => true;
            engine.Step();
            if (!knownStart)
            {
                engine.Scene.BackgroundScrollPositions.Clear();
            }
            return engine;
        }
        EventEngine live = Create();
        BackgroundScrollPosition? packet = null;
        int[]? operands = null;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.LSCROLL))
            {
                packet = live.Scene.BackgroundScrollPositions[0];
                operands = command.Arguments.ToArray();
            }
        };
        live.Step();
        Require(operands is not null && operands.SequenceEqual([65548, -65560, 65540]) && packet is { Mode: 4, Status: 0, X: 40, Y: -80, TargetX: 12, TargetY: -24, Duration: 4, NativeLinearVersion: 1 }, "LSCROLL changed three lowWORD operands, implicit layer0 or pre-driver current history.");
        Require(live.Scene.BackgroundScrollPositions[0] is { Status: 1, Counter: 1, PreviousX: 40, PreviousY: -80, X: 40, Y: -80 } && live.Capture().GetProperty("Fibers")[0].GetProperty("WaitReason").GetString() == "BackgroundScroll", "LSCROLL lost native counter0 or same-budget synchronization polling.");
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        for (short elapsed = 1; elapsed <= 4; elapsed++)
        {
            live.Step();
            restored.Step();
            BackgroundScrollPosition state = live.Scene.BackgroundScrollPositions[0];
            Require(state.X == NativeLinearScrollPhase.Interpolate(40, 12, 4, elapsed) && state.Y == NativeLinearScrollPhase.Interpolate(-80, -24, 4, elapsed) && state.Status == (elapsed == 4 ? 2 : 1) && JsonElement.DeepEquals(live.Capture(), restored.Capture()) && !live.Capture().GetProperty("Variables").TryGetProperty("5000", out _), "LSCROLL diverged from shared signed native linear arithmetic, inclusive endpoint or independent restore.");
        }
        live.Step();
        Require(live.Capture().GetProperty("Variables").GetProperty("5000").GetInt32() == 77, "LSCROLL synchronization did not resume after post-VM completion.");
        EventEngine zero = Create(0);
        zero.Step();
        Require(zero.Scene.BackgroundScrollPositions[0] is { Status: 2, Counter: 0, X: 40, Y: -80, TargetX: 12, TargetY: -24 }, "Zero-duration native linear scroll must retain its prior center, rather than select the target as cosine does.");
        foreach (EventEngine declined in new[] { Create(-1), Create(certificate: false), Create(knownStart: false) })
        {
            try
            {
                declined.Step();
                throw new InvalidOperationException("LSCROLL accepted negative duration, uncertified placement or unknown current history.");
            }
            catch (EventScriptException) when (declined.LastFault?.Opcode == nameof(Jsm.Opcode.LSCROLL))
            {
            }
            JsonElement fiber = declined.Capture().GetProperty("Fibers")[0];
            Require(fiber.GetProperty("Position").GetInt32() == 8 && fiber.GetProperty("Stack").GetArrayLength() == 3 && !declined.Scene.BackgroundScrollPositions.Values.Any(state => state.Mode == 4), "Rejected LSCROLL advanced PC, consumed operands or created a linear phase.");
        }
        Console.WriteLine("LSCROLL: implicit layer0, authentic start, lowWORD operands, shared linear phases, zero duration, synchronization and atomic refusal passed.");
    }
}
