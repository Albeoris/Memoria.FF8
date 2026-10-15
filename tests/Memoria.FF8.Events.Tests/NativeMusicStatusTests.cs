using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMusicStatusContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        MusicStatusCountingServices countedServices = new();
        EventEngine counted = new(countedServices, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        counted.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.MUSICSTATUS, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        counted.Step();
        Require(countedServices.QueryCount == 1, "MUSICSTATUS preflight executed an extra backend status query.");

        NativeGlobalSoundProcess backend = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        NativeGlobalSoundProcess changed = backend.RecordEffect(91, 0, out _);
        Require(changed.QueryMusicStatus() == 0 && changed.Rotation == 2 && changed.OriginalEffectIds[26] == 91, "Music status changed the shared allocator or inferred status from effect allocation.");
        try
        {
            (backend with { Backend = (NativeGlobalSoundBackend)1 }).QueryMusicStatus();
            throw new InvalidOperationException("Music status accepted an unimplemented native buffered backend.");
        }
        catch (InvalidDataException)
        {
        }
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 22), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.MUSICCHANGE, 0), (Jsm.Opcode.PSHN_L, 333), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 777), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.MUSICSTATUS, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 5000), (Jsm.Opcode.PSHI_L, 3), (Jsm.Opcode.POPM_L, 5001), (Jsm.Opcode.PSHI_L, 7), (Jsm.Opcode.POPM_L, 5002), (Jsm.Opcode.RET, 0));
        (GameSession Session, EventEngine Engine) Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.SetCharacterVitals(initial.DeepCopy());
            EventEngine engine = new(session);
            engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var live = Create();
        live.Engine.Step();
        Require(live.Engine.Scene.MusicPlaying && live.Engine.Scene.MusicChannel108 is { ChannelId: 0, MusicId: 22 }, "Music-status component did not execute its actual logical music producer.");
        JsonElement pending = live.Engine.Capture();
        NativeMusicProcessState? process = live.Engine.Scene.MusicProcess;
        NativeGlobalSoundProcess? sound = live.Session.State.GlobalSoundProcess;
        long calls = live.Session.State.ExamTimer!.ClockCalls;
        IEventServices services = live.Session;
        Require(services.QueryNativeMusicStatus() == 0 && services.QueryNativeMusicStatus() == 0 && live.Session.State.ExamTimer.ClockCalls == calls && live.Session.State.GlobalSoundProcess == sound, "Backend music query changed process/clock state or inferred logical playing.");
        var resumed = Create();
        resumed.Engine.Restore(pending);
        live.Engine.Step();
        resumed.Engine.Step();
        Require(JsonElement.DeepEquals(live.Engine.Capture(), resumed.Engine.Capture()) && live.Session.State.Variables[5000] == 0 && live.Session.State.Variables[5001] == 333 && live.Session.State.Variables[5002] == 777 && live.Engine.Scene.MusicPlaying && live.Engine.Scene.MusicProcess == process && live.Session.State.GlobalSoundProcess == sound, "MUSICSTATUS failed zero-operand return2, preserved registers/process or independent restore.");

        byte[] opaque = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.EFFECTLOAD, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.MUSICSTATUS, 0), (Jsm.Opcode.POPI_L, 2), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        GameSession opaqueSession = GameSession.CreateColdProcess();
        EventEngine opaqueEngine = new(opaqueSession) { LoadSoundResource = _ => SoundResourceStatus.Muted };
        opaqueEngine.Scene.Party.Clear();
        opaqueEngine.Scene.Party.AddRange([0, 255, 255]);
        opaqueEngine.LoadField(600, opaque, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        opaqueEngine.Step();
        opaqueEngine.Step();
        JsonElement opaqueFiber = opaqueEngine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(opaqueFiber.GetProperty("Stack").GetArrayLength() == 0 && opaqueFiber.GetProperty("Results").GetProperty("2").Deserialize<NativeScalarValue>()!.ActorInfoOrigin is { ResultRegister: 1 } && opaqueFiber.GetProperty("Results").GetProperty("0").GetInt32() == 0, "Zero-operand MUSICSTATUS consumed or materialized an unrelated unknown stack value.");

        GameSession cold = GameSession.CreateColdProcess();
        GameSession missing = new(cold.State with { GlobalSoundProcess = null });
        EventEngine unknown = new(missing);
        unknown.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.MUSICSTATUS, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        try
        {
            unknown.Step();
            throw new InvalidOperationException("MUSICSTATUS manufactured an absent native backend.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement failed = unknown.Capture().GetProperty("Fibers")[0];
        Require(unknown.LastFault is { Opcode: "MUSICSTATUS", Operation: 2 } && failed.GetProperty("Position").GetInt32() == 2 && failed.GetProperty("Stack")[0].GetInt32() == 123 && !failed.GetProperty("Results").TryGetProperty("0", out _) && missing.State.GlobalSoundProcess is null, "Unknown-backend MUSICSTATUS did not preserve PC, stack and result bank before refusal.");
    }

    private sealed class MusicStatusCountingServices : IEventServices
    {
        public NativeGlobalSoundProcess GlobalSoundProcess { get; } = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        public int QueryCount { get; private set; }
        public bool IsMoviePlaying => false;

        public int QueryNativeMusicStatus()
        {
            QueryCount++;
            return GlobalSoundProcess.QueryMusicStatus();
        }

        public void RequestMovie(int index)
        {
            throw new InvalidOperationException("Unexpected movie in music-status component.");
        }

        public void RequestField(int fieldId, int entryPoint)
        {
            throw new InvalidOperationException("Unexpected field departure in music-status component.");
        }

        public void ShowDialog(string text)
        {
            throw new InvalidOperationException("Unexpected dialogue in music-status component.");
        }
    }

}
