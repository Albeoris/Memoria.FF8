using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void RemainingCharacterCodeContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        (EventEngine Engine, GameSession Session) Create(Jsm.Opcode opcode, params int[] operands)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameCharacterAppearance(assets.ReadNewGameCharacterAppearance());
            session.InitializeNewGameSavedBattleFlags(assets.ReadNewGameSavedBattleFlags());
            session.SetInputConfiguration(assets.ReadNewGameInputConfiguration() with { Options = 0xAD020001u });
            session.SetWorldControls(session.WorldControls! with { EventFlags104 = 0x400818u });
            session.SetCharacterVitals(session.CharacterVitals!.WithSavedHitPoints(0, 4321));
            session.SetCharacterAppearance(session.CharacterAppearance!.SetDressAtRow(0, 231));
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 173), (Jsm.Opcode.POPM_B, 243), (Jsm.Opcode.PSHN_L, 314159), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 271828)];
            main.AddRange(operands.Select(value => (Jsm.Opcode.PSHN_L, value)));
            main.AddRange([(opcode, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 6000), (Jsm.Opcode.POPM_L, 6004), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 6008), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(74, SyntheticObjects([1], [init, main.ToArray()]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (engine, session);
        }
        foreach (var sample in new[] { (Jsm.Opcode.GETHP, new[] { 0 }, 4321), (Jsm.Opcode.GETDRESS, new[] { 256 }, 231), (Jsm.Opcode.ADDMAGIC, new[] { 256, 7, 1000 }, 314159), (Jsm.Opcode.SETODIN, Array.Empty<int>(), 314159), (Jsm.Opcode.LASTOUT, Array.Empty<int>(), 314159), (Jsm.Opcode.SETPARTY2, Array.Empty<int>(), 314159) })
        {
            (EventEngine engine, GameSession session) = Create(sample.Item1, sample.Item2);
            engine.Step();
            int expectedResult = sample.Item1 == Jsm.Opcode.GETHP ? session.CharacterVitals!.Records[0].HitPoints.Minimum : sample.Item3;
            JsonElement before = engine.Capture();
            for (int callback = 0; callback < 8 && !session.EventMemory.KnownValues.ContainsKey(6008); callback++)
            {
                engine.Step();
            }
            Require(session.EventMemory.Read(6000, 4).RequireExact() == expectedResult && session.EventMemory.Read(6004, 4).RequireExact() == 271828 && session.EventMemory.Read(6008, 1).RequireExact() == 73, $"{sample.Item1} lost R0, operand ownership or same-callback successor: {session.EventMemory.Read(6000, 4)} vs {expectedResult}; stack {session.EventMemory.Read(6004, 4)}; successor {session.EventMemory.Read(6008, 1)}.");
            if (sample.Item1 == Jsm.Opcode.SETODIN)
            {
                Require(session.State.SavedBattleFlags!.Value == 2 && !session.CharacterVitals!.DreamEnabled, "SETODIN failed its saved BYTE bit1 write or changed dream alias.");
            }
            if (sample.Item1 == Jsm.Opcode.LASTOUT)
            {
                Require(session.WorldControls!.EventFlags104 == 0x400018u && session.InputConfiguration!.Options == 0x00020001u && session.EventMemory.Read(243, 1).RequireExact() == 173, "LASTOUT changed retained source seals or failed independent flag/options aliases.");
            }
            if (sample.Item1 == Jsm.Opcode.SETPARTY2)
            {
                Require(session.CharacterVitals!.JunctionPartyBackup.SequenceEqual(new byte[] { 2, 3, 4, 2, 3, 4 }), "SETPARTY2 failed the ordered six-byte complement construction.");
            }
            if (sample.Item1 == Jsm.Opcode.ADDMAGIC)
            {
                Require(session.CharacterVitals!.Records[0].MagicInventory.Any(stock => stock.SpellId == 7 && stock.Count == 100), "ADDMAGIC failed native combat identity resolution or saturation.");
            }
            (EventEngine restored, GameSession restoredSession) = Create(sample.Item1, sample.Item2);
            restored.Restore(before);
            while (restored.Frame < engine.Frame)
            {
                restored.Step();
            }
            Require(JsonElement.DeepEquals(restored.Capture(), engine.Capture()), $"{sample.Item1} before-instruction restore diverged.");
            Require(restoredSession.State.SavedBattleFlags == session.State.SavedBattleFlags, $"{sample.Item1} before-instruction restore lost its saved battle flag BYTE.");
            (EventEngine resumed, GameSession resumedSession) = Create(sample.Item1, sample.Item2);
            resumed.Restore(engine.Capture());
            Require(resumedSession.State.SavedBattleFlags == session.State.SavedBattleFlags, $"{sample.Item1} after-instruction restore lost its saved battle flag BYTE.");
            engine.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(resumed.Capture(), engine.Capture()), $"{sample.Item1} after-instruction restore diverged.");
            if (sample.Item1 == Jsm.Opcode.SETODIN)
            {
                JsonElement receiving = resumed.Capture();
                JsonObject corrupt = JsonNode.Parse(receiving.GetRawText())!.AsObject();
                corrupt["SavedBattleFlags"]!["Value"] = resumedSession.State.SavedBattleFlags!.Value ^ 1;
                bool rejectedAlias = false;
                try
                {
                    resumed.Restore(JsonSerializer.SerializeToElement(corrupt));
                }
                catch (InvalidDataException)
                {
                    rejectedAlias = true;
                }
                Require(rejectedAlias && JsonElement.DeepEquals(receiving, resumed.Capture()), "A contradictory saved battle flag alias changed the receiving VM before refusal.");
            }
        }
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        NativeCharacterVital[] records = original.Records.ToArray();
        records[0] = records[0] with { MagicInventory = Enumerable.Range(1, 32).Select(id => new NativeMagicStock((byte)id, 100)).ToArray() };
        NativeCharacterVitals full = original with { Records = records, CombatParty = [0, 255, 255] };
        Require(full.AddEventMagic(0, 33, int.MaxValue).Records[0].MagicInventory.SequenceEqual(full.Records[0].MagicInventory), "ADDMAGIC overwrote full inventory.");
        Require(full.AddEventMagic(0, 1, int.MaxValue).Records[0].MagicInventory[0].Count == 100 && ReferenceEquals(full.AddEventMagic(255, 1, 1), full) && ReferenceEquals(full.AddEventMagic(0, 0, int.MaxValue), full) && ReferenceEquals(full.AddEventMagic(0, 1, -1), full), "ADDMAGIC cap, absent identity, spell0 or signed count differs.");
        NativeCharacterVital[] swapped = original.Records.ToArray();
        swapped[1] = swapped[1] with { CharacterId = 0, MagicInventory = Enumerable.Repeat(new NativeMagicStock(0, 9), 32).ToArray() };
        NativeCharacterVitals redirected = original with { Records = swapped, CombatParty = [1, 0, 255] };
        NativeCharacterVitals redirectedResult = redirected.AddEventMagic(256, 7, 2);
        Require(redirectedResult.Records[1].MagicInventory[0] == new NativeMagicStock(7, 11) && redirectedResult.Records[0].MagicInventory.SequenceEqual(original.Records[0].MagicInventory), "ADDMAGIC lost first combat-slot identity resolution or incremented an empty retained count incorrectly.");
        NativeCharacterVitals truncatedZero = redirected.AddEventMagic(0, 256, int.MaxValue);
        Require(truncatedZero.Records[1].MagicInventory[0] == new NativeMagicStock(0, 8) && truncatedZero.Records[1].MagicInventory[1].Count == 9, "ADDMAGIC full-DWORD nonzero/lowBYTE-zero repeated count did not wrap natively.");
        NativeCharacterVitals truncatedDuplicates = redirected.AddEventMagic(0, 257, 2);
        Require(truncatedDuplicates.Records[1].MagicInventory.Take(2).All(stock => stock == new NativeMagicStock(1, 10)), "ADDMAGIC truncated spell insertion incorrectly replaced full-DWORD comparisons.");
        (EventEngine rejected, _) = Create(Jsm.Opcode.GETHP, 256);
        try
        {
            for (int callback = 0; callback < 8; callback++)
            {
                rejected.Step();
            }
            throw new InvalidOperationException("GETHP truncated a full DWORD physical row.");
        }
        catch (EventScriptException) when (rejected.LastFault?.Opcode == nameof(Jsm.Opcode.GETHP))
        {
            JsonElement fiber = rejected.Capture().GetProperty("Fibers").EnumerateArray().Single(value => !value.GetProperty("Complete").GetBoolean());
            Require(fiber.GetProperty("Position").GetInt32() == rejected.LastFault.Operation && fiber.GetProperty("Stack").GetArrayLength() == 2, "GETHP refusal changed PC or operands.");
        }
        Console.WriteLine("Remaining character opcodes: native widths, saved owners, return2/R0, inventory cap/no-op, reserve complement, before/after restore and physical-row refusal PASS.");
    }
}
