using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class ResourceSourceViewTests
{
    [Fact]
    public void PrefixReadsPreferFrozenOverlayAndDelegateUnchangedResourcesWithoutFind()
    {
        using PrefixSource source = new();
        using ResourceSourceView view = new(source, [new(new("archive/main/member.bin"), "bin", 2, "overlay", [1, 2, 3])]);

        ResourcePrefix overlay = Assert.IsType<ResourcePrefix>(view.ReadPrefix(new("archive/main/member.bin"), 2));
        Assert.Equal(new byte[] { 1, 2 }, overlay.Bytes);
        Assert.Equal(3, overlay.Length);
        Assert.Empty(Assert.IsType<ResourcePrefix>(view.ReadPrefix(new("archive/main/member.bin"), 0)).Bytes);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<ResourcePrefix>(view.ReadPrefix(new("archive/main/member.bin"), int.MaxValue)).Bytes);
        Assert.Equal(0, source.PrefixCalls);
        Assert.Equal(new byte[] { 8, 9 }, Assert.IsType<ResourcePrefix>(view.ReadPrefix(new("archive/other.bin"), 2)).Bytes);
        Assert.Equal(1, source.PrefixCalls);
        Assert.Throws<ArgumentOutOfRangeException>(() => view.ReadPrefix(new("archive/other.bin"), -1));
        view.Dispose();
        Assert.Equal(new byte[] { 8 }, Assert.IsType<ResourcePrefix>(source.ReadPrefix(new("archive/other.bin"), 1)).Bytes);
    }

    [Fact]
    public void ExclusiveScopeBlocksPrefixFallbackButExplicitMemberStillWins()
    {
        using PrefixSource source = new();
        using ResourceSourceView view = new(source,
        [
            new(new("archive/main.idx"), "idx", 1, "carrier", [0], new("archive/main")),
            new(new("archive/main/member.bin"), "bin", 2, "member", [1, 2])
        ]);

        Assert.Null(view.ReadPrefix(new("archive/main/absent.bin"), 10));
        Assert.Equal(new byte[] { 1 }, Assert.IsType<ResourcePrefix>(view.ReadPrefix(new("archive/main/member.bin"), 1)).Bytes);
        ResourceService candidate = new(view);
        Assert.Throws<FileNotFoundException>(() => candidate.ReadPrefix(new("archive/main/absent.bin"), 10));
        Assert.Equal(0, source.PrefixCalls);
    }

    private static readonly ResourceKey Replaced = new("battle/d0c000.dat");
    private static readonly ResourceKey Original = new("battle/d1c000.dat");

    [Fact]
    public void CandidateViewSnapshotsSelectionsAndPreservesBorrowedDescriptorAndFallback()
    {
        using BorrowedSource source = new();
        List<EncodedResource> selected = [new(Replaced, "dat", 7, "candidate", [4, 5, 6])];
        using ResourceSourceView view = new(source, selected);
        selected.Clear();

        Assert.Same(source.Descriptor, view.Descriptor);
        ResourceContent replacement = Assert.IsType<ResourceContent>(view.Find(Replaced));
        Assert.Equal("candidate", replacement.Provenance);
        Assert.Equal(7, replacement.Revision);
        using Stream input = replacement.OpenRead();
        Assert.Equal(4, input.ReadByte());
        Assert.Equal(0, source.FindCalls);

        Assert.Same(source.Original, view.Find(Original));
        Assert.Null(view.Find(new("battle/missing.dat")));
        Assert.Equal(2, source.FindCalls);
    }

    [Fact]
    public void DisposingViewDoesNotDisposeBorrowedSourceOrChangeItsResources()
    {
        using BorrowedSource source = new();
        ResourceSourceView view = new(source, [new(Replaced, "dat", 7, "candidate", [4])]);

        view.Dispose();

        Assert.Equal(0, source.DisposeCalls);
        Assert.Same(source.Original, source.Find(Original));
        ResourceService active = new(source);
        Assert.Same(source, active.GetSource<BorrowedSource>());
        Assert.Same(source.Original, active.Get(Original));
    }

    private sealed class PrefixSource : IResourceSource, IResourcePrefixSource
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("prefix", GameDataEdition.Classic, []);
        public int PrefixCalls { get; private set; }
        public ResourceContent? Find(ResourceKey key) => throw new InvalidOperationException("Prefix lookup must not open ordinary source resources.");

        public ResourcePrefix? ReadPrefix(ResourceKey key, int maximumBytes)
        {
            PrefixCalls++;
            return new ResourcePrefix(new byte[] { 8, 9, 10 }.Take(maximumBytes).ToArray(), 3);
        }

        public void Dispose()
        {
        }
    }

    private sealed class BorrowedSource : IResourceSource
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("original", GameDataEdition.Classic, ["classic-ffnx"], "en");
        public ResourceContent Original { get; } = new(ResourceSourceViewTests.Original, "dat", 1, "installed", () => new MemoryStream([1, 2, 3], false));
        public int FindCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public ResourceContent? Find(ResourceKey key)
        {
            ObjectDisposedException.ThrowIf(DisposeCalls > 0, this);
            FindCalls++;
            return key == ResourceSourceViewTests.Original ? Original : null;
        }

        public void Dispose()
        {
            DisposeCalls++;
        }
    }
}
