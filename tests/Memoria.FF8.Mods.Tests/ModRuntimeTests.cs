namespace Memoria.FF8.Mods.Tests;

public sealed class ModRuntimeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-mod-runtime-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredPreparationReusesDiscoveryUntilAnExplicitRefreshInvalidatesIt(bool refresh)
    {
        Write("Memoria/Deferred/mod.ini", "enabled=1\n");
        Write("Memoria/Deferred/data.txt", "first");
        List<ModCatalogSnapshot> candidates = [];
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero, prepare: candidate =>
        {
            candidates.Add(candidate);
            return candidates.Count == 1 ? new(false) { Deferred = true } : ModPreparationResult.Accepted;
        });
        Assert.False(runtime.PreparePending(force: true));
        if (refresh)
        {
            Write("Memoria/Deferred/data.txt", "changed contents");
            runtime.RequestRefresh();
        }
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        Assert.Equal(2, candidates.Count);
        if (refresh)
        {
            Assert.NotSame(candidates[0], candidates[1]);
            Assert.Equal("changed contents", System.Text.Encoding.UTF8.GetString(Assert.Single(runtime.Catalog.Files).ReadBytes()));
        }
        else
        {
            Assert.Same(candidates[0], candidates[1]);
        }
    }

    [Fact]
    public void ExplicitModsDirectoryUsesItsOwnPolicyAndRetainsGameDirectoryPriority()
    {
        string gameRoot = Path.Combine(root, "game");
        string customRoot = Path.Combine(root, "Custom package", "Texture packs");
        Directory.CreateDirectory(customRoot);
        File.WriteAllText(Path.Combine(customRoot, "mods.ini"), "auto_enable_new_mods = 0\n");
        WriteAt(gameRoot, "Demaster/shared/test.png", "game");
        WriteAt(gameRoot, "Demaster/shared/mod.ini", "enabled = 1\n");
        string customPackage = Path.Combine(customRoot, "Demaster", "shared");
        Directory.CreateDirectory(customPackage);
        File.WriteAllText(Path.Combine(customPackage, "test.png"), "custom");
        Write("Demaster/ignored/test.png", "application");
        using ModRuntime runtime = new(root, watch: false, additionalBaseDirectories: [gameRoot], modsDirectory: customRoot + Path.DirectorySeparatorChar);
        Publish(runtime);
        Assert.Equal(new[] { Path.Combine(gameRoot, "Mods"), customRoot }, runtime.ModsDirectories);
        Assert.False(runtime.AutoEnableNewMods);
        Assert.Equal("game", System.Text.Encoding.UTF8.GetString(Assert.Single(runtime.Catalog.Files).ReadBytes()));
        Assert.DoesNotContain(runtime.Catalog.Packages, package => package.Id == "Demaster/ignored");
        Assert.True(runtime.SetAutoEnableNewMods(true).Success);
        Assert.Contains("auto_enable_new_mods = 1", File.ReadAllText(Path.Combine(customRoot, "mods.ini")));
        Assert.False(File.Exists(Path.Combine(root, "Mods", "mods.ini")));
        Directory.Delete(Path.Combine(gameRoot, "Mods", "Demaster", "shared"), true);
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Equal("custom", System.Text.Encoding.UTF8.GetString(Assert.Single(runtime.Catalog.Files).ReadBytes()));
    }

    [Fact]
    public void WatcherDiscoversMissingExplicitModsDirectoryWithCustomName()
    {
        Directory.CreateDirectory(root);
        string customRoot = Path.Combine(root, "Custom package", "Texture packs");
        using ModRuntime runtime = new(root, watch: true, debounce: TimeSpan.Zero, modsDirectory: customRoot);
        Publish(runtime);
        Assert.False(Directory.Exists(customRoot));
        string package = Path.Combine(customRoot, "Demaster", "example");
        Directory.CreateDirectory(package);
        string texture = Path.Combine(package, "test.png");
        File.WriteAllText(texture, "original");
        WaitFor(() => runtime.IsReloadPending);
        PumpUntilPublished(runtime, snapshot => snapshot.Files.Count == 1);
        Assert.Equal("original", System.Text.Encoding.UTF8.GetString(Assert.Single(runtime.Catalog.Files).ReadBytes()));
        File.WriteAllText(texture, "updated content");
        WaitFor(() => runtime.IsReloadPending);
        PumpUntilPublished(runtime, snapshot => snapshot.Files.Count == 1 && System.Text.Encoding.UTF8.GetString(snapshot.Files[0].ReadBytes()) == "updated content");
    }

    [Fact]
    public void VirtualNamespaceRetainsCandidatesInPackageAndPathOrder()
    {
        Write("Tonberry/zeta/textures/Test.png", "second");
        Write("Tonberry/Alpha/textures/test.png", "first");
        Write("Demaster/first/textures/test.png", "other format");
        using ModRuntime runtime = Runtime();
        Assert.True(runtime.PreparePending());
        Assert.Empty(runtime.Catalog.Packages);
        Assert.True(runtime.CommitPending());
        Assert.Equal(new[] { "Tonberry/Alpha", "Tonberry/zeta", "Demaster/first" }, runtime.Catalog.Packages.Select(package => package.Id));
        IReadOnlyList<ModFile> candidates = runtime.Catalog.FindAll(ModFormat.Tonberry, "TEXTURES\\TEST.PNG");
        Assert.Equal(new[] { "Tonberry/Alpha", "Tonberry/zeta" }, candidates.Select(file => file.PackageId));
        Assert.Equal("first", System.Text.Encoding.UTF8.GetString(candidates[0].ReadBytes()));
        Assert.Single(runtime.Catalog.FindAll(ModFormat.Demaster, "textures/test.png"));
        Assert.False(File.Exists(Path.Combine(root, "Mods/Tonberry/Alpha/mod.ini")));
    }

    [Fact]
    public void MultipleRootsMergePackagesByNameAndPreferEarlierRootsIgnoringPackageCase()
    {
        string gameRoot = Path.Combine(root, "game");
        Write("Tonberry/zeta/test.png", "application");
        Write("Demaster/EXAMPLE/test.png", "shadowed");
        Write("Demaster/Alpha/test.png", "application alpha");
        WriteAt(gameRoot, "Tonberry/Alpha/test.png", "game alpha");
        WriteAt(gameRoot, "Demaster/example/test.png", "game winner");
        WriteAt(gameRoot, "Demaster/zeta/test.png", "game zeta");
        using ModRuntime runtime = new(root, watch: false, additionalBaseDirectories: [gameRoot, Path.Combine(gameRoot, "."), root, root.ToUpperInvariant()]);
        Publish(runtime);
        Assert.Equal(new[] { Path.Combine(gameRoot, "Mods"), Path.Combine(root, "Mods") }, runtime.ModsDirectories);
        Assert.True(Assert.IsAssignableFrom<ICollection<string>>(runtime.ModsDirectories).IsReadOnly);
        Assert.Equal(new[] { "Tonberry/Alpha", "Tonberry/zeta", "Demaster/Alpha", "Demaster/example", "Demaster/zeta" }, runtime.Catalog.Packages.Select(package => package.Id));
        ModPackage winner = runtime.Catalog.Packages.Single(package => package.Id == "Demaster/example");
        Assert.Equal(Path.Combine(gameRoot, "Mods", "Demaster", "example"), winner.RootDirectory);
        Assert.Equal("game winner", System.Text.Encoding.UTF8.GetString(runtime.Catalog.FindAll(ModFormat.Demaster, "test.png")[1].ReadBytes()));
        Assert.Single(runtime.Diagnostics, diagnostic => diagnostic.Code == "duplicate-package");
    }

    [Fact]
    public void MultipleRootsUseApplicationGlobalPolicyAndWritePackageSettingsToTheirOwner()
    {
        string gameRoot = Path.Combine(root, "game");
        Write("mods.ini", "auto_enable_new_mods = 0\n");
        Write("Demaster/application/test.png", "application");
        WriteAt(gameRoot, "mods.ini", "auto_enable_new_mods = 1\n");
        WriteAt(gameRoot, "Demaster/game/test.png", "game");
        using ModRuntime runtime = new(root, watch: false, additionalBaseDirectories: [gameRoot]);
        Publish(runtime);
        Assert.False(runtime.AutoEnableNewMods);
        Assert.All(runtime.Catalog.Packages, package => Assert.False(package.RequestedEnabled));
        Assert.True(runtime.SetEnabled("Demaster/game", true).Success);
        Publish(runtime);
        Assert.Contains("enabled = 1", File.ReadAllText(Path.Combine(gameRoot, "Mods/Demaster/game/mod.ini")));
        Assert.False(File.Exists(Path.Combine(root, "Mods/Demaster/game/mod.ini")));
        Assert.True(runtime.SetAutoEnableNewMods(true).Success);
        Publish(runtime);
        Assert.Contains("auto_enable_new_mods = 1", File.ReadAllText(Path.Combine(root, "Mods/mods.ini")));
        Assert.Equal("auto_enable_new_mods = 1\n", File.ReadAllText(Path.Combine(gameRoot, "Mods/mods.ini")));
        Assert.All(runtime.Catalog.Packages, package => Assert.True(package.RequestedEnabled));
    }

    [Theory]
    [InlineData("example")]
    [InlineData("EXAMPLE")]
    public void RemovingPreferredPackageExposesFallbackBytesAndConfigurationWithIdenticalFileMetadata(string fallbackName)
    {
        string gameRoot = Path.Combine(root, "game");
        string winnerDirectory = Path.Combine(gameRoot, "Mods", "Demaster", "example");
        string fallbackDirectory = Path.Combine(root, "Mods", "Demaster", fallbackName);
        WriteAt(gameRoot, "Demaster/example/test.png", "first");
        WriteAt(gameRoot, "Demaster/example/mod.ini", "enabled = 1\nowner = game\n");
        Write($"Demaster/{fallbackName}/test.png", "other");
        Write($"Demaster/{fallbackName}/mod.ini", "enabled = 0\nowner = application\n");
        DateTime timestamp = File.GetLastWriteTimeUtc(Path.Combine(winnerDirectory, "test.png"));
        File.SetLastWriteTimeUtc(Path.Combine(fallbackDirectory, "test.png"), timestamp);
        using ModRuntime runtime = new(root, watch: false, additionalBaseDirectories: [gameRoot]);
        Publish(runtime);
        ModFile original = Assert.Single(runtime.Catalog.Files);
        Assert.Equal("first", System.Text.Encoding.UTF8.GetString(original.ReadBytes()));
        Directory.Delete(winnerDirectory, true);
        runtime.RequestRefresh();
        Publish(runtime);
        ModPackage fallback = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal(fallbackDirectory, fallback.RootDirectory);
        Assert.False(fallback.ExplicitEnabled);
        Assert.False(fallback.RequestedEnabled);
        Assert.Empty(runtime.Catalog.Files);
        Assert.DoesNotContain(runtime.Diagnostics, diagnostic => diagnostic.Code == "duplicate-package");
        Assert.True(runtime.SetEnabled(fallback.Id, true).Success);
        Publish(runtime);
        ModFile replacement = Assert.Single(runtime.Catalog.Files);
        Assert.True(replacement.Revision > original.Revision);
        Assert.Equal("other", System.Text.Encoding.UTF8.GetString(replacement.ReadBytes()));
        Assert.Equal("first", System.Text.Encoding.UTF8.GetString(original.ReadBytes()));
        Assert.Contains("enabled = 1", File.ReadAllText(Path.Combine(fallbackDirectory, "mod.ini")));
        Assert.Contains("owner = application", File.ReadAllText(Path.Combine(fallbackDirectory, "mod.ini")));
        Assert.False(Directory.Exists(winnerDirectory));
    }

    [Fact]
    public void GlobalOffPinsInheritedPackagesWithoutOverwritingExplicitDisable()
    {
        Write("Tonberry/inherited/test.png", "image");
        Write("Demaster/disabled/test.png", "image");
        Write("Demaster/disabled/mod.ini", "enabled = 0\ncustom = keep\n");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Assert.True(runtime.SetAutoEnableNewMods(false).Success);
        Assert.Contains("enabled = 1", File.ReadAllText(Path.Combine(root, "Mods/Tonberry/inherited/mod.ini")));
        Assert.Contains("enabled = 0", File.ReadAllText(Path.Combine(root, "Mods/Demaster/disabled/mod.ini")));
        Assert.Contains("custom = keep", File.ReadAllText(Path.Combine(root, "Mods/Demaster/disabled/mod.ini")));
        Write("Demaster/new/test.png", "new image");
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.True(runtime.Catalog.Packages.Single(package => package.Name == "inherited").RequestedEnabled);
        Assert.False(runtime.Catalog.Packages.Single(package => package.Name == "new").RequestedEnabled);
        Assert.True(runtime.SetAutoEnableNewMods(true).Success);
        Publish(runtime);
        Assert.True(runtime.Catalog.Packages.Single(package => package.Name == "new").RequestedEnabled);
        Assert.False(runtime.Catalog.Packages.Single(package => package.Name == "disabled").RequestedEnabled);
    }

    [Fact]
    public void FailedGlobalWriteRollsBackPinningAndKeepsPreviousPolicy()
    {
        Write("Tonberry/inherited/test.png", "image");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Directory.CreateDirectory(Path.Combine(root, "Mods/mods.ini"));
        Assert.False(runtime.SetAutoEnableNewMods(false).Success);
        Assert.True(runtime.AutoEnableNewMods);
        Assert.False(File.Exists(Path.Combine(root, "Mods/Tonberry/inherited/mod.ini")));
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "configuration-write-failed");
    }

    [Fact]
    public void FailedPreparationKeepsPublishedGenerationAndCanRetry()
    {
        Write("Demaster/example/test.png", "original");
        bool reject = false;
        int committed = 0;
        using ModRuntime runtime = Runtime(prepare: _ => new ModPreparationResult(!reject, Commit: () => committed++));
        Publish(runtime);
        ModCatalogSnapshot original = runtime.Catalog;
        reject = true;
        Assert.True(runtime.SetEnabled("Demaster/example", false).Success);
        Assert.False(runtime.PreparePending());
        Assert.False(runtime.CommitPending());
        Assert.Same(original, runtime.Catalog);
        Assert.Equal(1, committed);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "reload-failed");
        reject = false;
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Empty(runtime.Catalog.Files);
        Assert.Equal(ModEffectiveStatus.Disabled, runtime.Catalog.Packages[0].Status);
        Assert.Equal(2, committed);
    }

    [Fact]
    public void FailedCommitKeepsPublishedGeneration()
    {
        Write("Demaster/example/test.png", "original");
        bool reject = false;
        using ModRuntime runtime = Runtime(prepare: _ => new ModPreparationResult(true, Commit: () =>
        {
            if (reject)
            {
                throw new InvalidOperationException("Resident preparation refused publication.");
            }
        }));
        Publish(runtime);
        ModCatalogSnapshot original = runtime.Catalog;
        reject = true;
        Assert.True(runtime.SetEnabled("Demaster/example", false).Success);
        Assert.True(runtime.PreparePending());
        Assert.False(runtime.CommitPending());
        Assert.Same(original, runtime.Catalog);
        Assert.True(runtime.IsReloadPending);
    }

    [Fact]
    public void IncompatiblePackagesRemainVisibleWithoutExposingTheirFiles()
    {
        Write("Tonberry/example/hashmap/test.csv", "not parsed by metadata discovery");
        Write("Tonberry/example/mod.ini", "enabled = 1");
        Write("Demaster/example/test.png", "image");
        using ModRuntime runtime = Runtime(compatible: format => format != ModFormat.Tonberry);
        Publish(runtime);
        ModPackage tonberry = runtime.Catalog.Packages.Single(package => package.Format == ModFormat.Tonberry);
        Assert.True(tonberry.RequestedEnabled);
        Assert.Equal(ModEffectiveStatus.IncompatibleEdition, tonberry.Status);
        Assert.Empty(runtime.Catalog.FindAll(ModFormat.Tonberry, "hashmap/test.csv"));
        Assert.False(runtime.SetEnabled(tonberry.Id, false).Success);
        Assert.Single(runtime.Catalog.Files);
        Write("Tonberry/example/mod.ini", "enabled = 0");
        runtime.Refresh();
        Publish(runtime);
        tonberry = runtime.Catalog.Packages.Single(package => package.Format == ModFormat.Tonberry);
        Assert.False(tonberry.RequestedEnabled);
        Assert.Equal(ModEffectiveStatus.IncompatibleEdition, tonberry.Status);
    }

    [Fact]
    public void RevisionsAdvanceForChangedContentAndSubscriptionsOnlyObserveCommits()
    {
        Write("Demaster/example/test.png", "original");
        using ModRuntime runtime = Runtime();
        int notifications = 0;
        IDisposable subscription = runtime.Subscribe(_ => notifications++);
        Publish(runtime);
        ModFile original = runtime.Catalog.Files[0];
        byte[] originalBytes = original.ReadBytes();
        Write("Demaster/example/test.png", "replacement with different length");
        runtime.RequestRefresh();
        Assert.True(runtime.PreparePending());
        Assert.Equal(1, notifications);
        Assert.Same(original, runtime.Catalog.Files[0]);
        Assert.True(runtime.CommitPending());
        Assert.True(runtime.Catalog.Files[0].Revision > original.Revision);
        Assert.Equal(originalBytes, original.ReadBytes());
        Assert.Equal(2, notifications);
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending());
        Assert.False(runtime.CommitPending());
        subscription.Dispose();
        Assert.True(runtime.SetEnabled("Demaster/example", false).Success);
        Publish(runtime);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void CommittingOnAnotherThreadIsRejected()
    {
        using ModRuntime runtime = Runtime();
        Assert.True(runtime.PreparePending());
        Exception? failure = null;
        Thread worker = new(() => failure = Record.Exception(() => runtime.CommitPending()));
        worker.Start();
        worker.Join();
        Assert.IsType<InvalidOperationException>(failure);
        Assert.True(runtime.CommitPending());
    }

    [Fact]
    public void AdapterReportsNoUsableEntriesWithoutDiscoveryParsingFormats()
    {
        Write("Tonberry/example/readme.txt", "metadata");
        using ModRuntime runtime = Runtime(prepare: _ => new ModPreparationResult(true, []));
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.NoUsableEntries, runtime.Catalog.Packages[0].Status);
    }

    [Fact]
    public void OverflowRevalidatesCachedFilesEvenWhenSizeAndTimestampAreUnchanged()
    {
        Write("Demaster/example/test.png", "old");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        ModFile original = runtime.Catalog.Files[0];
        Assert.Equal("old", System.Text.Encoding.UTF8.GetString(original.ReadBytes()));
        string path = Path.Combine(root, "Mods/Demaster/example/test.png");
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        Write("Demaster/example/test.png", "new");
        File.SetLastWriteTimeUtc(path, timestamp);
        runtime.NotifyWatcherOverflow();
        Publish(runtime);
        Assert.True(runtime.Catalog.Files[0].Revision > original.Revision);
        Assert.Equal("new", System.Text.Encoding.UTF8.GetString(runtime.Catalog.Files[0].ReadBytes()));
        Assert.Equal("old", System.Text.Encoding.UTF8.GetString(original.ReadBytes()));
    }

    [Fact]
    public void WatcherDiscoversFirstInstallationWithoutCreatingModsOrPublishingOnCallback()
    {
        Directory.CreateDirectory(root);
        using ModRuntime runtime = new(root, watch: true, debounce: TimeSpan.Zero);
        Publish(runtime);
        Assert.False(Directory.Exists(Path.Combine(root, "Mods")));
        Write("Demaster/example/test.png", "original");
        WaitFor(() => runtime.IsReloadPending);
        Assert.Empty(runtime.Catalog.Packages);
        PumpUntilPublished(runtime, snapshot => snapshot.Packages.Count == 1);
        long generation = runtime.Catalog.Generation;
        Write("Demaster/example/test.png", "updated content");
        WaitFor(() => runtime.IsReloadPending);
        Assert.Equal(generation, runtime.Catalog.Generation);
        PumpUntilPublished(runtime, snapshot => snapshot.Generation > generation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultipleRootWatchersDiscoverFirstInstallationAndChangedContent(bool installInAdditionalRoot)
    {
        string applicationRoot = Path.Combine(root, "application");
        string gameRoot = Path.Combine(root, "game");
        Directory.CreateDirectory(applicationRoot);
        Directory.CreateDirectory(gameRoot);
        using ModRuntime runtime = new(applicationRoot, watch: true, debounce: TimeSpan.Zero, additionalBaseDirectories: [gameRoot]);
        Publish(runtime);
        Assert.All(runtime.ModsDirectories, directory => Assert.False(Directory.Exists(directory)));
        string installRoot = installInAdditionalRoot ? gameRoot : applicationRoot;
        WriteAt(installRoot, "Demaster/example/test.png", "original");
        WaitFor(() => runtime.IsReloadPending);
        Assert.Empty(runtime.Catalog.Packages);
        PumpUntilPublished(runtime, snapshot => snapshot.Packages.Count == 1);
        Assert.Equal("original", System.Text.Encoding.UTF8.GetString(Assert.Single(runtime.Catalog.Files).ReadBytes()));
        long generation = runtime.Catalog.Generation;
        WriteAt(installRoot, "Demaster/example/test.png", "updated content");
        WaitFor(() => runtime.IsReloadPending);
        Assert.Equal(generation, runtime.Catalog.Generation);
        PumpUntilPublished(runtime, snapshot => snapshot.Files.Count == 1 && System.Text.Encoding.UTF8.GetString(snapshot.Files[0].ReadBytes()) == "updated content");
        Assert.True(runtime.Catalog.Generation > generation);
    }

    [Fact]
    public void RetryPreparationIsScheduledAndStopsAfterFourAttempts()
    {
        Write("Demaster/example/test.png", "image");
        int attempts = 0;
        using ModRuntime runtime = Runtime(prepare: _ =>
        {
            attempts++;
            throw new IOException("The changed image is still locked.");
        });
        Assert.False(runtime.PreparePending());
        Assert.True(runtime.IsReloadPending);
        Assert.False(runtime.PreparePending());
        Assert.Equal(1, attempts);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Assert.False(runtime.PreparePending(force: true));
        }
        Assert.Equal(5, attempts);
        Assert.False(runtime.IsReloadPending);
        runtime.Refresh();
        Assert.False(runtime.PreparePending());
        Assert.Equal(6, attempts);
    }

    [Fact]
    public void ConfigTogglePreservesUnrelatedKeysAndComments()
    {
        Write("Demaster/example/test.png", "image");
        Write("Demaster/example/mod.ini", "; package preferences\r\nEnabled = 0 ; own setting\r\nquality = high\r\n");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Assert.True(runtime.SetEnabled("Demaster/example", true).Success);
        string contents = File.ReadAllText(Path.Combine(root, "Mods/Demaster/example/mod.ini"));
        Assert.Contains("; package preferences", contents);
        Assert.Contains("enabled = 1 ; own setting", contents);
        Assert.Contains("quality = high", contents);
        Publish(runtime);
        Assert.True(runtime.Catalog.Packages[0].ExplicitEnabled);
    }

    [Fact]
    public void EscapingDirectoryLinksAreReportedAndExcluded()
    {
        Write("Demaster/example/inside.png", "inside");
        string outside = Path.Combine(root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.png"), "outside");
        string link = Path.Combine(root, "Mods/Demaster/example/escape");
        try
        {
            Directory.CreateSymbolicLink(link, outside);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or PlatformNotSupportedException || exception is IOException && (exception.HResult & 0xFFFF) == 1314)
        {
            Assert.Skip("Creating symbolic links is unavailable in this test environment.");
        }
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Assert.Single(runtime.Catalog.Files);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "escaping-link-rejected");
    }

    [Fact]
    public void LazyDecodeFailureIsVisibleWithoutChangingCatalogAndRequiresOwningThread()
    {
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        ModCatalogSnapshot original = runtime.Catalog;
        runtime.ReportFailure(new("reload-failed", "The image could not be decoded."));
        Assert.Same(original, runtime.Catalog);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Message == "The image could not be decoded.");
        Exception? failure = null;
        Thread worker = new(() => failure = Record.Exception(() => runtime.ReportFailure(new("reload-failed", "wrong thread"))));
        worker.Start();
        worker.Join();
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void DeferredNotificationObservesPublishedCatalogAndCannotRollBackOrStarveListeners()
    {
        Write("Demaster/example/test.png", "image");
        ModRuntime? runtime = null;
        long observedGeneration = -1;
        using ModRuntime owner = Runtime(prepare: _ => new(true, AfterCommit: () =>
        {
            observedGeneration = runtime!.Catalog.Generation;
            throw new InvalidOperationException("A resource consumer failed after publication.");
        }));
        runtime = owner;
        int notifications = 0;
        using IDisposable subscription = owner.Subscribe(_ => notifications++);
        Publish(owner);
        Assert.Equal(owner.Catalog.Generation, observedGeneration);
        Assert.Equal(1, notifications);
        Assert.Contains(owner.Diagnostics, diagnostic => diagnostic.Code == "subscriber-failed");
    }

    [Fact]
    public void RepeatedStaleCommitFailuresExhaustTheSameBoundedRetrySchedule()
    {
        using ModRuntime runtime = Runtime(prepare: _ => new(true, Commit: () => throw new InvalidOperationException("Stale preparation.")));
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Assert.True(runtime.PreparePending(force: true));
            Assert.False(runtime.CommitPending());
        }
        Assert.False(runtime.IsReloadPending);
        Assert.Equal(0, runtime.Catalog.Generation);
    }

    [Fact]
    public void LazyFailureRetriesAreBoundedAndRepeatedReportsDoNotResetTheBudget()
    {
        Write("Demaster/example/test.png", "image");
        bool reject = false;
        int attempts = 0;
        using ModRuntime runtime = Runtime(prepare: _ =>
        {
            attempts++;
            if (reject)
            {
                throw new IOException("The image is incomplete.");
            }
            return ModPreparationResult.Accepted;
        });
        Publish(runtime);
        reject = true;
        ModDiagnostic failure = new("replacement-failed", "The first image decode failed.", "Demaster/example", "test.png");
        runtime.ReportFailure(failure);
        Assert.True(runtime.IsReloadPending);
        Assert.False(runtime.PreparePending());
        for (int retry = 0; retry < 4; retry++)
        {
            Assert.False(runtime.PreparePending(force: true));
        }
        Assert.Equal(5, attempts);
        Assert.False(runtime.IsReloadPending);
        runtime.ReportFailure(failure);
        Assert.False(runtime.IsReloadPending);
        Assert.False(runtime.PreparePending(force: true));
        Assert.Equal(5, attempts);
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("textures/../../outside.png")]
    [InlineData("C:/outside.png")]
    [InlineData("/outside.png")]
    public void VirtualPathsCannotEscapePackage(string path)
    {
        Assert.Throws<ArgumentException>(() => ModCatalogSnapshot.NormalizePath(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private ModRuntime Runtime(Func<ModFormat, bool>? compatible = null, Func<ModCatalogSnapshot, ModPreparationResult>? prepare = null)
    {
        return new(root, compatible, prepare, watch: false, debounce: TimeSpan.Zero);
    }

    private void Write(string relative, string contents)
    {
        WriteAt(root, relative, contents);
    }

    private static void WriteAt(string baseDirectory, string relative, string contents)
    {
        string path = Path.Combine(baseDirectory, "Mods", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending());
        Assert.True(runtime.CommitPending());
    }

    private static void WaitFor(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
        }
        Assert.True(condition(), "The watcher did not enqueue a filesystem change within five seconds.");
    }

    private static void PumpUntilPublished(ModRuntime runtime, Func<ModCatalogSnapshot, bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition(runtime.Catalog) && DateTime.UtcNow < deadline)
        {
            runtime.PreparePending(force: true);
            runtime.CommitPending();
            Thread.Sleep(20);
        }
        Assert.True(condition(runtime.Catalog));
    }
}
