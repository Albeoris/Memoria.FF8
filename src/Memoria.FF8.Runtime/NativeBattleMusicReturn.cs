using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMusicReturnReceipt([property: JsonRequired] ushort Flags, [property: JsonRequired] byte? SavedMusic199, [property: JsonRequired] NativeMusicPlaybackDecision? Playback, [property: JsonRequired] ushort? FadeChannel, [property: JsonRequired] int? FadeTarget, [property: JsonRequired] byte EffectiveDuration, [property: JsonRequired] uint RestoredEffectMask)
{
    [JsonIgnore]
    public bool KeptMusic => (Flags & 0x10) != 0;
    [JsonIgnore]
    public bool StopAllSounds => true;

    public void Validate()
    {
        Playback?.Validate();
        bool fade = !KeptMusic && SavedMusic199 == 255;
        bool cross = !KeptMusic && SavedMusic199 is < 255;
        if (RestoredEffectMask != 0 || KeptMusic && (SavedMusic199 is not null || Playback is not null || FadeChannel is not null || FadeTarget is not null || EffectiveDuration != 0) || !KeptMusic && SavedMusic199 is null || fade && (Playback is not null || FadeChannel is null || FadeTarget != 0 || EffectiveDuration != 60) || cross && (Playback is not { Channel: 0, ConcertRequested: false, StopRequested: true, PlayGain: not null, FinalGain: null } || FadeChannel is not null || FadeTarget is not null || EffectiveDuration != 0))
        {
            throw new InvalidDataException("Native52B4F0(0) requires its exact keep, return-word fade or saved199 cross-play branch and supported bufferless effect cache.");
        }
    }
}

public sealed record NativeBattleMusicReturnPreparation(NativeBattleMusicProcessState MusicProcess, NativeGlobalSoundProcess SoundProcess, NativeBattleMusicReturnReceipt Receipt);

public static class NativeBattleMusicReturn
{
    public static NativeBattleMusicReturnPreparation Prepare52B4F0(NativeBattleMusicProcessState process, NativeGlobalSoundProcess sound, ushort flags, byte? savedMusic199, byte? savedVolume197, NativeBattleMusicEntryResources? resources = null, Func<ushort>? readCurrentField = null)
    {
        process.Validate();
        sound.Validate();
        NativeBattleMusicEffectCache cache = process.EffectCache ?? throw new NotSupportedException("Native46BFD0 requires its actual entry-captured availability mask; missing history cannot be reconstructed.");
        if (cache.PlayingVoiceMask != 0)
        {
            throw new NotSupportedException("Native46BFD0 buffered effect restoration requires the actual saved voice records and buffer lifecycle.");
        }
        bool keep = (flags & 0x10) != 0;
        NativeMusicProcessState buffers = process.Buffers with { Complete214 = 1 };
        NativeGlobalSoundProcess nextSound = sound;
        NativeMusicPlaybackDecision? decision = null;
        ushort? fadeChannel = null;
        int? fadeTarget = null;
        byte duration = 0;
        if (!keep)
        {
            byte selected = savedMusic199 ?? throw new NotSupportedException("Native52B4F0(0) requires the retained saved primary music BYTE199.");
            NativeMusicPlaybackState playback = sound.MusicPlayback ?? NativeMusicPlaybackState.Unknown();
            if (selected == 255)
            {
                fadeChannel = process.ReturnChannel ?? throw new NotSupportedException("Native52B4F0(0) fade requires its actual unsigned return WORD1CD8FC4.");
                duration = 60;
                fadeTarget = 0;
                playback = playback.FadeGain46BBC0(fadeChannel.Value, duration, fadeTarget.Value);
            }
            else
            {
                byte bank = process.ReturnBank ?? throw new NotSupportedException("Native52B4F0(0) load requires its actual return BYTE1CFF6E9.");
                byte gain = savedVolume197 ?? throw new NotSupportedException("Native52B4F0(0) cross-play requires actual saved gain BYTE197.");
                NativeBattleMusicEntryResources original = resources ?? throw new NotSupportedException("Native52CD10 requires its actual original B8EE6C request template.");
                //52B4F0 forces201 opposite the return bank,52CD10(0) writes that opposite buffer,
                //then52CCE0 toggles201 back to the return bank before46B850.
                buffers = buffers with { Bank201 = checked((byte)(bank ^ 1)) };
                var payload = original.SelectPayload(selected);
                buffers = buffers.LoadBuffer(bank, new NativeMusicBufferIdentity(payload[4], NativeMusicBufferProducer.Field52CD10)) with { Bank201 = bank };
                playback = playback.Cross46B850(payload.AsSpan(), gain, readCurrentField);
                decision = new(0, playback.CurrentMusicId0, playback.CurrentMusicId0 != 0, true, false, gain, null);
            }
            nextSound = sound with { MusicPlayback = playback };
        }
        buffers = buffers with { SelectionAuthority = NativeMusicSelectionAuthority.BattleReturn };
        NativeBattleMusicReturnReceipt receipt = new(flags, keep ? null : savedMusic199, decision, fadeChannel, fadeTarget, duration, cache.PlayingVoiceMask);
        receipt.Validate();
        NativeBattleMusicProcessState next = process with { Buffers = buffers, Return = receipt };
        next.Validate();
        return new(next, nextSound, receipt);
    }
}
