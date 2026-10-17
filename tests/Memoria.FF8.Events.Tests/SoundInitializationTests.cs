using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSoundInitializationContract(string gameDirectory)
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1234), (Jsm.Opcode.INITSOUND, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHM_B, 197), (Jsm.Opcode.POPI_L, 4), (Jsm.Opcode.PSHM_B, 198), (Jsm.Opcode.POPI_L, 5), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.POPM_B, 198), (Jsm.Opcode.PSHM_B, 198), (Jsm.Opcode.POPI_L, 6), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine Create(byte[]? program = null)
        {
            GameSession session = CreateControlledMusicSession();
            session.SetWorldControls(NativeWorldControlState.CreateNewGame());
            EventEngine engine = new(session);
            engine.LoadField(100, program ?? content, []);
            SetControlledMusicChannels(engine);
            JsonNode source = JsonNode.Parse(engine.Capture().GetRawText())!;
            source["Scene"]!["LoadedMusic"] = 26;
            source["Scene"]!["MusicPlaying"] = true;
            source["Scene"]!["MusicVolume"] = 37;
            engine.Restore(JsonSerializer.SerializeToElement(source));
            GlobalSoundResource resource = new(1, GlobalSoundBackendStatus.Muted, new GlobalSoundLoopPolicy(0, 0, 0, 44100, 16, 1, null));
            engine.ResolveGlobalSoundResource = _ => resource;
            PreparedGlobalSoundEmission prepared = engine.PrepareGlobalSound(1, 1, 127, 127, session.GlobalSoundProcess!);
            session.SetGlobalSoundProcess(prepared.Process);
            GlobalSoundRequest request = prepared.Request;
            engine.Scene.GlobalSoundRequests.Add(request);
            return engine;
        }
        EventEngine first = Create();
        first.Step();
        NativeSoundInitialization reset = first.Scene.SoundInitialization ?? throw new InvalidOperationException("INITSOUND did not record its original muted reset.");
        Require(reset.MusicGainChannels is { } channels && channels.SequenceEqual([0, 1]), "INITSOUND lost its actual ordered event-channel gain calls.");
        JsonElement begun = first.Capture();
        JsonElement results = begun.GetProperty("Fibers")[0].GetProperty("Results");
        Require(reset is { Version: 1, FieldId: 100, Operation: 2, MusicVolume197: 127, MusicVolume198: 127, SoundVolume: 127, StopFirstVoice: 0, StopVoiceCount: 32, PanFirstVoice: 1, PanVoiceCount: 29, Pan: 64, BackendStatus: GlobalSoundBackendStatus.Muted } && first.Scene.SoundMusicVolume197 == 127 && first.Scene.SoundMusicVolume198 == 255 && first.Scene.LoadedMusic == 26 && first.Scene.MusicPlaying && first.Scene.MusicVolume == 127, "INITSOUND fabricated music identity or lost its original reset ranges/known byte writes.");
        Require(results.GetProperty("3").GetInt32() == 1234 && results.GetProperty("4").GetInt32() == 127 && results.GetProperty("5").GetInt32() == 127 && results.GetProperty("6").GetInt32() == 255 && first.Scene.GlobalSoundRequests.Count == 1 && first.Scene.GlobalSoundLatestAttempts.Count == 1, "INITSOUND consumed an operand, failed exact byte overlay or erased sound attempt history.");
        EventEngine resumed = Create();
        resumed.Restore(begun);
        for (int tick = 0; tick < 4; tick++)
        {
            first.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Sound reset history, exact current bytes or VM wait changed after independent restore.");
        }
        byte[] nextField = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        first.LoadField(101, nextField, []);
        Require(first.Scene.SoundInitialization == reset && first.Scene.SoundMusicVolume198 == 255 && first.Scene.GlobalSoundRequests.Count == 1, "Normal field load erased persistent sound reset evidence or current global bytes.");
        EventEngine loaded = Create(nextField);
        loaded.LoadField(101, nextField, []);
        loaded.Restore(first.Capture());
        Require(JsonElement.DeepEquals(first.Capture(), loaded.Capture()), "Persistent sound state failed independent restore after field load.");

        foreach ((Jsm.Opcode opcode, int address) in new[] { (Jsm.Opcode.PSHM_W, 196), (Jsm.Opcode.POPM_L, 197) })
        {
            byte[] unsupported = opcode == Jsm.Opcode.PSHM_W ? SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.INITSOUND, 0), (opcode, address), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.RET, 0)) : SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.INITSOUND, 0), (Jsm.Opcode.PSHN_L, 777), (opcode, address), (Jsm.Opcode.RET, 0));
            EventEngine refused = Create(unsupported);
            try
            {
                refused.Step();
                throw new InvalidOperationException("Mixed-width sound volume access bypassed its original byte boundary.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == opcode.ToString())
            {
            }
            Require(refused.Scene.SoundMusicVolume197 == 127 && refused.Scene.SoundMusicVolume198 == 127 && (opcode != Jsm.Opcode.POPM_L || refused.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 777), "Unsupported sound alias changed known bytes or consumed its original write operand.");
        }
        EventEngine ambiguous = Create();
        JsonNode ambiguousSource = JsonNode.Parse(ambiguous.Capture().GetRawText())!;
        SetCapturedCell(ambiguousSource, 197, 4, 65537);
        JsonElement beforeAlias = ambiguous.Capture();
        try
        {
            ambiguous.Restore(JsonSerializer.SerializeToElement(ambiguousSource));
            throw new InvalidOperationException("INITSOUND fixture restored a DWORD alias overlapping its canonical channels.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(beforeAlias, ambiguous.Capture()), "Rejected overlapping restore changed the controlled music fixture.");
        JsonNode malformed = JsonNode.Parse(begun.GetRawText())!;
        malformed["Scene"]!["SoundInitialization"]!["Pan"] = 63;
        try
        {
            Create().Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Malformed sound reset pan/range passed checkpoint validation.");
        }
        catch (InvalidDataException)
        {
        }
        Console.WriteLine("Native INITSOUND: zero operands/immediate return, B197/198 known exact writes, muted32-stop/volume127/pan1..29=64 provenance, music IDs/history retained; field-load and independent VM restore, mixed-width guards verified.");
        NativeMusicStopContract();
        NativeMusicVolumeContract(gameDirectory);
        NativeFadeNoneContract();
    }
}
