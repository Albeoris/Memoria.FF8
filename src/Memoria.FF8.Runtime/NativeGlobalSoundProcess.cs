using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeGlobalSoundBackend
{
    Bufferless
}

public sealed record NativeGlobalSoundAllocation([property: JsonRequired] byte BeforeRotation, [property: JsonRequired] byte AfterRotation, [property: JsonRequired] byte Voice);

/// <summary>The explicit managed bufferless backend owns native46B0F0 rotation and32 original-effect-ID words; transient output owns no native FF8 sound/music buffers or DirectMusic handles.</summary>
public sealed record NativeGlobalSoundProcess
{
    public const string CurrentRule = "ff8-en-46b0f0-bufferless-process-pe-rotation1-v1";

    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required NativeGlobalSoundBackend Backend { get; init; }
    [JsonRequired]
    public required byte Rotation { get; init; }
    [JsonRequired]
    public required ImmutableArray<uint> OriginalEffectIds { get; init; }

    [JsonIgnore]
    public bool IsColdProcess => Rotation == 1 && !OriginalEffectIds.IsDefault && OriginalEffectIds.Length == 32 && OriginalEffectIds.All(value => value == 0);

    public static NativeGlobalSoundProcess FromBufferlessColdProcess()
    {
        // B7F428's actual PE.data DWORD is1;1CD17C4's32 DWORDs are cold BSS zero.
        // This backend creates no native FF8 DirectSound voice/music buffers or DirectMusic handles.
        // Native46A5D0 and all three46B970 queries therefore report0.
        return new NativeGlobalSoundProcess { Version = 1, Rule = CurrentRule, Backend = NativeGlobalSoundBackend.Bufferless, Rotation = 1, OriginalEffectIds = Enumerable.Repeat(0u, 32).ToImmutableArray() };
    }

    public void Validate()
    {
        if (Version != 1 || Rule != CurrentRule || Backend != NativeGlobalSoundBackend.Bufferless || Rotation >= 6 || OriginalEffectIds.IsDefault || OriginalEffectIds.Length != 32)
        {
            throw new InvalidDataException("Global sound requires the current explicit bufferless process, retained allocator and all32 physical effect IDs; native buffered availability is unsupported.");
        }
    }

    public int QueryMusicStatus()
    {
        Validate();
        // Native46B970 ORs46D530(0),46EB00 and46C870. This explicit backend
        // creates no original DS music buffer, segment handles or COM player.
        // Transient output belongs to separate host instances/processes.
        // TODO(TD-003): — native buffered music status requires its real playback lifecycle.
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
        byte rotation = mask == 0 ? (byte)((Rotation + 1) % 6) : Rotation;
        byte voice = mask == 0 ? (byte)(rotation + 24) : checked((byte)(BitOperations.TrailingZeroCount(mask) + 1));
        allocation = new NativeGlobalSoundAllocation(Rotation, rotation, voice);
        return this with { Rotation = rotation };
    }

    public static void ValidateAllocation(uint rawMask, NativeGlobalSoundAllocation allocation)
    {
        uint mask = rawMask & 0xFFFFFF;
        if (allocation.BeforeRotation >= 6 || allocation.AfterRotation != (mask == 0 ? (allocation.BeforeRotation + 1) % 6 : allocation.BeforeRotation) || allocation.Voice != (mask == 0 ? allocation.AfterRotation + 24 : BitOperations.TrailingZeroCount(mask) + 1))
        {
            throw new InvalidDataException("Global sound request differs from the actual46B0F0 bufferless allocation.");
        }
    }
}
