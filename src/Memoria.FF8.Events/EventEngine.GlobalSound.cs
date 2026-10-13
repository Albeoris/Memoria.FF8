using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public enum GlobalSoundBackendStatus
{
    Muted,
    Failed
}

public sealed record GlobalSoundLoopPolicy(uint PlaybackFlags, uint LoopStartPcmByte, uint LoopEndPcmByte, uint SampleRate, int DecodedBitsPerSample, int Channels, int? LoopTransitionDelayMilliseconds);
public sealed record GlobalSoundResource(int SampleIndex, GlobalSoundBackendStatus BackendStatus, GlobalSoundLoopPolicy LoopPolicy);
public sealed record GlobalSoundRequest(uint OriginalEffectId, uint RawChannelMask, uint ChannelMask, int Voice, uint RawPan, uint EffectivePan, uint Volume, int PitchPercent, GlobalSoundResource Resource);

public sealed partial class EventEngine
{
    private readonly Dictionary<long, GlobalSoundRequest> preparedGlobalSounds = [];

    /// <summary>Resolves the mapped global sample descriptor and an explicit muted/failed backend, independently of the field-local EFFECTLOAD bank.</summary>
    public Func<int, GlobalSoundResource?>? ResolveGlobalSoundResource { get; set; }

    private static int MapGlobalSoundSample(uint effectId)
    {
        uint group = effectId / 10000;
        if (group == 69)
        {
            return effectId == 690000 ? 2060 : checked((int)(effectId - 688040));
        }
        uint offset = group switch
        {
            1 => 2150,
            21 => 150,
            22 => 180,
            24 => 370,
            30 => 710,
            35 => 980,
            40 => 1230,
            41 => 1510,
            42 => 1920,
            50 => 2784,
            _ => 0
        };
        return checked((int)(effectId % 10000 + offset));
    }

    private static bool ValidGlobalSoundResource(GlobalSoundResource resource, int sample)
    {
        GlobalSoundLoopPolicy loop = resource.LoopPolicy;
        return resource.SampleIndex == sample && Enum.IsDefined(resource.BackendStatus) && loop.LoopEndPcmByte >= loop.LoopStartPcmByte && loop.SampleRate > 0 && loop.DecodedBitsPerSample > 0 && loop.Channels > 0 && loop.LoopTransitionDelayMilliseconds is null or >= 0;
    }

    private string? UnsupportedGlobalSound(Fiber fiber)
    {
        if (fiber.Stack.Count < 4 || ResolveGlobalSoundResource is null)
        {
            return "Unsupported EFFECTPLAY requires its four original operands and an explicit global audio backend";
        }
        int[] arguments = fiber.Stack.Take(4).Reverse().ToArray();
        uint effect = unchecked((uint)arguments[0]);
        uint rawMask = unchecked((uint)arguments[1]);
        uint mask = rawMask & 0xFFFFFF;
        if (mask == 0)
        {
            return "Unsupported EFFECTPLAY automatic voice pool requires native availability and rotating-allocation state";
        }
        int sample = MapGlobalSoundSample(effect);
        GlobalSoundResource? resource;
        try
        {
            resource = ResolveGlobalSoundResource(sample);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or ArithmeticException or NotSupportedException)
        {
            return $"Unsupported EFFECTPLAY global audio descriptor: {exception.Message}";
        }
        if (resource is null || !ValidGlobalSoundResource(resource, sample))
        {
            return "Unsupported EFFECTPLAY requires a valid original global sample descriptor and explicit muted/failed backend";
        }
        uint rawPan = unchecked((uint)arguments[2]);
        uint volume = unchecked((uint)arguments[3]);
        // Native4699A0 rejects invalid unsigned pan/volume but51FEA0 still
        // consumes and advances. The effect ID was already stored on the voice.
        if (volume > 127 || (rawPan >> 1) > 127)
        {
            resource = resource with { BackendStatus = GlobalSoundBackendStatus.Failed };
        }
        int voice = System.Numerics.BitOperations.TrailingZeroCount(mask) + 1;
        preparedGlobalSounds[fiber.Sequence] = new GlobalSoundRequest(effect, rawMask, mask, voice, rawPan, rawPan >> 1, volume, 100, resource);
        return null;
    }

    private void ExecuteGlobalSound(Fiber fiber, out int[] arguments)
    {
        GlobalSoundRequest request = preparedGlobalSounds[fiber.Sequence];
        preparedGlobalSounds.Remove(fiber.Sequence);
        arguments = Pop(fiber, 4);
        Scene.GlobalSoundRequests.Add(request);
        // This is the latest attempt/original effect ID, not a claim that an
        // audible voice or the requested sample is active after backend failure.
        Scene.GlobalSoundVoices[request.Voice] = request;
    }
}
