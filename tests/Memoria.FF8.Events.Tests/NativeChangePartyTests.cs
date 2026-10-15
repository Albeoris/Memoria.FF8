using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeChangePartyContract()
    {
        using GameAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        (EventEngine Engine, GameSession Session) Create(byte[]? restoredCombat = null, int slot = 0)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1)];
            if (restoredCombat is not null)
            {
                //Executed saved-bank writes and JUNCTION0 deliberately establish
                //independent native field/combat banks, without snapshot seeding.
                byte[] backup = [11, 1, 4, .. restoredCombat];
                for (int index = 0; index < backup.Length; index++)
                {
                    main.AddRange([(Jsm.Opcode.PSHN_L, backup[index]), (Jsm.Opcode.POPM_B, 188 + index)]);
                }
                main.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.JUNCTION, 0)]);
            }
            main.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 314159), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.PSHN_L, 0x105), (Jsm.Opcode.CHANGEPARTY, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 6000), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.CHANGEPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.CHANGEPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 6004), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(600, SyntheticObjects([1], [init, main.ToArray()]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (engine, session);
        }

        foreach ((byte[]? restoredCombat, byte[] expectedCombat) in new (byte[]?, byte[])[] { (null, [1, 5, 4]), ([0, 255, 4], [0, 5, 4]), ([0, 1, 4], [5, 1, 4]) })
        {
            (EventEngine live, GameSession session) = Create(restoredCombat);
            NativeCharacterVitals? first = null;
            NativePartyBindingMemory? binding = null;
            byte[]? backup = null;
            int observed = 0;
            live.CommandExecuted += command =>
            {
                if (command.Name != nameof(Jsm.Opcode.CHANGEPARTY))
                {
                    return;
                }
                observed++;
                Require(live.Scene.PartyBindingMemory == binding && session.CharacterVitals!.JunctionPartyBackup.SequenceEqual(backup!), "CHANGEPARTY refreshed194..196 or changed independent JUNCTION backup bytes.");
                if (observed == 1)
                {
                    Require(live.Scene.Party.SequenceEqual([5, 1, 4]) && session.CharacterVitals!.CombatParty.SequenceEqual(expectedCombat), "CHANGEPARTY lost low-BYTE identity, old-identity combat lookup, first-FF or full-slot fallback ordering.");
                    first = session.CharacterVitals!.DeepCopy();
                }
                else if (observed == 2)
                {
                    Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(first), JsonSerializer.SerializeToElement(session.CharacterVitals)), "Already-present identity dereferenced the invalid full DWORD slot or rebuilt saved rows.");
                }
                else
                {
                    byte[] removed = expectedCombat.Select(row => row == 5 ? (byte)255 : row).ToArray();
                    NativeCharacterVitals expected = (first! with { CombatParty = removed }).RebuildParty();
                    Require(live.Scene.Party.SequenceEqual([11, 1, 4]) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(session.CharacterVitals)), "Unknown new identity did not preserve raw field BYTE11 and native FF physical-row lookup/rebuild.");
                }
            };
            bool reachedWait = false;
            for (int callback = 0; callback < 12; callback++)
            {
                live.Step();
                if (live.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("WaitReason").GetString() == "Frames"))
                {
                    reachedWait = true;
                    break;
                }
            }
            Require(reachedWait, $"CHANGEPARTY fixture failed to enter its declared frame wait: {live.Capture().GetProperty("Fibers").GetRawText()}");
            binding = live.Scene.PartyBindingMemory;
            backup = session.CharacterVitals!.JunctionPartyBackup.ToArray();
            (EventEngine independent, GameSession _) = Create(restoredCombat);
            independent.Restore(live.Capture());
            for (int callback = 0; callback < 6 && observed < 3; callback++)
            {
                live.Step();
                independent.Step();
                Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "CHANGEPARTY continuation diverged after independent restore.");
            }
            Require(observed == 3 && session.State.Variables[6000] == 73 && session.State.Variables[6004] == 314159, "Native return2 did not run its successor or CHANGEPARTY changed R0.");
        }

        (EventEngine rejected, GameSession rejectedSession) = Create(slot: 256);
        NativeCharacterVitals? before = null;
        rejected.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.WAIT))
            {
                before = rejectedSession.CharacterVitals!.DeepCopy();
            }
        };
        try
        {
            for (int callback = 0; callback < 8; callback++)
            {
                rejected.Step();
            }
            throw new InvalidOperationException("CHANGEPARTY truncated a full DWORD slot256.");
        }
        catch (EventScriptException) when (rejected.LastFault?.Opcode == nameof(Jsm.Opcode.CHANGEPARTY))
        {
            JsonElement held = rejected.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Complete").GetBoolean());
            Require(held.GetProperty("Position").GetInt32() == rejected.LastFault.Operation && held.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([0x105, 256]) && rejected.Scene.Party.SequenceEqual([0, 1, 4]) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before), JsonSerializer.SerializeToElement(rejectedSession.CharacterVitals)), "Rejected full DWORD slot mutated PC, operands, field party or saved combat owner.");
        }
        Console.WriteLine("CHANGEPARTY: full slot/low identity BYTE, no-op before invalid slot, old-identity/FF/slot combat selection, missing identity FF, unchanged bindings/backup/R0, return2 and independent restore PASS.");
    }
}
