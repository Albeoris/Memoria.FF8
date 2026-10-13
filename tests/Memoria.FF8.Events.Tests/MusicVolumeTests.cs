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
        NativeRetainedPartyByteAuthorityContract();
        NativeMusicVolumeTransitionContract();
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
        byte[] unknown = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.INITSOUND, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 37), (Jsm.Opcode.MUSICVOL, 0), (Jsm.Opcode.PSHM_B, 197), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine uncertain = Create(unknown);
        uncertain.Step();
        Require(uncertain.LastFault is null && uncertain.Scene.MusicVolumeRequest is { ChannelId: null, BothChannels: null, StoredVolume: 37, BackendStatus: GlobalSoundBackendStatus.Muted } && uncertain.Scene.SoundMusicVolume197 == 37 && uncertain.Scene.SoundMusicVolume198 == 127 && uncertain.Scene.MusicChannel108 is null && !uncertain.Scene.MusicPlaying && uncertain.Capture().GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 37, "Unknown muted routing prevented MUSICVOL's unconditional BYTE write or fabricated a current channel.");
        EventEngine uncertainRestore = Create(unknown);
        uncertainRestore.Restore(uncertain.Capture());
        for (int tick = 0; tick < 4; tick++)
        {
            uncertain.Step();
            uncertainRestore.Step();
            Require(JsonElement.DeepEquals(uncertain.Capture(), uncertainRestore.Capture()), "Unknown backend routing or authoritative volume BYTE changed after independent restore.");
        }
        foreach (bool initialize in new[] { false, true })
        {
            List<(Jsm.Opcode Opcode, int Parameter)> steps = [(Jsm.Opcode.LBL, 0)];
            if (initialize)
            {
                steps.Add((Jsm.Opcode.INITSOUND, 0));
            }
            steps.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MUSICSTOP, 0), (Jsm.Opcode.PSHN_L, 26), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.MUSICCHANGE, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.MUSICVOL, 0), (Jsm.Opcode.RET, 0)]);
            GameSession takenOverSession = new(new GameState());
            takenOverSession.StartNewGame();
            EventEngine takenOver = new(takenOverSession, takenOverSession.State.Variables);
            takenOver.LoadField(74, SyntheticProgram(steps.ToArray()), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            takenOver.Step();
            takenOver.Step();
            Require(takenOver.LastFault is null && takenOver.Scene.MusicChannel108?.ChannelId == 0 && takenOver.Scene.MusicChannel112?.ChannelId == -1 && takenOver.Scene.MusicVolumeRequest is { RawSlot: 3, Slot: 1, ChannelId: -1, BothChannels: true, StoredVolume: 255 } && takenOver.Scene.SoundMusicVolume198 == 255 && takenOver.Scene.MusicVolume == 127, "Native music load/play erased independent channel authority, blocked secondary BYTE write or changed primary volume.");
        }
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
        JsonNode impossibleRouting = JsonNode.Parse(uncertain.Capture().GetRawText())!;
        impossibleRouting["Scene"]!["MusicVolumeRequest"]!["BothChannels"] = true;
        try
        {
            Create(unknown).Restore(JsonSerializer.SerializeToElement(impossibleRouting));
            throw new InvalidOperationException("Unknown channel checkpoint manufactured exact backend routing.");
        }
        catch (InvalidDataException)
        {
        }
        Console.WriteLine("Native MUSICVOL: masked slot/full volume, unconditional B197/198 BYTE authority, duration16->0, independently unknown muted routing, takeover and exact independent restore verified.");
    }

    private static void NativeRetainedPartyByteAuthorityContract()
    {
        byte[] firstField = SyntheticObjects([1, 16 << 7],
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)]
        ]);
        byte[] secondField = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.MUSICVOL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        GameSession session = new(new GameState());
        EventEngine first = new(session, session.State.Variables);
        first.LoadField(100, firstField, []);
        first.Scene.Party.AddRange([0, 3]);
        first.Step();
        first.Step();
        Require(first.Scene.PartyBindingMemory == new NativePartyBindingMemory(1, 0, 1, 255), "Original REFRESHPARTY did not record independently typed save BYTE194..196 authority.");
        first.LoadField(101, secondField, []);
        first.Step();
        JsonElement paused = first.Capture();
        Require(first.Scene.NativeActorLayoutVersion == 0 && first.Scene.FieldPartyNativeActorIds.Count == 0 && first.Scene.PartyBindingMemory == new NativePartyBindingMemory(1, 0, 1, 255), "Field allocation confused retained global BYTE authority with current compact actor bindings.");
        GameSession restoredSession = new(new GameState());
        EventEngine restored = new(restoredSession, restoredSession.State.Variables);
        restored.LoadField(101, secondField, []);
        restored.Restore(paused);
        for (int tick = 0; tick < 5; tick++)
        {
            first.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(first.Capture(), restored.Capture()), "Mid-initialization restore lost retained party BYTE widths or MUSICVOL's independently unknown routing.");
        }
        Require(first.Scene.SoundMusicVolume197 == 255 && first.Scene.MusicVolumeRequest is { ChannelId: null, BothChannels: null } && session.State.Variables[194] == 0 && session.State.Variables[195] == 1 && session.State.Variables[196] == 255, "Initialization MUSICVOL rejected proved prior binding bytes, changed them, or manufactured a channel.");
        foreach (string property in new[] { "Version", "First" })
        {
            JsonNode malformed = JsonNode.Parse(paused.GetRawText())!;
            malformed["Scene"]!["PartyBindingMemory"]![property] = 2;
            try
            {
                restored.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Malformed or disagreeing retained party BYTE authority passed restore validation.");
            }
            catch (InvalidDataException)
            {
            }
        }
        restored.Restore(paused);
        restoredSession.State.Variables[196] = 65537;
        try
        {
            restored.Step();
            restored.Step();
            throw new InvalidOperationException("Retained binding receipt admitted a bare disagreeing mixed-width alias.");
        }
        catch (EventScriptException) when (restored.LastFault?.Opcode == "MUSICVOL")
        {
        }
        Require(restored.Scene.MusicVolumeRequest is null && restored.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 5 && restored.Capture().GetProperty("Fibers")[0].GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([511, 0]), "Refused mixed-width alias consumed the initialization MUSICVOL PC or operands.");
    }

    private static void NativeMusicVolumeTransitionContract()
    {
        foreach (int duration in new[] { -1, 0, 24, 25, 127, 128, 255, 256, int.MinValue, int.MaxValue })
        {
            foreach (bool known in new[] { false, true })
            {
                int rawSlot = known ? 2 : 3;
                List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.INITSOUND, 0)];
                if (known)
                {
                    operations.AddRange([(Jsm.Opcode.PSHN_L, rawSlot), (Jsm.Opcode.MUSICSTOP, 0)]);
                }
                operations.AddRange([(Jsm.Opcode.PSHN_L, rawSlot), (Jsm.Opcode.PSHM_L, 640), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.MUSICVOLTRANS, 0), (Jsm.Opcode.PSHM_B, 197 + (rawSlot & 1)), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
                byte[] content = SyntheticProgram(operations.ToArray());
                EventEngine Create()
                {
                    GameSession session = new(new GameState());
                    session.State.Variables[640] = duration;
                    EventEngine engine = new(session, session.State.Variables);
                    engine.LoadField(100, content, []);
                    return engine;
                }
                EventEngine first = Create();
                first.Step();
                NativeMusicVolume request = first.Scene.MusicVolumeRequest!;
                int doubledByte = (byte)(unchecked((uint)duration) << 1);
                Require(first.LastFault is null && request.RawSlot == rawSlot && request.Slot == (rawSlot & 1) && request.TransitionDurationOperand == duration && request.RequestedDuration == doubledByte && request.EffectiveDuration == (doubledByte < 50 ? 0 : doubledByte) && request.RequestedVolume == 511 && request.StoredVolume == 255 && request.ChannelId == (known ? -1 : null) && request.BothChannels == (known ? true : null), "MUSICVOLTRANS lost its original duration DWORD, doubled low-BYTE backend duration, native threshold or independent routing uncertainty.");
                Require(first.Capture().GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 255, "MUSICVOLTRANS deferred its unconditional target BYTE write until an invented audio ramp completed.");
                EventEngine restored = Create();
                restored.Restore(first.Capture());
                first.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(first.Capture(), restored.Capture()), "Music transition request/raw duration/current BYTE authority did not survive exact independent restore.");
                JsonNode invalid = JsonNode.Parse(first.Capture().GetRawText())!;
                invalid["Scene"]!["MusicVolumeRequest"]!["EffectiveDuration"] = request.EffectiveDuration + 1;
                try
                {
                    Create().Restore(JsonSerializer.SerializeToElement(invalid));
                    throw new InvalidOperationException("Malformed backend duration passed MUSICVOLTRANS checkpoint validation.");
                }
                catch (InvalidDataException)
                {
                }
            }
        }
    }
}
