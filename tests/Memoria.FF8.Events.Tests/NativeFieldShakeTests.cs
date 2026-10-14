using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFieldShakeContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeShakeTables tables = assets.ReadNativeShakeTables();
        NativeFieldShake cold = GameSession.CreateColdProcess().FieldShake!;
        Require(cold.IsColdProcess && cold.X.Cursor == 0 && cold.Y.Cursor == 0 && cold.X.Output == 0 && cold.X.Previous == 0 && cold.X.Target == 0 && cold.X.Amplitude == 0, "Cold PE shake words were not retained as one zero-filled process owner.");
        Require(tables.Random(0) == 7 && tables.Random(1) == 182 && tables.Cosine(128) == -4096 && tables.Cosine(192) == 0, "Authenticated native lookup golden values differ.");
        NativeShakeAxis axis = (cold.X with { Enabled = true, Amplitude = 256, Duration = 4 }).Step(tables);
        Require(axis.Cursor == 1 && axis.Previous == 0 && axis.Target == 7 && axis.Output == 0 && axis.Counter == 0, "First activation wrote output or used a guessed process cursor.");
        foreach (sbyte expected in new sbyte[] { 0, 1, 3, 5, 7 })
        {
            axis = axis.Step(tables);
            Require(axis.Output == expected && axis.Cursor == 1, "Native cosine output, strict duration comparison or cursor timing differs from4765C0.");
        }
        NativeShakeAxis nextPhase = axis.Step(tables);
        Require(nextPhase is { Cursor: 2, Previous: 7, Target: 74, Output: 7, Counter: 1 }, "Native amplitude product WORD wrap or sign-selected target division was omitted.");
        NativeFieldShake produced = cold with { X = nextPhase, Y = axis };
        NativeFieldShake reset = produced.ResetForLoad();
        Require(reset.X is { Enabled: false, Oscillating: false, Duration: 0, Counter: 0, Cursor: 2, Output: 7, Previous: 7, Target: 74, Amplitude: 256 }, "Normal field load reset a retained cursor/output/interpolation word.");
        NativeFieldShake activated = reset with { X = reset.X with { Enabled = true, Duration = 0 } };
        activated = activated.Step(tables);
        Require(activated.X.Cursor == 3 && activated.X.Output == 7, "Warm reactivation failed to use retained cursor or preserve prior output.");
        NativeShakeAxis decay = (axis with { Enabled = false }).Step(tables);
        Require(decay is { Cursor: 2, Oscillating: false, Previous: 7, Target: 0, Output: 7, Counter: 1 }, "SHAKEOFF reset output immediately or skipped native cursor advancement on residual decay.");
        foreach (sbyte expected in new sbyte[] { 6, 4, 2, 0 })
        {
            decay = decay.Step(tables);
            Require(decay.Output == expected && decay.Cursor == 2, "Native SHAKEOFF residual decay differs.");
        }
        NativeShakeAxis negative = (cold.X with { Enabled = true, Amplitude = 256, Duration = -1 }).Step(tables).Step(tables);
        Require(negative is { Duration: -1, Counter: 1, Cursor: 2, Target: 74 }, "Native signed negative duration was refused or narrowed incorrectly.");
        NativeShakeAxis overflow = (cold.X with { Enabled = true, Amplitude = short.MinValue, Duration = short.MaxValue }).Step(tables);
        Require(overflow.Target == -128, "Native signed amplitude low-WORD multiplication changed.");
        for (int callback = 0; callback <= short.MaxValue; callback++)
        {
            overflow = overflow.Step(tables);
        }
        Require(overflow.Counter == short.MinValue && overflow.Cursor == 1, "Native counter WORD increment trapped or was widened instead of wrapping.");

        byte[] content = FieldMediaProgram((Jsm.Opcode.PSHN_L, 65792), (Jsm.Opcode.PSHN_L, 65540), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.SHAKE, 0), (Jsm.Opcode.SHAKEOFF, 0), (Jsm.Opcode.PSHN_L, 17), (Jsm.Opcode.POPM_W, 512));
        EventEngine Create()
        {
            EventEngine engine = CreateFieldMedia(content);
            engine.ShakeTables = tables;
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        Require(live.FieldShake is { X.Cursor: 1, Y.Cursor: 1, X.Target: 7, Y.Target: 7, X.Duration: 4 } && !live.Capture().GetProperty("Variables").TryGetProperty("512", out _), "SHAKE WORD operands, first output or return3 yield differs.");
        JsonElement snapshot = live.Capture();
        Require(!snapshot.GetProperty("Scene").TryGetProperty("Shake", out _) && snapshot.GetProperty("FieldShake").GetProperty("X").GetProperty("Cursor").GetByte() == 1, "VM retained a second shake authority or alternative cursor domain.");
        EventEngine independent = Create();
        independent.Restore(snapshot);
        for (int callback = 0; callback < 15; callback++)
        {
            live.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Current native shake words diverged after independent exact-frame restore.");
        }
        Require(live.FieldShake is { X.Enabled: false, Y.Enabled: false, X.Output: 0, Y.Output: 0 } && live.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 17, "SHAKEOFF lost same-callback successor or residual completion.");
        byte[] runningContent = FieldMediaProgram((Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.SHAKE, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0));
        EventEngine moving = CreateFieldMedia(runningContent);
        moving.ShakeTables = tables;
        for (int callback = 0; callback < 6; callback++)
        {
            moving.Step();
        }
        NativeFieldShake priorLoad = moving.FieldShake!;
        Require(priorLoad.X is { Cursor: 1, Output: 7 }, "Component did not execute an actual nonzero native output before field load.");
        moving.LoadField(102, FieldMediaProgram(), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(moving.FieldShake == priorLoad.ResetForLoad() && moving.FieldShake!.X.Output == 7, "Executed normal field loader overwrote process cursor or output.");
        moving.Step();
        Require(moving.FieldShake!.X.Cursor == 1 && moving.FieldShake!.X.Output == 0, "First actual inactive field callback did not clear retained output without resetting cursor.");
        JsonElement before = live.Capture();
        JsonObject malformed = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        malformed["FieldShake"]!["X"]!.AsObject().Remove("Cursor");
        try
        {
            live.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Missing required current cursor was defaulted.");
        }
        catch (JsonException)
        {
        }
        Require(JsonElement.DeepEquals(before, live.Capture()), "Malformed shake restore mutated VM or process owners.");
        GameSession warm = GameSession.CreateColdProcess();
        warm.StartNewGame();
        warm.SetFieldShake(produced);
        warm.StartNewGame();
        Require(warm.FieldShake == produced && warm.State.ReplayJournal is { IsComplete: false }, "Warm NewGame reset process shake or fabricated fresh journal provenance.");
        EventEngine absent = new(new GameSession(new GameState()), new Dictionary<int, int>()) { ShakeTables = tables };
        absent.LoadField(101, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        absent.Step();
        try
        {
            absent.Step();
            throw new InvalidOperationException("SHAKE invented a process owner for unknown origin.");
        }
        catch (EventScriptException) when (absent.LastFault?.Opcode == "SHAKE")
        {
        }
        JsonElement refused = absent.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(refused.GetProperty("Stack").GetArrayLength() == 4 && absent.FieldShake is null, "Unknown SHAKE admission consumed operands or initialized cursor.");
        NativeShakeAccessServices access = new();
        EventEngine unavailable = new(access, new Dictionary<int, int>()) { ShakeTables = tables };
        unavailable.LoadField(101, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        unavailable.Step();
        access.CanStoreFieldShake = false;
        JsonElement beforeRefusal = unavailable.Capture();
        NativeFieldShake? beforeOwner = access.FieldShake;
        try
        {
            unavailable.Step();
            throw new InvalidOperationException("Unavailable shake writer was detected after callback mutation.");
        }
        catch (NotSupportedException)
        {
        }
        Require(JsonElement.DeepEquals(beforeRefusal, unavailable.Capture()) && beforeOwner == access.FieldShake, "Unavailable shake callback mutated the VM frame or process owner before refusal.");
        GameSession scalarSession = new(GameSession.CreateColdProcess().State with { ExamTimer = NativeExamTimerState.FromReachedSet(10) });
        byte[] scalarContent = FieldMediaProgram((Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.SHAKE, 0), (Jsm.Opcode.SHAKEOFF, 0), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0));
        EventEngine scalar = new(scalarSession, scalarSession.State.Variables) { ShakeTables = tables };
        scalar.LoadField(101, scalarContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        scalar.Step();
        scalar.Step();
        Require(scalar.FieldShake is { X.Enabled: true, X.Cursor: 1 } && scalar.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Stack").GetArrayLength() == 1, "SHAKE rejected or consumed unrelated deeper timer uncertainty.");
        scalar.Step();
        JsonElement scalarFiber = scalar.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(scalar.FieldShake is { X.Enabled: false, Y.Enabled: false } && scalarFiber.GetProperty("Stack").GetArrayLength() == 0 && scalarFiber.GetProperty("Results").TryGetProperty("1", out JsonElement retained) && retained.ValueKind == JsonValueKind.Object, "Zero-operand SHAKEOFF rejected/deleted unrelated uncertainty or failed its same-budget successor.");
        GameSession ambiguousSession = new(GameSession.CreateColdProcess().State with { ExamTimer = NativeExamTimerState.FromReachedSet(10) });
        byte[] ambiguousContent = FieldMediaProgram((Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.SHAKE, 0));
        EventEngine ambiguous = new(ambiguousSession, ambiguousSession.State.Variables) { ShakeTables = tables };
        ambiguous.LoadField(101, ambiguousContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        ambiguous.Step();
        try
        {
            ambiguous.Step();
            throw new InvalidOperationException("SHAKE selected one consumed ambiguous WORD.");
        }
        catch (EventScriptException) when (ambiguous.LastFault?.Opcode == "SHAKE")
        {
        }
        Require(ambiguous.FieldShake is { X.Enabled: false, X.Cursor: 0 } && ambiguous.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Stack").GetArrayLength() == 4, "Ambiguous consumed shake operands or current owner mutated before refusal.");
        EventEngine offWithoutTables = CreateFieldMedia(FieldMediaProgram((Jsm.Opcode.SHAKEOFF, 0)));
        offWithoutTables.Step();
        Require(offWithoutTables.FieldShake is { X.Enabled: false, X.Output: 0, Y.Enabled: false, Y.Output: 0 }, "Inactive zero-operand SHAKEOFF demanded unused lookup tables.");
        Console.WriteLine("Native shake: cold BSS cursor/output, WORD widths/wrap, asymmetric phases, native load retention, warm NewGame provenance, SHAKE/OFF cadence, required current schema and independent restore passed.");
    }
    private sealed class NativeShakeAccessServices : IEventServices
    {
        public NativeFieldShake? FieldShake { get; private set; } = NativeFieldShake.FromColdProcess();
        public bool CanStoreFieldShake { get; set; } = true;
        public bool IsMoviePlaying => false;

        public void SetFieldShake(NativeFieldShake state)
        {
            if (!CanStoreFieldShake)
            {
                throw new NotSupportedException("Component current shake writer unavailable.");
            }
            FieldShake = state;
        }

        public void RequestMovie(int index)
        {
        }

        public void RequestField(int fieldId, int entryPoint)
        {
        }

        public void ShowDialog(string text)
        {
        }
    }
}
