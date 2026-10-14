using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeRestAndMembershipContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        NativeCharacterVital[] records = initial.Records.ToArray();
        records[0] = records[0] with { HitPoints = new NativeHpWord(0, 0), Status = 0xFFFF };
        records[2] = records[2] with { HitPoints = new NativeHpWord(1, 20), Status = 0xFFFF, MemberFlags = 0xAB09 };
        records[3] = records[3] with { HitPoints = new NativeHpWord(10, 10), Status = 0xFFFF };
        NativeCharacterVitals source = (initial with { Records = records, CombatParty = [3, 255, 0] }).AcquireGuardianForce(3);
        byte[] restProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.REST, 0), (Jsm.Opcode.PSHN_L, 71), (Jsm.Opcode.POPM_B, 1100), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        (GameSession Session, EventEngine Engine) Create(NativeCharacterVitals? vitals, byte[] program)
        {
            GameSession session = new(new GameState { CharacterVitals = vitals, WorldControls = NativeWorldControlState.CreateNewGame() });
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(600, program, []);
            return (session, engine);
        }
        (GameSession session, EventEngine engine) = Create(source, restProgram);
        engine.Step();
        NativeCharacterVitals healed = session.CharacterVitals!;
        foreach (int row in new[] { 0, 2 })
        {
            ushort maximum = source.Definitions.CharacterMaximum(source.Records[row]);
            Require(healed.Records[row].HitPoints == new NativeHpWord(maximum, maximum) && healed.Records[row].Status == 0x80 && healed.Records[row].MemberFlags == source.Records[row].MemberFlags && healed.Records[row].Junction == source.Records[row].Junction, "REST did not heal each registered physical row to its actual current maximum and preserve only native status80.");
        }
        Require(healed.Records[3].HitPoints == new NativeHpWord(10, 10) && healed.Records[3].Status == 0xFFFF && healed.CombatParty.SequenceEqual(source.CombatParty) && healed.GuardianForces[3].MaximumHitPoints is ushort gfMaximum && healed.GuardianForces[3].HitPoints == gfMaximum && session.State.Variables[1100] == 71, "REST changed unregistered HP/status, lost raw combat holes, omitted acquired unassigned GF healing or inserted a callback yield.");
        Require(source.Records[0].HitPoints.IsZero && source.GuardianForces[3].MaximumHitPoints is null, "REST mutated its immutable original saved inputs.");
        (GameSession restoredSession, EventEngine restored) = Create(initial, restProgram);
        restored.Restore(engine.Capture());
        engine.Step();
        restored.Step();
        Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()) && healed.Records.SequenceEqual(restoredSession.CharacterVitals!.Records), "REST independent current-frame/next-callback restore changed saved owners or VM.");
        JsonElement complete = engine.Capture();
        JsonNode missingExperience = JsonNode.Parse(complete.GetRawText())!;
        missingExperience["CharacterVitals"]!["Records"]![0]!.AsObject().Remove("Experience");
        try
        {
            engine.Restore(JsonSerializer.SerializeToElement(missingExperience));
            throw new InvalidOperationException("Saved stats missing actual EXP were accepted.");
        }
        catch (JsonException)
        {
        }
        Require(JsonElement.DeepEquals(complete, engine.Capture()), "Missing current EXP restore partially changed VM or native saved owners.");

        NativeCharacterVitals dream = (source with { DreamEnabled = true, CombatParty = [0, 255, 3] }).Rest();
        Require(dream.Records[0].HitPoints.Minimum == source.Definitions.CharacterMaximum(source.Records[0]) && dream.Records[2] == source.Records[2] && dream.CombatParty.SequenceEqual(new byte[] { 0, 255, 3 }), "REST dream flag did not intersect registration with current physical combat slots.");

        byte[] unknownMaxProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SUBMEMBER, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.REST, 0), (Jsm.Opcode.RET, 0));
        (GameSession unknownMaxSession, EventEngine unknownMax) = Create(initial, unknownMaxProgram);
        unknownMax.Step();
        NativeCharacterVitals noMaximum = unknownMaxSession.CharacterVitals!;
        Require((noMaximum.Records[0].MemberFlags & 1) == 0 && (noMaximum.GuardianForces[0].Flags & 1) != 0 && noMaximum.GuardianForces[0].MaximumHitPoints is null, "Executed SUBMEMBER/naming fixture invented an unproduced GF maximum.");
        try
        {
            unknownMax.Step();
            throw new InvalidOperationException("REST guessed the maximum of a newly acquired GF with no eligible character rebuild.");
        }
        catch (EventScriptException) when (unknownMax.LastFault?.Opcode == "REST")
        {
        }
        Require(unknownMaxSession.CharacterVitals == noMaximum && unknownMax.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 5, "Unsupported REST changed its owners or advanced the original instruction.");

        (GameSession unknownSession, EventEngine unknown) = Create(null, restProgram);
        try
        {
            unknown.Step();
            throw new InvalidOperationException("REST supplied missing character/GF defaults.");
        }
        catch (EventScriptException) when (unknown.LastFault?.Opcode == "REST")
        {
        }
        Require(unknownSession.CharacterVitals is null && unknown.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 1, "Unknown saved state refusal changed REST PC or its owner.");

        foreach (int raw in Enumerable.Range(0, 8).Append(0x20000006))
        {
            int row = raw & 7;
            NativeCharacterVital[] flags = initial.Records.ToArray();
            flags[row] = flags[row] with { MemberFlags = 0xABF0 };
            NativeCharacterVitals updated = (initial with { Records = flags }).WithMemberFlags(raw, true);
            int mask = raw == 0 ? 13 : raw is 4 or 6 or 7 ? 1 : 9;
            Require(updated.Records[row].MemberFlags == (0xABF0 | mask) && flags[row].MemberFlags == 0xABF0, "ADDMEMBER full-DWORD switch, wrapped physical destination or preserved unrelated flag bits differs from native.");
        }
        Require(initial.WithPartyMemberFlags(6).Records[6].MemberFlags == 9 && initial.WithMemberFlags(6, true).Records[6].MemberFlags == 1, "ADDPARTY and ADDMEMBER incorrectly share the temporary-character registration mask.");
        NativeCharacterVital[] inventory = initial.WithMemberFlags(6, true).Records.ToArray();
        NativeMagicStock[] six = inventory[6].MagicInventory.ToArray();
        six[0] = new NativeMagicStock(3, 74);
        six[31] = new NativeMagicStock(5, 10);
        inventory[6] = inventory[6] with { MagicInventory = six };
        NativeCharacterVitals copied = (initial with { Records = inventory }).WithMemberFlags(6, false);
        Require(copied.Records[5].MagicInventory.SequenceEqual(six) && copied.Records[6].MagicInventory.All(stock => stock.SpellId == 0 && stock.Count == 0) && (copied.Records[6].MemberFlags & 9) == 0 && inventory[6].MagicInventory[0].Count == 74, "SUBMEMBER6 did not reset, clear flags, copy all64 bytes into row5 and zero row6 without mutating source.");

        NativeCharacterVitals donors = initial.WithMemberFlags(1, true).WithMemberFlags(5, true).WithMemberFlags(7, true);
        NativeCharacterVital[] transfer = donors.Records.ToArray();
        NativeMagicStock[] zero = transfer[0].MagicInventory.ToArray();
        NativeMagicStock[] one = transfer[1].MagicInventory.ToArray();
        NativeMagicStock[] seven = transfer[7].MagicInventory.ToArray();
        zero[0] = new NativeMagicStock(4, 90);
        one[0] = new NativeMagicStock(4, 80);
        seven[0] = new NativeMagicStock(4, 50);
        seven[1] = new NativeMagicStock(9, 10);
        seven[2] = new NativeMagicStock(4, 250);
        seven[3] = new NativeMagicStock(7, 5);
        transfer[0] = transfer[0] with { MagicInventory = zero };
        transfer[1] = transfer[1] with { MagicInventory = one };
        transfer[7] = transfer[7] with { MagicInventory = seven };
        NativeCharacterVitals distributed = (donors with { Records = transfer }).WithMemberFlags(7, false);
        Require(distributed.Records[0].MagicInventory[0] == new NativeMagicStock(4, 100) && distributed.Records[1].MagicInventory[0] == new NativeMagicStock(4, 100) && distributed.Records[5].MagicInventory[0] == new NativeMagicStock(4, 100) && distributed.Records[0].MagicInventory[1] == new NativeMagicStock(9, 10) && distributed.Records[0].MagicInventory[2] == new NativeMagicStock(7, 5) && distributed.Records[2].MagicInventory.All(stock => stock.SpellId == 0) && distributed.Records[7].MagicInventory.All(stock => stock.SpellId == 0 && stock.Count == 0), "SUBMEMBER7 inventory order, matching-cap100, first-empty receiver, inactive exclusion, leftover loss or source clear differs from native.");

        byte[] fullParty = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.ISMEMBER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 1100), (Jsm.Opcode.RET, 0));
        (GameSession fullSession, EventEngine full) = Create(initial with { CombatParty = [1, 0, 5] }, fullParty);
        full.Scene.Party.Clear();
        full.Scene.Party.AddRange([0, 1, 5]);
        full.Step();
        Require(full.Scene.Party.SequenceEqual([0, 1, 5]) && fullSession.CharacterVitals!.CombatParty.SequenceEqual(new byte[] { 1, 0, 5 }) && fullSession.CharacterVitals.Records[6].MemberFlags == 9 && fullSession.State.Variables[1100] == 1, "Full field-party ADDPARTY skipped native registration/rebuild or invented an insertion.");

        byte[] words = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.PSHN_L, 65537), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.RET, 0));
        (GameSession wordSession, EventEngine wordParty) = Create(initial, words);
        wordParty.Step();
        Require(wordParty.Scene.Party.SequenceEqual([0, 255, 1]) && wordSession.CharacterVitals!.CombatParty.SequenceEqual(new byte[] { 255, 0, 1 }), "SETPARTY did not narrow all DWORD operands to the actual three field/combat BYTEs.");

        byte[] guests = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.ISMEMBER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 1101), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.ISMEMBER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 1102), (Jsm.Opcode.PSHN_L, -256), (Jsm.Opcode.ISMEMBER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 1103), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.SUBMEMBER, 0), (Jsm.Opcode.RET, 0));
        (GameSession guestSession, EventEngine guest) = Create(initial, guests);
        for (int frame = 0; frame < 3; frame++)
        {
            guest.Step();
        }
        ushort seventhMaximum = initial.Definitions.CharacterMaximum(initial.Records[7]);
        Require(guestSession.State.WorldControls!.EventFlags104 == 0x1D && guestSession.State.Variables[1101] == 2 && guestSession.State.Variables[1102] == 8 && guestSession.State.Variables[1103] == 1 && guestSession.CharacterVitals!.Records[7].HitPoints == new NativeHpWord(seventhMaximum, seventhMaximum) && guestSession.CharacterVitals.Records.Select(row => row.CharacterId).SequenceEqual(initial.Records.Select(row => row.CharacterId)), "Guest membership lost raw mask results, native signed/full-DWORD comparison, missing-identity reset to row7, unrelated104 bits or introduced a guest remap.");

        foreach ((byte[] packet, int position, int[] operands) in new[]
        {
            (restProgram, 1, Array.Empty<int>()),
            (SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.SUBMEMBER, 0), (Jsm.Opcode.RET, 0)), 2, new[] { 9 })
        })
        {
            RejectingSavedCharacterServices service = new(source, NativeWorldControlState.CreateNewGame().WithGuestMember(9, true));
            EventEngine refused = new(service, new Dictionary<int, int>());
            refused.LoadField(600, packet, []);
            try
            {
                refused.Step();
                throw new InvalidOperationException("Native saved owner rejection was ignored.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement fiber = refused.Capture().GetProperty("Fibers")[0];
            Require(fiber.GetProperty("Position").GetInt32() == position && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(operands) && service.CharacterVitals == source && service.WorldControls.EventFlags104 == 0x1A, "Rejected REST or composite guest SUBMEMBER consumed PC/operands or partially published saved owners.");
        }
        Console.WriteLine("REST/membership: exact current stat/GF healing, normal/dream registration masks, no eligible unknown-max refusal, same-callback and restore, native flag widths, full-party ADDPARTY and temporary-character inventory transfers passed.");
    }

    private sealed class RejectingSavedCharacterServices(NativeCharacterVitals vitals, NativeWorldControlState controls) : IEventServices
    {
        public NativeCharacterVitals? CharacterVitals => vitals;
        public NativeWorldControlState WorldControls => controls;
        public bool CanStoreNativeEventOwners => true;
        public bool IsMoviePlaying => false;

        public void SetCharacterVitals(NativeCharacterVitals state)
        {
            throw new NotSupportedException("Test native saved-character publication rejection.");
        }

        public void SetNativeEventOwners(NativeWorldControlState state, NativeSeeDState? seeD = null, NativeExamTimerState? timer = null, NativeCharacterVitals? characterVitals = null, NativeEconomy? economy = null, NativeGlobalSoundProcess? globalSoundProcess = null, NativeSalaryPresentation? salaryPresentation = null, NativeEventRandomState? eventRandom = null, NativeVibrationProcessState? vibrationProcess = null, NativeFieldFadeState? fieldFade = null)
        {
            throw new NotSupportedException("Test composite native owner publication rejection.");
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
