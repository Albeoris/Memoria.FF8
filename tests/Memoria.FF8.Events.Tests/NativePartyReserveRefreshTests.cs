using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyReserveRefreshContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        (EventEngine Engine, GameSession Session, byte[] Program) Create(bool auxiliary, bool incomplete = false)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
            List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPARTY, 0)];
            if (auxiliary)
            {
                init.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.LASTIN, 0)]);
            }
            init.AddRange([(Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)]);
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1)];
            byte[] backup = [5, 4, 3, 2, 1, 0];
            for (int index = 0; index < backup.Length; index++)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, backup[index]), (Jsm.Opcode.POPM_B, 188 + index)]);
            }
            if (incomplete)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0)]);
            }
            main.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 314159), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 271828), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.POPM_L, 6000), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 6004), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
            byte[] content = SyntheticObjects([1], [init.ToArray(), main.ToArray()]);
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(575, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            for (int callback = 0; callback < 8 && !(session.CharacterVitals!.JunctionPartyBackup.SequenceEqual(backup) && engine.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("WaitReason").GetString() == "Frames")); callback++)
            {
                engine.Step();
            }
            Require(session.CharacterVitals!.JunctionPartyBackup.SequenceEqual(backup), "Reserve refresh component did not execute its declared before-action bank writes.");
            return (engine, session, content);
        }

        foreach (bool auxiliary in new[] { false, true })
        {
            (EventEngine live, GameSession session, byte[] _) = Create(auxiliary);
            NativeCharacterVitals before = session.CharacterVitals!.DeepCopy();
            int[] field = live.Scene.Party.ToArray();
            (EventEngine restored, GameSession _, byte[] _) = Create(auxiliary);
            restored.Restore(live.Capture());
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "REFRESHPARTY reserve refresh diverged after independent restore.");
            byte[] expectedBackup = auxiliary ? [2, 3, 4, 2, 3, 4] : before.JunctionPartyBackup;
            NativeCharacterVitals expected = before with { JunctionPartyBackup = expectedBackup };
            Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(session.CharacterVitals)) && live.Scene.Party.SequenceEqual(field), "52D050 changed current field/combat members, rebuilt stats or lost its flag-gated ascending complement.");
            Require(session.State.Variables[6000] == 271828 && session.State.Variables[6004] == 314159, "REFRESHPARTY consumed an operand, changedR0 or yielded before its successor.");
            restored.Restore(live.Capture());
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Completed reserve producer did not restore exactly.");
        }

        (EventEngine loading, GameSession loadingSession, byte[] content) = Create(true);
        byte[] beforeLoad = loadingSession.CharacterVitals!.JunctionPartyBackup.ToArray();
        // Normal52BC00 binds actors directly and must not invoke52D050.
        loading.LoadField(556, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        loading.Step();
        Require(loadingSession.CharacterVitals!.JunctionPartyBackup.SequenceEqual(beforeLoad), "Normal field initialization incorrectly invoked the52D050 reserve producer.");

        (EventEngine refused, GameSession refusedSession, byte[] _) = Create(true, true);
        JsonElement beforeRefusal = JsonSerializer.SerializeToElement(refusedSession.CharacterVitals);
        NativePartyBindingMemory? binding = refused.Scene.PartyBindingMemory;
        try
        {
            refused.Step();
            throw new InvalidOperationException("52D050 wrote more than three absent identities beyond its owned reserve banks.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == nameof(Jsm.Opcode.REFRESHPARTY))
        {
            JsonElement fiber = refused.Capture().GetProperty("Fibers").EnumerateArray().Single(row => !row.GetProperty("Complete").GetBoolean());
            Require(fiber.GetProperty("Position").GetInt32() == refused.LastFault.Operation && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([271828]) && refused.Scene.PartyBindingMemory == binding && JsonElement.DeepEquals(beforeRefusal, JsonSerializer.SerializeToElement(refusedSession.CharacterVitals)), "Refused reserve overflow changed PC, stack, saved backup or actor binding before refusal.");
        }
        Console.WriteLine("52D050 reserve refresh: canonical104&800 branch, raw ascending complement in both banks, flag-clear preservation, no operands/R0/stat/party mutation, normal load preserves reserves, independent restore and fail-before-mutation overflow PASS.");
    }
}
