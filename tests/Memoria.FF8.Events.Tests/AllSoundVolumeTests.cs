using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeAllSoundVolumeContract()
    {
        foreach (int rawVolume in new[] { -1, 0, 1, 2, 63, 64, 127, 128, 255, 256, int.MinValue, int.MaxValue })
        {
            foreach (bool previousVoice in new[] { false, true })
            {
                List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.INITSOUND, 0)];
                if (previousVoice)
                {
                    operations.AddRange([(Jsm.Opcode.PSHN_L, 69), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.EFFECTPLAY, 0)]);
                }
                operations.AddRange([(Jsm.Opcode.PSHN_L, 1234), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.ALLSEVOL, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
                byte[] content = SyntheticProgram(operations.ToArray());
                EventEngine Create()
                {
                    GameSession session = new(new GameState());
                    session.State.Variables[512] = rawVolume;
                    EventEngine engine = new(session, session.State.Variables) { ResolveGlobalSoundResource = MutedGlobalSample };
                    engine.LoadField(100, content, []);
                    return engine;
                }
                EventEngine first = Create();
                first.Step();
                NativeAllSoundVolume request = first.Scene.AllSoundVolume ?? throw new InvalidOperationException("ALLSEVOL failed to record its original volume request.");
                Require(first.LastFault is null && request.RawVolume == rawVolume && request.Volume == (rawVolume & 127) && request.DurationMilliseconds == 0 && request.FirstVoice == 0 && request.VoiceCount == 32 && request.BackendStatus == GlobalSoundBackendStatus.Muted, "ALLSEVOL lost its DWORD width, low-seven-bit input, immediate duration or original all32 voice range.");
                JsonElement state = first.Capture();
                Require(state.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 1234 && state.GetProperty("Fibers")[0].GetProperty("WaitReason").GetString() == "Frames" && first.Scene.SoundMusicVolume197 == 127 && first.Scene.SoundMusicVolume198 == 127, "ALLSEVOL consumed an extra operand, introduced an audio wait or overwrote music volume bytes.");
                Require(first.Scene.GlobalSoundVoices.Count == (previousVoice ? 1 : 0) && first.Scene.GlobalSoundRequests.Count == (previousVoice ? 1 : 0) && (!previousVoice || first.Scene.GlobalSoundVoices[3].Volume == 20), "ALLSEVOL invented active voices or rewrote original sound attempt history.");
                EventEngine restored = Create();
                restored.Restore(state);
                first.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(first.Capture(), restored.Capture()), "ALLSEVOL failed exact independent restore and continuation.");
                foreach ((string property, int wrong) in new[] { ("Version", 2), ("Volume", (rawVolume & 127) ^ 1), ("DurationMilliseconds", 1), ("FirstVoice", 1), ("VoiceCount", 29), ("Operation", 0), ("Frame", (int)first.Frame + 1) })
                {
                    JsonNode invalid = JsonNode.Parse(first.Capture().GetRawText())!;
                    invalid["Scene"]!["AllSoundVolume"]![property] = wrong;
                    JsonElement before = restored.Capture();
                    try
                    {
                        restored.Restore(JsonSerializer.SerializeToElement(invalid));
                        throw new InvalidOperationException("Malformed ALLSEVOL receipt passed restore.");
                    }
                    catch (InvalidDataException)
                    {
                    }
                    Require(JsonElement.DeepEquals(before, restored.Capture()), "Rejected ALLSEVOL checkpoint changed its receiver.");
                }
                byte[] next = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0));
                first.LoadField(101, next, []);
                Require(first.Scene.AllSoundVolume == request, "Field allocation erased the historical ALLSEVOL request.");
                GameSession historicalSession = new(new GameState());
                EventEngine historical = new(historicalSession, historicalSession.State.Variables);
                historical.LoadField(101, next, []);
                historical.Restore(first.Capture());
                Require(JsonElement.DeepEquals(first.Capture(), historical.Capture()), "Historical ALLSEVOL did not restore at the reset field frame.");
            }
        }
        Console.WriteLine("Native ALLSEVOL: exact one-DWORD consumption, direct seven-bit volume, voices0..31 and immediate muted request; independent restore, preserved history and atomic rejection verified.");
    }
}
