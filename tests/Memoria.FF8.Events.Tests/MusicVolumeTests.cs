using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMusicVolumeContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.INITSOUND, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MUSICSTOP, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.MUSICVOL, 0), (Jsm.Opcode.PSHM_B, 197), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine Create(byte[]? bytes = null)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, bytes ?? content, []);
            return engine;
        }
        EventEngine first = Create();
        first.Step();
        NativeMusicVolume request = first.Scene.MusicVolumeRequest ?? throw new InvalidOperationException("MUSICVOL did not record its original muted request.");
        Require(request is { RawSlot: 2, Slot: 0, ChannelId: -1, RequestedVolume: 511, StoredVolume: 255, RequestedDuration: 16, EffectiveDuration: 0, BothChannels: true, BackendStatus: GlobalSoundBackendStatus.Muted } && first.Scene.SoundMusicVolume197 == 255 && first.Scene.SoundMusicVolume198 == 127 && first.Scene.MusicVolume == 255 && !first.Scene.MusicPlaying, "MUSICVOL lost native slot/full-volume/low-byte routing or fabricated music playback.");
        Require(first.Capture().GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 255, "MUSICVOL did not continue immediately with exact B197 authority.");
        EventEngine resumed = Create();
        resumed.Restore(first.Capture());
        for (int tick = 0; tick < 4; tick++)
        {
            first.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "MUSICVOL history/current bytes changed after independent restore.");
        }
        byte[] unknown = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.INITSOUND, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 37), (Jsm.Opcode.MUSICVOL, 0), (Jsm.Opcode.RET, 0));
        EventEngine refused = Create(unknown);
        try
        {
            refused.Step();
            throw new InvalidOperationException("MUSICVOL invented unknown channel routing.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "MUSICVOL")
        {
        }
        Require(refused.Scene.MusicVolumeRequest is null && refused.Scene.SoundMusicVolume197 == 127 && refused.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 37 && refused.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[1].GetInt32() == 0, "Refused volume consumed operands or replaced canonical bytes.");
        JsonNode malformed = JsonNode.Parse(first.Capture().GetRawText())!;
        malformed["Scene"]!["MusicVolumeRequest"]!["StoredVolume"] = 127;
        try
        {
            Create().Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Malformed native volume byte passed restore validation.");
        }
        catch (InvalidDataException)
        {
        }
        Console.WriteLine("Native MUSICVOL: masked slot/full volume request, low-byte B197 write, duration16->0 and muted known-channel routing, immediate continuation, restore and unknown-channel preconsume refusal verified.");
    }
}
