using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static GlobalSoundResource MutedGlobalSample(int sample)
    {
        return new GlobalSoundResource(sample, GlobalSoundBackendStatus.Muted, new GlobalSoundLoopPolicy(1, 47844, 130622, 22050, 16, 1, 1087));
    }

    private static void NativeGlobalSoundContract()
    {
        byte[] ProgramFor(int effect = 69, int mask = 4, int pan = 100, int volume = 20) => SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, effect), (Jsm.Opcode.PSHN_L, mask), (Jsm.Opcode.PSHN_L, pan), (Jsm.Opcode.PSHN_L, volume), (Jsm.Opcode.EFFECTPLAY, 0), (Jsm.Opcode.PSHN_L, 999), (Jsm.Opcode.POPM_W, 800), (Jsm.Opcode.RET, 0));
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables) { ResolveGlobalSoundResource = MutedGlobalSample };
        engine.LoadField(100, ProgramFor(), []);
        engine.Step();
        GlobalSoundRequest request = engine.Scene.GlobalSoundRequests.Single();
        Require(request.OriginalEffectId == 69 && request.ChannelMask == 4 && request.Voice == 3 && request.RawPan == 100 && request.EffectivePan == 50 && request.Volume == 20 && request.PitchPercent == 100, "EFFECTPLAY lost full-DWORD argument order, explicit voice or native pan mapping.");
        Require(request.Resource.BackendStatus == GlobalSoundBackendStatus.Muted && request.Resource.LoopPolicy.LoopTransitionDelayMilliseconds == 1087 && engine.Scene.SoundRequests.Count == 0 && session.State.Variables[800] == 999, "Global muted loop request became a field-table effect or an invented VM audio wait.");
        var saved = engine.Capture();
        engine.Restore(saved);
        Require(saved.GetRawText() == engine.Capture().GetRawText(), "Global voice/request/loop metadata failed exact restore.");
        engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        Require(engine.Scene.GlobalSoundVoices[3] == request && engine.Scene.GlobalSoundRequests.Single() == request, "Field reload discarded native global voice IDs or request history.");

        foreach ((int effect, int sample) in new[] { (10089, 2239), (210005, 155), (220005, 185), (240005, 375), (300005, 715), (350005, 985), (400005, 1235), (410005, 1515), (420005, 1925), (500005, 2789), (690000, 2060), (690001, 1961), (700005, 5) })
        {
            engine.LoadField(100, ProgramFor(effect), []);
            engine.Step();
            Require(engine.Scene.GlobalSoundVoices[3].Resource.SampleIndex == sample && engine.Scene.GlobalSoundVoices[3].OriginalEffectId == effect, "Native46B120 sound-ID switch lost an original mapping branch.");
        }

        foreach (bool invalidParameters in new[] { false, true })
        {
            engine.ResolveGlobalSoundResource = sample => MutedGlobalSample(sample) with { BackendStatus = invalidParameters ? GlobalSoundBackendStatus.Muted : GlobalSoundBackendStatus.Failed };
            engine.LoadField(100, ProgramFor(volume: invalidParameters ? 128 : 20), []);
            engine.Step();
            Require(engine.Scene.GlobalSoundVoices[3].Resource.BackendStatus == GlobalSoundBackendStatus.Failed && session.State.Variables[800] == 999, "Native audio failure stopped VM execution or failed to store the original voice effect ID.");
        }

        foreach (bool automaticVoice in new[] { false, true })
        {
            GameSession boundedSession = new(new GameState());
            EventEngine bounded = new(boundedSession, boundedSession.State.Variables) { ResolveGlobalSoundResource = automaticVoice ? MutedGlobalSample : null };
            bounded.LoadField(100, ProgramFor(mask: automaticVoice ? 0 : 4), []);
            try
            {
                bounded.Step();
                throw new InvalidOperationException("Unsupported global sound backend/automatic voice was consumed.");
            }
            catch (EventScriptException) when (bounded.LastFault?.Opcode == "EFFECTPLAY")
            {
            }
            var frozen = bounded.Capture();
            var caller = frozen.GetProperty("Fibers")[0];
            Require(caller.GetProperty("Position").GetInt32() == 5 && caller.GetProperty("Stack").GetArrayLength() == 4 && bounded.Scene.GlobalSoundRequests.Count == 0 && !boundedSession.State.Variables.ContainsKey(800), "Unsupported global sound changed original PC, four operands or request state.");
            bounded.Restore(frozen);
            Require(frozen.GetRawText() == bounded.Capture().GetRawText(), "Unsupported global sound fault failed exact frozen restore.");
        }
    }

    private static void GardenGlobalSoundBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(137);
        GlobalAudioSampleDescriptor descriptor = GlobalAudioFormat.Read(gameDirectory, 69);
        GlobalSoundResource resource = new(69, GlobalSoundBackendStatus.Muted, new GlobalSoundLoopPolicy(descriptor.PlaybackFlags, descriptor.LoopStartPcmByte, descriptor.LoopEndPcmByte, descriptor.WaveFormat.SampleRate, 16, descriptor.WaveFormat.Channels, 1087));
        Require(resource == MutedGlobalSample(69) && descriptor.WaveFormat.Tag == 2 && descriptor.WaveFormat.BitsPerSample == 4, "Reached global69 original ADPCM/PCM loop descriptor differs from static native proof.");
        string path = Path.Combine("../fixtures/external-fixture.json");
        EventEngine Create(bool backend)
        {
            GameSession session = new(GameSession.Load(path));
            EventEngine result = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                ResolveGlobalSoundResource = backend ? sample => sample == 69 ? resource : null : null
            };
            result.LoadField(137, field.Jsm, field.Messages);
            result.Restore(session.State.EventState!.Value);
            return result;
        }
        EventEngine first = Create(true);
        EventEngine resumed = Create(true);
        Require(first.Frame == 6 && first.Scene.GlobalSoundRequests.Count == 0, "Actual137 EFFECTPLAY source changed.");
        PlayerAction wait = new(PlayerActionKind.Wait);
        first.Apply(wait);
        resumed.Apply(wait);
        Require(first.Capture().GetRawText() == resumed.Capture().GetRawText() && first.Frame == 7 && first.LastFault is null, "Actual original EFFECTPLAY did not advance immediately with exact replay.");
        GlobalSoundRequest request = first.Scene.GlobalSoundRequests.Single();
        Require(request == new GlobalSoundRequest(69, 1, 1, 1, 127, 63, 127, 100, resource) && first.Scene.GlobalSoundVoices[1] == request && first.Scene.SoundRequests.Count == 0, "Reached69/voice1 global request lost its original arguments, loop policy or explicit muted backend.");
        var advanced = first.Capture();
        first.Restore(advanced);
        Require(advanced.GetRawText() == first.Capture().GetRawText(), "Actual global ambient request did not survive exact checkpoint restore.");

        EventEngine missing = Create(false);
        try
        {
            missing.Apply(wait);
            throw new InvalidOperationException("Actual137 missing global backend was silently ignored.");
        }
        catch (EventScriptException) when (missing.LastFault?.Opcode == "EFFECTPLAY")
        {
        }
        var frozen = missing.Capture();
        var caller = frozen.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 20 && fiber.GetProperty("ScriptId").GetInt32() == 187);
        Require(caller.GetProperty("Position").GetInt32() == 5510 && caller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 127, 127, 1, 69 }) && missing.Scene.GlobalSoundRequests.Count == 0, "Actual missing backend consumed native EFFECTPLAY operands or advanced its PC.");
        missing.ResolveGlobalSoundResource = _ => resource;
        missing.Restore(frozen);
        Require(missing.LastFault is null, "Exact saved EFFECTPLAY fault did not retry when its declared backend became available.");
        missing.Step();
        Require(missing.Scene.GlobalSoundRequests.Single() == request, "Fault retry changed the exact native global request.");
        Console.WriteLine($"Original137 EFFECTPLAY: ordinary Wait1/frame6→7 global69/voice1 volume127/rawpan127/effective63/pitch100; explicitMuted ADPCM loop47844..130622/delay1087ms, noVMwait; next {first.NextTrigger.Kind}.");
    }
}
