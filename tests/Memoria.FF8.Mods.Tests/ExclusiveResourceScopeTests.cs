using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class ExclusiveResourceScopeTests
{
    private static readonly ResourceKey Carrier = new("archive/main.pack");
    private static readonly ResourceKey Scope = new("main/section");
    private static readonly ResourceKey Member = new("main/section/member.bin");
    private static readonly ResourceKey Sibling = new("main/section-extra/member.bin");

    [Fact]
    public void CompleteScopeBlocksEveryInstalledAcquisitionButExplicitMembersStillWin()
    {
        using CountingSource source = new();
        ResourceService service = new(source);
        service.Prepare(null, encodedResources: [Archive(), new(Member, "bin", 2, "loose", [7, 8, 9])]).Commit();

        Assert.Same(source.Descriptor, service.Source);
        Assert.Same(source, service.GetSource<IResourcePrefixSource>());
        Assert.Equal(Scope, Archive().ExclusiveScope);
        Assert.Equal(new byte[] { 7, 8, 9 }, service.Read(Member));
        Assert.Equal(new byte[] { 7 }, service.ReadPrefix(Member, 1).Bytes);
        Assert.Equal(3, service.ReadPrefix(Member, 1).Length);
        ResourceKey omitted = new("main/section/omitted.bin");
        Assert.Throws<FileNotFoundException>(() => service.Get(omitted));
        Assert.Throws<FileNotFoundException>(() => service.Read(omitted));
        Assert.Throws<FileNotFoundException>(() => service.ReadPrefix(omitted, 1));
        Assert.Equal(0, source.FullReads);
        Assert.Equal(0, source.PrefixReads);

        Assert.Equal(new byte[] { 1, 2, 3 }, service.Read(Scope));
        Assert.Equal(new byte[] { 1, 2, 3 }, service.Read(Sibling));
        Assert.Equal(2, source.FullReads);
    }

    [Fact]
    public void SourceViewUsesTheSameClosedNamespaceWithoutOwningTheInstalledSource()
    {
        using CountingSource source = new();
        using (ResourceSourceView view = new(source, [Archive(), new(Member, "bin", 2, "loose", [7])]))
        {
            Assert.Same(source.Descriptor, view.Descriptor);
            Assert.Null(view.Find(new("main/section/omitted.bin")));
            ResourceContent member = Assert.IsType<ResourceContent>(view.Find(Member));
            Assert.Equal("loose", member.Provenance);
            Assert.Equal(0, source.FullReads);
            Assert.NotNull(view.Find(Sibling));
            Assert.NotNull(view.Find(Scope));
        }
        Assert.False(source.Disposed);
        Assert.NotNull(source.Find(Member));
    }

    [Theory]
    [InlineData("get")]
    [InlineData("read")]
    [InlineData("prefix")]
    public void AcquiredOriginalCannotBeHiddenByAnOutsideCarrier(string acquisition)
    {
        using CountingSource source = new();
        ResourceService service = new(source);
        Acquire(service, acquisition);

        ResourceReloadRequiredException error = Assert.Throws<ResourceReloadRequiredException>(() => service.Prepare(null, encodedResources: [Archive()]));

        Assert.Equal(new[] { Member }, error.Resources);
        Assert.Equal(0, service.Generation);
        Assert.Equal(new byte[] { 1, 2, 3 }, service.Read(Member));
    }

    [Theory]
    [InlineData("get")]
    [InlineData("read")]
    [InlineData("prefix")]
    public void DeniedAcquisitionCannotBeUnhiddenWithoutAFreshService(string acquisition)
    {
        using CountingSource source = new();
        ResourceService service = new(source);
        service.Prepare(null, encodedResources: [Archive()]).Commit();
        Assert.Throws<FileNotFoundException>(() => Acquire(service, acquisition));

        ResourceReloadRequiredException error = Assert.Throws<ResourceReloadRequiredException>(() => service.Prepare(null, encodedResources: []));

        Assert.Equal(new[] { Member }, error.Resources);
        Assert.Equal(1, service.Generation);
        Assert.Equal(0, source.FullReads);
        Assert.Equal(0, source.PrefixReads);
        ResourceService fresh = new(source);
        Assert.Equal(new byte[] { 1, 2, 3 }, fresh.Read(Member));
    }

    [Fact]
    public void LateSourceAcquisitionPreventsScopeCommitAndPreservesVisibility()
    {
        using CountingSource source = new();
        ResourceService service = new(source);
        ResourceService.PreparedResources candidate = service.Prepare(null, encodedResources: [Archive()]);
        Assert.Equal(new byte[] { 1, 2, 3 }, service.Read(Member));

        ResourceReloadRequiredException error = Assert.Throws<ResourceReloadRequiredException>(() => candidate.Commit());

        Assert.Equal(new[] { Member }, error.Resources);
        Assert.Equal(0, service.Generation);
        Assert.Equal(new byte[] { 1, 2, 3 }, service.Read(Member));
    }

    [Fact]
    public void StaleScopePreparationCannotHideInstalledMembers()
    {
        using CountingSource source = new();
        ResourceService service = new(source);
        ResourceService.PreparedResources stale = service.Prepare(null, encodedResources: [Archive()]);
        service.Prepare(null, encodedResources: []).Commit();

        Assert.Throws<InvalidOperationException>(() => stale.Commit());

        Assert.Equal(1, service.Generation);
        Assert.Equal(new byte[] { 1, 2, 3 }, service.Read(Member));
    }

    [Fact]
    public void FailedTexturePreparationDoesNotPublishCandidateScopes()
    {
        using CountingSource source = new();
        ResourceService service = new(source);
        service.GetTexture(new(new(new("main/image.bin"), "texture/0"), source.Descriptor, 1, "test", 2, 2, () => new PixelImage(2, 2)));

        Assert.Throws<InvalidDataException>(() => service.Prepare(new FailingResolver(), encodedResources: [Archive()]));

        Assert.Equal(0, service.Generation);
        Assert.Equal(new byte[] { 1, 2, 3 }, service.Read(Member));
    }

    [Fact]
    public void NullEncodedSelectionPreservesScopesAndEmptySelectionClearsUnconsumedScopes()
    {
        using CountingSource source = new();
        ResourceService retained = new(source);
        retained.Prepare(null, encodedResources: [Archive()]).Commit();
        retained.Prepare(null).Commit();
        Assert.Throws<FileNotFoundException>(() => retained.Read(Member));

        ResourceService cleared = new(source);
        cleared.Prepare(null, encodedResources: [Archive()]).Commit();
        cleared.Prepare(null, encodedResources: []).Commit();
        Assert.Equal(new byte[] { 1, 2, 3 }, cleared.Read(Member));
    }

    [Fact]
    public void IdenticalCarrierBytesCanChangeScopeMetadataUntilMemberVisibilityIsAcquired()
    {
        using CountingSource source = new();
        ResourceService service = new(source);
        service.Prepare(null, encodedResources: [new(Carrier, "pack", 1, "archive", [4])]).Commit();
        Assert.Equal(new byte[] { 4 }, service.Read(Carrier));
        List<ResourceChangeSet> changes = [];
        using IDisposable subscription = service.Subscribe(changes.Add);

        service.Prepare(null, encodedResources: [Archive()]).Commit();

        Assert.Contains(Carrier, Assert.Single(changes).Resources);
        Assert.Throws<FileNotFoundException>(() => service.Read(Member));
    }

    [Fact]
    public void RemovingOneOverlappingScopeKeepsDeniedMembersHiddenByRemainingScope()
    {
        using CountingSource source = new();
        ResourceService service = new(source);
        EncodedResource outer = new(new("archive/outer.pack"), "pack", 1, "outer", [5], new("main"));
        service.Prepare(null, encodedResources: [Archive(), outer]).Commit();
        Assert.Throws<FileNotFoundException>(() => service.Read(Member));

        service.Prepare(null, encodedResources: [outer]).Commit();

        Assert.Throws<FileNotFoundException>(() => service.Read(Member));
        Assert.Equal(2, service.Generation);
        Assert.Equal(0, source.FullReads);
    }

    [Fact]
    public void ConsumedExplicitMemberKeepsItsSelectionWhenAnotherCarrierClosesTheNamespace()
    {
        using CountingSource source = new();
        ResourceService service = new(source);
        EncodedResource member = new(Member, "bin", 2, "loose", [7]);
        service.Prepare(null, encodedResources: [member]).Commit();
        Assert.Equal(new byte[] { 7 }, service.Read(Member));

        service.Prepare(null, encodedResources: [Archive(), member]).Commit();

        Assert.Equal(new byte[] { 7 }, service.Read(Member));
        Assert.Equal(0, source.FullReads);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("read")]
    [InlineData("prefix")]
    public void AcquiredUnscopedAbsenceRequiresFreshServiceBeforeResourceCanBeAdded(string acquisition)
    {
        using InMemoryResourceSource source = new(new("empty", GameDataEdition.Classic, []));
        ResourceService service = new(source);
        Assert.Throws<FileNotFoundException>(() => Acquire(service, acquisition));
        EncodedResource unrelated = new(Sibling, "bin", 1, "unrelated", [3]);
        service.Prepare(null, encodedResources: [unrelated]).Commit();
        EncodedResource added = new(Member, "bin", 1, "added", [7]);

        ResourceReloadRequiredException error = Assert.Throws<ResourceReloadRequiredException>(() => service.Prepare(null, encodedResources: [unrelated, added]));

        Assert.Equal(new[] { Member }, error.Resources);
        Assert.Equal(1, service.Generation);
        Assert.Throws<FileNotFoundException>(() => service.Get(Member));
        ResourceService fresh = new(source);
        fresh.Prepare(null, encodedResources: [added]).Commit();
        Assert.Equal(new byte[] { 7 }, fresh.Read(Member));
    }

    [Fact]
    public void UnscopedAbsenceAcquiredAfterPreparationAlsoBlocksCommit()
    {
        using InMemoryResourceSource source = new(new("empty", GameDataEdition.Classic, []));
        ResourceService service = new(source);
        ResourceService.PreparedResources candidate = service.Prepare(null, encodedResources: [new(Member, "bin", 1, "added", [7])]);
        Assert.Throws<FileNotFoundException>(() => service.Get(Member));

        ResourceReloadRequiredException error = Assert.Throws<ResourceReloadRequiredException>(() => candidate.Commit());

        Assert.Equal(new[] { Member }, error.Resources);
        Assert.Equal(0, service.Generation);
        Assert.Throws<FileNotFoundException>(() => service.Get(Member));
    }

    private static EncodedResource Archive() => new(Carrier, "pack", 1, "archive", [4], Scope);

    private static void Acquire(ResourceService service, string acquisition)
    {
        switch (acquisition)
        {
            case "get":
                _ = service.Get(Member);
                break;
            case "read":
                _ = service.Read(Member);
                break;
            case "prefix":
                _ = service.ReadPrefix(Member, 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(acquisition));
        }
    }

    private sealed class CountingSource : IResourceSource, IResourcePrefixSource
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("installed", GameDataEdition.Classic, ["archive"]);
        public int FullReads { get; private set; }
        public int PrefixReads { get; private set; }
        public bool Disposed { get; private set; }

        public ResourceContent? Find(ResourceKey key)
        {
            FullReads++;
            return new(key, "bin", 1, "installed", () => new MemoryStream([1, 2, 3], false));
        }

        public ResourcePrefix? ReadPrefix(ResourceKey key, int maximumBytes)
        {
            PrefixReads++;
            return new(new byte[] { 1, 2, 3 }.Take(maximumBytes).ToArray(), 3);
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class FailingResolver : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context) => throw new InvalidDataException("Candidate preparation failed.");
    }
}
