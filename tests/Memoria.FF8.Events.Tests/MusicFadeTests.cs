using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMusicFadeContract()
    {
        foreach (int duration in new[] { -1, 0, 4, 5, 24, 25, 60, 127, 128, 255, 256, int.MinValue, int.MaxValue })
        {
            foreach (bool knownChannel in new[] { false, true })
            {
                List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.INITSOUND, 0)];
                if (knownChannel)
                {
                    operations.AddRange([(Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.MUSICSTOP, 0)]);
                }
                operations.AddRange([(Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHM_L, 640), (Jsm.Opcode.PSHM_L, 644), (Jsm.Opcode.PSHM_L, 648), (Jsm.Opcode.MUSICVOLFADE, 0), (Jsm.Opcode.PSHM_B, 198), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHM_L, 640), (Jsm.Opcode.PSHM_L, 652), (Jsm.Opcode.ALLSEVOLTRANS, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.POPI_L, 4), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
                byte[] content = SyntheticProgram(operations.ToArray());
                EventEngine Create()
                {
                    GameSession session = new(new GameState());
                    session.State.Variables[640] = duration;
                    session.State.Variables[644] = int.MinValue;
                    session.State.Variables[648] = 511;
                    session.State.Variables[652] = int.MaxValue;
                    EventEngine engine = new(session, session.State.Variables);
                    engine.LoadField(100, content, []);
                    return engine;
                }
                EventEngine first = Create();
                first.Step();
                NativeMusicVolume fade = first.Scene.MusicVolumeRequest!;
                int durationByte = unchecked((byte)(2 * duration));
                Require(first.LastFault is null && fade.RawSlot == 3 && fade.Slot == 1 && fade.TransitionDurationOperand == duration && fade.FadeInitialVolumeOperand == int.MinValue && fade.RequestedVolume == 511 && fade.StoredVolume == 255 && fade.RequestedDuration == durationByte && fade.EffectiveDuration == (durationByte < 50 ? 0 : durationByte) && fade.ChannelId == (knownChannel ? -1 : null) && fade.BothChannels == (knownChannel ? true : null) && fade.BackendStatus == GlobalSoundBackendStatus.Muted, "MUSICVOLFADE lost full initial/target DWORDs, masked slot, immediate final BYTE or independent backend routing.");
                Require(first.Scene.SoundMusicVolume197 == 127 && first.Scene.SoundMusicVolume198 == 255 && first.Capture().GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 255, "MUSICVOLFADE failed immediate B198 readback or changed the other slot.");
                NativeAllSoundVolumeTransition all = first.Scene.AllSoundVolumeTransition!;
                int milliseconds = 10 * unchecked((byte)duration);
                Require(all.RawDuration == duration && all.RawVolume == int.MaxValue && all.DurationByte == unchecked((byte)duration) && all.DurationMilliseconds == (milliseconds < 50 ? 0 : milliseconds) && all.DoubledVolume == -2 && all.Volume == 126 && all.FirstVoice == 1 && all.VoiceCount == 29 && all.BackendStatus == GlobalSoundBackendStatus.Muted && first.Scene.GlobalSoundVoices.Count == 0 && first.Capture().GetProperty("Fibers")[0].GetProperty("Results").GetProperty("4").GetInt32() == 123, "ALLSEVOLTRANS swapped operands, reused doubled music time, invented voices or yielded instead of returning2.");
                EventEngine restored = Create();
                restored.Restore(first.Capture());
                first.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(first.Capture(), restored.Capture()), "Music fade/all-voice request failed independent exact restore.");
                foreach ((string receipt, string property, int wrong) in new[] { ("MusicVolumeRequest", "EffectiveDuration", fade.EffectiveDuration + 1), ("MusicVolumeRequest", "Operation", 0), ("AllSoundVolumeTransition", "FirstVoice", 0), ("AllSoundVolumeTransition", "VoiceCount", 30), ("AllSoundVolumeTransition", "DurationMilliseconds", all.DurationMilliseconds + 1), ("AllSoundVolumeTransition", "Operation", 0) })
                {
                    JsonNode invalid = JsonNode.Parse(first.Capture().GetRawText())!;
                    invalid["Scene"]![receipt]![property] = wrong;
                    JsonElement before = restored.Capture();
                    try
                    {
                        restored.Restore(JsonSerializer.SerializeToElement(invalid));
                        throw new InvalidOperationException("Malformed fade/all-voice receipt passed restore.");
                    }
                    catch (InvalidDataException)
                    {
                    }
                    Require(JsonElement.DeepEquals(before, restored.Capture()), "Rejected fade/all-voice checkpoint changed the receiver.");
                }
                first.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
                Require(first.Scene.AllSoundVolumeTransition == all && first.Scene.MusicVolumeRequest == fade, "Field allocation erased historical global audio requests.");
            }
        }
        foreach (int volume in new[] { -1, 0, 1, 63, 64, 127, 128, 255, 256, int.MinValue, int.MaxValue })
        {
            GameSession session = new(new GameState());
            session.State.Variables[640] = volume;
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.PSHM_L, 640), (Jsm.Opcode.ALLSEVOLTRANS, 0), (Jsm.Opcode.RET, 0)), []);
            engine.Step();
            Require(engine.Scene.AllSoundVolumeTransition is { DurationMilliseconds: 600 } request && request.RawVolume == volume && request.DoubledVolume == unchecked(2 * volume) && request.Volume == (unchecked(2 * volume) & 127), "ALLSEVOLTRANS lost signed/full volume doubling before backend seven-bit narrowing.");
        }
        Console.WriteLine("Native MUSICVOLFADE/ALLSEVOLTRANS: exact DWORD operand order, distinct byte duration conversions, immediate volume authority, voices1..29, muted uncertainty, full-width wrap and atomic restore verified.");
    }
}
