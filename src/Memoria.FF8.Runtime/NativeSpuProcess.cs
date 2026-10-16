using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Authenticated EyesOnMe resource and actual playback outcome; no host playback handles are persisted.</summary>
public sealed record NativeSpuAudioAttempt([property: JsonRequired] string ResourceId, [property: JsonRequired] string SourceSha256, [property: JsonRequired] long PcmBytes, [property: JsonRequired] int SampleRate, [property: JsonRequired] int Channels, [property: JsonRequired] bool PlaybackStarted)
{
    public void Validate()
    {
        if (ResourceId != "EyesOnMe" || SourceSha256 is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || PcmBytes <= 0 || SampleRate <= 0 || Channels is < 1 or > 2)
        {
            throw new InvalidDataException("SPU audio receipt lost its EyesOnMe resource identity, source hash or decoded PCM format.");
        }
    }
}

public sealed record NativeSpuReadyOrigin([property: JsonRequired] int FieldId, [property: JsonRequired] string ProgramHash, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] long Frame, [property: JsonRequired] int Selector);

/// <summary>Native51F520/51F5B0 callback clocks with the selected EyesOnMe resource playback lifecycle.</summary>
public sealed record NativeSpuProcess([property: JsonRequired] int Version, [property: JsonRequired] NativeSpuReadyOrigin Origin, [property: JsonRequired] NativeSpuAudioAttempt Audio, [property: JsonRequired] ulong CallbackCount, [property: JsonRequired] uint Counter, [property: JsonRequired] ulong? LastSyncCallback, [property: JsonRequired] uint LastSyncCounter, [property: JsonRequired] bool AudioActive, [property: JsonRequired] bool Stopped)
{
    public static void ValidateSelector(int selector)
    {
        if (selector != 0)
        {
            throw new NotSupportedException("SPUREADY selector addresses outside the proved original B8DCA8 packet are unsupported; the full DWORD operand is preserved.");
        }
    }

    public void Validate()
    {
        if (Version != 1 || Origin is null || Origin.FieldId < 72 || Origin.ProgramHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || Origin.ObjectId < 0 || Origin.ScriptId < 0 || Origin.Operation < 0 || Origin.Frame < 0 || Origin.Selector != 0 || Audio is null || Counter != unchecked((uint)CallbackCount) || LastSyncCallback > CallbackCount || LastSyncCallback is null && LastSyncCounter != 0 || LastSyncCallback is ulong read && LastSyncCounter != unchecked((uint)read) || AudioActive != (Audio.PlaybackStarted && !Stopped))
        {
            throw new InvalidDataException("SPU process lost its original selector, preparation provenance, executed callback/read counters or audio resource outcome.");
        }
        Audio.Validate();
    }

    public static NativeSpuProcess Prepare(NativeSpuReadyOrigin origin, NativeSpuAudioAttempt audio)
    {
        ValidateSelector(origin.Selector);
        audio.Validate();
        NativeSpuProcess state = new(1, origin, audio, 0, 0, null, 0, audio.PlaybackStarted, false);
        state.Validate();
        return state;
    }

    public NativeSpuProcess AdvanceCallback()
    {
        Validate();
        return this with { CallbackCount = checked(CallbackCount + 1), Counter = unchecked(Counter + 1) };
    }

    public NativeSpuProcess ReadSync()
    {
        Validate();
        return this with { LastSyncCallback = CallbackCount, LastSyncCounter = Counter };
    }

    public NativeSpuProcess Stop()
    {
        Validate();
        return this with { AudioActive = false, Stopped = true };
    }

    public bool SyncWaits(int rawOperand)
    {
        Validate();
        //530460 clears307F, and flags2's530490 never starts an ordinary movie.
        //Without a competing ordinary MOVIE,5305C0 therefore reads that actual zero byte.
        return rawOperand > 0 && unchecked((uint)rawOperand) >= Counter;
    }
}
