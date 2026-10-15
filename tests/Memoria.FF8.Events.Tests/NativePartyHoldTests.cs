using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyHoldContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        NativeCharacterVitals held = initial.WithHoldFlags(0, true);
        Require(held.Records[0].MemberFlags == (ushort)(initial.Records[0].MemberFlags | 6) && held.Records[0].HitPoints == initial.Records[0].HitPoints && held.CombatParty.SequenceEqual(initial.CombatParty) && held.GuardianForces.SequenceEqual(initial.GuardianForces), "HOLD did not preserve HP/stat producers while setting exactly physical membership bits2/4.");
        NativeCharacterVitals released = held.WithHoldFlags(0, false);
        Require(released.Records[0].MemberFlags == (ushort)(held.Records[0].MemberFlags & 0xFFF9), "HOLD release changed neighboring physical membership bits.");
        Require(initial.WithHoldFlags(257, true).Records[1].MemberFlags == (ushort)(initial.Records[1].MemberFlags | 6), "HOLD fullDWORD257 skipped native guest-path lowBYTE identity lookup.");
        Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(initial), JsonSerializer.SerializeToElement(initial.WithHoldFlags(255, true))), "Absent HOLD identity changed saved state instead of the native FF no-op.");

        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(initial);
        session.InitializeNewGameCharacterAppearance(assets.ReadNewGameCharacterAppearance());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine engine = new(session);
        ScriptCommand? holdCommand = null;
        engine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.HOLD))
            {
                holdCommand = command;
            }
        };
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.HOLD, 0), (Jsm.Opcode.RET, 8)]]);
        // A declared component executes the actual unknown out-of-retained-table
        // PGETINFO producer; no field visit or original route is claimed.
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        ushort flagsBeforeHold = session.CharacterVitals!.Records[0].MemberFlags;
        for (int callback = 0; holdCommand is null && callback < 5; callback++)
        {
            engine.Step();
        }
        Require(engine.LastFault is null && holdCommand is not null && session.CharacterVitals!.Records[0].MemberFlags == (ushort)(flagsBeforeHold | 6), "Executed HOLD rejected or fabricated its unused unknown third operand.");
        Require(holdCommand?.ScalarArguments is { Count: 3 } values && values[0].RequireExact() == 0 && values[1].RequireExact() == 1 && !values[2].IsExact && values[2].ActorInfoOrigin is not null, "HOLD receipt lost the actual opaque third operand provenance.");
        Require(!engine.Capture().GetProperty("Scene").TryGetProperty("Holds", out _), "Removed HOLD metadata remains a duplicate saved owner.");

        var rejectedSession = new GameSession(new GameState { CharacterVitals = initial });
        var rejected = new EventEngine(rejectedSession);
        byte[] invalid = SyntheticObjects([0], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.HOLD, 0), (Jsm.Opcode.RET, 8)]]);
        rejected.LoadField(600, invalid, []);
        try
        {
            rejected.Step();
            throw new InvalidOperationException("Negative HOLD physical-row write was admitted.");
        }
        catch (EventScriptException exception)
        {
            Require(exception.Message.Contains("HOLD"), "HOLD refusal lost its issuing opcode context.");
        }
        JsonElement fiber = rejected.Capture().GetProperty("Fibers").EnumerateArray().Single();
        Require(fiber.GetProperty("Position").GetInt32() == 4 && fiber.GetProperty("Stack").GetArrayLength() == 3 && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(initial), JsonSerializer.SerializeToElement(rejectedSession.CharacterVitals)), "Rejected negative physical-row HOLD consumed operands or mutated its saved owner.");
    }
}
