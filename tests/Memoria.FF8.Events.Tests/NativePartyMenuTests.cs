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
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine engine = new(session, session.State.Variables);
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 14), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.DISPTIMER, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.HOLD, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.HOLD, 0), (Jsm.Opcode.MENUPHS, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.RET, 8)]]);
        // Declared component: actual resource initialization and executed native commands;
        // it claims no original field allocation or journey.
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
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
        bool originalTimerDisplay = session.ExamTimer!.DisplayEnabled;
        Require(originalTimerDisplay, "Declared fixture did not execute its actual timer-display producer.");
        long fieldFrame = engine.Frame;
        long clockCalls = session.ExamTimer!.ClockCalls;
        Require(!engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted && engine.Frame == fieldFrame && session.ExamTimer.ClockCalls == clockCalls, "Party menu admitted ordinary field movement or consumed time for a rejected action.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.Entering } && !session.ExamTimer!.DisplayEnabled, "Native bootstrap did not cache and disable the timer display.");
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
        using (session.BeginReplayCapture())
        {
            engine.Restore(active);
        }
        Require(engine.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 1)).Accepted, "Explicit ordinary Zell selection was refused.");
        Require(session.CharacterVitals!.CombatParty.SequenceEqual(new byte[] { 4, 0, 255 }) && engine.Scene.Party.SequenceEqual(new[] { 0, 4, 255 }), "Temporary party selection prematurely wrote canonical saved parties.");
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && engine.PartyMenu is { Phase: NativePartyMenuPhase.Closing }, "Party confirmation did not enter the native completion/exit phase.");
        Require(session.CharacterVitals!.CombatParty.SequenceEqual(new byte[] { 4, 0, 1 }) && engine.Scene.Party.SequenceEqual(new[] { 0, 4, 1 }), "Native confirmation did not preserve forced-field slots and fill holes in selected menu order.");
        Require(engine.Frame == fieldFrame && session.ExamTimer.ClockCalls == clockCalls + 4 && !session.State.Variables.ContainsKey(500), "Explicit selection/confirmation changed field chronology instead of one native menu clock call each.");
        JsonElement closing = engine.Capture();
        session.SetEventState(closing);
        using (session.BeginReplayCapture())
        {
            engine.Restore(closing);
        }
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.FieldReentry } && engine.Frame == fieldFrame && session.ExamTimer.ClockCalls == clockCalls + 7 && session.ExamTimer.DisplayEnabled == originalTimerDisplay && session.WorldProcess == worldProcess, "Native completed menu did not exit without an extra field callback.");
        JsonElement reentry = engine.Capture();
        session.SetEventState(reentry);
        using (session.BeginReplayCapture())
        {
            engine.Restore(reentry);
        }
        ReplayJournalEntry selection = session.ReplayJournal!.Entries.Single(entry => entry.Kind == ReplayEntryKind.Action && entry.Code == (int)PlayerActionKind.SelectPartyMember && entry.Accepted == true);
        Require(session.ReplayJournal.IsComplete && selection.Value == 1 && selection.Operation == issuingOperation && selection.ObjectId == 0 && selection.ScriptId == 1 && selection.MessageId is null && selection.NativeSourceHash == engine.PartyMenu!.ProgramHash && selection.Trigger == (int)PlayerTriggerKind.PartyMenu, "Explicit menu input lost its actual issuing site/hash or became a fabricated dialogue.");
        try
        {
            engine.Step();
            throw new InvalidOperationException("Unproved native menu field reentry was silently skipped.");
        }
        catch (NotSupportedException exception)
        {
            Require(exception.Message.Contains("52B4F0(3)") && engine.Frame == fieldFrame && session.ExamTimer.ClockCalls == clockCalls + 7 && !session.State.Variables.ContainsKey(500) && JsonElement.DeepEquals(reentry, engine.Capture()), "Unsupported menu reentry consumed a field callback, changed the VM/pose or skipped its native producer.");
        }
        session.SetEventState(engine.Capture());
        using (session.BeginReplayCapture())
        {
            engine.Restore(session.State.EventState!.Value);
        }
        GameSession unknown = GameSession.CreateColdProcess();
        unknown.StartNewGame();
        unknown.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        unknown.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        unknown.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine refused = new(unknown, unknown.State.Variables);
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
    }
}
