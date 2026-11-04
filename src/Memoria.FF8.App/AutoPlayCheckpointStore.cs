using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class AutoPlayCheckpointStore
{
    private const int MinimumBlockSize = 8192;
    private const int BlockSize = 131072;
    private static readonly ulong[] Gear = CreateGear();
    internal const long MaxBlockBytes = 64 * 1024 * 1024;
    private const string Marker = "{\"AutoPlayCheckpoint\":";
    private sealed record Block(string Hash, int Length);
    private sealed record Index(int AutoPlayCheckpoint, long Length, List<Block> Blocks);

    public static bool IsStored(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Span<byte> prefix = stackalloc byte[Marker.Length];
        return stream.ReadAtLeast(prefix, prefix.Length, throwOnEndOfStream: false) == prefix.Length && prefix.SequenceEqual(System.Text.Encoding.UTF8.GetBytes(Marker));
    }

    public static void Write(string compactSource, string destination, long maxBlockBytes = MaxBlockBytes)
    {
        if (IsStored(compactSource))
        {
            Read(compactSource, materialized =>
            {
                Write(materialized, destination, maxBlockBytes);
                return true;
            });
            return;
        }
        string directory = Path.GetDirectoryName(Path.GetFullPath(destination))!;
        string blocksDirectory = Path.Combine(directory, ".checkpoint-blocks");
        Directory.CreateDirectory(blocksDirectory);
        CheckManagedPath(blocksDirectory);
        long storedBytes = Directory.EnumerateFiles(blocksDirectory).Sum(path => new FileInfo(path).Length);
        using FileStream source = File.OpenRead(compactSource);
        using Stream payload = OpenPayload(source);
        byte[] buffer = new byte[BlockSize];
        List<Block> blocks = [];
        long length = 0;
        void StoreBlock(int count)
        {
            string hash = Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, count))).ToLowerInvariant();
            string blockPath = Path.Combine(blocksDirectory, hash + ".br");
            if (File.Exists(blockPath))
            {
                ReadBlock(blockPath, new(hash, count));
            }
            else
            {
                string temporaryBlock = blockPath + ".tmp";
                try
                {
                    using (FileStream file = File.Create(temporaryBlock))
                    using (BrotliStream compressed = new(file, CompressionLevel.Fastest))
                    {
                        compressed.Write(buffer, 0, count);
                    }
                    long compressedBytes = new FileInfo(temporaryBlock).Length;
                    if (storedBytes > maxBlockBytes - compressedBytes)
                    {
                        throw new IOException("Automatic exploration checkpoint capture budget exceeded; retain a standalone checkpoint and collect unreferenced blocks before continuing.");
                    }
                    File.Move(temporaryBlock, blockPath);
                    storedBytes += compressedBytes;
                }
                finally
                {
                    File.Delete(temporaryBlock);
                }
            }
            blocks.Add(new(hash, count));
            length = checked(length + count);
        }
        byte[] input = new byte[65536];
        int count = 0;
        ulong rolling = 0;
        int read;
        while ((read = payload.Read(input)) != 0)
        {
            for (int i = 0; i < read; i++)
            {
                byte value = input[i];
                buffer[count++] = value;
                rolling = unchecked((rolling << 1) + Gear[value]);
                if (count >= MinimumBlockSize && ((rolling & 32767) == 0 || count == BlockSize))
                {
                    StoreBlock(count);
                    count = 0;
                    rolling = 0;
                }
            }
        }
        if (count > 0)
        {
            StoreBlock(count);
        }
        if (length == 0)
        {
            throw new InvalidDataException("A checkpoint payload cannot be empty.");
        }
        payload.Dispose();
        source.Dispose();
        string temporary = destination + ".index-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Index(1, length, blocks)));
            AutoPlayController.CommitCheckpointFile(temporary, destination);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static Stream OpenPayload(FileStream source)
    {
        using BinaryReader reader = new(source, System.Text.Encoding.UTF8, leaveOpen: true);
        if (!reader.ReadBytes(8).AsSpan().SequenceEqual("MFF8SAVE"u8))
        {
            source.Position = 0;
            return source;
        }
        if (reader.ReadInt32() != 1)
        {
            throw new InvalidDataException("Checkpoint source must be a supported compact save.");
        }
        int summaryLength = reader.ReadInt32();
        long payloadLength = reader.ReadInt64();
        if (summaryLength is < 1 or > 65536 || payloadLength < 1 || source.Length - 88 - summaryLength != payloadLength)
        {
            throw new InvalidDataException("Invalid compact checkpoint lengths.");
        }
        byte[] summaryHash = reader.ReadBytes(32);
        byte[] payloadHash = reader.ReadBytes(32);
        byte[] summary = reader.ReadBytes(summaryLength);
        if (!CryptographicOperations.FixedTimeEquals(summaryHash, SHA256.HashData(summary)) || !CryptographicOperations.FixedTimeEquals(payloadHash, SHA256.HashData(source)))
        {
            throw new InvalidDataException("Compact checkpoint checksum does not match.");
        }
        source.Position = 88 + summaryLength;
        return new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
    }

    public static GameState Load(string path) => Read(path, GameSession.Load);

    public static T ReadProjection<T>(string path) where T : class => Read(path, GameSession.ReadSnapshotProjection<T>);

    public static void Validate(string path)
    {
        Read(path, materialized => GameSession.ReadSnapshotProjection<Dictionary<string, JsonElement>>(materialized));
    }

    private static T Read<T>(string path, Func<string, T> read)
    {
        if (!IsStored(path))
        {
            return read(path);
        }
        Index index = ReadIndex(path);
        string temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, ".checkpoint-read-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                foreach (Block block in index.Blocks)
                {
                    output.Write(ReadBlock(BlockPath(path, block), block));
                }
            }
            return read(temporary);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static Index ReadIndex(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Index index = JsonSerializer.Deserialize<Index>(stream) ?? throw new InvalidDataException("Empty checkpoint index.");
        if (index.AutoPlayCheckpoint != 1 || index.Length <= 0 || index.Blocks is null || index.Blocks.Count == 0)
        {
            throw new InvalidDataException("Unsupported checkpoint index.");
        }
        long length = 0;
        for (int i = 0; i < index.Blocks.Count; i++)
        {
            Block block = index.Blocks[i];
            if (block is null || block.Hash is null || block.Hash.Length != 64 || block.Hash.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) || block.Length is < 1 or > BlockSize || i < index.Blocks.Count - 1 && block.Length < MinimumBlockSize)
            {
                throw new InvalidDataException("Invalid checkpoint block.");
            }
            length = checked(length + block.Length);
        }
        if (length != index.Length)
        {
            throw new InvalidDataException("Checkpoint length does not match its blocks.");
        }
        return index;
    }

    private static string BlockPath(string path, Block block)
    {
        string directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, ".checkpoint-blocks");
        CheckManagedPath(directory);
        return Path.Combine(directory, block.Hash + ".br");
    }

    private static void CheckManagedPath(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Checkpoint storage cannot follow redirected filesystem entries.");
        }
    }

    private static ulong[] CreateGear()
    {
        ulong[] gear = new ulong[256];
        ulong seed = 0;
        for (int i = 0; i < gear.Length; i++)
        {
            seed = unchecked(seed + 0x9e3779b97f4a7c15UL);
            ulong value = seed;
            value = unchecked((value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL);
            value = unchecked((value ^ (value >> 27)) * 0x94d049bb133111ebUL);
            gear[i] = value ^ (value >> 31);
        }
        return gear;
    }

    private static byte[] ReadBlock(string path, Block block)
    {
        CheckManagedPath(path);
        using FileStream source = File.OpenRead(path);
        using BrotliStream compressed = new(source, CompressionMode.Decompress);
        byte[] bytes = new byte[block.Length];
        compressed.ReadExactly(bytes);
        if (compressed.ReadByte() != -1 || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(block.Hash), SHA256.HashData(bytes)))
        {
            throw new InvalidDataException("Checkpoint block checksum or length does not match.");
        }
        return bytes;
    }

    public static void Collect(string directory, IEnumerable<string> retainedCheckpoints)
    {
        directory = Path.GetFullPath(directory);
        HashSet<string> retained = new(StringComparer.Ordinal);
        Dictionary<string, int> lengths = new(StringComparer.Ordinal);
        foreach (string checkpoint in retainedCheckpoints)
        {
            if (string.IsNullOrEmpty(checkpoint) || checkpoint != Path.GetFileName(checkpoint) || checkpoint is "." or "..")
            {
                throw new InvalidDataException("Retained checkpoint must be a filename within its run directory.");
            }
            string path = Path.Combine(directory, checkpoint);
            if (IsStored(path))
            {
                foreach (Block block in ReadIndex(path).Blocks)
                {
                    if (lengths.TryGetValue(block.Hash, out int existingLength))
                    {
                        if (existingLength != block.Length)
                        {
                            throw new InvalidDataException("Shared checkpoint block has conflicting lengths.");
                        }
                    }
                    else
                    {
                        ReadBlock(BlockPath(path, block), block);
                        lengths.Add(block.Hash, block.Length);
                    }
                    retained.Add(block.Hash + ".br");
                }
            }
        }
        string blocksDirectory = Path.Combine(directory, ".checkpoint-blocks");
        if (!Directory.Exists(blocksDirectory))
        {
            return;
        }
        CheckManagedPath(blocksDirectory);
        foreach (string block in Directory.EnumerateFiles(blocksDirectory, "*.br"))
        {
            string name = Path.GetFileNameWithoutExtension(block);
            if (name.Length == 64 && name.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f') && !retained.Contains(Path.GetFileName(block)))
            {
                File.Delete(block);
            }
        }
    }
}
