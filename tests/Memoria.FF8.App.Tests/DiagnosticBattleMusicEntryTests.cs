using System.Collections.Immutable;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticBattleMusicEntryTests
{
    [Fact]
    public void Selected_music_commits_the_same_live_owners_and_dispatches_the_full_receipt_once()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine engine = new(session);
        NativeMusicProcessState source = engine.InitializeNewGameMusicOwners(NativeFieldRuntimeProfile.FF8En);
        NativeMusicChannelState channel = engine.Scene.MusicChannel108!;
        List<object> commands = [];
        List<string> calls = [];
        engine.AudioCommandExecuted += command =>
        {
            commands.Add(command);
            FieldAudioPresentation.DispatchNativeBattleMusicEntry(Assert.IsType<NativeBattleMusicEntryReceipt>(command), slot => calls.Add($"stop:{slot}"), playback => calls.Add($"play:{playback.MusicId}"), () => calls.Add("stop-sounds"), () => calls.Add("reset-gain"), sound => calls.Add($"sound:{sound.Sound}"));
        };
        PreparedNativeBattleMusicEntry prepared = engine.PrepareNativeDiagnosticBattleMusicEntry(512, 0, Resources(), 7, -1, null);

        Assert.Same(source, prepared.SourceBuffers);
        Assert.Same(session.GlobalSoundProcess, prepared.SourceSound);
        Assert.Same(source, engine.Scene.MusicProcess);
        Assert.Empty(commands);
        Assert.Throws<InvalidOperationException>(() => engine.CommitNativeBattleMusicEntry(prepared, _ => throw new InvalidOperationException("rejected")));
        Assert.Same(source, engine.Scene.MusicProcess);
        Assert.Empty(commands);

        engine.CommitNativeBattleMusicEntry(prepared, native => session.SetGlobalSoundProcess(native.SoundProcess));

        Assert.Same(prepared.Native.SoundProcess, session.GlobalSoundProcess);
        Assert.Same(prepared.Native.MusicProcess.Buffers, engine.Scene.MusicProcess);
        Assert.Same(prepared.Native.Receipt, engine.Scene.NativeBattleMusicEntry);
        Assert.Same(prepared.Native.Receipt, Assert.Single(commands));
        Assert.Equal(new[] { "stop:0", "play:7", "stop-sounds", "reset-gain", "sound:10", "sound:11", "sound:12" }, calls);
        Assert.Equal((byte)5, engine.Scene.NativeBattleMusic203);
        Assert.Same(channel, engine.Scene.MusicChannel108);
        Assert.Empty(engine.Actors);
        Assert.Null(engine.NativeRuntimeProfile);
        Assert.Null(session.State.EventState);
        Assert.Throws<InvalidOperationException>(() => engine.CommitNativeBattleMusicEntry(prepared, _ => Assert.Fail("Repeated commit published twice.")));
        Assert.Single(commands);
    }

    [Fact]
    public void Replaced_source_sound_refuses_publication_and_audio_dispatch()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine engine = new(session);
        engine.InitializeNewGameMusicOwners(NativeFieldRuntimeProfile.FF8En);
        PreparedNativeBattleMusicEntry prepared = engine.PrepareNativeDiagnosticBattleMusicEntry(512, 0, Resources(), 5, -1, null);
        session.SetGlobalSoundProcess(prepared.SourceSound with { });
        engine.AudioCommandExecuted += _ => Assert.Fail("Stale preparation emitted audio.");

        Assert.Throws<InvalidOperationException>(() => engine.CommitNativeBattleMusicEntry(prepared, _ => Assert.Fail("Stale preparation published.")));
        Assert.Null(engine.Scene.NativeBattleMusicEntry);
    }

    [Fact]
    public void Uninitialized_or_unauthenticated_owners_cannot_prepare_diagnostic_music()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine engine = new(session);
        Assert.Throws<NotSupportedException>(() => engine.PrepareNativeDiagnosticBattleMusicEntry(512, 0, Resources(), 5, -1, null));
        engine.InitializeNewGameMusicOwners(NativeFieldRuntimeProfile.FF8En);

        Assert.Throws<InvalidOperationException>(() => engine.PrepareNativeDiagnosticBattleMusicEntry(512, 0, Resources() with { ExecutableSha256 = new string('0', 64) }, 5, -1, null));
        Assert.Null(engine.Scene.NativeBattleMusicEntry);
    }

    private static NativeBattleMusicEntryResources Resources()
    {
        // Synthetic protocol fixture; original resource authentication is covered by installed-resource checks.
        return new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Convert.FromHexString("0200000014000000080000001C00000008000000414B414F1B00900B414B414F1D000000").ToImmutableArray(), 24, Enumerable.Range(0, 43).Select(value => (ushort)value).ToImmutableArray());
    }
}
