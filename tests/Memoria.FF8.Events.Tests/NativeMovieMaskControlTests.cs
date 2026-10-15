using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMovieMaskControlContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeMovieCameraFlagResources(gameDirectory);
        foreach (int raw in new[] { 0, 1, 257, 511, -1, 65536, 0x7FFFFF })
        {
            NativeMovieMaskByteWidth(vitals, economy, raw);
        }
        foreach (bool overrideAfterStart in new[] { false, true })
        {
            NativeMovieMaskClockAndRender(vitals, economy, overrideAfterStart);
        }
        foreach (bool skipped in new[] { false, true })
        {
            NativeMovieMaskNativeTerminal(vitals, economy, skipped);
        }
        NativeMovieMaskTwoRecordStartupRefusal(vitals, economy);
        NativeMovieMaskAtomicRefusals(vitals, economy);
        Console.WriteLine("Native SETDCAMERA/movie mask: lowBYTE width, same-callback init successor, fresh loader/ready/start/cleanup owners, actual CAM flag40 parsing and8→0/16→1 order, record0/startup1/decoded callback draw latches, VM override, independent restores, required pre-CAM MSK draw selection and required-state/opaque/provider atomic refusals passed. Installed CAM24 is static metadata only; this VM scenario makes no MSK pixel or original-runtime parity claim.");
    }

    private static void NativeMovieMaskNativeTerminal(NativeCharacterVitals vitals, NativeEconomy economy, bool skipped)
    {
        NativeMovieCameraFlags flags = NativeMovieCameraReader.Read(NativeMovieMaskCameraBytes([16, 0, 8, 16, 8]));
        (Jsm.Opcode, int)[] body = [(Jsm.Opcode.PSHN_L, 1001), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.MOVIESYNC, 0), (Jsm.Opcode.PSHN_L, 55), (Jsm.Opcode.POPM_W, 6402)];
        var live = CreateNativeMovieMask(vitals, economy, body, flags);
        live.Engine.Step();
        live.Engine.Step();
        live.Engine.Step();
        Require(live.Session.IsMoviePlaying && live.Engine.Actors[0].MovementSpeed == 2002 && live.Engine.Actors[0].NativeMovementBaseSpeed == 2002 && live.Engine.Actors[0].AnimationSpeed == 32, "CAM-terminal fixture did not enter actual MOVIE word-doubling lifecycle.");
        live.Session.SetMoviePlayback(new(0, 8, true, false, false));
        live.Session.SetMoviePlayback(new(1, 8, true, false, false));
        var restored = CreateNativeMovieMask(vitals, economy, body, flags, state: live.Session.State);
        restored.Engine.Restore(live.Engine.Capture());
        int finalRecord = skipped ? 2 : 4;
        for (int record = 2; record <= finalRecord; record++)
        {
            live.Session.SetMoviePlayback(new(record, 8, true, false, false));
            restored.Session.SetMoviePlayback(new(record, 8, true, false, false));
            live.Engine.Step();
            restored.Engine.Step();
            Require(live.Engine.Scene.NativeMovieMask!.Playback!.AppliedRecord == record && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Shorter CAM terminal diverged from actual consecutive decoded callbacks after independent restore.");
            if (record < 4)
            {
                Require(live.Session.IsMoviePlaying && live.Engine.Scene.MovieActors!.EndFrame is null, "Native CAM playback closed before its actual terminal record or explicit skip.");
            }
        }
        long closingFrame = live.Engine.Frame;
        if (skipped)
        {
            live.Session.CompleteMovie(true);
            restored.Session.CompleteMovie(true);
        }
        Require(!live.Session.IsMoviePlaying && live.Session.NativeMovieClockFlag == false && live.Session.State.Scene == SceneKind.Field && live.Session.State.MoviePlayback is { TerminalFrame: 8, IsPlaying: false, Completed: true } playback && playback.FrameIndex == finalRecord && playback.Skipped == skipped && live.Engine.Frame == closingFrame && live.Engine.Scene.MovieActors!.EndFrame == closingFrame && live.Engine.Scene.NativeMovieMask is { Value: 1, Playback: { } mask } && mask.AppliedRecord == finalRecord && mask.DrawRecord == finalRecord, "Native CAM/skip cleanup sought to the AVI terminal, consumed unseen CAM flags, lost the actual frame or invented a callback.");
        Require(live.Engine.Actors[0].MovementSpeed == 1001 && live.Engine.Actors[0].NativeMovementBaseSpeed == 1001 && live.Engine.Actors[0].AnimationSpeed == 16 && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Native CAM/skip cleanup did not halve actual body words exactly once or preserve independent restored state.");
        live.Engine.Step();
        restored.Engine.Step();
        Require(live.Engine.Scene.NativeMovieMask!.Playback!.AppliedRecord == finalRecord && live.Session.State.MoviePlayback!.FrameIndex == finalRecord && live.Session.State.MoviePlayback.TerminalFrame == 8 && live.Session.State.Variables.GetValueOrDefault(6402) == 55 && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Post-CAM continuation consumed an extra CAM record or changed actual root playback while resuming the held VM.");
    }

    private static void NativeMovieMaskTwoRecordStartupRefusal(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        NativeMovieCameraFlags flags = NativeMovieCameraReader.Read(NativeMovieMaskCameraBytes([8, 16]));
        (Jsm.Opcode, int)[] body = [(Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.MOVIESYNC, 0)];
        var fixture = CreateNativeMovieMask(vitals, economy, body, flags);
        JsonElement? beforeFiber = null;
        JsonElement? beforeScene = null;
        JsonElement? beforeActors = null;
        fixture.Engine.CommandExecuted += command =>
        {
            if (command.Name == "NOP")
            {
                JsonElement saved = fixture.Engine.Capture();
                beforeFiber = saved.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
                beforeScene = saved.GetProperty("Scene");
                beforeActors = saved.GetProperty("Actors");
            }
        };
        for (int callback = 0; callback < 4 && fixture.Engine.LastFault is null; callback++)
        {
            try
            {
                fixture.Engine.Step();
            }
            catch (EventScriptException)
            {
                break;
            }
        }
        JsonElement after = fixture.Engine.Capture();
        Require(fixture.Engine.LastFault?.Opcode == "MOVIE" && beforeFiber is JsonElement fiber && beforeScene is JsonElement scene && beforeActors is JsonElement actors && JsonElement.DeepEquals(fiber, after.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1)) && JsonElement.DeepEquals(scene, after.GetProperty("Scene")) && JsonElement.DeepEquals(actors, after.GetProperty("Actors")) && !fixture.Session.IsMoviePlaying && fixture.Session.NativeMovieClockFlag == false && fixture.Session.State.MoviePlayback is null && fixture.Engine.Scene.MovieActors!.StartFrame is null && fixture.Engine.Scene.NativeMovieMask is { Value: 1, Playback.AppliedRecord: -1 }, "Two-record CAM startup mutated PC, operands, actor words, mask phase or physical movie state before refusing its unrepresented initial decode clock.");
    }

    private static void NativeMovieCameraFlagResources(string gameDirectory)
    {
        byte[] synthetic = NativeMovieMaskCameraBytes([0, 8, 16, 24]);
        NativeMovieCameraFlags parsed = NativeMovieCameraReader.Read(synthetic);
        Require(parsed.SourceSha256 == Convert.ToHexString(SHA256.HashData(synthetic)) && parsed.Flags.SequenceEqual(new byte[] { 0, 8, 16, 24 }) && parsed.Apply(0, 7) == 7 && parsed.Apply(1, 7) == 0 && parsed.Apply(2, 0) == 1 && parsed.Apply(3, 7) == 1, "CAM reader lost original44-byte flag offset40, source identity or native8-before16 precedence.");
        foreach (byte[] bad in new[] { synthetic[..^1], synthetic[..95], synthetic.ToArray() })
        {
            if (bad.Length == synthetic.Length)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(6), 99);
            }
            try
            {
                NativeMovieCameraReader.Read(bad);
                throw new InvalidOperationException("CAM reader accepted an incomplete bank or mismatched terminal header.");
            }
            catch (NotSupportedException)
            {
            }
        }
        // Bounded original metadata audit for the clip selected by reached735.
        // Reading this CAM does not claim that the future movie24 was played.
        byte[] installed = File.ReadAllBytes(Path.Combine(gameDirectory, "Data", "movies", "disc00_24.cam"));
        NativeMovieCameraFlags actual = NativeMovieCameraReader.Read(installed);
        Require(actual.SourceSha256 == "3372C8784F78E855ED9E7B46D3901000D91C8F2F1BEF196342BA623784AAE194" && actual.Flags.Length == 575 && actual.Flags[150] == 16 && actual.Flags[151] == 8 && actual.Flags[169] == 8 && actual.Flags[170] == 17 && actual.Flags[574] == 16, "Installed CAM24 does not match the bounded native header/flag-run evidence.");
        byte value = 1;
        for (int record = 150; record <= 171; record++)
        {
            byte draw = value;
            value = actual.Apply(record, value);
            Require(record != 151 || draw == 1 && value == 0, "CAM151 flags changed the current draw instead of the next draw.");
            Require(record != 152 || draw == 0, "CAM24 mask interval began before or after native draw152.");
            Require(record != 170 || draw == 0 && value == 1, "CAM170 flag16 failed to restore the byte after its current draw.");
            Require(record != 171 || draw == 1, "CAM24 normal draw admission did not return at171.");
        }
    }

    private static void NativeMovieMaskByteWidth(NativeCharacterVitals vitals, NativeEconomy economy, int raw)
    {
        (Jsm.Opcode, int)[] body = [(Jsm.Opcode.PSHN_L, 222), (Jsm.Opcode.PSHN_L, raw), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.SETDCAMERA, 0), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 6400)];
        var fixture = CreateNativeMovieMask(vitals, economy, body, initialize: true);
        Require(fixture.Engine.Scene.NativeMovieMask is { Version: 1, Value: 1, Playback: null }, "Fresh field471F70 did not establish the actual byte1 owner.");
        JsonElement? actorsBefore = null;
        JsonNode? sceneBefore = null;
        long issueFrame = -1;
        fixture.Engine.CommandExecuted += command =>
        {
            if (command.Name == "NOP")
            {
                JsonElement saved = fixture.Engine.Capture();
                actorsBefore = saved.GetProperty("Actors");
                sceneBefore = JsonNode.Parse(saved.GetProperty("Scene").GetRawText());
            }
            if (command.Name == "SETDCAMERA")
            {
                issueFrame = fixture.Engine.Frame;
                JsonElement saved = fixture.Engine.Capture();
                JsonElement caller = saved.GetProperty("Fibers")[0];
                sceneBefore!["NativeMovieMask"] = JsonNode.Parse(saved.GetProperty("Scene").GetProperty("NativeMovieMask").GetRawText());
                Require(command.Arguments.SequenceEqual(new[] { raw }) && caller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 222 }) && fixture.Engine.Scene.NativeMovieMask!.Value == unchecked((byte)raw) && actorsBefore is JsonElement actors && JsonElement.DeepEquals(actors, saved.GetProperty("Actors")) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(sceneBefore), saved.GetProperty("Scene")), "SETDCAMERA changed unrelated actor/camera/follow/scroll state, narrowed the wrong width or consumed more than its one original DWORD.");
            }
        };
        fixture.Engine.Step();
        Require(issueFrame == fixture.Engine.Frame && fixture.Session.State.Variables.GetValueOrDefault(6400) == 77 && fixture.Engine.Scene.CameraIndex == 0, "Actual init SETDCAMERA failed native return2/same-callback successor or incorrectly selected cameraIndex1.");
        var restored = CreateNativeMovieMask(vitals, economy, body, initialize: true);
        restored.Engine.Restore(fixture.Engine.Capture());
        Require(JsonElement.DeepEquals(fixture.Engine.Capture(), restored.Engine.Capture()), "SETDCAMERA lowBYTE owner did not restore independently.");
    }

    private static void NativeMovieMaskClockAndRender(NativeCharacterVitals vitals, NativeEconomy economy, bool overrideAfterStart)
    {
        NativeMovieCameraFlags flags = NativeMovieCameraReader.Read(NativeMovieMaskCameraBytes([8, 16, 8, 24, 0, 16]));
        List<(Jsm.Opcode, int)> body = [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETDCAMERA, 0), (Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.MOVIE, 0)];
        if (overrideAfterStart)
        {
            body.AddRange([(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.SETDCAMERA, 0)]);
        }
        body.AddRange([(Jsm.Opcode.MOVIESYNC, 0), (Jsm.Opcode.PSHN_L, 91), (Jsm.Opcode.POPM_W, 6401)]);
        var live = CreateNativeMovieMask(vitals, economy, body.ToArray(), flags);
        bool observedRecord0 = false;
        live.Engine.CommandExecuted += command =>
        {
            if (command.Name == "MOVIE")
            {
                observedRecord0 = live.Engine.Scene.NativeMovieMask is { Value: 0, Playback.AppliedRecord: 0, Playback.DrawRecord: null };
            }
        };
        live.Engine.Step();
        live.Engine.Step();
        Require(!live.Session.IsMoviePlaying && live.Engine.Scene.NativeMovieMask is { Value: 1, Playback.AppliedRecord: -1, Playback.DrawRecord: null }, "MOVIEREADY did not reset1 and bind the original CAM before its return3 yield.");
        live.Engine.Step();
        NativeMovieMaskState start = live.Engine.Scene.NativeMovieMask!;
        Require(observedRecord0 && live.Session.IsMoviePlaying && start is { Value: 1, Playback.AppliedRecord: 1, Playback.DrawRecord: 1 } && start.Playback.DrawValue == (overrideAfterStart ? 7 : 0), "Native movie startup did not consumeCAM0 insideMOVIE, latch after remaining VM writes, then applyCAM1 after the current draw.");
        JsonElement guardBefore = live.Engine.Capture();
        if (overrideAfterStart)
        {
            live.Engine.RequireSupportedMovieMaskRender(1, 0);
        }
        else
        {
            ExpectNativeMovieMaskRenderRequired(live.Engine, 0);
        }
        live.Engine.RequireSupportedMovieMaskRender(0, 0);
        Require(JsonElement.DeepEquals(guardBefore, live.Engine.Capture()), "Movie render admission mutated byte, callback or VM state.");
        live.Session.SetMoviePlayback(new(0, 5, true, false, false));
        live.Session.SetMoviePlayback(new(1, 5, true, false, false));
        Require(live.Engine.Scene.NativeMovieMask == start, "Initial decoded0/1 samples fabricated native CAM callbacks.");
        var restored = CreateNativeMovieMask(vitals, economy, body.ToArray(), flags, state: live.Session.State);
        restored.Engine.Restore(live.Engine.Capture());
        NativeMovieMaskMalformedRestore(vitals, economy, body.ToArray(), flags, live.Session.State, live.Engine.Capture());
        for (int record = 2; record <= 5; record++)
        {
            live.Session.SetMoviePlayback(new(record, 5, true, false, false));
            restored.Session.SetMoviePlayback(new(record, 5, true, false, false));
            byte before = live.Engine.Scene.NativeMovieMask!.Value;
            live.Engine.Step();
            restored.Engine.Step();
            NativeMovieMaskState current = live.Engine.Scene.NativeMovieMask!;
            Require(current.Playback?.DrawRecord == record && current.Playback.DrawValue == before && current.Value == flags.Apply(record, before) && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Consecutive decoded callback lost VM→current draw latch→late CAM flags order or independent active restore.");
            if (record == 2)
            {
                Require(current is { Value: 0, Playback.DrawValue: 1 }, "Frame2 native late flag8 did not retain the current draw's byte1.");
                live.Engine.RequireSupportedMovieMaskRender(1, record);
                NativeMovieMaskRootClockRefusal(vitals, economy, body.ToArray(), flags, live.Session.State, live.Engine.Capture());
            }
            if (record == 3)
            {
                Require(current is { Value: 1, Playback.DrawValue: 0 }, "Combined flag24 lost16 precedence or the earlier draw0 latch.");
                JsonElement saved = live.Engine.Capture();
                ExpectNativeMovieMaskRenderRequired(live.Engine, record);
                live.Engine.RequireSupportedMovieMaskRender(0, record);
                Require(live.Session.IsMoviePlaying && live.Engine.LastFault is null && JsonElement.DeepEquals(saved, live.Engine.Capture()), "MSK draw selection blocked decoding/VM or mutated the active phase.");
            }
        }
        long frame = live.Engine.Frame;
        live.Session.CompleteMovie();
        restored.Session.CompleteMovie();
        Require(live.Engine.Frame == frame && live.Engine.Scene.NativeMovieMask is { Value: 1, Playback.AppliedRecord: 5, Playback.DrawRecord: 5 } && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Movie cleanup failed its native reset1 or fabricated callbacks/discarded CAM receipts.");
        live.Engine.RequireSupportedMovieMaskRender(1, 3);
        live.Engine.Step();
        restored.Engine.Step();
        Require(live.Session.State.Variables.GetValueOrDefault(6401) == 91 && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Movie completion did not resume the original VM after native cleanup.");
        live.Engine.LoadField(601, SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(live.Engine.Scene.NativeMovieMask is { Value: 1, Playback: null }, "Fresh field retained an old CAM binding or inferred its mask byte from prior playback.");
    }

    private static (GameSession Session, EventEngine Engine) CreateNativeMovieMask(NativeCharacterVitals vitals, NativeEconomy economy, (Jsm.Opcode, int)[] body, NativeMovieCameraFlags? flags = null, bool initialize = false, bool opaque = false, GameState? state = null)
    {
        List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 1)];
        if (opaque)
        {
            init.AddRange([(Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.EFFECTLOAD, 0)]);
        }
        if (initialize)
        {
            init.AddRange(body);
        }
        init.Add((Jsm.Opcode.RET, 8));
        List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1)];
        if (!initialize)
        {
            main.AddRange(body);
        }
        main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        GameSession session = state is null ? CreateDirectedLinearTurnSession(vitals, economy) : new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!);
        session.MarkReplayUnavailable("Declared native movie mask/CAM timing component; no original gameplay journal or MSK rendering claim.");
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4, LoadSoundResource = _ => SoundResourceStatus.Muted, MoviePlayback = () => session.State.MoviePlayback, ReadMovieCameraFlags = flags is null ? null : (_, _) => flags };
        engine.LoadField(600, SyntheticObjects([1], [init.ToArray(), main.ToArray()]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return (session, engine);
    }

    private static byte[] NativeMovieMaskCameraBytes(byte[] flags)
    {
        byte[] bytes = new byte[8 + 44 * flags.Length];
        bytes[0] = (byte)'F';
        bytes[1] = (byte)'8';
        bytes[2] = (byte)'P';
        bytes[3] = 127;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)(flags.Length - 1)));
        for (int record = 0; record < flags.Length; record++)
        {
            bytes[8 + record * 44 + 40] = flags[record];
        }
        return bytes;
    }

    private static void ExpectNativeMovieMaskRenderRequired(EventEngine engine, int decodedFrame)
    {
        Require(engine.RequireSupportedMovieMaskRender(1, decodedFrame), "Movie rendering silently omitted the required pre-CAM MSK depth prepass.");
    }

    private static void NativeMovieMaskMalformedRestore(NativeCharacterVitals vitals, NativeEconomy economy, (Jsm.Opcode, int)[] body, NativeMovieCameraFlags flags, GameState state, JsonElement saved)
    {
        foreach (string variation in new[] { "Owner", "Version", "Source", "DrawRecord", "DrawValue", "AppliedRecord", "CoherentFrameShift", "MissingValue", "MissingPlayback", "MissingDrawValue" })
        {
            var target = CreateNativeMovieMask(vitals, economy, body, flags, state: state);
            JsonElement before = target.Engine.Capture();
            JsonNode bad = JsonNode.Parse(saved.GetRawText())!;
            JsonNode mask = bad["Scene"]!["NativeMovieMask"]!;
            switch (variation)
            {
                case "Owner":
                    bad["Scene"]!["NativeMovieMask"] = null;
                    break;
                case "Version":
                    mask["Version"] = 0;
                    break;
                case "Source":
                    mask["Playback"]!["CameraSha256"] = new string('B', 64);
                    break;
                case "DrawRecord":
                    mask["Playback"]!["DrawRecord"] = 4;
                    break;
                case "DrawValue":
                    mask["Playback"]!["DrawValue"] = 0;
                    mask["Value"] = 0;
                    break;
                case "AppliedRecord":
                    mask["Playback"]!["AppliedRecord"] = 99;
                    break;
                case "CoherentFrameShift":
                    mask["Playback"]!["AppliedRecord"] = 2;
                    mask["Playback"]!["DrawRecord"] = 2;
                    mask["Playback"]!["DrawValue"] = 1;
                    mask["Value"] = 0;
                    break;
                case "MissingValue":
                    mask.AsObject().Remove("Value");
                    break;
                case "MissingPlayback":
                    mask.AsObject().Remove("Playback");
                    break;
                case "MissingDrawValue":
                    mask["Playback"]!.AsObject().Remove("DrawValue");
                    break;
            }
            try
            {
                target.Engine.Restore(JsonSerializer.SerializeToElement(bad));
                throw new InvalidOperationException($"Movie mask restore accepted malformed {variation}.");
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException)
            {
            }
            Require(JsonElement.DeepEquals(before, target.Engine.Capture()), "Malformed mask/CAM phase restore mutated the receiving VM.");
        }
    }

    private static void NativeMovieMaskRootClockRefusal(NativeCharacterVitals vitals, NativeEconomy economy, (Jsm.Opcode, int)[] body, NativeMovieCameraFlags flags, GameState state, JsonElement saved)
    {
        var target = CreateNativeMovieMask(vitals, economy, body, flags, state: state);
        JsonElement before = target.Engine.Capture();
        Require(target.Session.State.MoviePlayback is { FrameIndex: 2, IsPlaying: true, Completed: false, Skipped: false }, "Coherent clock-refusal fixture did not retain its actual decoded frame2 producer.");
        JsonNode bad = JsonNode.Parse(saved.GetRawText())!;
        JsonNode mask = bad["Scene"]!["NativeMovieMask"]!;
        mask["Playback"]!["AppliedRecord"] = 3;
        mask["Playback"]!["DrawRecord"] = 3;
        mask["Playback"]!["DrawValue"] = 0;
        mask["Value"] = 1;
        bad["CurrentMoviePlayback"]!["FrameIndex"] = 3;
        try
        {
            target.Engine.Restore(JsonSerializer.SerializeToElement(bad));
            throw new InvalidOperationException("Movie mask restore trusted a coherently shifted VM clock/CAM phase instead of the actual root playback clock.");
        }
        catch (InvalidDataException)
        {
        }
        Require(target.Session.State.MoviePlayback is { FrameIndex: 2, IsPlaying: true, Completed: false, Skipped: false } && JsonElement.DeepEquals(before, target.Engine.Capture()), "Coherent VM clock/CAM forgery changed root playback or the receiving VM before refusal.");
    }

    private static void NativeMovieMaskAtomicRefusals(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        foreach (string variation in new[] { "opaque", "missing-owner", "missing-cam" })
        {
            (Jsm.Opcode, int)[] body = variation == "opaque" ? [(Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.SETDCAMERA, 0)] : variation == "missing-cam" ? [(Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.MOVIEREADY, 0)] : [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.SETDCAMERA, 0)];
            var fixture = CreateNativeMovieMask(vitals, economy, body, opaque: variation == "opaque");
            if (variation == "missing-owner")
            {
                typeof(SceneEventState).GetProperty("NativeMovieMask")!.SetValue(fixture.Engine.Scene, null);
            }
            JsonElement? beforeFiber = null;
            JsonElement? beforeScene = null;
            fixture.Engine.CommandExecuted += command =>
            {
                if (command.Name == "NOP")
                {
                    JsonElement saved = fixture.Engine.Capture();
                    beforeFiber = saved.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
                    beforeScene = saved.GetProperty("Scene");
                }
            };
            for (int callback = 0; callback < 4 && fixture.Engine.LastFault is null; callback++)
            {
                try
                {
                    fixture.Engine.Step();
                }
                catch (EventScriptException)
                {
                    break;
                }
            }
            JsonElement after = fixture.Engine.Capture();
            Require(fixture.Engine.LastFault?.Opcode == (variation == "missing-cam" ? "MOVIEREADY" : "SETDCAMERA") && beforeFiber is JsonElement fiberBefore && beforeScene is JsonElement sceneBefore && JsonElement.DeepEquals(fiberBefore, after.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1)) && JsonElement.DeepEquals(sceneBefore, after.GetProperty("Scene")), "Unproved movie mask input/provider/owner changed its original PC, operands or scene before refusal.");
        }
    }
}
