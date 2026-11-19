using Memoria.FF8.Resources;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets
{
    internal static class TextureAuthoringSnapshots
    {
        public static MemoryStream Read(IFlArchiveEntry entry)
        {
            if (entry.Size < 0 || entry.Size > 256L * 1024 * 1024)
            {
                throw new InvalidDataException("The authoring source snapshot exceeds its 256 MiB unit limit.");
            }
            byte[] snapshot = new byte[checked((int)entry.Size)];
            using Stream input = entry.OpenForRead();
            input.ReadExactly(snapshot);
            return new(snapshot, 0, snapshot.Length, false, true);
        }
    }

    public sealed partial class ClassicGameDataSource : ITextureAuthoringResourceSource
    {
        private readonly Queue<string> textureAuthoringArchives = [];
        private readonly Dictionary<string, long> textureAuthoringArchiveBytes = [];
        public bool IsTextureAuthoringSource { get; private set; }
        public int RetainedTextureAuthoringArchiveCount => textureAuthoringArchives.Count;
        public long RetainedTextureAuthoringArchiveBytes => textureAuthoringArchiveBytes.Values.Sum();
        internal void ConfigureTextureAuthoringCache() => IsTextureAuthoringSource = true;

        private void RememberTextureAuthoringArchive(string key, long bytes)
        {
            if (!IsTextureAuthoringSource)
            {
                return;
            }
            textureAuthoringArchives.Enqueue(key);
            textureAuthoringArchiveBytes[key] = bytes;
            while (textureAuthoringArchives.Count > 4 || textureAuthoringArchives.Count > 1 && RetainedTextureAuthoringArchiveBytes > 64L * 1024 * 1024)
            {
                ReleaseTextureAuthoringArchive(textureAuthoringArchives.Dequeue());
            }
        }

        private void ReleaseTextureAuthoringArchive(string key)
        {
            foreach (string path in archives.Keys.Where(path => path == key || path.StartsWith(key + "/", StringComparison.Ordinal)).OrderByDescending(path => path.Length).ToArray())
            {
                archives[path].Dispose();
                archives.Remove(path);
                textureAuthoringArchiveBytes.Remove(path);
            }
            foreach (string path in catalog.Keys.Where(path => path.StartsWith(key + "/", StringComparison.Ordinal)).ToArray())
            {
                catalog.Remove(path);
            }
        }

        public void ReleaseTextureAuthoringArchives()
        {
            lock (archiveGate)
            {
                if (!IsTextureAuthoringSource)
                {
                    throw new InvalidOperationException("Only independent authoring sources may release their archive units.");
                }
                while (textureAuthoringArchives.TryDequeue(out string? key))
                {
                    ReleaseTextureAuthoringArchive(key);
                }
            }
        }

        private ResourceContent TextureAuthoringContent(ResourceKey key, string format) => new(key, format, 1, $"classic:{key}", () => OpenTextureAuthoringSnapshot(key));

        private Stream OpenTextureAuthoringSnapshot(ResourceKey key)
        {
            lock (archiveGate)
            {
                return (FindCore(key, true) ?? throw new FileNotFoundException($"Authoring resource {key} is unavailable.")).OpenRead();
            }
        }
    }
}

namespace Memoria.FF8.Assets.Remastered
{
    public sealed partial class RemasteredGameDataSource : ITextureAuthoringResourceSource
    {
        private readonly Queue<string> textureAuthoringArchives = [];
        private readonly Dictionary<string, long> textureAuthoringArchiveBytes = [];
        public bool IsTextureAuthoringSource { get; private set; }
        public int RetainedTextureAuthoringArchiveCount => textureAuthoringArchives.Count;
        public long RetainedTextureAuthoringArchiveBytes => textureAuthoringArchiveBytes.Values.Sum();
        internal void ConfigureTextureAuthoringCache() => IsTextureAuthoringSource = true;

        private void RememberTextureAuthoringArchive(string key, long bytes)
        {
            if (!IsTextureAuthoringSource)
            {
                return;
            }
            textureAuthoringArchives.Enqueue(key);
            textureAuthoringArchiveBytes[key] = bytes;
            while (textureAuthoringArchives.Count > 4 || textureAuthoringArchives.Count > 1 && RetainedTextureAuthoringArchiveBytes > 64L * 1024 * 1024)
            {
                ReleaseTextureAuthoringArchive(textureAuthoringArchives.Dequeue());
            }
        }

        private void ReleaseTextureAuthoringArchive(string key)
        {
            foreach (string path in archives.Keys.Where(path => path == key || path.StartsWith(key + "/", StringComparison.Ordinal)).OrderByDescending(path => path.Length).ToArray())
            {
                archives[path].Dispose();
                archives.Remove(path);
                textureAuthoringArchiveBytes.Remove(path);
            }
            foreach (ResourceKey resource in catalog.Keys.Where(resource => resource.Path.StartsWith(key + "/", StringComparison.Ordinal)).ToArray())
            {
                catalog.Remove(resource);
            }
        }

        public void ReleaseTextureAuthoringArchives()
        {
            lock (archiveGate)
            {
                if (!IsTextureAuthoringSource)
                {
                    throw new InvalidOperationException("Only independent authoring sources may release their archive units.");
                }
                while (textureAuthoringArchives.TryDequeue(out string? key))
                {
                    ReleaseTextureAuthoringArchive(key);
                }
            }
        }

        private ResourceContent TextureAuthoringContent(ResourceKey key, string format) => new(key, format, 1, $"{Descriptor.Id}:{key}", () => OpenTextureAuthoringSnapshot(key));

        private Stream OpenTextureAuthoringSnapshot(ResourceKey key)
        {
            lock (archiveGate)
            {
                return (FindCore(key, true) ?? throw new FileNotFoundException($"Authoring resource {key} is unavailable.")).OpenRead();
            }
        }
    }
}
