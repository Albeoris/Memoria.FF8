using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The singleton501B60 request, not a queue: each original producer overwrites only its reached operands.</summary>
public sealed record NativeBattleMusicCommandState([property: JsonRequired] bool? Pending, [property: JsonRequired] byte? Command, [property: JsonRequired] int? Duration, [property: JsonRequired] int? TargetGain, [property: JsonRequired] uint? TargetBufferAddress, [property: JsonRequired] bool? Loading)
{
    public static NativeBattleMusicCommandState Unknown() => new(null, null, null, null, null, null);

    public void Validate()
    {
        if (TargetBufferAddress is not null and not (0 or 0x1CE075C or 0x1CDC750))
        {
            throw new InvalidDataException("Native501C60 music target must retain its original pointer or remain unknown.");
        }
    }
}

public sealed record NativeBattleMusicCommandReceipt(byte Command, NativeMusicPlaybackDecision? Playback = null, ushort? GainChannel = null, int? Gain = null, int? InitialGain = null, int EffectiveDuration = 0, bool PauseOutput = false, bool ResumeOutput = false);
public sealed record NativeBattleMusicCommandStep(NativeBattleMusicProcessState State, NativeGlobalSoundProcess Sound, NativeBattleMusicCommandReceipt? Receipt);

public sealed partial record NativeBattleMusicProcessState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMusicCommandState? Commands { get; init; }

    partial void ValidateCommands()
    {
        Commands?.Validate();
        LoadSource?.Validate();
    }

    public NativeBattleMusicProcessState InitializeCommands501C60()
    {
        Validate();
        byte bank = ReturnBank ?? throw new NotSupportedException("Native501C60 requires its retained1CFF6E9 selector before selecting a target buffer.");
        NativeBattleMusicCommandState previous = Commands ?? NativeBattleMusicCommandState.Unknown();
        return this with { Commands = previous with { Pending = false, TargetBufferAddress = bank == 0 ? 0x1CE075Cu : 0x1CDC750u, Loading = false } };
    }

    public NativeBattleMusicProcessState QueueStop501A70()
    {
        NativeBattleMusicCommandState previous = Commands ?? NativeBattleMusicCommandState.Unknown();
        return this with { Commands = previous with { Pending = true, Command = 1 } };
    }

    public NativeBattleMusicProcessState QueuePlay501A70()
    {
        NativeBattleMusicCommandState previous = Commands ?? NativeBattleMusicCommandState.Unknown();
        return this with { Commands = previous with { Pending = true, Command = 2 } };
    }

    public NativeBattleMusicProcessState QueueGain501B10(int duration, int gain)
    {
        NativeBattleMusicCommandState previous = Commands ?? NativeBattleMusicCommandState.Unknown();
        return this with { Commands = previous with { Pending = true, Command = 3, Duration = duration, TargetGain = gain } };
    }

    public NativeBattleMusicProcessState QueueFade501B40(int duration)
    {
        NativeBattleMusicCommandState previous = Commands ?? NativeBattleMusicCommandState.Unknown();
        return this with { Commands = previous with { Pending = true, Command = 4, Duration = duration } };
    }

    public NativeBattleMusicCommandStep DrainCommands501B60(NativeGlobalSoundProcess sound, Func<ushort>? readCurrentField = null)
    {
        Validate();
        sound.Validate();
        NativeBattleMusicCommandState request = Commands ?? throw new NotSupportedException("Native501B60 requires its retained singleton request owner.");
        bool pending = request.Pending ?? throw new NotSupportedException("Native501B60 requires its retained pending BYTE before dispatch.");
        if (!pending)
        {
            return new(this, sound, null);
        }
        byte command = request.Command ?? throw new NotSupportedException("Native501B60 requires the pending request's actual command BYTE.");
        NativeMusicPlaybackState playback = sound.MusicPlayback ?? NativeMusicPlaybackState.Unknown();
        NativeBattleMusicProcessState next = this;
        NativeBattleMusicCommandState completed = request with { Pending = false };
        NativeBattleMusicCommandReceipt receipt;
        switch (command)
        {
            case 1:
                playback = playback.Stop46C060(0).Stop46C060(1);
                receipt = new(command);
                break;
            case 2:
                uint address = request.TargetBufferAddress ?? throw new NotSupportedException("Native501B60 play requires the actual501C60 target pointer.");
                byte bank = address switch
                {
                    0x1CE075C => 0,
                    0x1CDC750 => 1,
                    _ => throw new InvalidDataException("Native501B60 cannot play an uninitialized music target pointer.")
                };
                byte encoded = Buffers.ReadBuffer(bank).EncodedAkaoId;
                int gain = playback.ReadGain(0);
                NativeMusicPlaybackState played = playback.PlayEncoded46B530(0, encoded, gain, readCurrentField);
                bool changed = playback.CurrentMusicId0 != played.CurrentMusicId0;
                receipt = new(command, new(0, played.CurrentMusicId0, changed, changed, false, gain, null), PauseOutput: true);
                playback = played;
                completed = completed with { Loading = true };
                next = next with { ReturnChannel = 0 };
                break;
            case 3:
                int duration = request.Duration ?? throw new NotSupportedException("Native501B60 gain requires its full retained duration DWORD.");
                int target = request.TargetGain ?? throw new NotSupportedException("Native501B60 gain requires its retained target DWORD.");
                ushort channel = ReturnChannel ?? throw new NotSupportedException("Native501B60 gain requires its actual1CD8FC4 return WORD.");
                byte narrowed = unchecked((byte)duration);
                playback = duration == 0 ? playback.SetGain46BB40(channel, target) : playback.TransitionGain46BC50(channel, narrowed, 0, target);
                receipt = new(command, GainChannel: channel, Gain: target, InitialGain: duration == 0 ? null : 0, EffectiveDuration: narrowed < 50 ? 0 : narrowed, ResumeOutput: true);
                completed = completed with { Loading = false };
                break;
            case 4:
                byte fade = unchecked((byte)(request.Duration ?? throw new NotSupportedException("Native501B60 fade requires its retained duration DWORD.")));
                playback = playback.FadeGain46BBC0(0, fade, 0);
                receipt = new(command, GainChannel: 0, Gain: 0, EffectiveDuration: fade < 50 ? 0 : fade);
                break;
            default:
                return new(this with { Commands = completed }, sound, new(command));
        }
        return new(next with { Commands = completed }, sound with { MusicPlayback = playback }, receipt);
    }
}
