using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDreamJunctionContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals().WithMemberFlags(1, true).WithMemberFlags(5, true);
        NativeCharacterAppearance appearance = assets.ReadNewGameCharacterAppearance();
        byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.JUNCTION, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 1000), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 7000), (Jsm.Opcode.SETHP, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.JUNCTION, 0), (Jsm.Opcode.PSHN_L, 74), (Jsm.Opcode.POPM_B, 1001), (Jsm.Opcode.RET, 8));
        (GameSession Session, EventEngine Engine) Create(byte[] content, NativeCharacterVitals? vitals = null, uint flags = 7)
        {
            GameSession session = new(new GameState { CharacterVitals = vitals ?? initial.DeepCopy(), CharacterAppearance = appearance, WorldControls = NativeWorldControlState.CreateNewGame() with { EventFlags104 = flags } });
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        (GameSession session, EventEngine engine) = Create(program);
        engine.Step();
        NativeCharacterVitals dream = session.CharacterVitals!;
        Require(dream.DreamEnabled && engine.Scene.Party.SequenceEqual([8, 9, 10]) && dream.CombatParty.SequenceEqual(new byte[] { 5, 0, 1 }), "JUNCTION1 did not assign guests in prior combat-slot order and swap only nonzero combat references.");
        Require(dream.FindPhysicalRow(8) == 0 && dream.FindPhysicalRow(9) == 1 && dream.FindPhysicalRow(10) == 5 && dream.FindPhysicalRow(1) == 255 && dream.JunctionPartyBackup.SequenceEqual(new byte[] { 0, 1, 5, 1, 0, 5 }), "JUNCTION identity lookup or six physical backup bytes differ from native.");
        Require(!session.State.Variables.ContainsKey(1000) && session.CharacterAppearance!.Records.SequenceEqual(appearance.Records), "JUNCTION native return3 failed to yield, or identity remap moved physical costume records.");
        foreach (int row in new[] { 0, 1, 5 })
        {
            ushort maximum = dream.Definitions.CharacterMaximum(dream.Records[row]);
            Require(dream.Records[row].HitPoints.Maximum <= maximum && dream.Records[row].Experience == initial.Records[row].Experience && dream.Records[row].Weapon == initial.Records[row].Weapon && dream.Records[row].MagicInventory.SequenceEqual(initial.Records[row].MagicInventory), "Guest remap copied a normal cached maximum, EXP, saved weapon or inventory.");
        }
        JsonElement pending = engine.Capture();
        (GameSession restoredSession, EventEngine restored) = Create(program);
        restored.Restore(pending);
        for (int tick = 0; tick < 6; tick++)
        {
            engine.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "JUNCTION independent restore changed next-callback behavior.");
        }
        Require(!session.CharacterVitals!.DreamEnabled && engine.Scene.Party.SequenceEqual([0, 1, 5]) && session.CharacterVitals.CombatParty.SequenceEqual(new byte[] { 1, 0, 5 }) && session.CharacterVitals.Records.Select(row => row.CharacterId).SequenceEqual(Enumerable.Range(0, 8).Select(row => (byte)row)) && session.State.Variables[1000] == 73 && session.State.Variables[1001] == 74, "JUNCTION0 did not restore original raw parties and every normal physical identity.");
        Require(session.CharacterVitals.Records[1].HitPoints == new NativeHpWord(7000, 7000), "JUNCTION0 invented a stat rebuild or reset instead of retaining the actual saved HP WORD.");
        Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.CharacterVitals), JsonSerializer.SerializeToElement(restoredSession.CharacterVitals)), "Restored guest transition lost canonical saved owners.");

        NativeCharacterVitals alternate = initial with { CombatParty = [5, 0, 1] };
        NativeJunctionResult reordered = alternate.PrepareJunction(1, [0, 5, 1], 7);
        Require(reordered.Vitals.FindPhysicalRow(9) == 5 && reordered.Vitals.FindPhysicalRow(10) == 1 && reordered.Vitals.CombatParty.SequenceEqual(new byte[] { 1, 0, 5 }), "Guest mapping was hardcoded to normal character IDs instead of current combat slots.");
        NativeJunctionResult masked = alternate.PrepareJunction(1, [0, 5, 1], 4);
        Require(masked.FieldParty.SequenceEqual(new byte[] { 8, 10, 255 }) && masked.Vitals.FindPhysicalRow(9) == 5 && masked.Vitals.CombatParty.SequenceEqual(new byte[] { 255, 0, 1 }), "Disabled guest bit altered identity assignment, compressed combat holes or removed mandatory leading guest8.");
        NativeJunctionResult unregistered = (initial with { CombatParty = [2, 0, 5] }).PrepareJunction(1, [0, 2, 5], 7);
        Require(unregistered.FieldParty.SequenceEqual(new byte[] { 8, 9, 255 }) && unregistered.Vitals.FindPhysicalRow(9) == 5 && unregistered.Vitals.Records[2].CharacterId == 2, "JUNCTION included a physical row without membership bit8 or advanced guest identity for it.");

        byte[] guestCommands = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 265), (Jsm.Opcode.PSHN_L, 257), (Jsm.Opcode.SETDRESS, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.SUBPARTY, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.ISMEMBER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 1002), (Jsm.Opcode.RET, 8));
        (GameSession guestSession, EventEngine guestEngine) = Create(guestCommands, dream);
        guestEngine.Scene.Party.Clear();
        guestEngine.Scene.Party.AddRange([8, 9, 10]);
        for (int tick = 0; tick < 3; tick++)
        {
            guestEngine.Step();
        }
        Require(guestSession.CharacterAppearance!.Records[1].Dress == 1 && guestSession.CharacterAppearance.Records[0].Dress == 0 && guestEngine.Scene.Party.SequenceEqual([8, 255, 10]) && guestSession.CharacterVitals!.CombatParty.SequenceEqual(new byte[] { 255, 0, 5 }) && guestSession.State.Variables[1002] == 2, "Guest SETDRESS, SUB/ADDPARTY, sparse SETPARTY or ISMEMBER did not consume canonical physical identity mapping.");

        NativeCharacterVitals bank = dream.WriteJunctionPartyBackup(189, 4, unchecked((int)0xFEDCBA98));
        Require(bank.ReadJunctionPartyBackup(189, 4) == unchecked((int)0xFEDCBA98) && dream.JunctionPartyBackup[1] == 1 && bank.JunctionPartyBackup[0] == 0 && bank.JunctionPartyBackup[5] == 5, "Unaligned backup DWORD write changed surrounding bytes or original owner.");
        const int pushedBankValue = -1234567;
        byte[] bankProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, pushedBankValue), (Jsm.Opcode.POPM_L, 189), (Jsm.Opcode.PSHM_L, 189), (Jsm.Opcode.POPM_L, 1004), (Jsm.Opcode.RET, 8));
        (GameSession bankSession, EventEngine bankEngine) = Create(bankProgram, dream);
        bankEngine.Step();
        Require(bankSession.State.Variables[1004] == pushedBankValue && !bankSession.State.Variables.ContainsKey(189) && bankSession.CharacterVitals!.ReadJunctionPartyBackup(189, 4) == pushedBankValue, "Native backup memory read/write created a duplicate VM authority or changed DWORD bits.");
        byte[] fullGuestOperand = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 257), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.RET, 8));
        (GameSession fullSession, EventEngine fullEngine) = Create(fullGuestOperand, dream, 0x200);
        fullEngine.Scene.Party.Clear();
        fullEngine.Scene.Party.AddRange([8, 9, 10]);
        fullEngine.Step();
        Require(fullSession.WorldControls!.EventFlags104 == (0x200u | 1u << 25) && fullSession.CharacterVitals!.Records.SequenceEqual(dream.Records) && fullSession.CharacterVitals.CombatParty.SequenceEqual(dream.CombatParty) && fullEngine.Scene.Party.SequenceEqual([8, 9, 10]), "Full field-party ADDPARTY failed to use full signed-DWORD guest branch/masked shift or performed an extra rebuild.");
        JsonNode missing = JsonNode.Parse(pending.GetRawText())!;
        missing["CharacterVitals"]!.AsObject().Remove("JunctionPartyBackup");
        JsonElement before = restored.Capture();
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(missing));
            throw new InvalidOperationException("Missing native JUNCTION backup inferred defaults.");
        }
        catch (JsonException)
        {
        }
        Require(JsonElement.DeepEquals(before, restored.Capture()), "Missing backup restore changed VM or saved owners.");
        JsonNode alias = JsonNode.Parse(pending.GetRawText())!;
        alias["Variables"]!["189"] = 4;
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(alias));
            throw new InvalidOperationException("Backup bank accepted a separately owned VM alias.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, restored.Capture()), "Backup alias refusal partially mutated current owners.");
        (GameSession menuSession, EventEngine menu) = Create(SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.JUNCTION, 0), (Jsm.Opcode.RET, 8)));
        NativeCharacterVitals untouched = menuSession.CharacterVitals!;
        try
        {
            menu.Step();
            throw new InvalidOperationException("JUNCTION3 invented native menu22 selection.");
        }
        catch (EventScriptException) when (menu.LastFault?.Opcode == "JUNCTION")
        {
        }
        Require(menuSession.CharacterVitals == untouched && menu.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2 && menu.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 3, "Unsupported menu22 consumed JUNCTION operand or partially remapped identities.");
    }
}
