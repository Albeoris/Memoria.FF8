using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class EncodedResourcePublicationTests
{
    private static readonly ResourceKey Model = new("field/main_chr/d000.mch");
    private static readonly ResourceKey OtherModel = new("field/main_chr/d001.mch");
    private static readonly ResourceKey Text = new("text/exe/card_names");

    [Fact]
    public void PublicationOwnsIndependentBytesAndBypassesInstalledPrefixWithoutHidingCapabilities()
    {
        using PrefixSource source = new();
        ResourceService service = new(source);
        byte[] bytes = [4, 5, 6];
        EncodedResource replacement = new(new("FIELD\\MAIN_CHR\\D000.MCH"), "mch", 7, "package-a", bytes);
        bytes[0] = 99;
        service.Prepare(null, encodedResources: [replacement]).Commit();

        Assert.Same(source.Descriptor, service.Source);
        Assert.Same(source, service.GetSource<IResourcePrefixSource>());
        Assert.Equal(Model, replacement.Key);
        Assert.Equal(new byte[] { 4, 5 }, service.ReadPrefix(Model, 2).Bytes);
        Assert.Equal(3, service.ReadPrefix(Model, 2).Length);
        Assert.Empty(service.ReadPrefix(Model, 0).Bytes);
        Assert.Equal(new byte[] { 4, 5, 6 }, service.Read(Model));
        Assert.Equal(0, source.PrefixReads);
        Assert.Equal(0, source.FullReads);
        ResourceContent content = service.Get(Model);
        Assert.Equal("mch", content.Format);
        Assert.Equal(7, content.Revision);
        Assert.Equal("package-a", content.Provenance);
        using Stream first = content.OpenRead();
        using Stream second = content.OpenRead();
        Assert.Equal(4, first.ReadByte());
        Assert.Equal(4, second.ReadByte());
        Assert.False(first.CanWrite);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("read")]
    [InlineData("prefix")]
    public void EveryAcquisitionBoundaryPreventsChangingAConsumedSelection(string acquisition)
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        service.Prepare(null, encodedResources: [Replacement(Model, 4)]).Commit();
        Consume(service, acquisition);
        long generation = service.Generation;

        ResourceReloadRequiredException error = Assert.Throws<ResourceReloadRequiredException>(() => service.Prepare(null, encodedResources: [Replacement(Model, 5)]));

        Assert.Equal(new[] { Model }, error.Resources);
        Assert.Equal(generation, service.Generation);
        Assert.Equal(new byte[] { 4 }, service.Read(Model));
    }

    [Fact]
    public void FailedPreparationKeepsEncodedAndTextPublicationsTogether()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        service.Prepare(null, [new(Text, ["before"], "a", "names.msd")], [Replacement(Model, 4)]).Commit();
        _ = service.Get(Model);
        long generation = service.Generation;

        Assert.Throws<ResourceReloadRequiredException>(() => service.Prepare(null, [new(Text, ["after"], "b", "names.msd")], [Replacement(Model, 5)]));

        Assert.Equal(generation, service.Generation);
        Assert.Equal("before", service.ReadText(Text, 0));
        Assert.Equal(new byte[] { 4 }, service.Read(Model));
    }

    [Fact]
    public void CompetingCommitMakesEarlierPreparationStaleWithoutPartiallyPublishingIt()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        ResourceService.PreparedResources stale = service.Prepare(null, [new(Text, ["stale"], "a", "names.msd")], [Replacement(Model, 4)]);
        service.Prepare(null, [new(Text, ["current"], "b", "names.msd")], [Replacement(Model, 5)]).Commit();
        long generation = service.Generation;
        bool callbackInvoked = false;

        Assert.Throws<InvalidOperationException>(() => stale.Commit(beforePublish: () => callbackInvoked = true));

        Assert.False(callbackInvoked);
        Assert.Equal(generation, service.Generation);
        Assert.Equal("current", service.ReadText(Text, 0));
        Assert.Equal(new byte[] { 5 }, service.Read(Model));
    }

    [Theory]
    [InlineData("get")]
    [InlineData("read")]
    [InlineData("prefix")]
    public void AcquisitionAfterPreparationStillBlocksCommit(string acquisition)
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        ResourceService.PreparedResources candidate = service.Prepare(null, [new(Text, ["candidate"], "a", "names.msd")], [Replacement(Model, 4)]);
        Consume(service, acquisition);
        bool callbackInvoked = false;

        ResourceReloadRequiredException error = Assert.Throws<ResourceReloadRequiredException>(() => candidate.Commit(beforePublish: () => callbackInvoked = true));

        Assert.False(callbackInvoked);
        Assert.Equal(new[] { Model }, error.Resources);
        Assert.Equal(0, service.Generation);
        Assert.Null(service.ReadText(Text, 0));
        Assert.Equal(new byte[] { 1, 2, 3 }, service.Read(Model));
    }

    [Fact]
    public void ConcurrentAcquisitionWaitsForCoordinatedMutationAndReadsPublishedSelection()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        ResourceService.PreparedResources candidate = service.Prepare(null, encodedResources: [Replacement(Model, 4)]);
        using ManualResetEventSlim attempted = new();
        using ManualResetEventSlim completed = new();
        byte[]? observed = null;
        Exception? readerFailure = null;
        Thread reader = new(() =>
        {
            try
            {
                attempted.Set();
                ResourceContent content = service.Get(Model);
                using Stream input = content.OpenRead();
                using MemoryStream bytes = new();
                input.CopyTo(bytes);
                observed = bytes.ToArray();
            }
            catch (Exception exception)
            {
                readerFailure = exception;
            }
            finally
            {
                completed.Set();
            }
        }) { IsBackground = true };
        bool coordinatedMutationCompleted = false;
        try
        {
            candidate.Commit(beforePublish: () =>
            {
                reader.Start();
                Assert.True(attempted.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(SpinWait.SpinUntil(() => completed.IsSet || (reader.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(5)));
                Assert.False(completed.IsSet);
                coordinatedMutationCompleted = true;
            });
        }
        finally
        {
            if ((reader.ThreadState & ThreadState.Unstarted) == 0)
            {
                Assert.True(reader.Join(TimeSpan.FromSeconds(5)));
            }
        }

        Assert.True(coordinatedMutationCompleted);
        Assert.Null(readerFailure);
        Assert.Equal(new byte[] { 4 }, observed);
        Assert.Equal(1, service.Generation);
    }

    [Fact]
    public void UnchangedBytesCanPublishNewRevisionAndProvenanceAfterConsumption()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        EncodedResource first = Replacement(Model, 4);
        service.Prepare(null, encodedResources: [first]).Commit();
        Assert.Equal(new byte[] { 4 }, service.Read(Model));
        EncodedResource refreshed = new(Model, "mch", 99, "refreshed-package", [4]);

        service.Prepare(null, encodedResources: [refreshed]).Commit();

        Assert.Equal(first.Fingerprint, refreshed.Fingerprint);
        Assert.Equal(99, service.Get(Model).Revision);
        Assert.Equal("refreshed-package", service.Get(Model).Provenance);
        Assert.Equal(new byte[] { 4 }, service.Read(Model));
    }

    [Fact]
    public void SameBytesWithDifferentFormatStillRequireReload()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        service.Prepare(null, encodedResources: [Replacement(Model, 4)]).Commit();
        _ = service.Get(Model);

        Assert.Throws<ResourceReloadRequiredException>(() => service.Prepare(null, encodedResources: [new(Model, "different-format", 2, "package-b", [4])]));
    }

    [Fact]
    public void ConsumingOriginalKeyDoesNotBlockUnrelatedReplacementButBlocksReplacingThatKey()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        Assert.Equal(new byte[] { 1, 2, 3 }, service.Read(Model));
        service.Prepare(null, encodedResources: [Replacement(OtherModel, 5)]).Commit();
        Assert.Equal(new byte[] { 5 }, service.Read(OtherModel));

        ResourceReloadRequiredException error = Assert.Throws<ResourceReloadRequiredException>(() => service.Prepare(null, encodedResources: [Replacement(Model, 4), Replacement(OtherModel, 5)]));

        Assert.Equal(new[] { Model }, error.Resources);
    }

    [Fact]
    public void UnconsumedReplacementCanBeRemovedBeforeAcquisition()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        service.Prepare(null, encodedResources: [Replacement(Model, 4)]).Commit();

        service.Prepare(null, encodedResources: []).Commit();

        Assert.Equal(new byte[] { 1, 2, 3 }, service.Read(Model));
    }

    [Fact]
    public void ConsumedReplacementRequiresFreshServiceForRemovalOrChange()
    {
        using InMemoryResourceSource source = Source();
        ResourceService current = new(source);
        current.Prepare(null, encodedResources: [Replacement(Model, 4)]).Commit();
        Assert.Equal(new byte[] { 4 }, current.Read(Model));
        Assert.Throws<ResourceReloadRequiredException>(() => current.Prepare(null, encodedResources: []));

        ResourceService changed = new(source);
        changed.Prepare(null, encodedResources: [Replacement(Model, 5)]).Commit();
        ResourceService removed = new(source);
        removed.Prepare(null, encodedResources: []).Commit();

        Assert.Equal(new byte[] { 5 }, changed.Read(Model));
        Assert.Equal(new byte[] { 1, 2, 3 }, removed.Read(Model));
        Assert.Equal(new byte[] { 4 }, current.Read(Model));
    }

    private static EncodedResource Replacement(ResourceKey key, byte value) => new(key, "mch", 1, "package-a", [value]);

    private static InMemoryResourceSource Source()
    {
        InMemoryResourceSource source = new(new("memory", GameDataEdition.Classic, []));
        source.Set(Model, [1, 2, 3], "mch");
        source.Set(OtherModel, [7, 8, 9], "mch");
        return source;
    }

    private static void Consume(ResourceService service, string acquisition)
    {
        switch (acquisition)
        {
            case "get":
                _ = service.Get(Model);
                break;
            case "read":
                _ = service.Read(Model);
                break;
            case "prefix":
                _ = service.ReadPrefix(Model, 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(acquisition));
        }
    }

    private sealed class PrefixSource : IResourceSource, IResourcePrefixSource
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("prefix", GameDataEdition.Classic, ["classic-model"]);
        public int PrefixReads { get; private set; }
        public int FullReads { get; private set; }

        public ResourcePrefix? ReadPrefix(ResourceKey key, int maximumBytes)
        {
            PrefixReads++;
            return new(new byte[] { 1, 2, 3 }.Take(maximumBytes).ToArray(), 3);
        }

        public ResourceContent? Find(ResourceKey key)
        {
            FullReads++;
            return new(key, "mch", 1, "installed", () => new MemoryStream([1, 2, 3], false));
        }

        public void Dispose()
        {
        }
    }
}
