using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldResourceHashTests
{
    [Fact]
    public void InstalledProgramAndWalkmeshHashesReadOnlyTheirEncodedLeavesAndReuseTheHashCache()
    {
        string directory = TestResources.GameDirectory;
        FieldAssets original;
        using (GameAssets direct = new(directory))
        {
            original = direct.LoadField(139);
        }
        using CountingSource source = new(directory);
        ResourceService resources = new(source);
        using GameAssets assets = new(directory, resources: resources);
        source.OpenedKeys.Clear();

        string program = assets.ReadFieldProgramHash(original.Id);
        string walkmesh = assets.ReadFieldWalkmeshHash(original.Id);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(original.Jsm)), program);
        Assert.Equal(original.WalkmeshHash, walkmesh);
        string programKey = $"field/{original.Name}/{original.Name}.jsm";
        string walkmeshKey = $"field/{original.Name}/{original.Name}.id";
        Assert.Equal(new[] { programKey, walkmeshKey }, source.OpenedKeys);
        Assert.DoesNotContain(source.OpenedKeys, key => key == $"field/{original.Name}.fl" || key == $"field/{original.Name}.fi" || key == $"field/{original.Name}.fs");

        Assert.Equal(program, assets.ReadFieldProgramHash(original.Id));
        Assert.Equal(walkmesh, assets.ReadFieldWalkmeshHash(original.Id));
        Assert.Equal(new[] { programKey, walkmeshKey }, source.OpenedKeys);
    }

    [Fact]
    public void AnInstalledProgramOverrideKeepsItsOriginalArchivePathAndNeverUsesTheOriginalHashCache()
    {
        string directory = TestResources.GameDirectory;
        FieldAssets original;
        using (GameAssets direct = new(directory))
        {
            original = direct.LoadField(139);
        }
        using CountingSource source = new(directory);
        ProgramOverride replacement = new(original.Name + ".jsm", [1, 2, 3]);
        using GameAssets assets = new(directory, replacement, new ResourceService(source));
        source.OpenedKeys.Clear();

        string first = assets.ReadFieldProgramHash(original.Id);
        replacement.Bytes = [4, 5, 6, 7];
        string second = assets.ReadFieldProgramHash(original.Id);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })), first);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(replacement.Bytes)), second);
        Assert.NotEqual(first, second);
        Assert.Equal(2, replacement.AppliedPaths.Count);
        Assert.All(replacement.AppliedPaths, path => Assert.EndsWith(original.Name + ".jsm", path, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain($"field/{original.Name}/{original.Name}.jsm", source.OpenedKeys);
        Assert.Equal(2, source.OpenedKeys.Count(key => key == $"field/{original.Name}.fs"));
    }

    private sealed class CountingSource(string directory) : IResourceSource, IGameArchiveSource
    {
        private readonly ClassicGameDataSource original = new(directory);

        public ResourceSourceDescriptor Descriptor => original.Descriptor;
        public List<string> OpenedKeys { get; } = [];

        public ResourceContent? Find(ResourceKey key)
        {
            ResourceContent? content = original.Find(key);
            if (content is null)
            {
                return null;
            }
            return content with
            {
                OpenRead = () =>
                {
                    OpenedKeys.Add(key.Path);
                    return content.OpenRead();
                }
            };
        }

        public IFlArchive OpenArchive(string family)
        {
            return original.OpenArchive(family);
        }

        public void Dispose()
        {
            original.Dispose();
        }
    }

    private sealed class ProgramOverride(string basename, byte[] bytes) : IAssetOverrideSource
    {
        public byte[] Bytes { get; set; } = bytes;
        public List<string> AppliedPaths { get; } = [];

        public bool TryRead(string logicalPath, out byte[] content)
        {
            if (logicalPath.Replace('\\', '/').Split('/')[^1].Equals(basename, StringComparison.OrdinalIgnoreCase))
            {
                AppliedPaths.Add(logicalPath);
                content = Bytes;
                return true;
            }
            content = [];
            return false;
        }
    }
}
