using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMusicEntryResources([property: JsonRequired] string ExecutableSha256, [property: JsonRequired] ImmutableArray<byte> Template36, [property: JsonRequired] int MusicByteOffset, [property: JsonRequired] ImmutableArray<ushort> SpecialEncounters)
{
    public void Validate(bool requireTemplate = true)
    {
        if (ExecutableSha256 != NativeFieldRuntimeProfile.FF8EnExecutableHash || SpecialEncounters.IsDefault || SpecialEncounters.Length != 43 || SpecialEncounters.Contains(ushort.MaxValue))
        {
            throw new InvalidDataException("Native52CE30 requires the authenticated original47CC70 encounter table.");
        }
        if (requireTemplate && (Template36.IsDefault || Template36.Length != 36 || MusicByteOffset != 24))
        {
            throw new InvalidDataException("Native52CE30 requires its original B8EE6C request and B8EE68 music byte offset.");
        }
    }

    public ImmutableArray<byte> SelectPayload(byte music203)
    {
        Validate();
        ImmutableArray<byte> request = Template36.SetItem(MusicByteOffset, unchecked((byte)(music203 + 1)));
        //530730(request,0) reads actual section offset/length at DWORD1/2.
        int offset = BinaryPrimitives.ReadInt32LittleEndian(request.AsSpan().Slice(4, 4));
        int length = BinaryPrimitives.ReadInt32LittleEndian(request.AsSpan().Slice(8, 4));
        if (offset < 0 || length < 5 || (long)offset + length > request.Length || offset + 4 != MusicByteOffset || !request.AsSpan().Slice(offset, 4).SequenceEqual("AKAO"u8))
        {
            throw new InvalidDataException("Native52CE30 request lost its selected actual AKAO section.");
        }
        return request.AsSpan().Slice(offset, length).ToArray().ToImmutableArray();
    }
}

public sealed record NativeBattleMusicEntryReceipt([property: JsonRequired] ushort Encounter, [property: JsonRequired] ushort Flags, [property: JsonRequired] byte PriorBank, [property: JsonRequired] byte Bank, [property: JsonRequired] byte ReturnBank, [property: JsonRequired] bool EarlyStop, [property: JsonRequired] bool SpecialSounds, [property: JsonRequired] NativeMusicPlaybackDecision? Playback, [property: JsonRequired] ImmutableArray<NativeBattleModuleSound> Sounds)
{
    [JsonIgnore]
    public bool KeptMusic => (Flags & 0x10) != 0;
    [JsonIgnore]
    public bool StopAllSounds => true;
    [JsonIgnore]
    public uint AllSoundVolume => 127;

    public void Validate()
    {
        if (PriorBank > 1 || Bank != (KeptMusic ? PriorBank : PriorBank ^ 1) || ReturnBank != (Bank ^ 1) || KeptMusic && (EarlyStop || Playback is not null) || !KeptMusic && Playback is null || Sounds.IsDefault || Sounds.Length != 3)
        {
            throw new InvalidDataException("Native52CE30 receipt lost its actual keep/buffer/return/sound branches.");
        }
        Playback?.Validate();
        if (Playback is { } playback && (playback.Channel != 0 || playback.ConcertRequested || playback.PlayGain is null || playback.FinalGain != 127 || playback.StopRequested != playback.PlayRequested))
        {
            throw new InvalidDataException("Native52CE30 plain play requires channel0, its old gain, a separate changed-ID stop and final gain127.");
        }
        uint first = SpecialSounds ? 0x84u : 10u;
        for (int index = 0; index < Sounds.Length; index++)
        {
            NativeBattleModuleSound sound = Sounds[index];
            if (sound is null || sound.Sound != first + index || sound.Flags != 0 || sound.RawPan != 128 || sound.Pan != 128 || sound.Volume != 127 || sound.Anchor is not null)
            {
                throw new InvalidDataException("Native52CE30 requires its exact three encounter SFX calls in order.");
            }
            _ = sound.ReadCommittedVoice();
            if (index > 0 && Sounds[index - 1].Allocation!.AfterRotation != sound.Allocation!.BeforeRotation)
            {
                throw new InvalidDataException("Native52CE30 sound allocations lost their shared automatic rotation order.");
            }
        }
    }
}

