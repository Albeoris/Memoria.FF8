using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeGlobalSoundBackend
{
    Bufferless
}

public sealed record NativeGlobalSoundAllocation([property: JsonRequired] byte? BeforeRotation, [property: JsonRequired] byte? AfterRotation, [property: JsonRequired] byte Voice);

/// <summary>The explicit managed bufferless backend owns native46B0F0 rotation and32 original-effect-ID words; transient output owns no native FF8 sound/music buffers or DirectMusic handles.</summary>
public sealed record NativeGlobalSoundProcess
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSpuProcess? Spu { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicPlaybackState? MusicPlayback { get; init; }

    public const string CurrentRule = "ff8-en-46b0f0-bufferless-process-pe-rotation1-v1";

    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required NativeGlobalSoundBackend Backend { get; init; }
    [JsonRequired]
    public required byte? Rotation { get; init; }
    [JsonRequired]
    public required ImmutableArray<uint?> OriginalEffectIds { get; init; }

    [JsonIgnore]
    public bool IsColdProcess => Spu is null && MusicPlayback?.IsColdProcess == true && Rotation == 1 && !OriginalEffectIds.IsDefault && OriginalEffectIds.Length == 32 && OriginalEffectIds.All(value => value == 0);

    public static NativeGlobalSoundProcess FromBufferlessColdProcess()
    {
        // B7F428's actual PE.data DWORD is1;1CD17C4's32 DWORDs are cold BSS zero.
        // This backend creates no native FF8 DirectSound voice/music buffers or DirectMusic handles.
        // Native46A5D0 and all three46B970 queries therefore report0.
        return new NativeGlobalSoundProcess { Version = 1, Rule = CurrentRule, Backend = NativeGlobalSoundBackend.Bufferless, Rotation = 1, OriginalEffectIds = Enumerable.Repeat<uint?>(0u, 32).ToImmutableArray(), MusicPlayback = NativeMusicPlaybackState.FromColdProcess() };
    }

    public static NativeGlobalSoundProcess UnknownBufferless()
    {
        //Backend availability is explicit; prior native allocation and ID history are not inferred.
        return new NativeGlobalSoundProcess { Version = 1, Rule = CurrentRule, Backend = NativeGlobalSoundBackend.Bufferless, Rotation = null, OriginalEffectIds = Enumerable.Repeat<uint?>(null, 32).ToImmutableArray() };
    }

    public void Validate()
    {
        Spu?.Validate();
        MusicPlayback?.Validate();
        if (Version != 1 || Rule != CurrentRule || Backend != NativeGlobalSoundBackend.Bufferless || Rotation is >= 6 || OriginalEffectIds.IsDefault || OriginalEffectIds.Length != 32)
        {
            throw new InvalidDataException("Global sound requires the current explicit bufferless process and32 known-or-unknown physical effect IDs; native buffered availability is unsupported.");
        }
    }

    public int QueryMusicStatus()
    {
        Validate();
        // Native46B970 ORs46D530(0),46EB00 and46C870. This explicit backend
        // creates no original DS music buffer, segment handles or COM player.
        // Transient output belongs to separate host instances/processes.
        // TODO(TD-003): native buffered music status requires its real playback lifecycle.
        return 0;
    }

    public NativeGlobalSoundProcess RecordEffect(uint originalEffectId, uint rawMask, out NativeGlobalSoundAllocation allocation)
    {
        NativeGlobalSoundProcess selected = SelectVoice(rawMask, out allocation);
        return selected with { OriginalEffectIds = selected.OriginalEffectIds.SetItem(allocation.Voice, originalEffectId) };
    }

    public NativeGlobalSoundProcess SelectVoice(uint rawMask, out NativeGlobalSoundAllocation allocation)
    {
        Validate();
        uint mask = rawMask & 0xFFFFFF;
        byte? rotation = Rotation;
        byte voice;
        if (mask == 0)
        {
            byte before = Rotation ?? throw new NotSupportedException("Native46B0F0 automatic sound selection requires the retained B7F428 rotation; its history is unknown.");
            rotation = (byte)((before + 1) % 6);
            voice = (byte)(rotation.Value + 24);
        }
        else
        {
            voice = checked((byte)(BitOperations.TrailingZeroCount(mask) + 1));
        }
        allocation = new NativeGlobalSoundAllocation(Rotation, rotation, voice);
        return this with { Rotation = rotation };
    }

    public static void ValidateAllocation(uint rawMask, NativeGlobalSoundAllocation allocation)
    {
        uint mask = rawMask & 0xFFFFFF;
        bool valid = allocation.BeforeRotation is not >= 6 && allocation.AfterRotation is not >= 6;
        if (mask == 0)
        {
            valid &= allocation.BeforeRotation is byte before && allocation.AfterRotation == (before + 1) % 6 && allocation.Voice == allocation.AfterRotation + 24;
        }
        else
        {
            valid &= allocation.BeforeRotation == allocation.AfterRotation && allocation.Voice == BitOperations.TrailingZeroCount(mask) + 1;
        }
        if (!valid)
        {
            throw new InvalidDataException("Global sound request differs from the actual46B0F0 bufferless allocation.");
        }
    }
}
