using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMusicLoadContract()
    {
        NativeMusicSkipContract();
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.FADENONE, 0), (Jsm.Opcode.MUSICCHANGE, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        (GameSession Session, EventEngine Engine) Fresh(byte[]? bytes = null)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(74, bytes ?? content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        (GameSession session, EventEngine first) = Fresh();
        Require(first.Scene.MusicChannel108 is { ChannelId: -1, MusicId: 255 } && first.Scene.MusicChannel112 is { ChannelId: -1, MusicId: 255 } && session.WorldControls is { EventFlags104: 0x18, MusicKeep207: 0 } && first.Scene.MusicProcess is { Pending: null, Bank201: 0 }, "Fresh native bootstrap invented pending music or lost the original reset bytes.");
        first.Step();
        Require(first.Scene.MusicLoadRequest is { RawMusic: 300, Music: 44, PriorSecondChannel: -1, Loaded: true } && first.Scene.MusicPlayRequest is { ConsumedPending: true, Music: 44, PriorBank: 0, Bank: 1, PriorKeepBit400: true, Channel: 0 } && session.WorldControls is { EventFlags104: 0x18, MusicKeep207: 0 } && first.Scene.MusicProcess is { Pending: false, Requested213: 44, Complete214: 1, Bank201: 1 } && first.Scene.MusicChannel108 is { ChannelId: 0, MusicId: 44 } && first.Scene.MusicChannel112 is { ChannelId: -1, MusicId: 255 }, "Native pending load, byte narrowing, primary play or independent second-channel state changed.");
        Require(!session.State.Variables.ContainsKey(500), "Native MUSICCHANGE return3 failed to yield the caller.");
        Require(first.FieldFade is { Physical.Mode: 0, Saved.Mode: 0 }, "Proved music BYTE213 was mistaken for an overlapping fade WORD alias.");
        JsonElement snapshot = first.Capture();
        (GameSession resumedSession, EventEngine resumed) = Fresh();
        resumed.Restore(snapshot);
        first.Step();
        resumed.Step();
        Require(session.State.Variables[500] == 7 && JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Native music yield did not resume or restore identically.");

        byte[] noPending = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.MUSICCHANGE, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 501), (Jsm.Opcode.RET, 0));
        first.LoadField(100, noPending, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        first.Step();
        Require(session.State.Variables[501] == 9 && first.Scene.MusicPlayRequest is { ConsumedPending: false, Channel: null } && first.Scene.MusicChannel108 is { ChannelId: 0, MusicId: 44 } && first.Scene.MusicChannel112?.ChannelId == -1, "Already consumed MUSICCHANGE changed channels or introduced a yield.");

        byte[] stopped = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MUSICSTOP, 0), (Jsm.Opcode.PSHN_L, 44), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.CROSSMUSIC, 0), (Jsm.Opcode.RET, 0));
        first.LoadField(101, stopped, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        first.Step();
        Require(first.Scene.MusicPlayRequest is { Opcode: "CROSSMUSIC", ConsumedPending: true, Volume: 127, Duration: -2, Channel: 0 } && first.Scene.MusicChannel112?.ChannelId == -1 && first.Scene.MusicCrossfadeFrames == 0 && first.Scene.SoundMusicVolume197 == 127, "CROSSMUSIC operand widths, installed ignored duration or persistent volume bytes changed.");
        JsonNode malformed = JsonNode.Parse(first.Capture().GetRawText())!;
        malformed["Scene"]!["MusicLoadRequest"]!["Music"] = 43;
        string before = first.Capture().GetRawText();
        try
        {
            first.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Malformed pending music source was restored.");
        }
        catch (InvalidDataException)
        {
        }
        Require(before == first.Capture().GetRawText(), "Music checkpoint refusal mutated the current VM.");

        malformed = JsonNode.Parse(first.Capture().GetRawText())!;
        malformed["Scene"]!["MusicProcess"]!["Bank201"] = 1;
        try
        {
            first.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Music bank disagreement was restored.");
        }
        catch (InvalidDataException)
        {
        }
        Require(before == first.Capture().GetRawText(), "Music bank refusal mutated the current VM.");

        byte[] pending = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 26), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.MUSICCHANGE, 0), (Jsm.Opcode.RET, 0));
        (GameSession pendingSession, EventEngine pendingEngine) = Fresh(pending);
        pendingEngine.Step();
        Require(pendingSession.WorldControls is { EventFlags104: 0x418, MusicKeep207: 1 } && pendingEngine.Scene.MusicProcess is { Pending: true, Requested213: 26, Complete214: 1, Bank201: 0 }, "Synchronous music load lost its pending receipt before play.");
        (GameSession pendingResumedSession, EventEngine pendingResumed) = Fresh(pending);
        pendingResumed.Restore(pendingEngine.Capture());
        pendingEngine.Step();
        pendingResumed.Step();
        Require(JsonElement.DeepEquals(pendingEngine.Capture(), pendingResumed.Capture()) && pendingEngine.Scene.MusicChannel108 is { ChannelId: 0, MusicId: 26 }, "Pending music load did not restore and consume identically.");

        byte[] mixed = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 26), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.POPM_W, 213), (Jsm.Opcode.RET, 0));
        (GameSession mixedSession, EventEngine mixedEngine) = Fresh(mixed);
        try
        {
            mixedEngine.Step();
            throw new InvalidOperationException("A mixed-width store replaced known music bytes.");
        }
        catch (EventScriptException) when (mixedEngine.LastFault?.Opcode == "POPM_W")
        {
        }
        Require(mixedSession.State.Variables[213] == 26 && mixedEngine.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 4, "Rejected mixed-width music store consumed its operand or byte receipt.");

        GameSession ambiguousFadeSession = new(new GameState());
        ambiguousFadeSession.State.Variables[213] = 26;
        EventEngine ambiguousFade = new(ambiguousFadeSession, ambiguousFadeSession.State.Variables);
        ambiguousFade.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.FADENONE, 0), (Jsm.Opcode.RET, 0)), []);
        try
        {
            ambiguousFade.Step();
            throw new InvalidOperationException("A small untyped scalar was inferred to be a music BYTE.");
        }
        catch (EventScriptException) when (ambiguousFade.LastFault?.Opcode == "FADENONE")
        {
        }
        Require(ambiguousFade.FieldFade is null && ambiguousFadeSession.State.Variables[213] == 26, "Unknown fade refusal changed memory.");

        byte[] dual = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PHSPOWER, 0), (Jsm.Opcode.PSHN_L, 26), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.DUALMUSIC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 44), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.DUALMUSIC, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 501), (Jsm.Opcode.RET, 0));
        (GameSession dualSession, EventEngine dualEngine) = Fresh(dual);
        dualEngine.Step();
        Require(dualEngine.Scene.MusicPlayRequest is { Opcode: "DUALMUSIC", ConsumedPending: true, Channel: 1, Volume: 127, PriorBank: 0, Bank: 0, PriorKeepBit400: true } && dualEngine.Scene.MusicChannel108 is { ChannelId: -1, MusicId: 255 } && dualEngine.Scene.MusicChannel112 is { ChannelId: 1, MusicId: 26 } && dualSession.WorldControls is { EventFlags104: 0x218, MusicKeep207: 0 } && dualEngine.Scene.MusicProcess is { Pending: false }, "DUALMUSIC lost its secondary return, bank selection, byte identity or keep-bit branch.");
        Require(!dualEngine.Scene.MusicPlaying && !dualSession.State.Variables.ContainsKey(500) && dualEngine.Scene.SoundMusicVolume197 == 127 && dualEngine.Scene.SoundMusicVolume198 == 127, "Secondary playback changed primary state, volume bytes or native yield.");
        NativeWorldControlState dualControls = dualSession.State.WorldControls!;
        Require(dualControls.SourceDisabled && dualControls.ScriptBattleDisabled20 == false && dualControls.Menu209 == 0, "Music bit400 changes replaced independent native source, battle or menu controls.");
        JsonNode malformedDual = JsonNode.Parse(dualEngine.Capture().GetRawText())!;
        malformedDual["Scene"]!["MusicPlayRequest"]!["Channel"] = 0;
        string beforeDual = dualEngine.Capture().GetRawText();
        try
        {
            dualEngine.Restore(JsonSerializer.SerializeToElement(malformedDual));
            throw new InvalidOperationException("A secondary music request restored as primary.");
        }
        catch (InvalidDataException)
        {
        }
        Require(beforeDual == dualEngine.Capture().GetRawText(), "Malformed secondary request refusal mutated the VM.");
        (GameSession dualResumedSession, EventEngine dualResumed) = Fresh(dual);
        dualResumed.Restore(dualEngine.Capture());
        dualEngine.Step();
        dualResumed.Step();
        Require(JsonElement.DeepEquals(dualEngine.Capture(), dualResumed.Capture()) && dualSession.State.Variables[500] == 7, "DUALMUSIC post-yield restoration differs.");
        dualEngine.Step();
        Require(dualEngine.Scene.MusicLoadRequest is { PriorSecondChannel: 1, Loaded: false, Music: 44 } && dualEngine.Scene.MusicPlayRequest is { Opcode: "DUALMUSIC", ConsumedPending: false, Channel: null, Volume: 7 } && dualEngine.Scene.MusicChannel112 is { ChannelId: 1, MusicId: 26 } && dualSession.State.Variables[501] == 9 && dualSession.State.WorldControls == dualControls, "An active secondary channel loaded/replaced music, yielded a no-pending play or altered independent controls.");
        byte[] reopened = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MUSICSTOP, 0), (Jsm.Opcode.PSHN_L, 33), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.DUALMUSIC, 0), (Jsm.Opcode.RET, 0));
        dualEngine.LoadField(100, reopened, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        dualEngine.Step();
        Require(dualEngine.Scene.MusicLoadRequest is { Loaded: true, PriorSecondChannel: -1 } && dualEngine.Scene.MusicPlayRequest is { Opcode: "DUALMUSIC", ConsumedPending: true, Volume: 0, Bank: 0 } && dualEngine.Scene.MusicChannel112 is { ChannelId: 1, MusicId: 33 } && dualSession.State.WorldControls == dualControls, "Native stop did not reopen the secondary load branch or normal allocation lost music/control ownership.");

        foreach (byte keep in new byte[] { 0, 2, 255 })
        {
            (GameSession preservedSession, EventEngine preserved) = Fresh(pending);
            const uint originalFlags = 0xABCDF618;
            NativeWorldControlState originalControls = preservedSession.WorldControls! with { EventFlags104 = originalFlags, MusicKeep207 = keep, Menu209 = 0xE1 };
            preservedSession.SetWorldControls(originalControls);
            preserved.Step();
            uint loadedFlags = keep == 0 ? originalFlags | 0x400u : originalFlags & ~0x400u;
            Require(preservedSession.WorldControls == originalControls with { EventFlags104 = loadedFlags, MusicKeep207 = 1 } && preserved.Scene.MusicLoadRequest!.PriorKeep207 == keep, "MUSICLOAD did not use the current full BYTE207 or preserve every unrelated104 bit/menu byte.");
            preserved.Step();
            Require(preservedSession.WorldControls == originalControls with { EventFlags104 = originalFlags & ~0x400u, MusicKeep207 = (byte)(keep == 0 ? 0 : 1) } && preserved.Scene.MusicPlayRequest!.PriorKeepBit400 == (keep == 0), "Music play failed its exact inverse400-to207 write or replaced unrelated canonical controls.");
        }
        foreach (bool play in new[] { false, true })
        {
            byte[] rejectedContent = play ? SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 26), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.CROSSMUSIC, 0), (Jsm.Opcode.RET, 0)) : SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 26), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.RET, 0));
            (GameSession sourceSession, EventEngine sourceEngine) = Fresh(rejectedContent);
            sourceEngine.Step();
            (GameSession rejectedSession, _) = Fresh(rejectedContent);
            RejectingMusicControlServices service = new(rejectedSession);
            EventEngine rejected = new(service, rejectedSession.State.Variables);
            rejected.LoadField(74, rejectedContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            rejected.Restore(sourceEngine.Capture());
            NativeWorldControlState beforeControls = rejectedSession.WorldControls!;
            NativeMusicProcessState? beforeProcess = rejected.Scene.MusicProcess;
            NativeMusicLoadRequest? beforeLoad = rejected.Scene.MusicLoadRequest;
            NativeMusicPlayRequest? beforePlay = rejected.Scene.MusicPlayRequest;
            NativeMusicChannelState? beforeChannel = rejected.Scene.MusicChannel108;
            service.Reject = true;
            try
            {
                rejected.Step();
                throw new InvalidOperationException("Music ignored a rejected canonical control publication.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement rejectedFiber = rejected.Capture().GetProperty("Fibers")[0];
            Require(rejected.LastFault?.Opcode == (play ? "CROSSMUSIC" : "MUSICLOAD") && rejectedFiber.GetProperty("Position").GetInt32() == (play ? 7 : 4) && rejectedFiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(play ? new[] { 3, 255 } : new[] { 26 }) && rejectedSession.WorldControls == beforeControls && rejected.Scene.MusicProcess == beforeProcess && rejected.Scene.MusicLoadRequest == beforeLoad && rejected.Scene.MusicPlayRequest == beforePlay && rejected.Scene.MusicChannel108 == beforeChannel, "Rejected music control publication changed PC/operands, canonical flags, pending phase, request or channel.");
        }

        GameSession unknownSession = new(new GameState());
        EventEngine unknown = new(unknownSession, unknownSession.State.Variables);
        unknown.LoadField(100, content, []);
        try
        {
            unknown.Step();
            throw new InvalidOperationException("Unknown second music channel acquired a default pending-load branch.");
        }
        catch (EventScriptException) when (unknown.LastFault?.Opcode == "MUSICLOAD")
        {
        }
        Require(unknown.Scene.MusicLoadRequest is null && unknown.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 300, "Unknown music load consumed its operand or selected a default channel.");

        GameSession unknownDualSession = new(new GameState());
        EventEngine unknownDual = new(unknownDualSession, unknownDualSession.State.Variables);
        unknownDual.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.DUALMUSIC, 0), (Jsm.Opcode.RET, 0)), []);
        try
        {
            unknownDual.Step();
            throw new InvalidOperationException("DUALMUSIC invented an unknown pending-load branch.");
        }
        catch (EventScriptException) when (unknownDual.LastFault?.Opcode == "DUALMUSIC")
        {
        }
        Require(unknownDual.Scene.MusicPlayRequest is null && unknownDual.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 255, "Unknown DUALMUSIC consumed its volume operand.");
    }
    private sealed class RejectingMusicControlServices(GameSession session) : IEventServices
    {
        public bool Reject { get; set; }
        public bool IsMoviePlaying => false;
        public NativeWorldControlState? WorldControls => session.WorldControls;
        public NativeExamTimerState? ExamTimer => session.ExamTimer;
        public NativeSeeDState? SeeD => session.SeeD;
        public bool CanStoreSeeD => true;
        public NativeMessageVariables? MessageVariables => session.MessageVariables;
        public bool CanStoreMessageVariables => true;
        public NativeCardInventory? CardInventory => session.CardInventory;
        public bool CanStoreCardInventory => true;
        public bool CanStoreNativeEventOwners => true;
        public void SetExamTimer(NativeExamTimerState state) => session.SetExamTimer(state);
        public void SetSeeD(NativeSeeDState state) => session.SetSeeD(state);
        public void SetMessageVariables(NativeMessageVariables state) => session.SetMessageVariables(state);
        public void SetCardInventory(NativeCardInventory state) => session.SetCardInventory(state);
        public void SetNativeEventOwners(NativeWorldControlState controls, NativeSeeDState? seeD = null, NativeExamTimerState? timer = null, NativeCharacterVitals? characterVitals = null, NativeEconomy? economy = null, NativeGlobalSoundProcess? globalSoundProcess = null, NativeSalaryPresentation? salaryPresentation = null, NativeEventRandomState? eventRandom = null, NativeVibrationProcessState? vibrationProcess = null, NativeFieldFadeState? fieldFade = null, Dictionary<int, NativeMemoryValue>? nativeVariableValues = null) => session.SetNativeEventOwners(controls, seeD, timer, characterVitals, economy, globalSoundProcess, salaryPresentation, eventRandom, vibrationProcess, fieldFade, nativeVariableValues);

        public void SetWorldControls(NativeWorldControlState controls)
        {
            if (Reject)
            {
                throw new NotSupportedException("Test canonical music control publication rejection.");
            }
            session.SetWorldControls(controls);
        }

        public void RequestMovie(int index)
        {
        }

        public void RequestField(int fieldId, int entryPoint)
        {
        }

        public void ShowDialog(string text)
        {
        }
    }

}
