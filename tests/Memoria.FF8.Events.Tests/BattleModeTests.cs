using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBattleModeContract()
    {
        foreach (int raw in new[] { 0, -1, 0x10004, 0x10100 })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1234), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.SetEventMemory(session.State.EventMemory.Write(512, 4, NativeScalarValue.Exact(raw)));
            EventEngine first = new(session);
            first.LoadField(999, content, []);
            first.Step();
            NativeBattleFlagWrite write = first.Scene.NativeBattleFlagWrites![0];
            ushort word = unchecked((ushort)raw);
            Require(write.Writer == NativeBattleFlagWriter.BattleMode && write.RawOperand == raw && write.PersistentFlags == word && write.ActiveFlags == word && write.Operation == 3, "BATTLEMODE lost its low WORD, source operation or full replacement of both native words.");
            JsonElement snapshot = first.Capture();
            Require(snapshot.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 1234 && snapshot.GetProperty("Fibers")[0].GetProperty("WaitReason").GetString() == "Frames", "BATTLEMODE consumed a sentinel or yielded its own instruction budget.");
            Require(first.ExamTimer!.Event182Mask4 == ((word & 4) != 0) && first.ExamTimer.SaveGlobalMask4 == ((word & 4) != 0) && first.ExamTimer.Event183Mask1 == ((word & 0x100) != 0), "Reached SETTIMER ignored the authentic battle-mode WORD masks.");
            GameSession other = new(new GameState());
            EventEngine restored = new(other);
            restored.LoadField(999, content, []);
            restored.Restore(snapshot);
            first.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(first.Capture(), restored.Capture()), "Battle-mode flags or timer consumers diverged after independent restore.");
            JsonNode corrupt = JsonNode.Parse(snapshot.GetRawText())!;
            corrupt["Scene"]!["NativeBattleFlagWrites"]![0]!["PersistentFlags"] = word ^ 1;
            JsonElement before = restored.Capture();
            try
            {
                restored.Restore(JsonSerializer.SerializeToElement(corrupt));
                throw new InvalidOperationException("Corrupt battle-mode full WORD was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, restored.Capture()), "Corrupt battle-mode restore published partial state.");
        }

        byte[] battle = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0x120), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 250), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.DISPTIMER, 0), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.KILLTIMER, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        GameSession battleSession = new(new GameState { WorldControls = NativeWorldControlState.CreateNewGame(), ExamTimer = NativeExamTimerState.FromColdProcess() });
        EventEngine engine = new(battleSession);
        engine.LoadField(998, battle, []);
        engine.Step();
        NativeBattleFlagWrite current = engine.Scene.NativeBattleFlagWrites![^1];
        Require(current.PersistentFlags == 0x124 && current.ActiveFlags == 2 && engine.ExamTimer is { Event182Mask4: true, SaveGlobalMask4: false, Event183Mask1: true, DisplayEnabled: true }, "BATTLE ORed saved mode into its flags, changed persistence, or lost the independent timer UI state.");
        NativeBattleCompletion completion = engine.Scene.NativeBattleCompletions![0];
        Require(completion.FlagConsumer is { RequestedFlags: 2, PersistentModeFlags: 0x124, ReplacedActiveFlags: true, NativeEncounterFlagMergeEmulated: false } && completion.EmulatedCombatCallbacks == 0 && completion.Result is null, "Minimal battle backend discarded its flags or fabricated the native encounter merge/combat.");
        JsonElement pending = engine.Capture();
        GameSession resumedSession = new(new GameState { WorldControls = NativeWorldControlState.CreateNewGame() });
        EventEngine resumed = new(resumedSession);
        resumed.LoadField(998, battle, []);
        resumed.Restore(pending);
        JsonNode changedConsumer = JsonNode.Parse(pending.GetRawText())!;
        changedConsumer["Scene"]!["NativeBattleCompletions"]![0]!["FlagConsumer"]!["PersistentModeFlags"] = 0x125;
        try
        {
            resumed.Restore(JsonSerializer.SerializeToElement(changedConsumer));
            throw new InvalidOperationException("Battle consumer accepted independently changed persisted mode flags.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(pending, resumed.Capture()), "Changed battle consumer receipt published partial checkpoint state.");
        resumed.Step();
        Require(resumed.Scene.NativeBattleResult == 0 && resumed.Scene.NativeBattleFlagWrites![^1] is { Writer: NativeBattleFlagWriter.BattleMode, PersistentFlags: 0, ActiveFlags: 0 } && resumed.ExamTimer is { DisplayEnabled: false, Event182Mask4: false, SaveGlobalMask4: false, Event183Mask1: false }, "Battle return, KILLTIMER masking or second full mode replacement diverged.");

        byte[] loader = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0x120), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 250), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.DISPTIMER, 0), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        GameSession loaderSession = new(new GameState { WorldControls = NativeWorldControlState.CreateNewGame(), ExamTimer = NativeExamTimerState.FromColdProcess() });
        EventEngine loading = new(loaderSession);
        loading.LoadField(996, loader, []);
        loading.Step();
        loading.Step();
        loading.LoadField(997, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        Require(loading.Scene.NativeBattleFlagWrites![^1] is { Writer: NativeBattleFlagWriter.FieldLoad, PersistentFlags: 0x124, ActiveFlags: 0x124 } && loading.ExamTimer!.SaveGlobalMask4 == true, "Normal field load did not copy saved WORD182 back into active battle flags.");
        JsonElement loaded = loading.Capture();
        GameSession loadedSession = new(new GameState());
        EventEngine loadedEngine = new(loadedSession);
        loadedEngine.LoadField(997, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        loadedEngine.Restore(loaded);
        Require(JsonElement.DeepEquals(loaded, loadedEngine.Capture()), "Battle-mode field-loader write did not survive independent restore.");
        loading.LoadField(996, loader, []);
        EventEngine revisited = new(loadedSession);
        revisited.LoadField(996, loader, []);
        revisited.Restore(loading.Capture());
        Require(JsonElement.DeepEquals(loading.Capture(), revisited.Capture()), "Battle-mode history treated an earlier visit's frame as a current field operation.");

        GameSession suppressedSession = new(new GameState { WorldControls = NativeWorldControlState.CreateNewGame() with { EventFlags104 = 0x38 } });
        EventEngine suppressed = new(suppressedSession);
        suppressed.LoadField(994, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0x123), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.RET, 0)), []);
        suppressed.Step();
        Require(suppressed.Scene.NativeBattleFlagWrites is { Count: 1 } && suppressed.Scene.NativeBattleFlagWrites[0].ActiveFlags == 0x123 && suppressed.Scene.NativeBattleCompletions![0].FlagConsumer is null, "Suppressed BATTLE changed active flags or invented a battle consumer.");

        GameSession uiSession = GameSession.CreateColdProcess();
        uiSession.StartNewGame();
        EventEngine ui = new(uiSession);
        ui.LoadField(995, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 250), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.DISPTIMER, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.RET, 0)), []);
        ui.Step();
        Require(ui.ExamTimer is { DisplayEnabled: true, Event182Mask4: false, SaveGlobalMask4: false, Event183Mask1: false }, "BATTLEMODE cleared timer UI state while replacing native flag WORDs.");
        ui.ExamTimer!.Validate();

        GameSession aliasSession = new(new GameState());
        aliasSession.SetEventMemory(aliasSession.State.EventMemory.Write(182, 4, NativeScalarValue.Exact(123)));
        EventEngine alias = new(aliasSession);
        alias.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.RET, 0)), []);
        try
        {
            alias.Step();
            throw new InvalidOperationException("Battle-mode legacy WORD alias was accepted.");
        }
        catch (EventScriptException) when (alias.LastFault?.Opcode == "BATTLEMODE")
        {
        }
        Require(alias.Scene.NativeBattleFlagWrites is null && alias.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2 && alias.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 7, "Refused BATTLEMODE changed its operand, PC or mode state.");
        Console.WriteLine("Native BATTLEMODE: low-WORD dual replacement, immediate continuation, timer masks, BATTLE flag replacement, field-load copy, consumer receipt and atomic independent restore.");
    }
}
