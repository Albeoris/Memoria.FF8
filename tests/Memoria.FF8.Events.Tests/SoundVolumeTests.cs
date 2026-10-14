using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSoundVolumeContract()
    {
        foreach (uint rawMask in new[] { 1u, 4u, 5u, 0x800000u, 0xFFFFFFu, 0xFF800005u })
        {
            foreach (int rawVolume in new[] { -1, 0, 2, 63, 64, 127, 128, 255, 256, int.MinValue, int.MaxValue })
            {
                byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.INITSOUND, 0), (Jsm.Opcode.PSHN_L, 69), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.EFFECTPLAY, 0), (Jsm.Opcode.PSHN_L, 1234), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.PSHM_L, 516), (Jsm.Opcode.SEVOL, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
                EventEngine Create()
                {
                    GameSession session = GameSession.CreateColdProcess();
                    session.State.Variables[512] = unchecked((int)rawMask);
                    session.State.Variables[516] = rawVolume;
                    EventEngine engine = new(session, session.State.Variables) { ResolveGlobalSoundResource = MutedGlobalSample };
                    engine.LoadField(100, content, []);
                    return engine;
                }
                EventEngine first = Create();
                first.Step();
                NativeSoundVolume request = first.Scene.SoundVolume ?? throw new InvalidOperationException("SEVOL did not record its original volume request.");
                uint mask = rawMask & 0xFFFFFF;
                int voice = System.Numerics.BitOperations.TrailingZeroCount(mask) + 1;
                Require(first.LastFault is null && request.RawMask == rawMask && request.RawVolume == rawVolume && request.Mask == mask && request.Voice == voice && request.Volume == (rawVolume & 127) && request.DurationMilliseconds == 0 && request.BackendStatus == GlobalSoundBackendStatus.Muted, "SEVOL lost native mask/volume DWORD order, low24 selection, least voice, low-seven-bit volume or immediate duration.");
                JsonElement state = first.Capture();
                Require(state.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 1234 && state.GetProperty("Fibers")[0].GetProperty("WaitReason").GetString() == "Frames" && first.Scene.SoundMusicVolume197 == 127 && first.Scene.SoundMusicVolume198 == 127, "SEVOL consumed an extra operand, introduced a sound wait or rewrote music bytes.");
                Require(first.Scene.GlobalSoundLatestAttempts.Count == 1 && first.Scene.GlobalSoundRequests.Count == 1 && first.Scene.GlobalSoundLatestAttempts[3].Volume == 20 && first.Scene.GlobalSoundLatestAttempts[3].OriginalEffectId == 69, "SEVOL invented buffers or altered original effect-ID/attempt history.");
                EventEngine restored = Create();
                restored.Restore(state);
                first.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(first.Capture(), restored.Capture()), "SEVOL failed independent exact restore and continuation.");
                foreach ((string property, int wrong) in new[] { ("Version", 2), ("Mask", (int)mask ^ 1), ("Voice", voice + 1), ("Volume", (rawVolume & 127) ^ 1), ("DurationMilliseconds", 1), ("Operation", 0), ("Frame", (int)first.Frame + 1), ("LoadEpoch", 0), ("LoadEpoch", (int)first.Scene.AudioLoadEpoch + 1) })
                {
                    JsonNode invalid = JsonNode.Parse(first.Capture().GetRawText())!;
                    invalid["Scene"]!["SoundVolume"]![property] = wrong;
                    JsonElement before = restored.Capture();
                    try
                    {
                        restored.Restore(JsonSerializer.SerializeToElement(invalid));
                        throw new InvalidOperationException("Malformed SEVOL receipt passed restore.");
                    }
                    catch (InvalidDataException)
                    {
                    }
                    Require(JsonElement.DeepEquals(before, restored.Capture()), "Rejected SEVOL receipt changed its receiver.");
                }
                byte[] next = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0));
                first.LoadField(101, next, []);
                Require(first.Scene.SoundVolume == request, "Field allocation erased the historical SEVOL request.");
                GameSession historicalSession = new(new GameState());
                EventEngine historical = new(historicalSession, historicalSession.State.Variables);
                historical.LoadField(101, next, []);
                historical.Restore(first.Capture());
                Require(JsonElement.DeepEquals(first.Capture(), historical.Capture()), "Historical SEVOL failed to restore at the reset field clock.");
                first.LoadField(100, content, []);
                historical.LoadField(100, content, []);
                historical.Restore(first.Capture());
                Require(first.Frame == 0 && first.Scene.SoundVolume == request && first.Scene.AudioLoadEpoch > request.LoadEpoch && JsonElement.DeepEquals(first.Capture(), historical.Capture()), "SEVOL lost its original load identity when revisiting the same field before its previous local frame.");
            }
        }
        foreach (uint rawMask in new[] { 0u, 0xFF000000u })
        {
            GameSession session = new(new GameState());
            session.State.Variables[512] = unchecked((int)rawMask);
            EventEngine refused = new(session, session.State.Variables);
            refused.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.PSHN_L, 127), (Jsm.Opcode.SEVOL, 0), (Jsm.Opcode.RET, 0)), []);
            try
            {
                refused.Step();
                throw new InvalidOperationException("SEVOL admitted an unknown automatic voice.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == "SEVOL")
            {
            }
            JsonElement fiber = refused.Capture().GetProperty("Fibers")[0];
            Require(refused.Scene.SoundVolume is null && fiber.GetProperty("Position").GetInt32() == 3 && fiber.GetProperty("Stack")[0].GetInt32() == 127 && unchecked((uint)fiber.GetProperty("Stack")[1].GetInt32()) == rawMask, "Refused SEVOL changed its original operands, position or receipt.");
        }
        Console.WriteLine("Native SEVOL: mask/volume DWORD order, low24 least-bit voice, direct low-seven-bit volume and immediate muted request; independent restore/history, atomic rejection and zero-mask refusal verified.");
    }
}
