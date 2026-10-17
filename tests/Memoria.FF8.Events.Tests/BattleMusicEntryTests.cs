using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBattleMusicEntryContract()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidOperationException("Set MEMORIA_FF8_GAME_DIR to the installed game directory.");
        NativeBattleMusicEntryResources resources = new NativeMusicCatalog(directory).ReadBattleEntryResources();
        GameSession session = CreateNativeMusicSession();
        EventEngine engine = new(session);
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0));
        engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        SetControlledMusicChannels(engine, -1, 1);
        List<object> output = [];
        engine.AudioCommandExecuted += command => output.Add(command);
        PreparedNativeBattleMusicEntry prepared = engine.PrepareNativeBattleMusicEntry(94, 0, resources);
        string sceneBefore = engine.Capture().GetRawText();
        NativeGlobalSoundProcess soundBefore = session.GlobalSoundProcess!;
        try
        {
            engine.CommitNativeBattleMusicEntry(prepared, _ => throw new InvalidOperationException("controlled atomic host rejection"));
            throw new InvalidOperationException("Rejected battle entry unexpectedly committed.");
        }
        catch (InvalidOperationException exception) when (exception.Message == "controlled atomic host rejection")
        {
        }
        Require(engine.Capture().GetRawText() == sceneBefore && ReferenceEquals(session.GlobalSoundProcess, soundBefore) && output.Count == 0, "Rejected native battle entry partially changed field, global sound or output.");
        NativeMusicChannelState? channel108 = engine.Scene.MusicChannel108;
        NativeMusicChannelState? channel112 = engine.Scene.MusicChannel112;
        NativeMusicProcessState prior = engine.Scene.MusicProcess!;
        engine.CommitNativeBattleMusicEntry(prepared, candidate => session.SetGlobalSoundProcess(candidate.SoundProcess));
        Require(output.Count == 1 && ReferenceEquals(output[0], prepared.Native.Receipt) && ReferenceEquals(engine.Scene.MusicProcess, prepared.Native.MusicProcess.Buffers), "Native battle entry lost its single fresh output receipt or same physical buffer transfer.");
        NativeMusicProcessState published = engine.Scene.MusicProcess ?? throw new InvalidOperationException("Native battle entry did not publish its prepared music buffers.");
        Require(engine.Scene.MusicChannel108 == channel108 && engine.Scene.MusicChannel112 == channel112 && published.Pending == prior.Pending && published.Requested213 == prior.Requested213 && engine.Scene.NativeBattleMusic203 == 5, "Native52CE30 changed event channel/pending/saved-music fields that the native routine does not write.");
        Require(prepared.Native.Receipt.EarlyStop && prepared.Native.Receipt.Playback is { StopRequested: true, PlayRequested: true, MusicId: 5 }, "Native battle entry flattened the separate early stop and changed-ID plain-play stop.");
        JsonElement saved = engine.Capture();
        GameSession independent = CloneMusicSession(session.State);
        EventEngine clone = new(independent);
        clone.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        int restoredOutput = 0;
        clone.AudioCommandExecuted += _ => restoredOutput++;
        clone.Restore(saved);
        Require(JsonElement.DeepEquals(saved, clone.Capture()) && restoredOutput == 0, "Battle music entry restore replayed output or replaced its retained physical buffers.");
        try
        {
            engine.CommitNativeBattleMusicEntry(prepared, _ => throw new InvalidOperationException("Stale callback must not run."));
            throw new InvalidOperationException("Native battle entry accepted stale source owners twice.");
        }
        catch (InvalidOperationException exception) when (exception.Message == "Prepared native battle music no longer matches its actual source owners.")
        {
        }
        NativeBattleMusicProcessState live = prepared.Native.MusicProcess with { Buffers = prepared.Native.MusicProcess.Buffers with { Bank201 = 0 } };
        engine.SynchronizeNativeBattleMusicProcess(live);
        Require(ReferenceEquals(engine.Scene.MusicProcess, live.Buffers) && output.Count == 1, "Battle live music synchronization created an alternate buffer owner or replayed entry output.");
    }
}