public sealed record NativeBattleMusicEntryPreparation(NativeBattleMusicProcessState MusicProcess, NativeGlobalSoundProcess SoundProcess, NativeBattleMusicEntryReceipt Receipt);

public static class NativeBattleMusicEntry
{
    public static NativeBattleMusicEntryPreparation Prepare52CE30(ushort encounter, ushort flags, NativeMusicProcessState buffers, byte? music203, int? primaryChannel, NativeGlobalSoundProcess sound, NativeBattleMusicEntryResources resources, ushort? priorReturnChannel = null, ushort? currentFieldWord = null)
    {
        bool keep = (flags & 0x10) != 0;
        resources.Validate(!keep);
        buffers.Validate();
        sound.Validate();
        byte priorBank = buffers.Bank201 is byte bank && bank < 2 ? bank : throw new NotSupportedException("Native52CE30 tail requires actual BYTE201 even when music is kept.");
        bool earlyStop = false;
        ushort? returnChannel = priorReturnChannel;
        NativeMusicPlaybackDecision? decision = null;
        NativeGlobalSoundProcess nextSound = sound;
        if (!keep)
        {
            int channel = primaryChannel ?? throw new NotSupportedException("Native52CE30 requires event DWORD108 before its conditional early stop.");
            NativeMusicPlaybackState playback = sound.MusicPlayback ?? NativeMusicPlaybackState.Unknown();
            earlyStop = channel == -1;
            if (earlyStop)
            {
                playback = playback.Stop46B800(0);
            }
            ImmutableArray<byte> payload = resources.SelectPayload(music203 ?? throw new NotSupportedException("Native52CE30 requires the actual saved battle music BYTE203."));
            byte selected = checked((byte)(priorBank ^ 1));
            buffers = buffers.LoadBuffer(selected, new NativeMusicBufferIdentity(payload[4], NativeMusicBufferProducer.Battle52CE30)) with { Complete214 = 1, Bank201 = selected };
            int gain = playback.ReadGain(0);
            int? comparedId = playback.CurrentMusicId0;
            playback = playback.Play46B530(0, payload.AsSpan(), gain, () => currentFieldWord ?? throw new NotSupportedException("Native52CE30 song17 requires the actual current-field WORD1CD2FC0."));
            bool play = playback.CurrentMusicId0 != comparedId;
            playback = playback.SetGain46BB40(0, 127);
            decision = new NativeMusicPlaybackDecision(0, playback.CurrentMusicId0, play, play, false, gain, 127);
            nextSound = sound with { MusicPlayback = playback };
            returnChannel = 0;
        }
        buffers = buffers with { SelectionAuthority = NativeMusicSelectionAuthority.Battle };
        byte finalBank = buffers.Bank201!.Value;
        byte returnBank = checked((byte)(finalBank ^ 1));
        bool special = resources.SpecialEncounters.Contains(encounter);
        uint firstSound = special ? 0x84u : 10u;
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>(3);
        for (uint index = 0; index < 3; index++)
        {
            NativeBattleModuleSound request = new(firstSound + index, 0, null) { RawPan = 128, Pan = 128, Volume = 127 };
            (NativeGlobalSoundProcess allocated, NativeBattleModuleSound committed) = request.Commit46B2A0(nextSound);
            nextSound = allocated;
            sounds.Add(committed);
        }
        NativeBattleMusicEntryReceipt receipt = new(encounter, flags, priorBank, finalBank, returnBank, earlyStop, special, decision, sounds.MoveToImmutable());
        // Explicit bufferless availability is0; unobserved copied B40-byte metadata remains unknown.
        NativeBattleMusicProcessState process = new(buffers, returnBank, returnChannel) { CurrentFieldWord = currentFieldWord, EffectCache = new NativeBattleMusicEffectCache(0, null), Entry = receipt };
        process.Validate();
        return new(process, nextSound, receipt);
    }
}
