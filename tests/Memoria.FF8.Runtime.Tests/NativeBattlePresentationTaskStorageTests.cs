using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePresentationTaskStorageTests
{
    [Fact]
    public void ConsecutiveReservationsUseAlignedDisjointBanks()
    {
        NativeBattleEffectMemory original = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleOriginalEffectState effect = HostedEffect();
        (NativeBattleEffectMemory first, uint firstAddress) = NativeBattlePresentationTaskStorage.Reserve(original, effect, 13, 3);
        (NativeBattleEffectMemory second, uint secondAddress) = NativeBattlePresentationTaskStorage.Reserve(first, effect, 36, 25);

        Assert.Null(original.PresentationTaskNextAddress);
        Assert.Equal(0x80000000u, firstAddress);
        Assert.Equal(0x80000030u, first.PresentationTaskNextAddress);
        Assert.Equal(0x80000030u, secondAddress);
        Assert.Equal(0x800003C0u, second.PresentationTaskNextAddress);
        Assert.True(NativeBattlePresentationTaskStorage.IsHostedBank(firstAddress, 13, 3));
        Assert.True(NativeBattlePresentationTaskStorage.IsHostedBank(secondAddress, 36, 25));
    }

    [Theory]
    [InlineData(null, 0x80000130u)]
    [InlineData(0x80000010u, 0x80000130u)]
    [InlineData(0x80000200u, 0x80000200u)]
    public void MissingOrStaleCursorUsesOwnedCurrentAndDetachedExtents(uint? retainedCursor, uint expected)
    {
        NativeBattleEffectTaskPool live = NativeBattleEffectTaskPool.Initialize(0x1000, 0x80000010, 36, 2);
        NativeBattleEffectTaskPool detached = NativeBattleEffectTaskPool.Initialize(0x1010, 0x80000100, 13, 3);
        NativeBattleEffectTaskPool native = NativeBattleEffectTaskPool.Initialize(0x1020, 0x2000000, 12, 1);
        NativeBattleOriginalEffectState effect = HostedEffect() with { Pools = [live, native] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            PresentationTaskNextAddress = retainedCursor,
            DetachedReturnedTask = HostedEffect() with { Pools = [detached], ReturnedDescriptor = detached.DescriptorAddress },
            ReturnedTaskPointer1D96AAC = detached.DescriptorAddress
        };

        (NativeBattleEffectMemory next, uint address) = NativeBattlePresentationTaskStorage.Reserve(memory, effect, 12, 1);

        Assert.Equal(expected, address);
        Assert.Equal(expected + 16, next.PresentationTaskNextAddress);
        Assert.Equal(retainedCursor, memory.PresentationTaskNextAddress);
    }

    [Fact]
    public void LastAlignedBankCanEndAtTheDomainBoundary()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { PresentationTaskNextAddress = 0xEFFFFFF0 };
        NativeBattleOriginalEffectState effect = HostedEffect();
        (NativeBattleEffectMemory reserved, uint address) = NativeBattlePresentationTaskStorage.Reserve(memory, effect, 16, 1);

        Assert.Equal(0xEFFFFFF0u, address);
        Assert.Equal(0xF0000000u, reserved.PresentationTaskNextAddress);
        reserved.Validate();
        Assert.True(NativeBattlePresentationTaskStorage.IsHostedBank(address, 16, 1));
        Assert.False(NativeBattlePresentationTaskStorage.IsHostedBank(address, 17, 1));
        Assert.False(NativeBattlePresentationTaskStorage.IsHostedBank(0xF0000000, 12, 1));
        Assert.False(NativeBattlePresentationTaskStorage.IsHostedBank(0xF1000000, 12, 1));
        Assert.False(NativeBattlePresentationTaskStorage.IsHostedBank(0xF2000000, 12, 1));
        Assert.Throws<InvalidOperationException>(() => NativeBattlePresentationTaskStorage.Reserve(reserved, effect, 12, 1));
    }

    [Theory]
    [InlineData(11, 1)]
    [InlineData(12, 0)]
    [InlineData(12, -1)]
    [InlineData(12, 65536)]
    public void InvalidDimensionsRejectBeforeReservation(ushort stride, int capacity)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        Assert.False(NativeBattlePresentationTaskStorage.IsHostedBank(0x80000000, stride, capacity));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattlePresentationTaskStorage.Reserve(memory, HostedEffect(), stride, capacity));
        Assert.Null(memory.PresentationTaskNextAddress);
    }

    [Theory]
    [InlineData(0x7FFFFFF0u)]
    [InlineData(0x80000001u)]
    [InlineData(0xF0000010u)]
    [InlineData(0xFFFFFFF0u)]
    public void InvalidCursorsRejectInMemoryValidationAndReservation(uint cursor)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { PresentationTaskNextAddress = cursor };
        Assert.Throws<InvalidDataException>(memory.Validate);
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationTaskStorage.Reserve(memory, HostedEffect(), 12, 1));
    }

    [Fact]
    public void InvalidRetainedSpansAndUnownedEffectsReject()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleOriginalEffectState effect = HostedEffect();
        NativeBattleEffectTaskPool crossing = NativeBattleEffectTaskPool.Initialize(0x1000, 0xEFFFFFF0, 17, 1);
        NativeBattleEffectTaskPool entering = NativeBattleEffectTaskPool.Initialize(0x1000, 0x7FFFFFF0, 17, 1);
        NativeBattleEffectTaskPool unaligned = NativeBattleEffectTaskPool.Initialize(0x1000, 0x80000004, 12, 1);
        Assert.False(NativeBattlePresentationTaskStorage.IsHostedBank(0xFFFFFFF0, ushort.MaxValue, ushort.MaxValue));
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationTaskStorage.Reserve(memory, effect with { Pools = [crossing] }, 12, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationTaskStorage.Reserve(memory, effect with { Pools = [entering] }, 12, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationTaskStorage.Reserve(memory, effect with { Pools = [unaligned] }, 12, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattlePresentationTaskStorage.Reserve(memory, effect with { PresentationRoster = null }, 12, 1));
        Assert.Throws<InvalidOperationException>(() => NativeBattlePresentationTaskStorage.Reserve(memory, effect, ushort.MaxValue, ushort.MaxValue));
    }

    [Fact]
    public void NativeJsonOmitsTheCursorAndHostedRestoreContinuesItsReservation()
    {
        NativeBattleEffectMemory native = NativeBattleEffectMemory.FromColdProcess();
        string nativeJson = NativeBattleTestSnapshots.Json(native);
        using JsonDocument nativeDocument = JsonDocument.Parse(nativeJson);
        Assert.False(nativeDocument.RootElement.TryGetProperty(nameof(NativeBattleEffectMemory.PresentationTaskNextAddress), out _));
        NativeBattleEffectMemory legacy = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(nativeJson);
        Assert.Null(legacy.PresentationTaskNextAddress);

        NativeBattleOriginalEffectState effect = HostedEffect();
        (NativeBattleEffectMemory reserved, _) = NativeBattlePresentationTaskStorage.Reserve(legacy, effect, 36, 25);
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(reserved));
        restored.Validate();
        Assert.Equal(reserved.PresentationTaskNextAddress, restored.PresentationTaskNextAddress);
        (_, uint address) = NativeBattlePresentationTaskStorage.Reserve(restored, effect, 12, 1);
        Assert.Equal(0x80000390u, address);
    }

    private static NativeBattleOriginalEffectState HostedEffect()
    {
        BattleModelRoster roster = new([new BattleModelOwner("actor", true), null, null, null, null, null, null]);
        return new(5, 4, new(5, 1, 0), null, null) { PresentationRoster = roster };
    }
}
