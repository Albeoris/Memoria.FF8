using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDiscSaveContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        Require(session.DiscProcess is { SavedDisc204: 1, CurrentDiscIndex: 0 }, "NewGame did not execute its event-bank initialization and process disc assignment.");
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine engine = new(session, session.State.Variables);
        //Declared native command component; no fabricated field journey or original raster claim.
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 258), (Jsm.Opcode.DISC, 0), (Jsm.Opcode.PSHSM_B, 204), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.MENUSAVE, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 501), (Jsm.Opcode.RET, 8)]]);
        engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        Require(session.DiscProcess is { SavedDisc204: 2, CurrentDiscIndex: 1 } && session.State.Variables[500] == 2 && !session.State.Variables.ContainsKey(204), "DISC changed its low-BYTE pop, process index or canonical BYTE204 projection.");
        Require(engine.PartyMenu is { Kind: NativeFieldMenuKind.Save, NativeMenuId: 24, NativeMode: 5, NativeEntrySelection: 1, NativeReturnResult: 0, Phase: NativePartyMenuPhase.Requested } && !session.State.Variables.ContainsKey(501), "MENUSAVE changed its zero-operand request or advanced its owner before interaction.");
        long frame = engine.Frame;
        long clocks = session.ExamTimer!.ClockCalls;
        engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 13));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.Entering, SavePanel.NativeState: 11, SavePanel.Fade70: 4096 } && engine.Frame == frame, "Save entry skipped its actual eleven409-unit fade callbacks.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.Active, TemporaryCombatParty: null } && engine.Frame == frame, "Save menu fabricated a party selection or advanced field chronology.");
        Require(!engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && !engine.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 1)).Accepted, "Save menu silently accepted an unsupported write or party mutation.");
        JsonElement active = engine.Capture();
        session.SetEventState(active);
        using (session.BeginReplayCapture())
        {
            engine.Restore(active);
        }
        Require(JsonElement.DeepEquals(active, engine.Capture()), "Active save menu lost its owned disc or issuing context on restore.");
        JsonNode corrupt = JsonNode.Parse(active.GetRawText())!;
        corrupt["DiscProcess"]!["CurrentDiscIndex"] = 0;
        JsonElement before = engine.Capture();
        try
        {
            engine.Restore(JsonSerializer.SerializeToElement(corrupt));
            throw new InvalidOperationException("Invalid native disc index was admitted.");
        }
        catch (InvalidDataException)
        {
            Require(JsonElement.DeepEquals(before, engine.Capture()), "Rejected disc restore mutated the live menu or owner.");
        }
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted && engine.PartyMenu is { Phase: NativePartyMenuPhase.Closing, NativeReturnResult: 0 }, "Save Cancel synthesized a successful save or party-change result.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 13));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.FieldReentry } && engine.Frame == frame && !session.State.Variables.ContainsKey(501), "Save Cancel skipped the common teardown or resumed its script early.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.FieldResumeInitialization } && engine.Frame == frame, "Save return skipped the clock-only native loader callback.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(engine.PartyMenu is null && engine.Frame == frame + 1 && session.State.Variables[501] == 77 && session.ExamTimer.ClockCalls == clocks + 32 && session.DiscProcess is { SavedDisc204: 2, CurrentDiscIndex: 1 }, "Save Cancel lost the advanced owner, native clock count or disc state.");
        GameSession signedSession = GameSession.CreateColdProcess();
        signedSession.StartNewGame();
        signedSession.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        signedSession.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        signedSession.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
        signedSession.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine signed = new(signedSession, signedSession.State.Variables);
        byte[] signedContent = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.DISC, 0), (Jsm.Opcode.PSHSM_B, 204), (Jsm.Opcode.POPM_L, 510), (Jsm.Opcode.RET, 8)]]);
        signed.LoadField(74, signedContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        signed.Step();
        signed.Step();
        Require(signedSession.DiscProcess is { SavedDisc204: 255, CurrentDiscIndex: 254 } && signedSession.State.Variables[510] == -1, "DISC/PSHSM_B lost their full BYTE domain or native sign extension.");
        JsonElement completed = engine.Capture();
        session.SetEventState(completed);
        string checkpoint = Path.Combine(Path.GetTempPath(), $"ff8-native-disc-save-{Guid.NewGuid():N}.json");
        try
        {
            session.Save(checkpoint, registerJournalReference: false);
            Require(GameSession.Load(checkpoint).DiscProcess == session.DiscProcess, "Root/VM disc ownership did not survive a real checkpoint round trip.");
        }
        finally
        {
            File.Delete(checkpoint);
        }
    }
}
