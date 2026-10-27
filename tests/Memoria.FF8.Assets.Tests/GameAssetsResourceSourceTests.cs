using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class GameAssetsResourceSourceTests
{
    [Fact]
    public void FieldCatalogUsesSuppliedArchiveAndLogicalResourcesWithoutClassicFiles()
    {
        using CatalogSource source = new() { ExecutableBytes = [1, 2, 3] };
        ResourceService resources = new(source);
        using (GameAssets assets = new(Path.Combine(Path.GetTempPath(), "ff8-memory-source-" + Guid.NewGuid().ToString("N")), resources: resources))
        {
            Assert.Equal(new[] { "field" }, source.OpenedFamilies);
            Assert.Equal(new[] { "field/mapdata.fl", "field/mapdata.fi", "field/mapdata.fs", "field/mapdata/maplist" }, source.ReadKeys);
            Assert.Null(assets.NativeRuntimeProfile);
            Assert.Throws<NotSupportedException>(() => assets.ReadNativeEncounterTables());
            Assert.Equal(new[] { "field/mapdata.fl", "field/mapdata.fi", "field/mapdata.fs", "field/mapdata/maplist" }, source.ReadKeys);
            Assert.Throws<FileNotFoundException>(() => assets.ReadNativeVibrationBank());
            Assert.Equal(new[] { "field/mapdata.fl", "field/mapdata.fi", "field/mapdata.fs", "field/mapdata/maplist", "tables/vibration" }, source.ReadKeys);
            Assert.Empty(source.ResolvedFiles);
        }
        Assert.True(source.RootDisposed);
        Assert.False(source.Disposed);
    }

    [Fact]
    public void NativeExecutableHelperUsesOnlyTheProviderLogicalKey()
    {
        using CatalogSource source = new();
        Assert.Throws<FileNotFoundException>(() => GameDataResources.ReadNativeExecutable(new ResourceService(source)));
        Assert.Equal(new[] { "runtime/native-executable" }, source.ReadKeys);
    }

    [Fact]
    public void NativeExecutableImageIsReusedWithinItsResourceServiceAndIsolatedFromOtherSources()
    {
        using CatalogSource firstSource = new() { ExecutableBytes = [1, 2, 3] };
        using CatalogSource secondSource = new() { ExecutableBytes = [4, 5, 6] };
        ResourceService firstResources = new(firstSource);
        ResourceService secondResources = new(secondSource);
        byte[] first = GameDataResources.ReadNativeExecutable(firstResources);
        byte[] second = GameDataResources.ReadNativeExecutable(secondResources);
        Assert.Equal(new byte[] { 1, 2, 3 }, first);
        Assert.Equal(new byte[] { 4, 5, 6 }, second);
        Assert.NotSame(first, second);
        Assert.Same(first, GameDataResources.ReadNativeExecutable(firstResources));
        Assert.Same(second, GameDataResources.ReadNativeExecutable(secondResources));
        Assert.Equal(new[] { "runtime/native-executable" }, firstSource.ReadKeys);
        Assert.Equal(new[] { "runtime/native-executable" }, secondSource.ReadKeys);
    }

    [Fact]
    public void ProviderAuthenticationFailureIsRetriedAndDoesNotPoisonTheExecutableCache()
    {
        using CatalogSource source = new() { ExecutableBytes = [7, 8, 9], ExecutableAuthenticationFailures = 2 };
        ResourceService resources = new(source);
        Assert.Throws<NotSupportedException>(() => GameDataResources.ReadNativeExecutable(resources));
        Assert.Throws<NotSupportedException>(() => GameDataResources.ReadNativeExecutable(resources));
        byte[] executable = GameDataResources.ReadNativeExecutable(resources);
        Assert.Equal(new byte[] { 7, 8, 9 }, executable);
        Assert.Same(executable, GameDataResources.ReadNativeExecutable(resources));
        Assert.Equal(new[] { "runtime/native-executable", "runtime/native-executable", "runtime/native-executable" }, source.ReadKeys);
    }

    [Fact]
    public void MissingRuntimeCapabilityRetainsNotSupportedBoundary()
    {
        using CatalogSource source = new();
        Assert.Throws<NotSupportedException>(() => GameDataResources.RequireNativeRuntimeProfile(new ResourceService(source)));
        using GameAssets assets = new(Path.GetTempPath(), resources: new ResourceService(source));
        Assert.Throws<NotSupportedException>(() => assets.ReadNewGameCharacterVitals());
    }

    [Fact]
    public void ClassicExecutableResourceRejectsUnauthenticatedBytes()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ff8-executable-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "FF8_EN.exe"), [0x4D, 0x5A, 1, 2]);
            using ClassicGameDataSource source = new(directory);
            Assert.Null(source.NativeRuntimeProfile);
            Assert.Throws<NotSupportedException>(() => source.RequireNativeRuntimeProfile());
            Assert.Throws<NotSupportedException>(() => GameDataResources.ReadNativeExecutable(new ResourceService(source)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void InstalledClassicExecutableResourcePreservesAuthenticatedRawBytes()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_GAME_DIR to run optional installed Classic checks.");
        using ClassicGameDataSource source = new(directory!);
        Assert.NotNull(source.NativeRuntimeProfile);
        byte[] executable = GameDataResources.ReadNativeExecutable(new ResourceService(source));
        Assert.Equal(File.ReadAllBytes(Path.Combine(directory!, "FF8_EN.exe")), executable);
    }

    [Fact]
    public void MovieLookupUsesSourceFilesAndSkipsMissingPreferredSuffix()
    {
        string movie = Path.Combine(Path.GetTempPath(), "ff8-source-movie-" + Guid.NewGuid().ToString("N") + ".bik");
        File.WriteAllBytes(movie, [1]);
        try
        {
            using CatalogSource source = new() { MoviePath = movie };
            using GameAssets assets = new(Path.GetTempPath(), resources: new ResourceService(source));
            Assert.Equal(movie, assets.GetMoviePath(1));
            Assert.Equal(new[] { "movies/disc00_01h.avi", "movies/disc00_01.bik" }, source.ResolvedFiles);
        }
        finally
        {
            File.Delete(movie);
        }
    }

    [Fact]
    public void SourceWithoutArchiveCapabilityFailsBeforeOpeningClassicFiles()
    {
        using InMemoryResourceSource source = new(new("memory", GameDataEdition.Classic, []));
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => new GameAssets(Path.GetTempPath(), resources: new ResourceService(source)));
        Assert.Contains("archive enumeration", exception.Message);
        Assert.Throws<NotSupportedException>(() => new BattleAssets(Path.GetTempPath(), resources: new ResourceService(source)));
        Assert.Throws<FileNotFoundException>(() => new WorldAssets(Path.GetTempPath(), resources: new ResourceService(source)));
    }

    [Theory]
    [InlineData("field")]
    [InlineData("battle")]
    public void FailedConstructorDisposesOpenedArchiveWithoutDisposingSuppliedSource(string family)
    {
        using CatalogSource source = new() { ArchiveFamily = family, FailEntryEnumeration = true };
        ResourceService resources = new(source);
        Action construct = family switch
        {
            "field" => () => new GameAssets(Path.GetTempPath(), resources: resources),
            "battle" => () => new BattleAssets(Path.GetTempPath(), resources: resources),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        Assert.Throws<InvalidDataException>(construct);
        Assert.True(source.RootDisposed);
        Assert.False(source.Disposed);
    }

    [Fact]
    public void WorldConstructionUsesLogicalResourcesWithoutEnumeratingStorage()
    {
        using CatalogSource source = new() { ArchiveFamily = "world", FailEntryEnumeration = true };
        Assert.Throws<FileNotFoundException>(() => new WorldAssets(Path.GetTempPath(), resources: new ResourceService(source)));
        Assert.Empty(source.OpenedFamilies);
        Assert.False(source.Disposed);
    }

    [Fact]
    public void CompleteWorldSourceDoesNotRequireArchiveCapability()
    {
        using IResourceSource installed = GameDataSourceFactory.Open(TestResources.GameDirectory);
        using ResourceOnlySource source = new(installed);
        using (WorldAssets world = new(Path.GetTempPath(), resources: new ResourceService(source)))
        {
            _ = world.GetSegment(0);
            Assert.Equal(1, world.LoadedSegmentCount);
            Assert.Contains(new ResourceKey("world/wmx.obj"), source.Reads);
            Assert.Contains(new ResourceKey("world/wmsetus.obj"), source.Reads);
        }
        Assert.False(source.Disposed);
    }

    private sealed class ResourceOnlySource(IResourceSource installed) : IResourceSource
    {
        public ResourceSourceDescriptor Descriptor => installed.Descriptor;
        public HashSet<ResourceKey> Reads { get; } = [];
        public bool Disposed { get; private set; }

        public ResourceContent? Find(ResourceKey key)
        {
            Reads.Add(key);
            return installed.Find(key);
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    [Fact]
    public void FailedFieldCatalogReadDisposesOpenedArchiveWithoutDisposingSuppliedSource()
    {
        using CatalogSource source = new() { FailMaplist = true };
        Assert.Throws<InvalidDataException>(() => new GameAssets(Path.GetTempPath(), resources: new ResourceService(source)));
        Assert.Contains("field/mapdata/maplist", source.ReadKeys);
        Assert.True(source.RootDisposed);
        Assert.False(source.Disposed);
    }

    private sealed class CatalogSource : IResourceSource, IGameArchiveSource, IGameFileSource
    {
        private readonly Dictionary<string, byte[]> contents;

        public CatalogSource()
        {
            byte[] maplist = Encoding.ASCII.GetBytes("test\n");
            byte[] listing = Encoding.ASCII.GetBytes(Albeoris.Games.FF8.FlArchives.FlArchive.InternalPathPrefix + "maplist\r\n");
            using MemoryStream index = new();
            using (BinaryWriter writer = new(index, Encoding.ASCII, true))
            {
                writer.Write((uint)maplist.Length);
                writer.Write(0u);
                writer.Write(0);
            }
            contents = new Dictionary<string, byte[]>
            {
                ["field/mapdata.fl"] = listing,
                ["field/mapdata.fi"] = index.ToArray(),
                ["field/mapdata.fs"] = maplist,
                ["field/mapdata/maplist"] = maplist
            };
        }

        public ResourceSourceDescriptor Descriptor { get; } = new("memory", GameDataEdition.Classic, []);
        public List<string> OpenedFamilies { get; } = [];
        public List<string> ReadKeys { get; } = [];
        public List<string> ResolvedFiles { get; } = [];
        public string? MoviePath { get; init; }
        public string ArchiveFamily { get; init; } = "field";
        public bool FailEntryEnumeration { get; init; }
        public bool FailMaplist { get; init; }
        public byte[]? ExecutableBytes { get; init; }
        public int ExecutableAuthenticationFailures { get; set; }
        public bool RootDisposed { get; private set; }
        public bool Disposed { get; private set; }

        public IFlArchive OpenArchive(string family)
        {
            OpenedFamilies.Add(family);
            Assert.Equal(ArchiveFamily, family);
            return new CatalogArchive(contents.Where(pair => pair.Key.Count(character => character == '/') == 1).Select(pair => new CatalogEntry(pair.Key.Split('/')[^1], pair.Value)).ToArray(), () => RootDisposed = true, FailEntryEnumeration);
        }

        public ResourceContent? Find(ResourceKey key)
        {
            ReadKeys.Add(key.Path);
            if (FailMaplist && key.Path == "field/mapdata/maplist")
            {
                throw new InvalidDataException("Invalid field catalog.");
            }
            if (key.Path == "runtime/native-executable")
            {
                if (ExecutableAuthenticationFailures > 0)
                {
                    ExecutableAuthenticationFailures--;
                    throw new NotSupportedException("The provider rejected unauthenticated executable bytes.");
                }
                if (ExecutableBytes is byte[] executable)
                {
                    return new ResourceContent(key, "bin", 1, "memory", () => new MemoryStream(executable, false));
                }
            }
            return contents.TryGetValue(key.Path, out byte[]? bytes) ? new ResourceContent(key, "bin", 1, "memory", () => new MemoryStream(bytes, false)) : null;
        }

        public string ResolveFile(string relativeDataPath)
        {
            ResolvedFiles.Add(relativeDataPath);
            return relativeDataPath == "movies/disc00_01.bik" && MoviePath is not null ? MoviePath : throw new FileNotFoundException();
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class CatalogArchive(IReadOnlyList<IFlArchiveEntry> entries, Action dispose, bool failEntryEnumeration = false) : IFlArchive
    {
        public IReadOnlyList<IFlArchiveEntry> Entries => failEntryEnumeration ? throw new InvalidDataException("Invalid archive listing.") : entries;
        public IFlArchiveEntry AddEntry(string relativePath) => throw new NotSupportedException();
        public void RemoveEntry(string relativePath) => throw new NotSupportedException();
        public void Flush() => throw new NotSupportedException();
        public void Dispose() => dispose();
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CatalogEntry(string relativePath, byte[] bytes) : IFlArchiveEntry
    {
        public string RelativePath => relativePath;
        public uint Offset => 0;
        public uint Size => checked((uint)bytes.Length);
        public Stream OpenForRead() => new MemoryStream(bytes, false);
        public Stream OpenForWrite(uint desiredSize) => throw new NotSupportedException();
    }
}
