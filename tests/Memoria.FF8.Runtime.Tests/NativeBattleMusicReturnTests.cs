using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMusicReturnTests
{
    [Fact]
    public void Keep_skips_unknown_saved_music_gain_selectors_and_global_playback_but_keeps_the_native_tail()
    {
        NativeBattleMusicProcessState state = State() with { Buffers = new(1, null, null, null, null, null), ReturnBank = null, ReturnChannel = null };
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.UnknownBufferless();
        NativeBattleMusicReturnPreparation returned = NativeBattleMusicReturn.Prepare52B4F0(state, sound, 0x10, null, null);
        Assert.Same(sound, returned.SoundProcess);
        Assert.Equal((byte)1, returned.MusicProcess.Buffers.Complete214);
        Assert.Null(returned.MusicProcess.Buffers.Bank201);
        Assert.Null(returned.Receipt.SavedMusic199);
        Assert.Null(returned.Receipt.Playback);
        Assert.Null(returned.Receipt.FadeChannel);
        Assert.Equal(0u, returned.Receipt.RestoredEffectMask);
        Assert.True(returned.Receipt.StopAllSounds);
        Assert.Same(returned.Receipt, returned.MusicProcess.Return);
    }

    [Theory]
    [InlineData((ushort)0, 0, 42)]
    [InlineData((ushort)1, 63, 0)]
    [InlineData(ushort.MaxValue, 0, 0)]
    public void Absent_saved_track_fades_the_actual_unsigned_return_channel_without_reading_bank_or_saved_gain(ushort channel, int first, int second)
    {
        NativeBattleMusicProcessState state = State() with { ReturnBank = null, ReturnChannel = channel };
        NativeGlobalSoundProcess sound = Sound();
        NativeBattleMusicReturnPreparation returned = NativeBattleMusicReturn.Prepare52B4F0(state, sound, 0, 255, null);
        Assert.Equal(channel, returned.Receipt.FadeChannel);
        Assert.Equal(0, returned.Receipt.FadeTarget);
        Assert.Equal((byte)60, returned.Receipt.EffectiveDuration);
        Assert.Null(returned.Receipt.Playback);
        Assert.Equal(first, returned.SoundProcess.MusicPlayback!.Gain0);
        Assert.Equal(second, returned.SoundProcess.MusicPlayback.Gain1);
        Assert.Equal(5, returned.SoundProcess.MusicPlayback.CurrentMusicId0);
        Assert.Equal(6, returned.SoundProcess.MusicPlayback.CurrentMusicId1);
        Assert.Same(state.Buffers.Buffer0, returned.MusicProcess.Buffers.Buffer0);
        Assert.Same(state.Buffers.Buffer1, returned.MusicProcess.Buffers.Buffer1);
    }

    [Theory]
    [InlineData((byte)0, (byte)0, false)]
    [InlineData((byte)0, (byte)7, true)]
    [InlineData((byte)1, (byte)7, true)]
    [InlineData((byte)1, (byte)254, true)]
    public void Saved_track_forces_load_to_the_return_bank_and_cross_stops_even_when_decoded_zero_will_not_play(byte bank, byte music, bool play)
    {
        NativeBattleMusicProcessState state = State() with { ReturnBank = bank, ReturnChannel = null, Buffers = State().Buffers with { Bank201 = null } };
        NativeGlobalSoundProcess sound = Sound();
        NativeBattleMusicReturnPreparation returned = NativeBattleMusicReturn.Prepare52B4F0(state, sound, 0, music, 100, NativeBattleMusicEntryTests.Resources());
        NativeMusicPlaybackDecision receipt = returned.Receipt.Playback!;
        Assert.Equal(music, returned.Receipt.SavedMusic199);
        Assert.Equal(new NativeMusicPlaybackDecision(0, music, play, true, false, 100, null), receipt);
        Assert.Equal(music == 0 ? 63 : 100, returned.SoundProcess.MusicPlayback!.Gain0);
        Assert.Equal(music, returned.SoundProcess.MusicPlayback.CurrentMusicId0);
        Assert.Equal(42, returned.SoundProcess.MusicPlayback.Gain1);
        Assert.Equal(bank, returned.MusicProcess.Buffers.Bank201);
        Assert.Equal((byte)1, returned.MusicProcess.Buffers.Complete214);
        Assert.Equal(new NativeMusicBufferIdentity((byte)(music + 1), NativeMusicBufferProducer.Field52CD10), returned.MusicProcess.Buffers.ReadBuffer(bank));
        Assert.Same(state.Buffers.ReadBuffer((byte)(bank ^ 1)), returned.MusicProcess.Buffers.ReadBuffer((byte)(bank ^ 1)));
        Assert.Equal(state.Buffers.Pending, returned.MusicProcess.Buffers.Pending);
        Assert.Equal(state.Buffers.Requested213, returned.MusicProcess.Buffers.Requested213);
        Assert.Equal(state.Buffers.Bank202, returned.MusicProcess.Buffers.Bank202);
        Assert.Null(returned.MusicProcess.ReturnChannel);
        Assert.Equal(sound.Rotation, returned.SoundProcess.Rotation);
        Assert.Equal(sound.OriginalEffectIds, returned.SoundProcess.OriginalEffectIds);
    }

    [Theory]
    [InlineData((ushort)339, 17)]
    [InlineData((ushort)340, 99)]
    public void Landing_track_reads_the_actual_current_field_only_after_the_native_source_selects_it(ushort field, int expected)
    {
        int reads = 0;
        NativeBattleMusicReturnPreparation result = NativeBattleMusicReturn.Prepare52B4F0(State(), Sound(), 0, 17, 127, NativeBattleMusicEntryTests.Resources(), () =>
        {
            reads++;
            return field;
        });
        Assert.Equal(1, reads);
        Assert.Equal(expected, result.Receipt.Playback!.MusicId);
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicReturn.Prepare52B4F0(State(), Sound(), 0, 17, 127, NativeBattleMusicEntryTests.Resources()));
    }

    [Fact]
    public void Cross_can_replace_unknown_global_history_without_fabricating_the_unwritten_other_channel()
    {
        NativeBattleMusicReturnPreparation result = NativeBattleMusicReturn.Prepare52B4F0(State(), NativeGlobalSoundProcess.UnknownBufferless(), 0, 7, 127, NativeBattleMusicEntryTests.Resources());
        Assert.Equal(7, result.SoundProcess.MusicPlayback!.CurrentMusicId0);
        Assert.Equal(127, result.SoundProcess.MusicPlayback.Gain0);
        Assert.Null(result.SoundProcess.MusicPlayback.CurrentMusicId1);
        Assert.Null(result.SoundProcess.MusicPlayback.Gain1);
        Assert.Null(result.SoundProcess.Rotation);
    }

    [Fact]
    public void Missing_reached_operands_and_buffered_cache_refuse_without_mutating_any_input()
    {
        NativeBattleMusicProcessState state = State();
        NativeGlobalSoundProcess sound = Sound();
        string before = NativeBattleTestSnapshots.Json(state);
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicReturn.Prepare52B4F0(state, sound, 0, null, null));
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicReturn.Prepare52B4F0(state with { ReturnChannel = null }, sound, 0, 255, null));
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicReturn.Prepare52B4F0(state with { ReturnBank = null }, sound, 0, 7, 127, NativeBattleMusicEntryTests.Resources()));
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicReturn.Prepare52B4F0(state, sound, 0, 7, null, NativeBattleMusicEntryTests.Resources()));
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicReturn.Prepare52B4F0(state, sound, 0, 7, 127));
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicReturn.Prepare52B4F0(state with { EffectCache = null }, sound, 0x10, null, null));
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicReturn.Prepare52B4F0(state with { EffectCache = new(2, null) }, sound, 0x10, null, null));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(state));
    }

    [Fact]
    public void Return_JSON_keeps_the_same_buffers_and_does_not_infer_a_missing_old_receipt()
    {
        NativeBattleMusicReturnPreparation returned = NativeBattleMusicReturn.Prepare52B4F0(State(), Sound(), 0, 7, 127, NativeBattleMusicEntryTests.Resources());
        NativeBattleMusicReturnPreparation restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleMusicReturnPreparation>(NativeBattleTestSnapshots.Json(returned))!;
        restored.MusicProcess.Validate();
        Assert.Equal(NativeBattleTestSnapshots.Json(returned), NativeBattleTestSnapshots.Json(restored));
        Assert.Null(State().Return);
        Assert.Throws<InvalidDataException>(() => (returned.Receipt with { FadeChannel = 0 }).Validate());
    }

    [Fact]
    public void Atomic_field_transport_clears_live_battle_and_preserves_the_same_retained_music_and_global_owners()
    {
        GameSession host = GameSession.CreateColdProcess();
        host.StartNewGame();
        BattleSession battle = new(new() { NativeMusicProcess = State(), NativeGlobalSoundProcess = Sound(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        host.SetBattle(battle.Capture());
        NativeBattleMusicReturnPreparation returned = NativeBattleMusicReturn.Prepare52B4F0(State(), host.GlobalSoundProcess!, 0, 7, 127, NativeBattleMusicEntryTests.Resources());
        NativeBattleFieldReturnOwners owners = new(returned, host.WorldControls!, host.SeeD!, host.ExamTimer!, host.SalaryPresentation!, host.VibrationProcess!, host.FieldFade!, host.EventMemory);
        GameState before = host.State;
        Assert.Throws<InvalidDataException>(() => host.SetNativeBattleFieldReturn(owners with { Music = returned with { MusicProcess = returned.MusicProcess with { ReturnBank = 2 } } }));
        Assert.Same(before, host.State);
        NativeBattleRetainedMemory reloaded = host.State.NativeBattleMemory! with { FileStaging = NativeBattleFileStaging.Unknown().Write(0x18DD018, [1, 2, 3, 4]), FieldTextureAnimations = new(new string('A', 64), []) };
        Assert.Throws<InvalidDataException>(() => host.SetNativeBattleFieldReturn(owners with { ReloadedMemory = reloaded with { EffectIoResult = 19 } }));
        Assert.Same(before, host.State);
        NativeBattleActionHeaderSlot changedHeader = reloaded.ActionPackets.Headers[0] with { Payload = reloaded.ActionPackets.Headers[0].Payload.SetItem(0, 0xA5) };
        NativeBattleRetainedMemory changedBytes = reloaded with { ActionPackets = reloaded.ActionPackets with { Headers = reloaded.ActionPackets.Headers.SetItem(0, changedHeader) } };
        InvalidDataException changedOwner = Assert.Throws<InvalidDataException>(() => host.SetNativeBattleFieldReturn(owners with { ReloadedMemory = changedBytes }));
        Assert.Contains("unrelated retained battle owner", changedOwner.Message);
        Assert.Same(before, host.State);
        owners = owners with { ReloadedMemory = reloaded };
        host.SetNativeBattleFieldReturn(owners);
        Assert.Null(host.State.Battle);
        Assert.Equal(NativeBattleTestSnapshots.Json(reloaded.FileStaging), NativeBattleTestSnapshots.Json(host.State.NativeBattleMemory!.FileStaging));
        Assert.Equal(NativeBattleTestSnapshots.Json(reloaded.FieldTextureAnimations), NativeBattleTestSnapshots.Json(host.State.NativeBattleMemory.FieldTextureAnimations));
        Assert.Same(returned.MusicProcess, host.State.NativeBattleMemory!.MusicProcess);
        Assert.Same(returned.SoundProcess, host.GlobalSoundProcess);
        Assert.Same(owners.Vibration, host.State.NativeBattleMemory.VibrationProcess);
        Assert.Equal(NativeBattleTestSnapshots.Json(host.State), NativeBattleTestSnapshots.Json(new GameSession(JsonSerializer.Deserialize<GameState>(NativeBattleTestSnapshots.Json(host.State))!).State));
    }

    private static NativeBattleMusicProcessState State()
    {
        return new(new(1, true, 26, 1, 1, 255) { Buffer0 = new(5, NativeMusicBufferProducer.Field52CD10), Buffer1 = new(6, NativeMusicBufferProducer.Battle52CE30) }, 0, 0) { EffectCache = new(0, null) };
    }

    private static NativeGlobalSoundProcess Sound()
    {
        return NativeGlobalSoundProcess.FromBufferlessColdProcess() with { MusicPlayback = new(63, 42, 5, 6) };
    }
}
