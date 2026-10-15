using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSetHitPointsContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        NativeCharacterVital[] records = initial.Records.ToArray();
        records[3] = records[3] with { HitPoints = new NativeHpWord(0, ushort.MaxValue), Status = 0xFFFF };
        NativeCharacterVitals source = initial with { Records = records, CombatParty = [3, 0, 255] };
        foreach (int rawHp in new[] { 0, 1, 9999, 65535, -1, 65536, 65537, int.MinValue, int.MaxValue })
        {
            NativeCharacterVitals changed = source.WithSavedHitPoints(3, rawHp);
            ushort expected = unchecked((ushort)rawHp);
            Require(changed.Records[3].HitPoints == new NativeHpWord(expected, expected) && changed.Records[3].Status == 0xFFFF && changed.Records[3].Junction == source.Records[3].Junction && changed.CombatParty.SequenceEqual(source.CombatParty), "SETHP did not preserve every native HP WORD bit or altered status/junction/combat slots.");
            Require(changed.Records.Where((_, row) => row != 3).SequenceEqual(source.Records.Where((_, row) => row != 3)) && source.Records[3].HitPoints == new NativeHpWord(0, ushort.MaxValue), "SETHP changed unrelated saved records or its immutable source.");
        }
        Require(source.WithSavedHitPoints(3, 9999).Records[3].HitPoints.Minimum > source.Definitions.CharacterMaximum(source.Records[3]), "SETHP silently rebuilt or clamped HP to a cached maximum.");
        const int wrappedRow = 0x20000003;
        Require(source.WithSavedHitPoints(wrappedRow, 321).Records[3].HitPoints == new NativeHpWord(321, 321), "Native full-DWORD address arithmetic lost an actual wrapped owned HP WORD.");
        foreach (int rawRow in new[] { -1, 8, 255, 259, int.MaxValue })
        {
            try
            {
                source.WithSavedHitPoints(rawRow, 9999);
                throw new InvalidOperationException("Out-of-table SETHP was mapped through an identity or truncated row BYTE.");
            }
            catch (NotSupportedException)
            {
            }
        }
        byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 2097152), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.CAL, 2), (Jsm.Opcode.PSHN_L, -3), (Jsm.Opcode.CAL, 1), (Jsm.Opcode.PSHN_L, 9999), (Jsm.Opcode.SETHP, 0), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_B, 1100), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        (GameSession Session, EventEngine Engine) Create(NativeCharacterVitals? vitals, byte[] content)
        {
            GameSession session = new(new GameState { CharacterVitals = vitals });
            EventEngine engine = new(session);
            engine.LoadField(600, content, []);
            return (session, engine);
        }
        (GameSession session, EventEngine engine) = Create(source, program);
        int executions = 0;
        engine.CommandExecuted += command =>
        {
            if (command.Name == "SETHP")
            {
                executions++;
                Require(command.Arguments.SequenceEqual([wrappedRow, 9999]) && engine.Capture().GetProperty("Fibers")[0].GetProperty("Stack").GetArrayLength() == 0, "SETHP lost original DWORD arguments, pushed a result or failed to consume exactly two operands.");
            }
        };
        engine.Step();
        Require(executions == 1 && session.CharacterVitals!.Records[3].HitPoints == new NativeHpWord(9999, 9999) && session.CharacterVitals.Records[3].Status == 0xFFFF && session.State.Variables[1100] == 77, "SETHP did not write the selected physical row and execute its successor in the same callback without changing the result bank.");
        JsonElement checkpoint = engine.Capture();
        (GameSession copySession, EventEngine copy) = Create(initial, program);
        copy.Restore(checkpoint);
        Require(JsonElement.DeepEquals(checkpoint, copy.Capture()) && copySession.CharacterVitals!.Records[3].HitPoints == new NativeHpWord(9999, 9999), "Complete independent VM restore lost exact SETHP state.");
        engine.Step();
        copy.Step();
        Require(JsonElement.DeepEquals(engine.Capture(), copy.Capture()), "SETHP successor wait diverged after independent restore.");
        string savePath = Path.Combine(Path.GetTempPath(), $"memoria-sethp-{Guid.NewGuid():N}.json");
        try
        {
            GameState savedState = copySession.State with { Scene = SceneKind.Field, FieldId = 600, EventState = copy.Capture() };
            GameSession savedSession = new(savedState);
            savedSession.Save(savePath);
            GameState loaded = GameSession.Load(savePath);
            Require(loaded.CharacterVitals!.Records[3].HitPoints == new NativeHpWord(9999, 9999) && loaded.CharacterVitals.Records[3].Status == 0xFFFF && JsonElement.DeepEquals(loaded.EventState!.Value, savedState.EventState!.Value), "Game save/load lost exact current HP or native root/VM ownership.");
            NativeCharacterVital[] altered = savedState.CharacterVitals!.Records.ToArray();
            altered[3] = altered[3] with { HitPoints = new NativeHpWord(1, 1) };
            new GameSession(savedState with { CharacterVitals = savedState.CharacterVitals with { Records = altered } }).Save(savePath);
            try
            {
                GameSession.Load(savePath);
                throw new InvalidOperationException("Root/VM HP disagreement was accepted.");
            }
            catch (InvalidDataException exception) when (exception.Message.Contains("character vitals checkpoints disagree", StringComparison.Ordinal))
            {
            }
        }
        finally
        {
            File.Delete(savePath);
        }
        engine.LoadField(601, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        Require(session.CharacterVitals!.Records[3].HitPoints == new NativeHpWord(9999, 9999) && session.CharacterVitals.Records[3].Status == 0xFFFF, "Field load changed process-owned saved HP/status.");
        NativeCharacterVitals dying = source.WithSavedHitPoints(3, 0).RepairDyingParty();
        Require(dying.Records[3].HitPoints == new NativeHpWord(1, 1) && dying.Records[3].Status == 0xFFFE, "Later native DYING did not independently repair a SETHP zero and its death bit.");
        foreach ((int rawRow, NativeCharacterVitals? vitals) in new[] { (259, (NativeCharacterVitals?)source), (3, (NativeCharacterVitals?)null) })
        {
            byte[] refusedProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, rawRow), (Jsm.Opcode.PSHN_L, 9999), (Jsm.Opcode.SETHP, 0), (Jsm.Opcode.RET, 0));
            (GameSession refusedSession, EventEngine refused) = Create(vitals, refusedProgram);
            try
            {
                refused.Step();
                throw new InvalidOperationException("SETHP supplied missing provenance or admitted an unowned address.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement fiber = refused.Capture().GetProperty("Fibers")[0];
            Require(refused.LastFault is { Opcode: "SETHP", Operation: 3 } && fiber.GetProperty("Position").GetInt32() == 3 && fiber.GetProperty("Stack").EnumerateArray().Select(item => item.GetInt32()).SequenceEqual([9999, rawRow]) && refusedSession.CharacterVitals == vitals, "Unsupported SETHP consumed operands/PC or changed saved HP.");
        }
        RejectingSetHitPointsServices rejectingServices = new(source);
        EventEngine rejecting = new(rejectingServices, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        rejecting.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 9999), (Jsm.Opcode.SETHP, 0), (Jsm.Opcode.RET, 0)), []);
        try
        {
            rejecting.Step();
            throw new InvalidOperationException("SETHP ignored a rejected persistent HP commit.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement rejectedFiber = rejecting.Capture().GetProperty("Fibers")[0];
        Require(rejecting.LastFault is { Opcode: "SETHP", Operation: 3 } && rejectedFiber.GetProperty("Position").GetInt32() == 3 && rejectedFiber.GetProperty("Stack").EnumerateArray().Select(item => item.GetInt32()).SequenceEqual([9999, 3]) && rejectingServices.CharacterVitals == source, "Rejected SETHP publication changed PC, operands or HP owner.");
    }

    private sealed class RejectingSetHitPointsServices(NativeCharacterVitals vitals) : IEventServices
    {
        public NativeCharacterVitals? CharacterVitals => vitals;
        public bool IsMoviePlaying => false;

        public void SetCharacterVitals(NativeCharacterVitals state)
        {
            throw new NotSupportedException("Test native HP commit rejection.");
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
