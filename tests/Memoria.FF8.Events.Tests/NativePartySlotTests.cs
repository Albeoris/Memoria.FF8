using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartySlotContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        (GameSession session, EventEngine engine) = CreateFullPartyMenuComponent(assets);
        NativePartyMenuState original = engine.PartyMenu!;
        Require(original.NativeReturnResult == 0, "Native constructor invented the confirmation result before4ABF10.");
        Require(original.TemporaryCombatParty!.SequenceEqual(new byte[] { 0, 1, 5 }) && (original.LockedMask & 1) != 0 && original.TemporaryReserveParty!.Contains((byte)4), "Declared command component did not produce its full party, locked Squall and available Rinoa.");
        byte[] combatBefore = session.CharacterVitals!.CombatParty.ToArray();
        int[] fieldBefore = engine.Scene.Party.ToArray();
        long frame = engine.Frame;
        long clocks = session.ExamTimer!.ClockCalls;
        Require(engine.Apply(new PlayerAction(PlayerActionKind.SelectPartySlot, 0)).Accepted, "Locked party row could not be selected for a native same-pane operation.");
        NativePartyMenuState locked = engine.PartyMenu!;
        Require(locked.NativeReturnResult == 0, "Pending cell selection prematurely wrote the confirmation result.");
        Require(!engine.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 4)).Accepted && !engine.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 255)).Accepted, "Locked Squall was allowed to leave the party pane.");
        Require(engine.PartyMenu!.Callbacks == locked.Callbacks && engine.PartyMenu.SelectedPartySlot == 0 && engine.PartyMenu.TemporaryCombatParty!.SequenceEqual(combatBefore), "Rejected locked crossing changed the selection or consumed a callback.");
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted && engine.PartyMenu!.SelectedPartySlot is null, "Native Cancel did not clear only the pending source marker.");
        Require(engine.Apply(new PlayerAction(PlayerActionKind.SelectPartySlot, 2)).Accepted && !engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Pending selected cell was skipped by whole-party confirmation.");
        JsonElement selected = engine.Capture();
        (GameSession resumedSession, EventEngine resumed) = CreateFullPartyMenuComponent(assets);
        resumed.Restore(selected);
        Require(JsonElement.DeepEquals(selected, resumed.Capture()), "Independent selected-cell restore lost the native reserve order or origin marker.");
        Require(engine.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 4)).Accepted && resumed.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 4)).Accepted, "Explicit occupied Selphie-to-Rinoa replacement was refused.");
        Require(engine.PartyMenu!.TemporaryCombatParty!.SequenceEqual(new byte[] { 0, 1, 4 }) && engine.PartyMenu.TemporaryReserveParty!.Contains((byte)5) && engine.PartyMenu.SelectedPartySlot is null, "Native two-cell exchange duplicated, removed or reordered another member.");
        Require(session.CharacterVitals!.CombatParty.SequenceEqual(combatBefore) && engine.Scene.Party.SequenceEqual(fieldBefore), "Temporary replacement prematurely wrote either canonical saved party.");
        Require(JsonElement.DeepEquals(engine.Capture(), resumed.Capture()) && engine.Frame == frame && session.ExamTimer.ClockCalls == clocks + 4, "Selection/replacement changed field chronology or the original four active menu callbacks.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        resumed.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(JsonElement.DeepEquals(engine.Capture(), resumed.Capture()), "Pending party state diverged on the next ordinary menu wait.");
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && engine.PartyMenu!.Phase == NativePartyMenuPhase.Closing && engine.PartyMenu.NativeReturnResult == 1 && session.CharacterVitals!.CombatParty.SequenceEqual(new byte[] { 0, 1, 4 }) && engine.Scene.Party.SequenceEqual(new[] { 1, 0, 4 }), "Explicit Done failed native held-field preservation, saved combat ordering or4ABF10 result1.");

        (GameSession removalSession, EventEngine removal) = CreateFullPartyMenuComponent(assets);
        removal.Apply(new PlayerAction(PlayerActionKind.SelectPartySlot, 2));
        Require(removal.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 255)).Accepted && removal.PartyMenu!.TemporaryCombatParty![2] == 255 && removal.PartyMenu.TemporaryReserveParty!.Contains((byte)5), "Explicit empty reserve cell did not move the selected member into reserve.");
        Require(!removal.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && removalSession.CharacterVitals!.CombatParty.SequenceEqual(combatBefore), "Incomplete temporary party was confirmed or saved before Done.");
        Require(removal.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 5)).Accepted && removal.PartyMenu!.TemporaryCombatParty!.SequenceEqual(combatBefore), "Existing empty-cell input did not return the actual removed member from its reserve cell.");
        removal.Apply(new PlayerAction(PlayerActionKind.SelectPartySlot, 0));
        removal.Apply(new PlayerAction(PlayerActionKind.SelectPartySlot, 1));
        Require(removal.PartyMenu!.TemporaryCombatParty!.SequenceEqual(new byte[] { 1, 0, 5 }) && removal.PartyMenu.SelectedPartySlot is null, "Same-party-pane swap incorrectly applied cross-pane locked-member restrictions.");
        NativePartyMenuState exposed = removal.PartyMenu;
        exposed.TemporaryReserveParty![0] = 255;
        Require(removal.PartyMenu!.TemporaryReserveParty![0] != 255, "Public party-menu view exposed mutable native reserve storage.");

        JsonElement stable = removal.Capture();
        foreach (string malformed in new[] { "missing-reserve", "duplicate-member", "invalid-slot" })
        {
            JsonNode bad = JsonNode.Parse(stable.GetRawText())!;
            if (malformed == "missing-reserve")
            {
                bad["PartyMenu"]!["TemporaryReserveParty"] = null;
            }
            else if (malformed == "duplicate-member")
            {
                byte[] duplicate = Convert.FromBase64String(bad["PartyMenu"]!["TemporaryReserveParty"]!.GetValue<string>());
                duplicate[0] = 0;
                bad["PartyMenu"]!["TemporaryReserveParty"] = Convert.ToBase64String(duplicate);
            }
            else
            {
                bad["PartyMenu"]!["SelectedPartySlot"] = 3;
            }
            JsonElement owners = JsonSerializer.SerializeToElement(removalSession.State);
            try
            {
                removal.Restore(JsonSerializer.SerializeToElement(bad));
                throw new InvalidOperationException("Malformed party pane/cursor was restored.");
            }
            catch (InvalidDataException)
            {
                Require(JsonElement.DeepEquals(stable, removal.Capture()) && JsonElement.DeepEquals(owners, JsonSerializer.SerializeToElement(removalSession.State)), "Invalid party selection restore changed its receiver or canonical owners before refusal.");
            }
        }
        Console.WriteLine("Native party slots: explicit occupied replacement, reserveFF removal, cross-pane locking/same-pane exchange, deferred saved writes, exact menu clocks, independent restore/Wait and malformed owner refusal PASS.");
    }

    private static (GameSession Session, EventEngine Engine) CreateFullPartyMenuComponent(GameAssets assets)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine engine = new(session, session.State.Variables);
        //Declared component executes original producers; it does not represent a field visit or reconstructed journey.
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.HOLD, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.MENUPHS, 0), (Jsm.Opcode.RET, 8)]]);
        engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        for (int callback = 0; engine.PartyMenu is null && callback < 8; callback++)
        {
            engine.Step();
        }
        Require(engine.PartyMenu is { Phase: NativePartyMenuPhase.Requested }, "Declared native party fixture did not reach its actual MENUPHS request.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2));
        return (session, engine);
    }
}
