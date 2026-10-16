using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeLastInContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        JsmProgram original = JsmProgram.Read(assets.LoadField(596).Jsm);
        Require(original.Operations[908].Opcode == Jsm.Opcode.LASTIN, "Original596 LASTIN instruction changed.");
        foreach (int argument in new[] { 0, 1, 256, -256 })
        {
            (EventEngine Engine, GameSession Session) Create(bool invalidParty = false)
            {
                GameSession session = GameSession.CreateColdProcess();
                session.StartNewGame();
                session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
                session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
                session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
                session.SetWorldControls(session.WorldControls! with { EventFlags104 = session.WorldControls!.EventFlags104 | 0x400000u });
                (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, invalidParty ? 6 : 5), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
                (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 85), (Jsm.Opcode.POPM_B, 243), (Jsm.Opcode.PSHN_L, 314159), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 271828), (Jsm.Opcode.PSHN_L, argument), (Jsm.Opcode.LASTIN, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 6000), (Jsm.Opcode.POPM_L, 6004), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 6008), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)];
                EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
                engine.LoadField(596, SyntheticObjects([1], [init, main]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return (engine, session);
            }
            (EventEngine engine, GameSession session) = Create();
            engine.Step();
            JsonElement before = engine.Capture();
            for (int callback = 0; callback < 8 && !session.EventMemory.KnownValues.ContainsKey(6000); callback++)
            {
                engine.Step();
            }
            byte expected = argument == 0 ? (byte)85 : (byte)255;
            Require((session.WorldControls!.EventFlags104 & 0x400800u) == 0x400800u && session.EventMemory.Read(243, 1).RequireExact() == expected && session.InputConfiguration!.Options == (0x00020001u | ((uint)expected << 24)), "LASTIN lost fullDWORD truth, retained BYTE243, unrelated flags or options low24 bits.");
            Require(session.CharacterVitals!.JunctionPartyBackup.SequenceEqual(new byte[] { 2, 3, 4, 2, 3, 4 }) && engine.Scene.Party.SequenceEqual([0, 1, 5]), "LASTIN did not fill only six reserve bytes from identities absent in the field party.");
            Require(session.EventMemory.Read(6000, 1).RequireExact() == 73 && session.EventMemory.Read(6004, 4).RequireExact() == 271828 && session.EventMemory.Read(6008, 4).RequireExact() == 314159, "LASTIN consumed another stack slot, changed R0 or yielded before the next instruction.");
            Require(NativeBattleLimitRules.ReadFieldBattleRules(session.EventMemory, session.WorldControls) == expected, "Battle restriction consumer ignored canonical control flags.");
            (EventEngine restored, _) = Create();
            restored.Restore(before);
            while (restored.Frame < engine.Frame)
            {
                restored.Step();
            }
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "LASTIN restore before execution changed its result.");
            JsonElement after = engine.Capture();
            (EventEngine resumed, _) = Create();
            resumed.Restore(after);
            engine.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), resumed.Capture()), "LASTIN restore after execution changed its current owners or next callback.");
            (EventEngine rejected, GameSession rejectedSession) = Create(invalidParty: true);
            rejected.Step();
            byte[] retainedBackup = rejectedSession.CharacterVitals!.JunctionPartyBackup.ToArray();
            uint retainedFlags = rejectedSession.WorldControls!.EventFlags104;
            uint retainedOptions = rejectedSession.InputConfiguration!.Options;
            try
            {
                for (int callback = 0; callback < 8; callback++)
                {
                    rejected.Step();
                }
                throw new InvalidOperationException("LASTIN admitted reserve writes beyond its owned backup bank.");
            }
            catch (EventScriptException) when (rejected.LastFault?.Opcode == nameof(Jsm.Opcode.LASTIN))
            {
                JsonElement fiber = rejected.Capture().GetProperty("Fibers").EnumerateArray().Single(value => !value.GetProperty("Complete").GetBoolean());
                Require(fiber.GetProperty("Position").GetInt32() == rejected.LastFault.Operation && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([argument, 271828]), "Rejected LASTIN changed its PC or stack operands.");
                Require(rejectedSession.WorldControls!.EventFlags104 == retainedFlags && rejectedSession.InputConfiguration!.Options == retainedOptions && rejectedSession.CharacterVitals!.JunctionPartyBackup.SequenceEqual(retainedBackup) && rejectedSession.EventMemory.Read(243, 1).RequireExact() == 85, "Rejected LASTIN changed native flags, projection, reserve or source byte before refusal.");
            }
        }
        Console.WriteLine("LASTIN fullDWORD zero/nonzero, flags104, BYTE243, options highBYTE, reserve188..193, native return2, stack/R0 and before/after restore PASS.");
    }
}
