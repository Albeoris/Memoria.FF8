namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModuleSound(uint Sound, uint Flags, NativeBattleModuleAnchor? Anchor)
{
    public NativeGlobalSoundAllocation? Allocation { get; init; }
    public byte? Pan { get; init; }
    public uint? RawPan { get; init; }
    public uint Volume { get; init; } = 127;

    public (NativeGlobalSoundProcess Process, NativeBattleModuleSound Sound) Commit46B2A0(NativeGlobalSoundProcess? process)
    {
        if (Allocation is not null)
        {
            throw new InvalidDataException("Native46B2A0 sound receipt has already consumed its global allocation.");
        }
        NativeGlobalSoundProcess current = process ?? throw new NotSupportedException("Native46B2A0 requires its retained global sound process; explicitly migrate the standalone snapshot.");
        NativeGlobalSoundProcess next = current.RecordEffect(Sound, Flags, out NativeGlobalSoundAllocation allocation);
        return (next, this with { Allocation = allocation });
    }

    public byte ReadCommittedVoice()
    {
        NativeGlobalSoundAllocation allocation = Allocation ?? throw new NotSupportedException("Native battle sound playback requires its committed46B0F0 allocation receipt.");
        NativeGlobalSoundProcess.ValidateAllocation(Flags, allocation);
        return allocation.Voice;
    }
}
