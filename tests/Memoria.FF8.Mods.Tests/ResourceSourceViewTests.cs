using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class ResourceSourceViewTests
{
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
