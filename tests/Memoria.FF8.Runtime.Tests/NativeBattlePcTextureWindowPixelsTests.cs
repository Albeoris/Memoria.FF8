using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePcTextureWindowPixelsTests
{
    [Fact]
    public void Direct_page_reads_continue_linearly_into_the_retained_static_tail()
    {
        NativeBattleEffectTextureState owner = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 511, 1, 1, [0xCD, 0xAB]).Upload45BD30(960, 511, 1, 1, [0x34, 0x12]);
        owner = owner with { VramReadTail = owner.VramReadTail!.Value.SetItem(0, 0x78).SetItem(1, 0x56) };
        NativeBattlePcTextureWindowPixels captured = NativeBattlePcTextureWindowPixels.Capture(95, new(0, 0, 256, 256), owner);
        Assert.Equal((ushort)0xABCD, captured.ReadWord(1024, 510));
        Assert.Equal((ushort)0x1234, captured.ReadWord(960, 511));
        Assert.Equal((ushort)0x5678, captured.ReadWord(1024, 511));
        Assert.Equal((ushort)0, captured.ReadWord(1215, 511));
        captured.ValidateMapping(95, new(0, 0, 256, 256));
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(owner)))!);
        Assert.Equal(captured.Data.ToArray(), NativeBattlePcTextureWindowPixels.Capture(95, new(0, 0, 256, 256), restored).Data.ToArray());
        Assert.Equal(owner.VramReadTail, owner.Initialize500400().VramReadTail);
    }

    [Fact]
    public void Missing_warm_tail_is_required_only_when_the_actual_read_reaches_it()
    {
        NativeBattleEffectTextureState unknown = NativeBattleEffectTextureState.FromColdProcess() with { VramReadTail = null };
        NativeBattlePcTextureWindowPixels.Capture(31, new(0, 0, 256, 256), unknown);
        Assert.Throws<NotSupportedException>(() => NativeBattlePcTextureWindowPixels.Capture(95, new(0, 0, 256, 256), unknown));
        Assert.All(NativeBattleEffectTextureState.FromColdProcess().VramReadTail!.Value, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void SameImmutableOwnerAndEqualRegionReturnTheSameCapturedPixelObject()
    {
        NativeBattleEffectTextureState owner = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        NativeBattlePcTextureWindowPixels first = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 2), owner);
        Assert.Same(first, NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 2), owner));
        Assert.NotSame(first, NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 4, 2), owner));
        Assert.Same(first, NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 2), owner with { }));
        Assert.Equal((ushort)0x1234, first.ReadWord(0, 0));
        NativeBattlePcTextureWindowPixels[] concurrent = new NativeBattlePcTextureWindowPixels[16];
        Parallel.For(0, concurrent.Length, index => concurrent[index] = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 2), owner));
        Assert.All(concurrent, capture => Assert.Same(first, capture));
    }

    [Fact]
    public void NewStateRecordsWithTheSamePublicationAndExactInputsReuseWithoutAllocating()
    {
        NativeBattleEffectTextureState owner = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        NativeBattleEffectTextureState[] states = Enumerable.Range(0, 16).Select(_ => owner with { Count = 0 }).ToArray();
        NativeBattlePrimitiveTextureWindow rectangle = new(0, 0, 8, 2);
        NativeBattlePcTextureWindowPixels first = NativeBattlePcTextureWindowPixels.Capture(0, rectangle, owner);
        foreach (NativeBattleEffectTextureState state in states)
        {
            Assert.Same(owner.VramPublicationIdentity, state.VramPublicationIdentity);
            Assert.Same(first, NativeBattlePcTextureWindowPixels.Capture(0, rectangle, state));
        }
        for (int index = 0; index < 32; index++)
        {
            _ = NativeBattlePcTextureWindowPixels.Capture(0, rectangle, states[index % states.Length]);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            _ = NativeBattlePcTextureWindowPixels.Capture(0, rectangle, states[index % states.Length]);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void UnrelatedPublicationsReuseTheSameRegionAcrossHistoricalBranchesWithoutAllocating()
    {
        NativeBattleEffectTextureState[] versions = new NativeBattleEffectTextureState[16];
        versions[0] = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        for (int index = 1; index < versions.Length; index++)
        {
            versions[index] = versions[index - 1].Upload45BD30(512, 128, 1, 1, [checked((byte)index), 0]);
        }
        NativeBattlePrimitiveTextureWindow rectangle = new(0, 0, 8, 8);
        NativeBattlePcTextureWindowPixels first = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, versions[0]);
        foreach (NativeBattleEffectTextureState version in versions.Skip(1))
        {
            Assert.NotSame(versions[0].VramPublicationIdentity, version.VramPublicationIdentity);
            Assert.Equal(versions[0].LiveTextureSources, version.LiveTextureSources);
            Assert.Equal(versions[0].TextureRegistrations, version.TextureRegistrations);
            Assert.Same(first, NativeBattlePcTextureWindowPixels.Capture(64, rectangle, version));
        }
        for (int index = 0; index < 32; index++)
        {
            _ = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, versions[index % versions.Length]);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            _ = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, versions[index % versions.Length]);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Same(first, NativeBattlePcTextureWindowPixels.Capture(64, rectangle, versions[0]));
        Assert.Equal((ushort)0x1234, first.ReadWord(0, 0));
    }

    [Fact]
    public void ConcurrentHistoricalRequestsReadTheirOwnImmutablePublication()
    {
        NativeBattleEffectTextureState before = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        NativeBattleEffectTextureState after = before.Upload45BD30(0, 0, 1, 1, [0xCD, 0xAB]);
        NativeBattlePrimitiveTextureWindow rectangle = new(0, 0, 8, 8);
        NativeBattlePcTextureWindowPixels[] captures = new NativeBattlePcTextureWindowPixels[32];
        Parallel.For(0, captures.Length, index => captures[index] = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, index % 2 == 0 ? before : after));
        for (int index = 0; index < captures.Length; index++)
        {
            Assert.Equal(index % 2 == 0 ? (ushort)0x1234 : (ushort)0xABCD, captures[index].ReadWord(0, 0));
        }
    }

    [Fact]
    public void SeparatePhysicalOwnersAndDetachedSnapshotsRemainIsolated()
    {
        NativeBattleEffectTextureState first = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        NativeBattleEffectTextureState second = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        NativeBattlePrimitiveTextureWindow rectangle = new(0, 0, 8, 8);
        NativeBattlePcTextureWindowPixels original = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, first);
        NativeBattlePcTextureWindowPixels isolated = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, second);
        Assert.NotSame(original, isolated);
        Assert.Equal(original.Data.ToArray(), isolated.Data.ToArray());
        NativeBattleEffectTextureState snapshot = NativeBattleStorageSnapshots.Capture(first);
        NativeBattlePcTextureWindowPixels detached = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, snapshot);
        Assert.NotSame(original, detached);
        Assert.Equal(original.Data.ToArray(), detached.Data.ToArray());
        Assert.Same(detached, NativeBattlePcTextureWindowPixels.Capture(64, rectangle, snapshot with { }));
    }

    [Fact]
    public void ReplacingOneChangedRegionAtCapacityKeepsUnchangedSiblingCaptures()
    {
        NativeBattleEffectTextureState owner = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        NativeBattlePcTextureWindowPixels original = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 8), owner);
        NativeBattlePcTextureWindowPixels sibling = NativeBattlePcTextureWindowPixels.Capture(64, new(8, 0, 8, 8), owner);
        for (int index = 2; index < 128; index++)
        {
            _ = NativeBattlePcTextureWindowPixels.Capture(64, new(index % 16 * 8, index / 16 * 8, 8, 8), owner);
        }
        NativeBattleEffectTextureState changed = owner.Upload45BD30(0, 0, 1, 1, [0xCD, 0xAB]);
        NativeBattlePcTextureWindowPixels replacement = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 8), changed);
        Assert.NotSame(original, replacement);
        Assert.Equal((ushort)0xABCD, replacement.ReadWord(0, 0));
        Assert.Same(sibling, NativeBattlePcTextureWindowPixels.Capture(64, new(8, 0, 8, 8), changed));
        Assert.Equal((ushort)0x1234, original.ReadWord(0, 0));
    }

    [Fact]
    public void UpdatedEntriesDoNotPinEarlierPublicationVersions()
    {
        (NativeBattleEffectTextureState current, NativeBattlePcTextureWindowPixels captured, WeakReference<object> previous) = CaptureUnrelatedPublication();
        for (int index = 0; index < 3; index++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(previous.TryGetTarget(out _));
        Assert.Same(captured, NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 8), current));
        GC.KeepAlive(current);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (NativeBattleEffectTextureState Current, NativeBattlePcTextureWindowPixels Captured, WeakReference<object> Previous) CaptureUnrelatedPublication()
    {
        NativeBattleEffectTextureState previous = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        NativeBattlePcTextureWindowPixels captured = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 8), previous);
        WeakReference<object> publication = new(previous.VramPublicationIdentity);
        NativeBattleEffectTextureState current = previous.Upload45BD30(512, 128, 1, 1, [0xCD, 0xAB]);
        _ = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 8), current);
        return (current, captured, publication);
    }

    [Fact]
    public void SamePublicationDoesNotAliasChangedSourceRegistrationOrNullableProvenance()
    {
        NativeBattleTextureOrigin origin = new("sample.tex", 1, NativeBattleTextureRole.Pixels, 16, 8, 8);
        NativeBattleEffectTextureState owner = NativeBattleEffectTextureState.FromColdProcess() with
        {
            LiveTextureSources = [new(origin, 0, 0, 8, 8)],
            TextureRegistrations = [new(origin, 0, 0, 8, 8, [new(0, 0, 8, 8)])]
        };
        NativeBattlePrimitiveTextureWindow rectangle = new(0, 0, 8, 8);
        NativeBattlePcTextureWindowPixels first = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, owner);
        NativeBattleEffectTextureState changedSource = owner with { LiveTextureSources = [new(origin with { Member = 2 }, 0, 0, 8, 8)] };
        NativeBattlePcTextureWindowPixels source = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, changedSource);
        Assert.NotSame(first, source);
        Assert.Equal(1, first.LiveTextureSources!.Value[0].Origin.Member);
        Assert.Equal(2, source.LiveTextureSources!.Value[0].Origin.Member);
        NativeBattleEffectTextureState changedFrame = owner with { TextureRegistrations = owner.TextureRegistrations!.Value.SetItem(0, owner.TextureRegistrations.Value[0] with { CurrentFrame = 0 }) };
        NativeBattlePcTextureWindowPixels frame = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, changedFrame);
        Assert.NotSame(first, frame);
        Assert.Equal(-1, first.TextureRegistrations!.Value[0].CurrentFrame);
        Assert.Equal(0, frame.TextureRegistrations!.Value[0].CurrentFrame);
        NativeBattleEffectTextureState unknown = owner with { LiveTextureSources = null, TextureRegistrations = null };
        NativeBattlePcTextureWindowPixels missing = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, unknown);
        NativeBattlePcTextureWindowPixels empty = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, unknown with { LiveTextureSources = [], TextureRegistrations = [] });
        Assert.NotSame(missing, empty);
        Assert.Null(missing.LiveTextureSources);
        Assert.Empty(empty.LiveTextureSources!.Value);
        Assert.Throws<InvalidDataException>(() => NativeBattlePcTextureWindowPixels.Capture(64, rectangle, unknown with { LiveTextureSources = default(ImmutableArray<NativeBattleTextureSourceRegion>) }));
    }

    [Fact]
    public void ChangedTailCannotReuseAnEarlierCaptureEvenWhenVramPublicationMatches()
    {
        NativeBattleEffectTextureState owner = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattlePrimitiveTextureWindow rectangle = new(0, 0, 256, 256);
        NativeBattlePcTextureWindowPixels first = NativeBattlePcTextureWindowPixels.Capture(95, rectangle, owner);
        NativeBattleEffectTextureState changed = owner with { VramReadTail = owner.VramReadTail!.Value.SetItem(0, 0x34).SetItem(1, 0x12) };
        Assert.Same(owner.VramPublicationIdentity, changed.VramPublicationIdentity);
        NativeBattlePcTextureWindowPixels second = NativeBattlePcTextureWindowPixels.Capture(95, rectangle, changed);
        Assert.NotSame(first, second);
        Assert.Equal((ushort)0, first.ReadWord(1024, 511));
        Assert.Equal((ushort)0x1234, second.ReadWord(1024, 511));
        Assert.Throws<NotSupportedException>(() => NativeBattlePcTextureWindowPixels.Capture(95, rectangle, changed with { VramReadTail = null }));
    }

    [Fact]
    public void BoundedWindowEvictionDoesNotMutatePublishedCaptures()
    {
        NativeBattleEffectTextureState owner = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        NativeBattlePcTextureWindowPixels first = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 8), owner);
        for (int index = 1; index <= 128; index++)
        {
            _ = NativeBattlePcTextureWindowPixels.Capture(64, new(index % 16 * 8, index / 16 * 8, 8, 8), owner);
        }
        NativeBattlePcTextureWindowPixels afterEviction = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 8), owner);
        Assert.NotSame(first, afterEviction);
        Assert.Equal((ushort)0x1234, first.ReadWord(0, 0));
        Assert.Equal(first.Data.ToArray(), afterEviction.Data.ToArray());
        Assert.Same(afterEviction, NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 8), owner with { }));
    }

    [Fact]
    public void FreshLargeCaptureDoesNotAllocateASecondFullPixelArray()
    {
        NativeBattleEffectTextureState owner = NativeBattleEffectTextureState.FromColdProcess();
        _ = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 8), owner);
        NativeBattlePrimitiveTextureWindow rectangle = new(0, 0, 256, 256);
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattlePcTextureWindowPixels captured = NativeBattlePcTextureWindowPixels.Capture(64, rectangle, owner);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(131072, captured.Data.Length);
        Assert.InRange(allocated, captured.Data.Length, captured.Data.Length + 32768L);
    }

    [Fact]
    public void PublicationBucketDoesNotRetainObsoleteTextureStateRecords()
    {
        (NativeBattleEffectTextureState owner, NativeBattlePcTextureWindowPixels captured, WeakReference<NativeBattleEffectTextureState> previous) = CaptureTemporaryState();
        for (int index = 0; index < 3; index++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(previous.TryGetTarget(out _));
        Assert.Same(captured, NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 8), owner));
        GC.KeepAlive(owner);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (NativeBattleEffectTextureState Owner, NativeBattlePcTextureWindowPixels Captured, WeakReference<NativeBattleEffectTextureState> Previous) CaptureTemporaryState()
    {
        NativeBattleEffectTextureState owner = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleEffectTextureState previous = owner with { };
        NativeBattlePcTextureWindowPixels captured = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 8), previous);
        return (owner, captured, new(previous));
    }

    [Fact]
    public void ChangedOwnerProducesNewWordsWhilePriorCaptureAndRestoredWordsRemainUnchanged()
    {
        NativeBattleEffectTextureState before = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        NativeBattlePcTextureWindowPixels original = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), before);
        NativeBattleEffectTextureState after = before.Upload45BD30(0, 0, 1, 1, [0xCD, 0xAB]);
        NativeBattlePcTextureWindowPixels changed = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), after);
        Assert.NotSame(original, changed);
        Assert.Equal((ushort)0x1234, original.ReadWord(0, 0));
        Assert.Equal((ushort)0xABCD, changed.ReadWord(0, 0));
        NativeBattlePcTextureWindowPixels historical = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), before);
        Assert.Equal(original.Data.ToArray(), historical.Data.ToArray());
        Assert.Same(historical, NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), before));
        Assert.Equal(changed.Data.ToArray(), NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), after).Data.ToArray());
        string json = JsonSerializer.Serialize(original);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattlePcTextureWindowPixels>(json)));
        NativeBattleEffectTextureState restoredOwner = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(after)))!);
        NativeBattlePcTextureWindowPixels restoredCapture = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), restoredOwner);
        Assert.NotSame(changed, restoredCapture);
        Assert.Equal(changed.Data.ToArray(), restoredCapture.Data.ToArray());
        Assert.Same(restoredCapture, NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), restoredOwner));
    }
}
