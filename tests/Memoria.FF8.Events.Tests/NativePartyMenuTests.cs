using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyMenuContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        Require(NativeFieldLocationPose.FromPhysicalWords(-1, -4097, 65535, 255) == new NativeFieldLocationPose(-1, -2, 65535, 255), "Native470250 changed its SAR12 or WORD/BYTE preservation into division/defaults.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine engine = new(session);
        // HALT keeps each completed default component at a supported native idle boundary.
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 14), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.DISPTIMER, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.HOLD, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.HOLD, 0), (Jsm.Opcode.MENUPHS, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        // Declared component: actual resource initialization and executed native commands;
        // it claims no original field allocation or journey.
        engine.LoadField(74, content, [], previousFieldId: 123, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        for (int callback = 0; engine.PartyMenu is null && callback < 8; callback++)
        {
            engine.Step();
        }
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.Requested } && engine.NextTrigger.Kind == PlayerTriggerKind.PartyMenu, "MENUPHS did not retain its original explicit pending request.");
        Require(engine.PartyMenu!.NativeMode == 5 && engine.PartyMenu.NativeMenuId == 1 && engine.PartyMenu.NativeEntrySelection == 2 && !session.State.Variables.ContainsKey(500), "MENUPHS changed its native zero-operand request or resumed its issuing VM before player input.");
        int issuingOperation = engine.PartyMenu!.Operation;
        NativeCharacterVitals removedHeld = session.CharacterVitals!.WithMemberFlags(4, false);
        Require(removedHeld.PreparePartyMenuFieldSlots(engine.Scene.Party, new byte[] { 0, 1, 255 }).SequenceEqual(new byte[] { 0, 1, 255 }), "Ineligible row retaining hold bit4 was incorrectly preserved by the native eligible forced-field mask.");
        NativeWorldProcess worldProcess = session.WorldProcess!;
        NativeVibrationProcessState vibrationBeforeMenu = session.VibrationProcess!;
        bool originalTimerDisplay = session.ExamTimer!.DisplayEnabled;
        Require(originalTimerDisplay, "Declared fixture did not execute its actual timer-display producer.");
        long fieldFrame = engine.Frame;
        long clockCalls = session.ExamTimer!.ClockCalls;
        Require(!engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted && engine.Frame == fieldFrame && session.ExamTimer.ClockCalls == clockCalls, "Party menu admitted ordinary field movement or consumed time for a rejected action.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.Entering } && !session.ExamTimer!.DisplayEnabled, "Native bootstrap did not cache and disable the timer display.");
        Require(session.NativeFieldLocation is { FieldId: 74, PreviousFieldId: 123, NativeKind: 1 } location && location.Frame == fieldFrame && location.Operation == issuingOperation && location.Slots.All(slot => slot.RuntimeBinding is null && slot.Pose is null), "Actual470250 capture guessed an unknown physical binding/pose, confused previous field with camera or lost its issuing source.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.Active } && engine.PartyMenu.TemporaryCombatParty!.SequenceEqual(new byte[] { 4, 0, 255 }) && engine.PartyMenuCandidates.Contains((byte)1), "Native menu entry did not copy its actual combat slots and expose eligible unlocked Zell.");
        Require(engine.Frame == fieldFrame && session.ExamTimer.ClockCalls == clockCalls + 2 && session.SalaryPresentation!.Phase == 9, "Native menu entry advanced the field VM or invented a timer pause/UI reset.");
        Require(!engine.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 0)).Accepted && !engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Party menu admitted a locked member or incomplete confirmation.");
        JsonElement active = engine.Capture();
        JsonNode malformed = JsonNode.Parse(active.GetRawText())!;
        malformed["PartyMenu"]!["LockedMask"] = (byte)(engine.PartyMenu!.LockedMask | 2);
        JsonElement ownersBeforeRefusal = JsonSerializer.SerializeToElement(session.State);
        try
        {
            engine.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Malformed party-menu lock mask was admitted.");
        }
        catch (InvalidDataException)
        {
            Require(JsonElement.DeepEquals(ownersBeforeRefusal, JsonSerializer.SerializeToElement(session.State)), "Invalid menu restore changed canonical owners before refusing its masks.");
        }
        session.SetEventState(active);
        engine.Restore(active);
        Require(engine.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 1)).Accepted, "Explicit ordinary Zell selection was refused.");
        Require(session.CharacterVitals!.CombatParty.SequenceEqual(new byte[] { 4, 0, 255 }) && engine.Scene.Party.SequenceEqual(new[] { 0, 4, 255 }), "Temporary party selection prematurely wrote canonical saved parties.");
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && engine.PartyMenu is { Phase: NativePartyMenuPhase.Closing }, "Party confirmation did not enter the native completion/exit phase.");
        Require(session.CharacterVitals!.CombatParty.SequenceEqual(new byte[] { 4, 0, 1 }) && engine.Scene.Party.SequenceEqual(new[] { 0, 4, 1 }), "Native confirmation did not preserve forced-field slots and fill holes in selected menu order.");
        Require(engine.Frame == fieldFrame && session.ExamTimer.ClockCalls == clockCalls + 4 && !session.State.Variables.ContainsKey(500), "Explicit selection/confirmation changed field chronology instead of one native menu clock call each.");
        JsonElement closing = engine.Capture();
        session.SetEventState(closing);
        engine.Restore(closing);
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.FieldReentry } && engine.Frame == fieldFrame && session.ExamTimer.ClockCalls == clockCalls + 7 && session.ExamTimer.DisplayEnabled == originalTimerDisplay && session.WorldProcess == worldProcess, "Native completed menu did not exit without an extra field callback.");
        NativeVibrationProcessState expectedVibration = vibrationBeforeMenu.MarkControllerPending(0).ClearControllerRequests().Advance(2).ClearControllerRequests();
        Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expectedVibration), JsonSerializer.SerializeToElement(session.VibrationProcess)), "Menu vibration queue did not receive exactly its two active49A970 ticks and actual teardown49E8E0 request clear.");
        JsonElement reentry = engine.Capture();
        session.SetEventState(reentry);
        engine.Restore(reentry);
        Require(engine.PartyMenu!.NativeFieldEntryKind == 6, "Script-requested changed PHS incorrectly selected native player-open kind10.");
        engine.Step();
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.FieldResumeInitialization } && engine.Frame == fieldFrame && engine.Scene.FieldCallbackClock!.FieldLoaderCallbacks == 1 && !session.State.Variables.ContainsKey(500), "Confirmed PHS did not execute its clock-only kind6 bootstrap.");
        JsonElement bootstrap = engine.Capture();
        session.SetEventState(bootstrap);
        engine.Restore(bootstrap);
        engine.Step();
        Require(engine.PartyMenu is null && engine.Frame == fieldFrame + 1 && session.ExamTimer.ClockCalls == clockCalls + 11 && session.State.Variables[500] == 77 && engine.Scene.Party.SequenceEqual(new[] { 0, 4, 1 }) && session.CharacterVitals!.CombatParty.SequenceEqual(new byte[] { 4, 0, 1 }), "Confirmed script PHS replayed fresh init/default scripts instead of resuming its original advanced PC and selected parties.");
        session.SetEventState(engine.Capture());
        engine.Restore(session.State.EventState!.Value);
        GameSession unknown = GameSession.CreateColdProcess();
        unknown.StartNewGame();
        unknown.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        unknown.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        unknown.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine refused = new(unknown);
        refused.LoadField(600, SyntheticObjects([0], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.MENUPHS, 0), (Jsm.Opcode.RET, 8)]]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        JsonElement unknownVitals = JsonSerializer.SerializeToElement(unknown.CharacterVitals);
        try
        {
            refused.Step();
            throw new InvalidOperationException("MENUPHS invented its missing shot-availability owner.");
        }
        catch (EventScriptException exception)
        {
            Require(exception.Message.Contains("MENUPHS") && refused.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 1 && unknown.ShotAvailability is null && JsonElement.DeepEquals(unknownVitals, JsonSerializer.SerializeToElement(unknown.CharacterVitals)), "Unknown menu entry consumed its issuing operation or published guessed saved owners.");
        }
        NativeNormalMenuContract(assets);
        NativeRetainedPartyFlagsContract(assets);
    }

    private static void NativeRetainedPartyFlagsContract(GameAssets assets)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
        (Jsm.Opcode, int)[] Player(int label, int character) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, character), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([1, (16 << 7) | 1], [Player(0, 0), [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Player(16, 1), [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.FOLLOWON, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        Require(engine.Actors[1].NativePartyFlags is { AutoAnimation4: false, DetachedFollow64: false }, "Fresh declared actor allocation invented a geometry-dependent party-binding mask4 producer.");
        engine.Step();
        Require(engine.Actors[0].NativePartyFlags is { AutoAnimation4: true, DetachedFollow64: false } && engine.Actors[1].NativePartyFlags is { AutoAnimation4: false, DetachedFollow64: true } && engine.Scene.FieldPartyNativeActorIds[1] == 255, "Executed UCOFF/UCON/FOLLOWOFF did not set control mask4 then clear it and set follower detach mask64.");
        JsonElement detached = engine.Capture();
        JsonNode malformed = JsonNode.Parse(detached.GetRawText())!;
        malformed["Actors"]![1]!["NativePartyFlags"] = null;
        JsonElement owners = JsonSerializer.SerializeToElement(session.State);
        try
        {
            engine.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Allocated actor with missing native mask producer was admitted.");
        }
        catch (InvalidDataException)
        {
            Require(JsonElement.DeepEquals(owners, JsonSerializer.SerializeToElement(session.State)), "Missing actor-mask restore mutated canonical owners before refusal.");
        }
        session.SetEventState(detached);
        engine.Restore(detached);
        engine.Step();
        engine.Step();
        Require(engine.Scene.FieldPartyNativeActorIds[1] == 1 && engine.Actors[1].NativePartyFlags is { AutoAnimation4: false, DetachedFollow64: false }, "FOLLOWON inferred animation mask4 from its restored runtime binding; native51ECF0 only clears mask64.");
    }

    private static void NativeNormalMenuContract(GameAssets assets)
    {
        //Declared command component, not an original field or reconstructed journey.
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine engine = new(session);
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.MAPFADEOFF, 0), (Jsm.Opcode.MENUNORMAL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        Require(engine.PartyMenu is { Kind: NativeFieldMenuKind.Normal, NativeMenuId: 0, NativeReturnResult: 0 } request && request.NativeEntrySelection == session.WorldControls!.Menu209, "MENUNORMAL changed its zero-operand521D10 request or inferred its BYTE209 selection.");
        Require(session.FieldFade!.MapFadeDisabled == 1, "Declared menu fixture did not execute its native MAPFADEOFF producer.");
        long fieldFrame = engine.Frame;
        long clocks = session.ExamTimer!.ClockCalls;
        NativeCharacterVitals before = session.CharacterVitals!;
        byte[] fieldParty = engine.Scene.Party.Select(row => (byte)row).ToArray();
        engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.Normal } && engine.Frame == fieldFrame && session.CharacterVitals!.CombatParty.SequenceEqual(before.CombatParty) && session.CharacterVitals.DreamEnabled == before.DreamEnabled, "Normal menu entry reset identities/dream mode or automatically changed party.");
        Require(!engine.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 1)).Accepted && !engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Read-only normal menu silently used PHS selection/confirmation.");
        JsonElement active = engine.Capture();
        session.SetEventState(active);
        engine.Restore(active);
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted && engine.PartyMenu!.NativeReturnResult == 0, "Ordinary normal-menu cancel synthesized party-change result1.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 3));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.FieldReentry } && engine.Frame == fieldFrame && !session.State.Variables.ContainsKey(500), "Normal menu resumed the issuing PC before nativekind6.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.FieldResumeInitialization } && engine.Frame == fieldFrame && engine.Scene.FieldCallbackClock!.FieldLoaderCallbacks == 1 && !session.State.Variables.ContainsKey(500), "Kind6 bootstrap skipped its clock-only471F70 callback or advanced the VM.");
        Require(session.FieldFade!.MapFadeDisabled == 1, "Clock-only loader cleared4905 before actual4767B0 resumed.");
        JsonElement bootstrap = engine.Capture();
        session.SetEventState(bootstrap);
        engine.Restore(bootstrap);
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(engine.PartyMenu is null && engine.Frame == fieldFrame + 1 && session.State.Variables[500] == 77 && session.ExamTimer.ClockCalls == clocks + 10 && engine.Scene.Party.SequenceEqual(fieldParty.Select(row => (int)row)) && session.CharacterVitals!.CombatParty.SequenceEqual(before.CombatParty), "Verified unchanged kind6 failed its original retained VM, ten clock units or unchanged saved parties.");
        Require(session.FieldFade!.MapFadeDisabled == 0, "First resumed4767B0 retained the old4905 byte instead of executing its actual reset.");
        Require(engine.Scene.FieldCallbackClock!.OrdinaryCallbacks == fieldFrame && engine.Scene.FieldCallbackClock.InitializationTransactions == 1 && engine.Scene.FieldCallbackClock.MenuCallbacks == 6 && engine.Scene.FieldCallbackClock.FieldLoaderCallbacks == 1 && engine.Scene.FieldCallbackClock.TimerCalls == session.ExamTimer.ClockCalls, "Menu/loader/VM callback ledger conflated native process clock with actor frame history.");
        JsonElement completed = engine.Capture();
        session.SetEventState(completed);
        engine.Restore(completed);
        Require(JsonElement.DeepEquals(completed, engine.Capture()), "Completed normal menu lost its exact retained kind6 state on restore.");
    }
}
