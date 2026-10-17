using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMusicEntryTests
{
    [Theory]
    [InlineData(0, 0, 0, false, false)]
    [InlineData(0, 7, 7, false, false)]
    [InlineData(0, 5, 7, false, true)]
    [InlineData(-1, 7, 7, true, true)]
    [InlineData(-1, 7, 0, true, false)]
    [InlineData(1, 5, 255, false, true)]
    public void Entry_keeps_early_stop_separate_from_the_plain_play_comparison(int eventChannel, int priorId, byte selected, bool earlyStop, bool changed)
    {
        NativeGlobalSoundProcess sound = Sound(priorId);
        NativeMusicProcessState buffers = Buffers();
        NativeBattleMusicEntryPreparation result = NativeBattleMusicEntry.Prepare52CE30(512, 0, buffers, selected, eventChannel, sound, Resources());
        NativeMusicPlaybackDecision decision = result.Receipt.Playback!;
        Assert.Equal(earlyStop, result.Receipt.EarlyStop);
        Assert.Equal(changed, decision.PlayRequested);
        Assert.Equal(changed, decision.StopRequested);
        Assert.Equal(selected == 255 ? -1 : selected, decision.MusicId);
        Assert.Equal(63, decision.PlayGain);
        Assert.Equal(127, decision.FinalGain);
        Assert.Equal(127, result.SoundProcess.MusicPlayback!.Gain0);
        Assert.Equal(42, result.SoundProcess.MusicPlayback.Gain1);
        Assert.Equal(6, result.SoundProcess.MusicPlayback.CurrentMusicId1);
        Assert.Equal((ushort)0, result.MusicProcess.ReturnChannel);
        Assert.Equal((byte)1, result.MusicProcess.Buffers.Bank201);
        Assert.Equal((byte)0, result.MusicProcess.ReturnBank);
        Assert.Same(buffers.Buffer0, result.MusicProcess.Buffers.Buffer0);
        Assert.Equal(new NativeMusicBufferIdentity(unchecked((byte)(selected + 1)), NativeMusicBufferProducer.Battle52CE30), result.MusicProcess.Buffers.Buffer1);
        Assert.Equal(buffers.Pending, result.MusicProcess.Buffers.Pending);
        Assert.Equal(buffers.Requested213, result.MusicProcess.Buffers.Requested213);
        Assert.Equal(buffers.Bank202, result.MusicProcess.Buffers.Bank202);
        Assert.Equal(NativeMusicSelectionAuthority.Battle, result.MusicProcess.Buffers.SelectionAuthority);
        Assert.Equal(63, sound.MusicPlayback!.Gain0);
        Assert.Equal((byte)1, sound.Rotation);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void Keep_branch_skips_unknown_music_sources_but_still_commits_tail_and_three_shared_allocations(byte bank, byte returnBank)
    {
        NativeMusicProcessState buffers = new(1, null, null, null, bank, null);
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.UnknownBufferless() with { Rotation = 5 };
        NativeBattleMusicEntryResources resources = Resources() with { Template36 = default, MusicByteOffset = -1 };
        NativeBattleMusicEntryPreparation result = NativeBattleMusicEntry.Prepare52CE30(94, 0x10, buffers, null, null, sound, resources, ushort.MaxValue);
        Assert.Null(result.Receipt.Playback);
        Assert.Null(result.SoundProcess.MusicPlayback);
        Assert.Null(result.MusicProcess.Buffers.Buffer0);
        Assert.Null(result.MusicProcess.Buffers.Buffer1);
        Assert.Null(result.MusicProcess.Buffers.Complete214);
        Assert.Equal(bank, result.MusicProcess.Buffers.Bank201);
        Assert.Equal((byte)(returnBank ^ 1), result.MusicProcess.ReturnBank);
        Assert.Equal(ushort.MaxValue, result.MusicProcess.ReturnChannel);
        Assert.Equal(new uint[] { 132, 133, 134 }, result.Receipt.Sounds.Select(receipt => receipt.Sound));
        Assert.Equal(new byte[] { 24, 25, 26 }, result.Receipt.Sounds.Select(receipt => receipt.ReadCommittedVoice()));
        Assert.Equal((byte)2, result.SoundProcess.Rotation);
        Assert.True(result.Receipt.StopAllSounds);
        Assert.Equal(127u, result.Receipt.AllSoundVolume);
        Assert.Equal(0u, result.MusicProcess.EffectCache!.PlayingVoiceMask);
        Assert.Null(result.MusicProcess.EffectCache.VoiceRecords);
    }

    [Fact]
    public void Normal_tail_selects_original_tones_and_preserves_other_voice_IDs()
    {
        NativeGlobalSoundProcess sound = Sound(5);
        NativeBattleMusicEntryPreparation result = NativeBattleMusicEntry.Prepare52CE30(512, 0, Buffers(), 7, 0, sound, Resources());
        Assert.False(result.Receipt.SpecialSounds);
        Assert.Equal(new uint[] { 10, 11, 12 }, result.Receipt.Sounds.Select(receipt => receipt.Sound));
        Assert.Equal(new byte[] { 26, 27, 28 }, result.Receipt.Sounds.Select(receipt => receipt.ReadCommittedVoice()));
        Assert.Equal((byte)4, result.SoundProcess.Rotation);
        for (int index = 0; index < 32; index++)
        {
            Assert.Equal(index is >= 26 and <= 28 ? (uint?)(index - 16) : sound.OriginalEffectIds[index], result.SoundProcess.OriginalEffectIds[index]);
        }
    }

    [Theory]
    [InlineData((ushort)339, 17)]
    [InlineData((ushort)340, 99)]
    public void Landing_music_reads_only_the_actual_current_field_word(ushort field, int expected)
    {
        NativeBattleMusicEntryPreparation result = NativeBattleMusicEntry.Prepare52CE30(512, 0, Buffers(), 17, 0, Sound(5), Resources(), currentFieldWord: field);
        Assert.Equal(expected, result.Receipt.Playback!.MusicId);
        Assert.Equal(field, result.MusicProcess.CurrentFieldWord);
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicEntry.Prepare52CE30(512, 0, Buffers(), 17, 0, Sound(5), Resources()));
    }

    [Fact]
    public void Missing_reached_sources_refuse_without_publishing_or_advancing_the_input_process()
    {
        NativeMusicProcessState buffers = Buffers();
        NativeGlobalSoundProcess sound = Sound(5);
        string before = JsonSerializer.Serialize(sound);
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicEntry.Prepare52CE30(512, 0, buffers, null, 0, sound, Resources()));
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicEntry.Prepare52CE30(512, 0, buffers, 7, null, sound, Resources()));
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicEntry.Prepare52CE30(512, 0, buffers, 7, 0, sound with { MusicPlayback = null }, Resources()));
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicEntry.Prepare52CE30(512, 0x10, buffers with { Bank201 = null }, null, null, sound, Resources()));
        Assert.Throws<NotSupportedException>(() => NativeBattleMusicEntry.Prepare52CE30(512, 0x10, buffers, null, null, sound with { Rotation = null }, Resources()));
        Assert.Equal(before, JsonSerializer.Serialize(sound));
        Assert.Equal((byte)0, buffers.Bank201);
    }

    [Fact]
    public void Same_song_does_not_require_a_known_output_mode_and_entry_JSON_keeps_all_physical_state()
    {
        NativeBattleMusicEntryPreparation entry = NativeBattleMusicEntry.Prepare52CE30(94, 0, Buffers(), 7, 0, Sound(7), Resources());
        Assert.False(entry.Receipt.Playback!.PlayRequested);
        Assert.Null(entry.SoundProcess.MusicPlayback!.Mode0);
        NativeBattleMusicEntryPreparation restored = JsonSerializer.Deserialize<NativeBattleMusicEntryPreparation>(JsonSerializer.Serialize(entry))!;
        restored.MusicProcess.Validate();
        restored.SoundProcess.Validate();
        Assert.Equal(JsonSerializer.Serialize(entry), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (entry.Receipt with { Sounds = entry.Receipt.Sounds.SetItem(1, entry.Receipt.Sounds[0]) }).Validate());
    }

    [Fact]
    public void Battle_snapshot_is_live_music_authority_and_old_absence_does_not_fall_back_to_the_entry_definition()
    {
        NativeBattleMusicEntryPreparation entry = NativeBattleMusicEntry.Prepare52CE30(94, 0, Buffers(), 7, 0, Sound(5), Resources());
        BattleDefinition definition = new() { NativeMusicProcess = entry.MusicProcess, NativeGlobalSoundProcess = entry.SoundProcess, Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleSnapshot initial = session.Capture();
        Assert.Same(entry.MusicProcess, initial.NativeMusicProcess);
        NativeBattleMusicProcessState advanced = entry.MusicProcess with { ReturnChannel = ushort.MaxValue, ReturnBank = 1 };
        BattleSnapshot changed = initial with { NativeMusicProcess = advanced };
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(changed))!);
        Assert.Equal(JsonSerializer.Serialize(advanced), JsonSerializer.Serialize(restored.NativeMusicProcess));
        Assert.Equal((ushort)0, restored.Capture().Definition.NativeMusicProcess!.ReturnChannel);
        Assert.Null(BattleSession.Restore(initial with { NativeMusicProcess = null }).NativeMusicProcess);
        GameSession host = GameSession.CreateColdProcess();
        host.SetBattle(changed);
        Assert.Equal(JsonSerializer.Serialize(advanced), JsonSerializer.Serialize(host.State.NativeBattleMemory!.MusicProcess));
        host.SetBattle(null);
        Assert.Equal(JsonSerializer.Serialize(advanced), JsonSerializer.Serialize(host.State.NativeBattleMemory!.MusicProcess));
    }

    internal static NativeBattleMusicEntryResources Resources()
    {
        // Explicit synthetic protocol fixture reproducing B8EE6C's byte layout; installed authentication is tested in Assets.
        return new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Convert.FromHexString("0200000014000000080000001C00000008000000414B414F1B00900B414B414F1D000000").ToImmutableArray(), 24, Enumerable.Range(0, 42).Select(value => (ushort)value).Append((ushort)94).ToImmutableArray());
    }

    private static NativeMusicProcessState Buffers()
    {
        return new(1, true, 26, 1, 0, 255) { Buffer0 = new(4, NativeMusicBufferProducer.Field52CD10), Buffer1 = new(5, NativeMusicBufferProducer.WorldWmset5306E0) };
    }

    private static NativeGlobalSoundProcess Sound(int priorId)
    {
        return NativeGlobalSoundProcess.FromBufferlessColdProcess() with { MusicPlayback = new(63, 42, priorId, 6) };
    }
}
