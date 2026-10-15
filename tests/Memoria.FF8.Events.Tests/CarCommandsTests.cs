using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeCarCommandsContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeShakeTables tables = assets.ReadNativeShakeTables();
        NativeShakeAxis cold = NativeShakeAxis.FromColdProcess();
        Require(cold.Cursor == 0 && cold.Output == 0 && cold.Previous == 0 && cold.Target == 0, "Authenticated cold process shake block was not zero-filled.");
        NativeShakeAxis first = (cold with { Enabled = true, Amplitude = 8, Duration = 4 }).Step(tables);
        Require(first.Output == 0 && first.Cursor == 1 && first.Previous == 0 && first.Target == unchecked((short)(8 * tables.Random(0))) / 256, "First native shake phase wrote output or used the wrong process cursor.");
        foreach (short amplitude in new short[] { short.MaxValue, short.MinValue, -8 })
        {
            NativeShakeAxis wrapped = (cold with { Enabled = true, Amplitude = amplitude, Duration = 0 }).Step(tables);
            Require(wrapped.Target == unchecked((short)(amplitude * tables.Random(0))) / 256, "Large signed shake amplitude did not narrow its product before native division.");
            _ = wrapped.Step(tables);
        }

        (Jsm.Opcode, int)[] leader = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SUBPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] direct = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 65533), (Jsm.Opcode.PSHN_L, 65540), (Jsm.Opcode.DSCROLL, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] follower = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] detach = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 30), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SEVOLTRANS, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] other = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] otherDetach = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] control = [(Jsm.Opcode.LBL, 48), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] shake = [(Jsm.Opcode.LBL, 49), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.SHAKE, 0), (Jsm.Opcode.SHAKEOFF, 0), (Jsm.Opcode.PSHN_L, 30), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([1, (16 << 7) | 1, (32 << 7) | 1, (48 << 7) | 1], [leader, direct, follower, detach, other, otherDetach, control, shake]);
        (GameSession Session, EventEngine Engine) Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals().DeepCopy());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            EventEngine engine = new(session) { ShakeTables = tables, AnimationFrameCount = (_, _) => 5, CanSetBackgroundScrollPosition = (layer, x, y) => layer == 0 && x == -3 && y == 4 };
            engine.LoadField(100, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.ResolveNativeNormalBackgroundScrollCenter = () => ((short)0, (short)0);
            return (session, engine);
        }
        var live = Create();
        live.Engine.Step();
        int[] aliases = [0, 2, 1];
        live.Engine.Step();
        Require(live.Engine.Scene.BackgroundScrollPositions[0] is { Mode: 3, Status: 2, X: -3, Y: 4 } && !live.Session.State.Variables.ContainsKey(5000), "DSCROLL lost two-WORD narrowing, same-callback direct placement or return3 yield.");
        Require(live.Engine.Scene.Party.SequenceEqual([0, 6, 1]) && live.Engine.Scene.FieldPartyNativeActorIds.SequenceEqual([0, 255, 255]) && Enumerable.Range(194, 3).Select(address => live.Session.State.Variables[address]).SequenceEqual(aliases), "FOLLOWOFF changed canonical party/saved aliases or retained runtime follower bindings.");
        Require(live.Engine.Actors[1].NativeFollowOff is { PartySlot: 2, BaseAnimationId: 42, AnimationFrames: 5 } && live.Engine.Actors[2].NativeFollowOff is { PartySlot: 1 } && live.Engine.Actors[1].AnimationFrame == 0 && live.Engine.Actors[1].AnimationLoop, "FOLLOWOFF did not reset its original base animation through the known model frames.");
        Require(live.Engine.Scene.SoundVolumeTransition is { Voice: 1, DurationByte: 60, DurationMilliseconds: 600, Volume: 0, BackendStatus: GlobalSoundBackendStatus.Muted } && live.Session.State.Variables[5001] == 7, "SEVOLTRANS lost native narrowing or introduced a field wait.");
        Require(live.Engine.FieldShake is { X.Enabled: true, Y.Enabled: true } && live.Engine.FieldShake.X.Output == 0, "SHAKE failed to preserve first-phase output before its return3 yield.");
        JsonElement saved = live.Engine.Capture();
        var independent = Create();
        independent.Engine.Restore(saved);

        for (int frame = 0; frame < 40; frame++)
        {
            live.Engine.Step();
            independent.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), independent.Engine.Capture()), "Shared car command state or current shake words diverged after independent restore.");

        }
        Require( live.Engine.FieldShake!.X.Output == 0 && live.Engine.FieldShake.Y.Output == 0 && !live.Engine.FieldShake.X.Enabled && !live.Engine.FieldShake.Y.Enabled && live.Session.State.Variables[5000] == 9, "SHAKEOFF fabricated immediate zero or failed to finish native residual decay without blocking VM progression.");

        foreach (int duration in new[] { 0, 2, 30, 128, -1 })
        {
            GameSession session = GameSession.CreateColdProcess();
            EventEngine engine = new(session);
            engine.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0x1000005), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SEVOLTRANS, 0), (Jsm.Opcode.RET, 0)), []);
            engine.Step();
            byte narrowed = unchecked((byte)(duration * 2));
            Require(engine.Scene.SoundVolumeTransition is { Mask: 5, Voice: 1, Volume: 127 } request && request.DurationMilliseconds == (narrowed < 5 ? 0 : narrowed * 10), "SEVOLTRANS differs from native mask, least voice, BYTE duration wrap or sub50ms clamp.");
        }
        GameSession unknownSession = new(new GameState());
        EventEngine rejected = new(unknownSession) { ShakeTables = tables };
        byte[] invalid = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.SHAKE, 0), (Jsm.Opcode.RET, 0));
        rejected.LoadField(100, invalid, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        try
        {
            rejected.Step();
            throw new InvalidOperationException("SHAKE initialized an unknown process origin.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement fiber = rejected.Capture().GetProperty("Fibers")[0];
        Require(fiber.GetProperty("Position").GetInt32() == 5 && fiber.GetProperty("Stack").GetArrayLength() == 4, "Car SHAKE preflight consumed the original operands or operation.");
        Console.WriteLine("Car commands: DSCROLL layer0 WORD/yield, FOLLOWOFF runtime detachment/idle/saved aliases, native cold SHAKE cursor/output/decay, SEVOLTRANS immediate typed muted request and independent persistence passed.");
    }
}
