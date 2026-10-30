namespace Memoria.FF8;

internal static class OriginalSavePublication
{
    private static readonly object PublicationLock = new();

    public static void Publish(string temporarySavePath, string destinationPath, bool overwrite, bool refreshPreview = false)
    {
        lock (PublicationLock)
        {
            PublishCore(Path.GetFullPath(temporarySavePath), Path.GetFullPath(destinationPath), overwrite, refreshPreview);
        }
    }

    private static void PublishCore(string temporarySavePath, string destinationPath, bool overwrite, bool refreshPreview)
    {
        if (string.Equals(temporarySavePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Original save publication requires a separate staged file.");
        }
        string? metadataTemporary = null;
        string? rollbackTemporary = null;
        bool published = false;
        byte[] content = [];
        byte[]? previousSave = null;
        DateTime? previousTimestamp = null;
        try
        {
            using (FileStream staged = new(temporarySavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                staged.Flush(flushToDisk: true);
            }
            content = File.ReadAllBytes(temporarySavePath);
            previousSave = File.Exists(destinationPath) ? File.ReadAllBytes(destinationPath) : null;
            previousTimestamp = previousSave is null ? null : File.GetLastWriteTimeUtc(destinationPath);
            if (!overwrite && (previousSave is not null || Directory.Exists(destinationPath)))
            {
                throw new IOException("The original save destination already exists.");
            }
            if (refreshPreview)
            {
                content = OriginalSavePreviewSequence.Refresh(content, destinationPath);
                using FileStream staged = new(temporarySavePath, FileMode.Truncate, FileAccess.Write, FileShare.None);
                staged.Write(content);
                staged.Flush(flushToDisk: true);
            }
            OriginalSaveMetadataUpdate? metadata = OriginalSaveMetadata.Prepare(destinationPath, content);
            if (metadata is not null)
            {
                metadataTemporary = NewTemporary(metadata.Path);
                WriteFlushed(metadataTemporary, metadata.UpdatedBytes.AsSpan());
                RequireUnchanged(metadata.Path, metadata.OriginalBytes?.ToArray(), "Original save metadata changed during publication.");
            }
            RequireUnchanged(destinationPath, previousSave, "The original save destination changed during publication.");
            File.Move(temporarySavePath, destinationPath, overwrite);
            published = true;
            if (metadata is not null)
            {
                RequireUnchanged(metadata.Path, metadata.OriginalBytes?.ToArray(), "Original save metadata changed during publication.");
                File.Move(metadataTemporary!, metadata.Path, overwrite: metadata.OriginalBytes.HasValue);
            }
        }
        catch
        {
            if (published)
            {
                RequireUnchanged(destinationPath, content, "The original save changed after publication failed; its newer contents were preserved.");
                if (previousSave is null)
                {
                    File.Delete(destinationPath);
                }
                else
                {
                    rollbackTemporary = NewTemporary(destinationPath);
                    WriteFlushed(rollbackTemporary, previousSave);
                    File.Move(rollbackTemporary, destinationPath, overwrite: true);
                    File.SetLastWriteTimeUtc(destinationPath, previousTimestamp!.Value);
                }
            }
            throw;
        }
        finally
        {
            DeleteTemporary(metadataTemporary);
            DeleteTemporary(rollbackTemporary);
            DeleteTemporary(temporarySavePath);
        }
    }

    private static void RequireUnchanged(string path, byte[]? expected, string message)
    {
        bool exists = File.Exists(path);
        if (Directory.Exists(path) || exists != (expected is not null) || exists && !File.ReadAllBytes(path).AsSpan().SequenceEqual(expected))
        {
            throw new IOException(message);
        }
    }

    private static string NewTemporary(string path)
    {
        return Path.Combine(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
    }

    private static void WriteFlushed(string path, ReadOnlySpan<byte> bytes)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void DeleteTemporary(string? path)
    {
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
