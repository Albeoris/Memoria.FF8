using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeGuardianForceResetContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        Require(initial.Records.All(record => record.Junction.GuardianForces == 0 && record.Junction.MagicJunctions.All(value => value == 0) && initial.Definitions.CharacterMaximum(record) > 0), "Authentic New Game did not initialize each saved character's original junction/maximum HP fields.");
        NativeCharacterVital[] records = initial.Records.ToArray();
        records[6] = records[6] with { HitPoints = new NativeHpWord(0, 0), Status = 0xFFFF, Junction = records[6].Junction with { GuardianForces = 0x8003, Commands = 0x12345678, Abilities = 0x87654321, MagicJunctions = Enumerable.Range(1, 20).Select(value => (byte)value).ToArray() } };
        NativeCharacterVitals source = initial with { Records = records, CombatParty = [0, 6, 3] };
        Require(source.Records[6].Junction.GuardianForceAssignments.SequenceEqual([0, 1, 15]), "Saved GF assignment WORD did not feed its original sixteen-bit consumer.");
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.PSHN_L, 262), (Jsm.Opcode.RESETGF, 0), (Jsm.Opcode.DYING, 0), (Jsm.Opcode.PSHN_L, 57), (Jsm.Opcode.POPM_B, 1100), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        (GameSession Session, EventEngine Engine) Create(NativeCharacterVitals? state)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.Restore(session.State with { CharacterVitals = state, WorldControls = NativeWorldControlState.CreateNewGame(), InputConfiguration = assets.ReadNewGameInputConfiguration() });
            EventEngine engine = new(session);
            engine.LoadField(323, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return (session, engine);
        }
        (GameSession session, EventEngine engine) = Create(source);
        engine.Step();
        Require(engine.AcquiredGuardianForces!.SetEquals([0]), "Native naming did not establish the separate positive GF acquisition before reset.");
        int executions = 0;
        engine.CommandExecuted += command =>
        {
            if (command.Name == "RESETGF")
            {
                executions++;
                Require(command.Arguments.SequenceEqual([262]) && engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Stack").GetArrayLength() == 0, "RESETGF changed its full original command argument, added a result or failed to pop its operand.");
            }
        };
        engine.Apply(new PlayerAction(PlayerActionKind.EnterName));
        engine.Step();
        NativeCharacterVitals reset = session.CharacterVitals!;
        NativeCharacterVital selected = reset.Records[6];
        ushort maximum = source.Definitions.CharacterMaximum(source.Records[6] with { Junction = source.Records[6].Junction.Reset() });
        Require(executions == 1 && selected.HitPoints == new NativeHpWord(maximum, maximum) && selected.Status == 0x0080 && selected.Junction.GuardianForces == 0 && selected.Junction.Commands == 0 && selected.Junction.Abilities == 0 && selected.Junction.MagicJunctions.All(value => value == 0) && !selected.Junction.GuardianForceAssignments.Any(), "RESETGF did not clear GF/command/passive/magic junctions, heal to the native unjunctioned maximum and retain only status bit0080.");
        Require(reset.CombatParty.SequenceEqual(source.CombatParty) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(reset.Records[7]), JsonSerializer.SerializeToElement(source.Records[7])) && source.Records[6].Junction.GuardianForces == 0x8003 && source.Records[6].Junction.MagicJunctions[0] == 1 && engine.AcquiredGuardianForces!.SetEquals([0]) && session.State.Variables[1100] == 57, "Reset lost original combat slots/acquisition, mutated its source or failed to continue through DYING and the successor in the same VM callback.");
        JsonElement checkpoint = engine.Capture();
        (GameSession copySession, EventEngine copy) = Create(initial);
        copy.Restore(checkpoint);
        Require(JsonElement.DeepEquals(checkpoint, copy.Capture()) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(reset), JsonSerializer.SerializeToElement(copySession.CharacterVitals)), "Independent restore lost authentic junction/HP state.");
        string savePath = Path.Combine(Path.GetTempPath(), $"memoria-reset-gf-{Guid.NewGuid():N}.json");
        try
        {
            GameSession savedSession = new(session.State with { Scene = SceneKind.Field, FieldId = 323, EventState = checkpoint });
            savedSession.Save(savePath);
            GameState loaded = GameSession.Load(savePath);
            Require(loaded.CharacterVitals!.Records.SequenceEqual(reset.Records) && JsonElement.DeepEquals(loaded.EventState!.Value, checkpoint), "Independent game save/load changed junction byte content or matching root/event identity.");
            NativeCharacterVital[] changedRecords = reset.Records.ToArray();
            changedRecords[6] = changedRecords[6] with { Junction = changedRecords[6].Junction with { GuardianForces = 1 } };
            GameSession mismatchedSession = new(savedSession.State with { CharacterVitals = reset with { Records = changedRecords } });
            mismatchedSession.Save(savePath);
            try
            {
                GameSession.Load(savePath);
                throw new InvalidOperationException("Mismatched game/event GF assignments were accepted.");
            }
            catch (InvalidDataException exception) when (exception.Message.Contains("character vitals checkpoints disagree", StringComparison.Ordinal))
            {
            }
        }
        finally
        {
            File.Delete(savePath);
        }
        engine.Step();
        copy.Step();
        Require(JsonElement.DeepEquals(engine.Capture(), copy.Capture()), "Ordinary successor wait diverged after RESETGF restore.");
        engine.LoadField(324, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        Require(engine.AcquiredGuardianForces!.SetEquals([0]) && session.CharacterVitals!.Records[6].Junction.GuardianForces == 0 && session.CharacterVitals.Records[6].HitPoints == new NativeHpWord(maximum, maximum), "Field loading lost persistent GF acquisition or reset junction/HP state.");

        foreach (int raw in new[] { -1, 8, 255, 264 })
        {
            NativeCharacterVitals unmapped = source.ResetGuardianForces(raw);
            Require(unmapped.Records[7].HitPoints == new NativeHpWord(source.Definitions.CharacterMaximum(source.Records[7] with { Junction = source.Records[7].Junction.Reset() }), source.Definitions.CharacterMaximum(source.Records[7] with { Junction = source.Records[7].Junction.Reset() })) && unmapped.Records[6].Junction.GuardianForces == 0x8003 && unmapped.CombatParty.SequenceEqual(source.CombatParty), "Native unmapped low-BYTE identity did not returnFF, zero-extend and clamp to record7.");
        }
        NativeCharacterVitals lowByte = source.ResetGuardianForces(256 + 6);
        Require(lowByte.Records[6].Junction.GuardianForces == 0 && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(lowByte.Records[7]), JsonSerializer.SerializeToElement(source.Records[7])), "RESETGF did not use the native low-BYTE identity lookup.");

        byte[] resetOnly = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.RESETGF, 0), (Jsm.Opcode.RET, 0));
        GameSession missingSession = new(new GameState());
        EventEngine missing = new(missingSession);
        missing.LoadField(323, resetOnly, []);
        try
        {
            missing.Step();
            throw new InvalidOperationException("RESETGF invented missing authentic junction state.");
        }
        catch (EventScriptException)
        {
        }
        Require(missing.LastFault is { Opcode: "RESETGF", Operation: 2 } && missing.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2 && missing.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 6 && missingSession.CharacterVitals is null, "Missing RESETGF provenance consumed the original operand or advanced PC.");

        RejectingGuardianForceServices rejectingServices = new(source);
        EventEngine rejecting = new(rejectingServices, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        rejecting.LoadField(323, resetOnly, []);
        try
        {
            rejecting.Step();
            throw new InvalidOperationException("RESETGF ignored a rejected state commit.");
        }
        catch (EventScriptException)
        {
        }
        Require(rejecting.LastFault is { Opcode: "RESETGF", Operation: 2 } && rejecting.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2 && rejecting.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 6 && rejectingServices.CharacterVitals == source, "Rejected RESETGF commit consumed PC/operand or mutated the source state.");

        JsonObject malformed = JsonNode.Parse(checkpoint.GetRawText())!.AsObject();
        malformed["CharacterVitals"]!["Records"]![6]!.AsObject().Remove("Junction");
        try
        {
            copy.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Checkpoint without authentic junction fields was defaulted.");
        }
        catch (JsonException)
        {
        }
    }

    private sealed class RejectingGuardianForceServices(NativeCharacterVitals vitals) : IEventServices
    {
        public NativeCharacterVitals? CharacterVitals => vitals;
        public bool IsMoviePlaying => false;

        public void SetCharacterVitals(NativeCharacterVitals state)
        {
            throw new NotSupportedException("Test native junction commit rejection.");
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
