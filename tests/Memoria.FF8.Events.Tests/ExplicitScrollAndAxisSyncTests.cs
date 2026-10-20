using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void ExplicitScrollAndAxisSyncContracts()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        EventEngine Create(bool certificate = true, int duration = 65539)
        {
            byte[] program = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 65547), (Jsm.Opcode.PSHN_L, 65558), (Jsm.Opcode.DSCROLL3, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 65540), (Jsm.Opcode.PSHN_L, 65531), (Jsm.Opcode.PSHN_L, 65556), (Jsm.Opcode.PSHN_L, 65566), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.CSCROLL3, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SCROLLSYNC2, 0), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]]);
            EventEngine engine = new(CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy()))
            {
                CanSetBackgroundScrollPosition = (_, _, _) => true,
                CanSetExplicitCosineBackgroundScroll = (query, _) => certificate && query.Layer == 1 && query.StartX == 4 && query.StartY == -5 && query.TargetX == 20 && query.TargetY == 30 && query.Duration >= 0
            };
            engine.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        foreach (short duration in new short[] { 0, 3 })
        {
            EventEngine live = Create(duration: 65536 + duration);
            live.Step();
            Require(live.Scene.BackgroundScrollPositions[1] is { Mode: 3, Status: 2, X: 11, Y: 22 } && !CapturedVariables(live.Capture()).TryGetProperty("5000", out _), "DSCROLL3 failed lowWORD target, direct post-VM completion or return3 yield.");
            JsonElement? issued = null;
            live.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.CSCROLL3))
                {
                    BackgroundScrollPosition row = live.Scene.BackgroundScrollPositions[1];
                    Require(row is { Mode: 5, Status: 0, X: 4, Y: -5, TargetX: 20, TargetY: 30, Counter: 0 } && row.Duration == duration && row.PreviousX is null && row.PreviousY is null, "CSCROLL3 lost explicit start/target lowWORDs or overwrote previous/counter early.");
                    issued = live.Capture();
                }
            };
            live.Step();
            Require(issued is not null && CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 77 && !CapturedVariables(live.Capture()).TryGetProperty("5001", out _), "CSCROLL3 failed return2 same-budget continuation or completed sync before post-VM endpoint.");
            EventEngine restored = Create(duration: 65536 + duration);
            restored.Restore(live.Capture());
            for (int elapsed = 1; elapsed <= duration + 1; elapsed++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Explicit cosine phase changed after independent restore.");
                BackgroundScrollPosition row = live.Scene.BackgroundScrollPositions[1];
                Require(row.X == NativeCosineInterpolation.Interpolate(4, 20, duration, Math.Min(elapsed, duration)) && row.Y == NativeCosineInterpolation.Interpolate(-5, 30, duration, Math.Min(elapsed, duration)), "Explicit cosine did not use its explicit start and inclusive duration endpoint.");
            }
            Require(live.Scene.BackgroundScrollPositions[1] is { Status: 2, X: 20, Y: 30 } && CapturedVariables(live.Capture()).GetProperty("5001").GetInt32() == 88, "Explicit cosine/sync failed its endpoint and following-VM completion.");
        }
        EventEngine declined = Create(false);
        declined.Step();
        try
        {
            declined.Step();
            throw new InvalidOperationException("CSCROLL3 ignored a refused renderer certificate.");
        }
        catch (EventScriptException) when (declined.LastFault?.Opcode == nameof(Jsm.Opcode.CSCROLL3))
        {
            Require(declined.Scene.BackgroundScrollPositions[1] is { Mode: 3, X: 11, Y: 22 }, "Refused explicit cosine modified its retained direct row.");
        }

        byte[] axisProgram = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 65536 + 90), (Jsm.Opcode.PSHN_L, 65536 + 3), (Jsm.Opcode.AXIS, 0), (Jsm.Opcode.AXISSYNC, 0), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Axis()
        {
            EventEngine engine = new(CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy()));
            engine.LoadField(100, axisProgram, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        EventEngine axis = Axis();
        axis.Step();
        Require(!CapturedVariables(axis.Capture()).TryGetProperty("5000", out _) && axis.InputAxisDirection == 0, "AXISSYNC failed to yield after issue before the next pre-VM AXIS callback.");
        EventEngine axisCopy = Axis();
        axisCopy.Restore(axis.Capture());
        for (int callback = 0; callback < 3; callback++)
        {
            axis.Step();
            axisCopy.Step();
            Require(JsonElement.DeepEquals(axis.Capture(), axisCopy.Capture()), "AXISSYNC retained elapsed/duration changed across independent restore.");
        }
        Require(axis.InputAxisDirection == 90 && CapturedVariables(axis.Capture()).GetProperty("5000").GetInt32() == 99, "AXISSYNC failed its first VM poll after WORD elapsed/duration equality.");
        Console.WriteLine("DSCROLL3/CSCROLL3/AXISSYNC: lowWORD stores, return3/2 timing, explicit cosine start, zero-duration endpoint, sync polling, atomic refusal and independent restore passed.");
    }
}
