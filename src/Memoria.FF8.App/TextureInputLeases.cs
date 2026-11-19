using System.Security.Cryptography;

namespace Memoria.FF8;

/// <summary>Read leases keep the validated input revision coherent through owner-thread publication.</summary>
internal sealed class TextureInputLeases : IDisposable
{
    private sealed record Input(string Path, FileStream? Stream, long Length, DateTime Modified, byte[]? Hash);
    private readonly List<Input> inputs = [];

    internal TextureInputLeases(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (string path in paths.Select(Path.GetFullPath).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (inputs.Count >= 512)
                {
                    throw new InvalidDataException("Select at most 256 independent image/workspace inputs per coherent import transaction.");
                }
                if (!File.Exists(path))
                {
                    inputs.Add(new(path, null, 0, default, null));
                    continue;
                }
                FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
                try
                {
                    FileInfo stamp = new(path);
                    if (input.Length > 128L * 1024 * 1024)
                    {
                        throw new InvalidDataException("The texture input exceeds its 128 MiB limit.");
                    }
                    byte[] hash = SHA256.HashData(input);
                    input.Position = 0;
                    inputs.Add(new(path, input, input.Length, stamp.LastWriteTimeUtc, hash));
                }
                catch
                {
                    input.Dispose();
                    throw;
                }
            }
            ValidateStamps();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void ValidateContents(CancellationToken cancellationToken = default)
    {
        ValidateStamps();
        foreach (Input input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (input.Stream is not { } stream)
            {
                continue;
            }
            stream.Position = 0;
            if (!SHA256.HashData(stream).AsSpan().SequenceEqual(input.Hash))
            {
                throw Changed(input.Path);
            }
            stream.Position = 0;
        }
        ValidateStamps();
    }

    internal void ValidateStamps()
    {
        foreach (Input input in inputs)
        {
            FileInfo current = new(input.Path);
            if (input.Stream is null)
            {
                if (current.Exists)
                {
                    throw Changed(input.Path);
                }
            }
            else if (!current.Exists || current.Length != input.Length || current.LastWriteTimeUtc != input.Modified || input.Stream.Length != input.Length)
            {
                throw Changed(input.Path);
            }
        }
    }

    public void Dispose()
    {
        foreach (Input input in inputs)
        {
            input.Stream?.Dispose();
        }
        inputs.Clear();
    }

    private static InvalidDataException Changed(string path) => new($"{Path.GetFileName(path)} changed after review. Refresh / reimport before installing.");
}
