using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public enum GlobalSoundBackendStatus
{
    Muted,
    Failed
}

public sealed record GlobalSoundLoopPolicy(uint PlaybackFlags, uint LoopStartPcmByte, uint LoopEndPcmByte, uint SampleRate, int DecodedBitsPerSample, int Channels, int? LoopTransitionDelayMilliseconds);
public sealed record GlobalSoundResource(int SampleIndex, GlobalSoundBackendStatus BackendStatus, GlobalSoundLoopPolicy LoopPolicy);
public sealed record GlobalSoundRequest(uint OriginalEffectId, uint RawChannelMask, uint ChannelMask, int Voice, uint RawPan, uint EffectivePan, uint Volume, int PitchPercent, GlobalSoundResource Resource, [property: System.Text.Json.Serialization.JsonRequired] NativeGlobalSoundAllocation? Allocation = null);
public sealed record PreparedGlobalSoundEmission(GlobalSoundRequest Request, NativeGlobalSoundProcess Process);

public sealed partial class EventEngine
{
    private readonly Dictionary<long, PreparedGlobalSoundEmission> preparedGlobalSounds = [];

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
        if (services.GlobalSoundProcess is not NativeGlobalSoundProcess process || !services.CanStoreGlobalSoundProcess)
        {
            return "EFFECTPLAY requires its explicit current global backend and persistent native allocator";
        }
        int[] arguments = fiber.Stack.Take(4).Reverse().ToArray();
        try
        {
            preparedGlobalSounds[fiber.Sequence] = PrepareGlobalSound(unchecked((uint)arguments[0]), unchecked((uint)arguments[1]), unchecked((uint)arguments[2]), unchecked((uint)arguments[3]), process);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or ArithmeticException or NotSupportedException)
        {
            return $"Unsupported EFFECTPLAY global audio descriptor: {exception.Message}";
        }
        return null;
    }

    public PreparedGlobalSoundEmission PrepareGlobalSound(uint effect, uint rawMask, uint rawPan, uint volume, NativeGlobalSoundProcess process)
    {
        process.Validate();
        if (ResolveGlobalSoundResource is null)
        {
            throw new NotSupportedException("Global sound requires its installed descriptor resolver.");
        }
        int sample = MapGlobalSoundSample(effect);
        GlobalSoundResource? resource = ResolveGlobalSoundResource(sample);
        if (resource is null || !ValidGlobalSoundResource(resource, sample))
        {
            throw new NotSupportedException("Global sound requires a valid original sample descriptor and explicit muted/failed output.");
        }
        // Native4699A0 rejects invalid unsigned pan/volume but51FEA0 still
        // consumes and advances. The effect ID was already stored on the voice.
        if (volume > 127 || (rawPan >> 1) > 127)
        {
            resource = resource with { BackendStatus = GlobalSoundBackendStatus.Failed };
        }
        NativeGlobalSoundProcess next = process.RecordEffect(effect, rawMask, out NativeGlobalSoundAllocation allocation);
        return new PreparedGlobalSoundEmission(new GlobalSoundRequest(effect, rawMask, rawMask & 0xFFFFFF, allocation.Voice, rawPan, rawPan >> 1, volume, 100, resource, allocation), next);
    }

    private void ExecuteGlobalSound(Fiber fiber, out int[] arguments)
    {
        PreparedGlobalSoundEmission prepared = preparedGlobalSounds[fiber.Sequence];
        PublishGlobalSoundProcess(fiber, prepared.Process);
        preparedGlobalSounds.Remove(fiber.Sequence);
        arguments = Pop(fiber, 4);
        Scene.GlobalSoundRequests.Add(prepared.Request);
        AudioCommandExecuted?.Invoke(prepared.Request);
    }

    private void PublishGlobalSoundProcess(Fiber fiber, NativeGlobalSoundProcess process)
    {
        try
        {
            services.SetGlobalSoundProcess(process);
        }
        catch (NotSupportedException exception)
        {
            throw NativeOwnerPublicationFault(fiber, exception);
        }
    }
}
