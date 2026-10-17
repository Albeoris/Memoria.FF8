using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static GameSession CreateControlledMusicSession()
    {
        GameSession session = GameSession.CreateColdProcess();
        // Fresh protocol fixture: declare the genuine new-game control words needed by the existing UI callback transport.
        session.SetWorldControls(NativeWorldControlState.CreateNewGame());
        return session;
    }

    // Controlled protocol fixtures explicitly declare event channel routing, independently of global playback history.
    private static void SetControlledMusicChannels(EventEngine engine, int first = 0, int second = 1)
    {
        JsonNode saved = JsonNode.Parse(engine.Capture().GetRawText())!;
        saved["Scene"]!["MusicChannel108"] = JsonSerializer.SerializeToNode(new NativeMusicChannelState(first, null));
        saved["Scene"]!["MusicChannel112"] = JsonSerializer.SerializeToNode(new NativeMusicChannelState(second, null));
        engine.Restore(JsonSerializer.SerializeToElement(saved));
    }

    private static void NativeMusicPlaybackContract()
    {
        foreach (int channel in new[] { -1, 0, 1, 2, int.MinValue })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.MUSICVOL, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            GameSession session = CreateControlledMusicSession();
            session.SetGlobalSoundProcess(session.GlobalSoundProcess! with { MusicPlayback = null });
            EventEngine engine = new(session);
            engine.LoadField(100, content, []);
            SetControlledMusicChannels(engine, channel, -1);
            engine.Step();
            NativeMusicPlaybackState expected = NativeMusicPlaybackState.Unknown().FadeGain46BBC0(unchecked((uint)channel), 16, 300);
            Require(session.GlobalSoundProcess!.MusicPlayback == expected && engine.Scene.SoundMusicVolume197 == 44, "MUSICVOL substituted saved BYTE197 for its full signed global target or routed by script slot.");
            GameSession cloneSession = CreateControlledMusicSession();
            EventEngine clone = new(cloneSession);
            clone.LoadField(100, content, []);
            clone.Restore(engine.Capture());
            engine.Step();
            clone.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), clone.Capture()), "Partially known native playback diverged after independent restore.");
        }
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.INITSOUND, Jsm.Opcode.MUSICSTOP, Jsm.Opcode.MUSICVOL })
        {
            List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1234)];
            if (opcode is Jsm.Opcode.MUSICSTOP or Jsm.Opcode.MUSICVOL)
            {
                operations.Add((Jsm.Opcode.PSHN_L, 0));
            }
            if (opcode == Jsm.Opcode.MUSICVOL)
            {
                operations.Add((Jsm.Opcode.PSHN_L, 300));
            }
            operations.Add((opcode, 0));
            operations.Add((Jsm.Opcode.RET, 0));
            GameSession session = CreateControlledMusicSession();
            EventEngine engine = new(session);
            engine.LoadField(100, SyntheticProgram(operations.ToArray()), []);
            NativeGlobalSoundProcess sound = session.GlobalSoundProcess!;
            try
            {
                engine.Step();
                throw new InvalidOperationException("Native music read an unknown event channel as inactive or zero.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == opcode.ToString())
            {
            }
            JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
            int[] expectedStack = operations.Where(operation => operation.Opcode == Jsm.Opcode.PSHN_L).Select(operation => operation.Parameter).Reverse().ToArray();
            Console.WriteLine($"Unknown music routing diagnostic opcode={opcode} PC={fiber.GetProperty("Position").GetInt32()} expectedPC={operations.Count - 2} stack={fiber.GetProperty("Stack").GetRawText()} sameGlobal={session.GlobalSoundProcess == sound} saved197={engine.Scene.SoundMusicVolume197}");
            Require(fiber.GetProperty("Position").GetInt32() == operations.Count - 2 && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(expectedStack) && session.GlobalSoundProcess == sound && engine.Scene.SoundMusicVolume197 is null, "Unknown routing consumed operands, advanced PC or partially published native music.");
        }
        GameSession initialized = CreateNativeMusicSession();
        EventEngine boot = new(initialized);
        boot.LoadField(74, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(boot.Scene.MusicProcess is { Buffer0: null, Buffer1: null } && initialized.GlobalSoundProcess!.MusicPlayback == NativeMusicPlaybackState.FromColdProcess(), "New Game reset fabricated native buffer contents or changed the global BSS gain latch to127.");
        foreach ((Jsm.Opcode opcode, int music, NativeMusicPlaybackState initial, int expectedId, int expectedGain, bool expectedPlay, bool expectedStop) in new[]
        {
            (Jsm.Opcode.MUSICCHANGE, 7, new NativeMusicPlaybackState(300, 500, 7, 7), 7, 127, false, false),
            (Jsm.Opcode.MUSICCHANGE, 255, new NativeMusicPlaybackState(300, 500, 7, 7), -1, 127, true, true),
            (Jsm.Opcode.CROSSMUSIC, 0, new NativeMusicPlaybackState(300, 500, 7, 7), 0, 300, false, true),
            (Jsm.Opcode.DUALMUSIC, 7, new NativeMusicPlaybackState(300, 500, 7, 7), 7, 500, false, false)
        })
        {
            List<(Jsm.Opcode Opcode, int Parameter)> steps = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, music), (Jsm.Opcode.MUSICLOAD, 0)];
            if (opcode is Jsm.Opcode.CROSSMUSIC or Jsm.Opcode.DUALMUSIC)
            {
                steps.Add((Jsm.Opcode.PSHN_L, 127));
            }
            if (opcode == Jsm.Opcode.CROSSMUSIC)
            {
                steps.Add((Jsm.Opcode.PSHN_L, 25));
            }
            steps.AddRange([(opcode, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
            GameSession session = CreateNativeMusicSession();
            session.SetGlobalSoundProcess(session.GlobalSoundProcess! with { MusicPlayback = initial });
            EventEngine engine = new(session);
            engine.LoadField(74, SyntheticProgram(steps.ToArray()), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            NativeMusicPlaybackDecision decision = engine.Scene.MusicPlayRequest?.Playback ?? throw new InvalidOperationException("Actual music play lost its committed native branch receipt.");
            int channel = opcode == Jsm.Opcode.DUALMUSIC ? 1 : 0;
            NativeMusicPlaybackState actual = session.GlobalSoundProcess!.MusicPlayback!;
            Require(decision.Channel == channel && decision.MusicId == expectedId && decision.PlayRequested == expectedPlay && decision.StopRequested == expectedStop && !decision.ConcertRequested && actual.ReadGain(channel) == expectedGain, "Native same-ID/cross/dual/decodedFFFFFFFF decision differs from committed globals.");
            Require(decision.PlayGain == (opcode is Jsm.Opcode.CROSSMUSIC or Jsm.Opcode.DUALMUSIC ? 127 : 300) && decision.FinalGain == (opcode == Jsm.Opcode.MUSICCHANGE ? 127 : null), "Music receipt lost actual full old play gain or invented a later saved-byte gain reset.");
            Require(engine.Scene.MusicProcess!.Buffer0 is null && engine.Scene.MusicProcess.Buffer1 is { Producer: NativeMusicBufferProducer.Field52CD10 } buffer && buffer.EncodedAkaoId == unchecked((byte)(music + 1)), "MUSICLOAD populated the wrong bank, invented the other bank or lost encoded0.");
        }
        foreach (bool cross in new[] { false, true })
        {
            List<(Jsm.Opcode Opcode, int Parameter)> steps = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1234), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.MUSICLOAD, 0)];
            if (cross)
            {
                steps.AddRange([(Jsm.Opcode.PSHN_L, 127), (Jsm.Opcode.PSHN_L, 25)]);
            }
            steps.AddRange([(cross ? Jsm.Opcode.CROSSMUSIC : Jsm.Opcode.MUSICCHANGE, 0), (Jsm.Opcode.RET, 0)]);
            GameSession session = CreateNativeMusicSession();
            session.SetGlobalSoundProcess(session.GlobalSoundProcess! with { MusicPlayback = null });
            EventEngine engine = new(session);
            engine.LoadField(74, SyntheticProgram(steps.ToArray()), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            if (cross)
            {
                engine.Step();
                Require(session.GlobalSoundProcess!.MusicPlayback == new NativeMusicPlaybackState(127, null, 3, null), "Cross did not establish only its real stop/play writes from unknown global playback.");
            }
            else
            {
                try
                {
                    engine.Step();
                    throw new InvalidOperationException("Plain play inferred its old gain from saved BYTE197 or the cold SFX allocator.");
                }
                catch (EventScriptException) when (engine.LastFault?.Opcode == nameof(Jsm.Opcode.MUSICCHANGE))
                {
                }
                Require(session.GlobalSoundProcess!.MusicPlayback is null && engine.Scene.MusicPlayRequest is null && engine.Scene.MusicProcess is { Pending: true, Buffer1.EncodedAkaoId: 4 } && engine.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 1234, "Missing old gain partially committed playback or discarded the actual prior load/sentinel.");
            }
        }
        Console.WriteLine("Native music playback: full-DWORD routing, partial knowledge, exact independent restore, unknown-channel preflight and uninitialized New Game buffers verified.");
    }
}
