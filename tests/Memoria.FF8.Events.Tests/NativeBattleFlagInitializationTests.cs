using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBattleFlagInitializationContract()
    {
        foreach (int timerMode in new[] { 0, 1, 2 })
        {
            List<(Jsm.Opcode Opcode, int Parameter)> callback = [(Jsm.Opcode.LBL, 1)];
            if (timerMode > 0)
            {
                callback.AddRange([(Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 250), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.DISPTIMER, 0)]);
            }
            if (timerMode == 2)
            {
                callback.Add((Jsm.Opcode.KILLTIMER, 0));
            }
            callback.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            byte[] initial = BattleModeCallbackProgram(callback.ToArray());
            byte[] destination = BattleModeCallbackProgram((Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8));
            string destinationHash = Convert.ToHexString(SHA256.HashData(destination));
            bool pending = false;
            EventEngine Create(GameSession owner, int field, byte[] content)
            {
                EventEngine engine = new(owner) { HasPendingRandomBattle = () => pending };
                engine.LoadField(field, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            GameSession session = CreateNativeMusicSession();
            EventEngine live = Create(session, 74, initial);
            Require(live.Scene.NativeBattleFlagWrites is { Count: 1 } && live.Scene.NativeBattleFlagWrites[0] is { Writer: NativeBattleFlagWriter.NewGame, Frame: 0, PersistentFlags: 0, ActiveFlags: 0 }, "Genuine cold field entry did not initialize both battle flag words before any BATTLEMODE.");
            JsonElement cold = live.Capture();
            GameSession coldSession = CloneMusicSession(session.State);
            EventEngine coldClone = Create(coldSession, 74, initial);
            coldClone.Restore(cold);
            Require(JsonElement.DeepEquals(cold, coldClone.Capture()), "Cold battle flag initialization did not survive independent frame-zero restore.");
            foreach (string corruption in new[] { "PersistentFlags", "ActiveFlags", "FieldId", "Frame", "ObjectId", "RawOperand", "second-reset" })
            {
                JsonNode invalid = JsonNode.Parse(cold.GetRawText())!;
                JsonArray history = invalid["Scene"]!["NativeBattleFlagWrites"]!.AsArray();
                if (corruption == "second-reset")
                {
                    history.Add(history[0]!.DeepClone());
                }
                else
                {
                    history[0]![corruption] = corruption == "FieldId" ? 999 : corruption == "ObjectId" || corruption == "RawOperand" ? 0 : 1;
                }
                string beforeInvalidRestore = coldClone.Capture().GetRawText();
                try
                {
                    coldClone.Restore(JsonSerializer.SerializeToElement(invalid));
                    throw new InvalidOperationException($"Cold flag checkpoint accepted corrupt {corruption}.");
                }
                catch (InvalidDataException)
                {
                }
                Require(coldClone.Capture().GetRawText() == beforeInvalidRestore, $"Refused corrupt {corruption} changed the live engine.");
            }
            for (int frame = 0; frame < 4; frame++)
            {
                live.Step();
            }
            ushort expected = timerMode == 1 ? (ushort)4 : (ushort)0;
            Require(live.Scene.NativeBattleFlagWrites![^1].PersistentFlags == expected && live.Scene.NativeBattleFlagWrites[^1].ActiveFlags == expected && !live.Scene.NativeBattleFlagWrites.Any(write => write.Writer == NativeBattleFlagWriter.BattleMode), "Timer flag writers required an invented BATTLEMODE before random battle entry.");
            Require(timerMode == 0 || live.Scene.NativeBattleFlagWrites.Any(write => write.Writer == NativeBattleFlagWriter.DisplayTimer), "DISPTIMER did not record its shared flag write after cold initialization.");
            Require(timerMode != 2 || live.Scene.NativeBattleFlagWrites.Any(write => write.Writer == NativeBattleFlagWriter.KillTimer), "KILLTIMER did not record its shared flag write after cold initialization.");
            JsonNode imported = JsonNode.Parse(live.Capture().GetRawText())!;
            JsonNode importedWriter = imported["Scene"]!["NativeBattleFlagWrites"]![0]!.DeepClone();
            importedWriter["Writer"] = "SavedStateMigration";
            importedWriter["PersistentFlags"] = expected;
            importedWriter["ActiveFlags"] = expected;
            imported["Scene"]!["NativeBattleFlagWrites"] = new JsonArray(importedWriter);
            coldClone.Restore(JsonSerializer.SerializeToElement(imported));
            Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(imported), coldClone.Capture()), "Explicit saved-state flag import did not survive independent restore.");
            foreach (string corruption in new[] { "second-import", "other-bits", "active-word", "bootstrap" })
            {
                JsonNode invalid = imported.DeepClone();
                if (corruption == "second-import")
                {
                    invalid["Scene"]!["NativeBattleFlagWrites"]!.AsArray().Add(importedWriter.DeepClone());
                }
                else if (corruption == "bootstrap")
                {
                    invalid["Scene"]!["MusicProcess"]!.AsObject().Remove("Bootstrap");
                }
                else
                {
                    invalid["Scene"]!["NativeBattleFlagWrites"]![0]![corruption == "other-bits" ? "PersistentFlags" : "ActiveFlags"] = expected ^ 2;
                }
                JsonElement beforeInvalid = coldClone.Capture();
                try
                {
                    coldClone.Restore(JsonSerializer.SerializeToElement(invalid));
                    throw new InvalidOperationException($"Saved-state flag import accepted corrupt {corruption}.");
                }
                catch (InvalidDataException)
                {
                }
                Require(JsonElement.DeepEquals(beforeInvalid, coldClone.Capture()), "Rejected saved-state flag import changed the current owners.");
            }
            live.LoadField(159, destination, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            Require(live.Scene.NativeBattleFlagWrites![^1] is { Writer: NativeBattleFlagWriter.FieldLoad } copied && copied.PersistentFlags == expected && copied.ActiveFlags == expected, "Field transition lost the initialized flag words without BATTLEMODE.");
            for (int frame = 0; frame < 4; frame++)
            {
                live.Step();
            }
            pending = true;
            RandomEncounterRequest request = new(SceneKind.Field, 159, live.Frame, 94, destinationHash);
            session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), request));
            Require(live.BeginNativeRandomFieldBattle(request) == expected && live.PendingHostedBattle is null, "Random entry without BATTLEMODE lost the initialized active battle flags or invented a scripted owner.");
            JsonElement suspended = live.Capture();
            GameSession independent = CloneMusicSession(session.State);
            EventEngine clone = Create(independent, 159, destination);
            clone.Restore(suspended);
            Require(JsonElement.DeepEquals(suspended, clone.Capture()) && clone.BeginNativeRandomFieldBattle(request) == expected, "Random owner or initialized flag words did not survive independent restoration.");
        }

        GameSession unknownSession = CreateNativeMusicSession();
        byte[] unknownContent = BattleModeCallbackProgram((Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8));
        bool unknownPending = false;
        EventEngine unknown = new(unknownSession) { HasPendingRandomBattle = () => unknownPending };
        unknown.LoadField(999, unknownContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        for (int frame = 0; frame < 4; frame++)
        {
            unknown.Step();
        }
        unknownPending = true;
        string before = unknown.Capture().GetRawText();
        RandomEncounterRequest unknownRequest = new(SceneKind.Field, 999, unknown.Frame, 94, Convert.ToHexString(SHA256.HashData(unknownContent)));
        try
        {
            unknown.BeginNativeRandomFieldBattle(unknownRequest);
            throw new InvalidOperationException("Unknown initial field silently invented cold battle flags.");
        }
        catch (NotSupportedException exception) when (exception.Message == "Native random field entry requires its observed active battle flags.")
        {
        }
        Require(unknown.Scene.NativeBattleFlagWrites is null && before == unknown.Capture().GetRawText(), "Refused unknown initial flags changed the field or invented initialization history.");

        GameSession unknownScriptSession = CreateNativeMusicSession();
        byte[] unknownScriptContent = BattleModeCallbackProgram((Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8));
        EventEngine unknownScript = new(unknownScriptSession) { UseInteractiveBattle = (_, _) => true };
        unknownScript.LoadField(159, unknownScriptContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        try
        {
            for (int frame = 0; frame < 4; frame++)
            {
                unknownScript.Step();
            }
            throw new InvalidOperationException("Interactive scripted battle entered without the saved return WORD.");
        }
        catch (EventScriptException exception) when (exception.Message.Contains("saved WORD182", StringComparison.Ordinal))
        {
        }
        Require(unknownScript.PendingInteractiveBattle is null && unknownScript.Scene.NativeBattleFlagWrites is null && unknownScript.Scene.NativeBattleCompletions is null, "Refused unknown scripted flags published a battle request or invented a saved return WORD.");
        JsonElement refusedFiber = unknownScript.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Stack").GetArrayLength() == 2);
        Require(refusedFiber.GetProperty("Stack")[0].GetInt32() == 4 && refusedFiber.GetProperty("Stack")[1].GetInt32() == 94, "Refused unknown scripted flags consumed its original operands.");
    }
}
