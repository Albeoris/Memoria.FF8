using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A571900 heap allocation has a logical identity, not a portable native heap pointer.</summary>
public sealed record NativeBattleEffectHeapAllocation([property: JsonRequired] string Identity, [property: JsonRequired] ImmutableArray<byte> Data);

/// <summary>Process-retained571870 allocator and501230 publication fields. Only571870 proves the arena clear.</summary>
public sealed partial record NativeBattleEffectMemory([property: JsonRequired] NativeBattleEffectArena? Arena, [property: JsonRequired] int Cursor, [property: JsonRequired] int AllocationCount, [property: JsonRequired] ImmutableArray<NativeBattleEffectHeapAllocation?> Allocations, [property: JsonRequired] int Active, [property: JsonRequired] int SelectedIndex, [property: JsonRequired] uint SoundPendingFlags, [property: JsonRequired] uint SoundPublishedFlags)
{
    public NativeBattleEffectTextureState? Textures { get; init; }
    public byte? Parameter80 { get; init; }
    public short? CureContextF4 { get; init; }

    public static NativeBattleEffectMemory FromColdProcess() => new(null, 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0) { Textures = NativeBattleEffectTextureState.FromColdProcess(), Parameter80 = 0, CureContextF4 = 0, PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GuardianWorkBuffer = NativeBattleGuardianWorkBuffer.FromColdProcess(), EffectIo = NativeBattleEffectIoState.FromColdProcess(), GuardianCasterBank = 0, QuezacotlPause = 0, QuezacotlSecondaryHeads = [0, 0, 0, 0, 0], SummonStream = NativeBattleSummonStreamState.FromColdProcess() };

    public void Validate()
    {
        if (Cursor is < 0 or > NativeBattleEffectArena.Length || AllocationCount is < 0 or > 256 || Active is not (0 or 1) || SelectedIndex is < 0 or >= 400 || Allocations.IsDefault || Allocations.Length != 256 || Allocations.Any(allocation => allocation is not null && (string.IsNullOrEmpty(allocation.Identity) || allocation.Data.IsDefault)))
        {
            throw new InvalidDataException("Native effect allocator requires its retained cursor, count and256 physical heap slots.");
        }
        Arena?.Validate();
        Textures?.Validate();
        PrimitiveProcess?.Validate();
        GuardianWorkBuffer?.Validate();
        QuezacotlExecutableMemory?.Validate();
    }

    public NativeBattleEffectMemory BeginLoad(int selectedIndex)
    {
        Validate();
        ImmutableArray<NativeBattleEffectHeapAllocation?> allocations = Allocations;
        for (int index = 0; index < AllocationCount; index++)
        {
            allocations = allocations.SetItem(index, null);
        }
        return this with { Arena = NativeBattleEffectArena.Clear(), Cursor = 0, AllocationCount = 0, Allocations = allocations, Active = 1, SelectedIndex = selectedIndex, EffectIo = EffectIo is { } io ? io with { Callback = 0x534150 } : null };
    }

    public NativeBattleEffectMemory LoadIntoArena(ReadOnlySpan<byte> bytes)
    {
        Validate();
        NativeBattleEffectArena arena = Arena ?? throw new InvalidDataException("Native571B80 requires the arena established by571870.");
        return this with { Arena = arena.Write(Cursor, bytes), Cursor = checked(Cursor + bytes.Length) };
    }

    public NativeBattleEffectMemory PublishSound()
    {
        //501230: the authenticated PC46C040 callback literally returns0; this is not an audio-duration approximation.
        return this with { SoundPublishedFlags = SoundPendingFlags | (SoundPublishedFlags & 0x80000000), SoundPendingFlags = 0 };
    }
}
