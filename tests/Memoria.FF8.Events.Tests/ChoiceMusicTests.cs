using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeChoiceMusicContract()
    {
        foreach (int rawMask in new[] { 2, 4, 8, 4080, 4096, 516096, 3670016, 4194304, 4095, 0, -1, -8388608 })
        {
            uint mask = unchecked((uint)rawMask) & 0x03FFFFFFu;
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.PSHN_L, 43), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.PSHN_L, rawMask), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.CHOICEMUSIC, 0), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            (GameSession Session, EventEngine Engine) Create()
            {
                GameSession session = CreateNativeMusicSession();
                EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
                engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return (session, engine);
            }
            (GameSession session, EventEngine first) = Create();
            NativeGlobalSoundProcess? backend = session.State.GlobalSoundProcess;
            while (first.Scene.MusicPlayRequest is null && first.Frame < 4)
            {
                first.Step();
            }
            Require(first.Scene.MusicPlayRequest is { Opcode: "CHOICEMUSIC", ConsumedPending: true, Music: 43, Bank: 1, Channel: 0, Volume: null, Duration: null, SkipPosition: null } play && play.ChoiceMask == mask && first.Scene.MusicChannel108 is { ChannelId: 0, MusicId: 43 } primary && primary.ChoiceMask == mask && first.Scene.MusicChannel112 is { ChannelId: -1, MusicId: 255, ChoiceMask: null } && first.Scene.MusicProcess is { Pending: false, Requested213: 43, Complete214: 1, Bank201: 1 } && session.WorldControls is { EventFlags104: 0x18, MusicKeep207: 0 } && session.State.GlobalSoundProcess == backend && !session.State.Variables.ContainsKey(500), "CHOICEMUSIC lost mask width, pending/bank/channel/control ownership, invented volume/duration or failed native return3 yield.");
            JsonElement pending = first.Capture();
            JsonElement fiber = pending.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
            Require(fiber.GetProperty("Stack").GetArrayLength() == 1 && fiber.GetProperty("Stack")[0].GetInt32() == 77, "CHOICEMUSIC did not consume exactly the ignored top DWORD and lower mask operand.");
            (GameSession resumedSession, EventEngine resumed) = Create();
            resumed.Restore(pending);
            for (int tick = 0; tick < 3; tick++)
            {
                first.Step();
                resumed.Step();
                Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "CHOICEMUSIC independent pending restore diverged.");
            }
            Require(session.State.Variables[500] == 77 && resumedSession.State.Variables[500] == 77, "CHOICEMUSIC consumed unrelated operands or delayed past its callback yield.");
            JsonNode malformed = JsonNode.Parse(pending.GetRawText())!;
            malformed["Scene"]!["MusicPlayRequest"]!.AsObject().Remove("ChoiceMask");
            string before = resumed.Capture().GetRawText();
            try
            {
                resumed.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("CHOICEMUSIC accepted a receipt without its original mask.");
            }
            catch (InvalidDataException)
            {
            }
            Require(before == resumed.Capture().GetRawText(), "CHOICEMUSIC malformed restore mutated the VM.");

            byte[] noPending = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.CHOICEMUSIC, 0), (Jsm.Opcode.POPM_B, 501), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            first.LoadField(100, noPending, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            first.Step();
            first.Step();
            Require(session.State.Variables[501] == 88 && first.Scene.MusicPlayRequest is { ConsumedPending: false, ChoiceMask: 4, Channel: null, Bank: 1, Volume: null, Duration: null } && first.Scene.MusicChannel108?.ChoiceMask == mask && first.Scene.MusicChannel108.MusicId == 43 && session.State.GlobalSoundProcess == backend, "No-pending CHOICEMUSIC changed the prior selection, touched the backend, used its discarded top operand or introduced a yield.");
            resumed.LoadField(100, noPending, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            resumed.Restore(first.Capture());
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Concert selection was lost across normal field load or no-pending restore.");
            if (rawMask == 2)
            {
                JsonNode missingSelection = JsonNode.Parse(pending.GetRawText())!;
                missingSelection["Scene"]!["MusicChannel108"]!.AsObject().Remove("ChoiceMask");
                before = resumed.Capture().GetRawText();
                try
                {
                    resumed.Restore(JsonSerializer.SerializeToElement(missingSelection));
                    throw new InvalidOperationException("CHOICEMUSIC accepted a channel without its executed selection.");
                }
                catch (InvalidDataException)
                {
                }
                Require(before == resumed.Capture().GetRawText(), "Missing concert channel selection refusal mutated the VM.");
                GameSession seed = CreateNativeMusicSession();
                GameSession missing = new(seed.State with { GlobalSoundProcess = null });
                EventEngine unknown = new(missing) { AnimationFrameCount = (_, _) => 4 };
                unknown.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                try
                {
                    for (int tick = 0; tick < 4; tick++)
                    {
                        unknown.Step();
                    }
                    throw new InvalidOperationException("CHOICEMUSIC invented an absent native backend.");
                }
                catch (EventScriptException)
                {
                }
                JsonElement failed = unknown.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
                Require(unknown.LastFault is { Opcode: "CHOICEMUSIC" } fault && failed.GetProperty("Position").GetInt32() == fault.Operation && failed.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([2, 2, 77]) && unknown.Scene.MusicProcess?.Pending == true && unknown.Scene.MusicChannel108?.ChannelId == -1 && unknown.Scene.MusicPlayRequest is null && missing.State.GlobalSoundProcess is null, "CHOICEMUSIC missing-backend refusal consumed operands, changed its pending/channel owner or advanced PC.");

                byte[] ordinary = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 26), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.MUSICCHANGE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
                first.LoadField(101, ordinary, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                first.Step();
                first.Step();
                Require(first.Scene.MusicChannel108 is { MusicId: 26, ChoiceMask: null } && first.Scene.MusicPlayRequest is { Opcode: "MUSICCHANGE", ChoiceMask: null, Volume: 127, Channel: 0 } && !first.Capture().GetProperty("Scene").GetProperty("MusicChannel108").TryGetProperty("ChoiceMask", out _), "Ordinary music failed to replace a concert selection or introduced a required save field.");
                resumed.LoadField(101, ordinary, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                resumed.Restore(first.Capture());
                Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Existing ordinary music channel/receipt could not restore without optional concert fields.");
            }
        }
        Console.WriteLine("CHOICEMUSIC: two-DWORD consumption,26-bit masks, pending/bank/control ownership, no-pending return2, pending return3, field carry and independent restore passed; native bufferless backend remains unchanged.");
    }
}
