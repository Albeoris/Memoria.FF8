using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets;

public sealed record TextureExportProgress(string Stage, int Completed, long BytesWritten, string? Current = null);
public sealed record TextureExportResult(int Written, long BytesWritten, IReadOnlyList<string> Files);

/// <summary>Sequential CPU exchange. Call on a worker owning its GameAssets source; decoded memory is bounded by one source unit.</summary>
public static class TextureExchange
{
    private const int MaximumMetadataBytes = TextureExchangeMetadata.MaximumBytes;
    private static readonly byte[] MetadataKeyword = "Memoria.Texture\0"u8.ToArray();

    public static TextureExportResult Export(IEnumerable<TextureSourceSnapshot> textures, string destination, bool resolved = false, CancellationToken cancellationToken = default, IProgress<TextureExportProgress>? progress = null, TextureExportJournal? journal = null)
    {
        return ExportSources(textures.Select(snapshot => (snapshot.Context, resolved ? snapshot.Resolved : null)), destination, cancellationToken, progress, journal);
    }

    public static TextureExportResult Export(GameAssets assets, IEnumerable<TextureSourceSnapshot> textures, string destination, bool resolved = false, CancellationToken cancellationToken = default, IProgress<TextureExportProgress>? progress = null, TextureExportJournal? journal = null)
    {
        IEnumerable<(TextureBuildContext, TextureValue?)> Read()
        {
            foreach (TextureSourceSnapshot snapshot in textures)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (TextureBuildContext context in assets.LoadAuthoringTextures(snapshot))
                {
                    yield return (context, resolved ? ReferenceEquals(context, snapshot.Context) ? snapshot.Resolved : assets.Resources.ResolveForExport(context) : null);
                }
            }
        }
        return ExportSources(Read(), destination, cancellationToken, progress, journal);
    }

    public static TextureExportResult Export(GameAssets assets, IEnumerable<TextureCatalogEntry> textures, string destination, bool resolved = false, CancellationToken cancellationToken = default, IProgress<TextureExportProgress>? progress = null, TextureExportJournal? journal = null)
    {
        IEnumerable<(TextureBuildContext, TextureValue?)> Read()
        {
            string? previousUnit = null;
            IReadOnlyList<TextureBuildContext> unit = [];
            foreach (TextureCatalogEntry entry in textures)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new("Preparing", 0, 0, entry.Name));
                string locator = entry.Reader == "texl" ? $"{entry.SourceUnit}:{entry.Index}" : entry.SourceUnit;
                if (locator != previousUnit)
                {
                    unit = assets.LoadCatalogTextureUnit(entry);
                    previousUnit = locator;
                }
                TextureBuildContext context = unit.Single(context => TextureTarget.FromContext(context) == entry.Target);
                yield return (context, resolved ? assets.Resources.ResolveForExport(context) : null);
            }
        }
        return ExportSources(Read(), destination, cancellationToken, progress, journal);
    }

    private static TextureExportResult ExportSources(IEnumerable<(TextureBuildContext Context, TextureValue? Resolved)> sources, string destination, CancellationToken cancellationToken, IProgress<TextureExportProgress>? progress, TextureExportJournal? journal)
    {
        string root = Path.GetFullPath(destination);
        Directory.CreateDirectory(root);
        List<string> files = [];
        HashSet<TextureTarget> seen = [];
        Dictionary<(ResourceKey, long), string> sourceHashes = [];
        long bytesWritten = 0;
        foreach ((TextureBuildContext context, TextureValue? resolved) in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Layout is "native-effect-texture" or "native-pc-texture-upload" or "native-battle-gf-atlas")
            {
                throw new NotSupportedException($"Texture {context.Key} is a transient native sampling window. Export its complete source-owned texture from the installation catalog.");
            }
            TextureTarget target = TextureTarget.FromContext(context, context.Game);
            if (!seen.Add(target))
            {
                journal?.AddAlias();
                continue;
            }
            PixelImage original = context.Original;
            PixelImage selected = resolved?.Image ?? original;
            bool nativeFlags = TexturePixelExchange.UsesNativeFlags(context);
            PixelImage editable = nativeFlags ? TexturePixelExchange.ToEditable(selected) : selected;
            string? sourceHash = null;
            if (context.EncodedSource is { } encoded)
            {
                if (!sourceHashes.TryGetValue((encoded.Key, encoded.Revision), out sourceHash))
                {
                    using Stream source = encoded.OpenRead();
                    sourceHash = Convert.ToHexString(SHA256.HashData(source));
                    sourceHashes.Add((encoded.Key, encoded.Revision), sourceHash);
                }
            }
            TextureExchangeMetadata metadata = new(1, target, TextureTargetContract.FromContext(context), sourceHash, PixelFingerprint(nativeFlags ? TexturePixelExchange.ToEditable(original) : original), PixelFingerprint(editable), resolved?.Provenance ?? context.Source.Id, nativeFlags ? TexturePixelExchange.CaptureNativeFlags(selected) : null) { NativeWidth = nativeFlags ? selected.Width : null, NativeHeight = nativeFlags ? selected.Height : null };
            byte[] serialized = metadata.ToBytes();
            byte[] png = EmbedMetadata(LocalizationArtworkExporter.EncodePng(editable), serialized);
            string path = SafeOutput(root, target.CanonicalPath());
            if (File.Exists(path) || File.Exists(path + ".texture.json"))
            {
                throw new IOException($"Export preserves existing edits: {target.CanonicalPath()} already exists. Choose a fresh working folder.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteAtomic(path, png, cancellationToken);
            WriteAtomic(path + ".texture.json", serialized, cancellationToken);
            files.Add(path);
            bytesWritten += png.Length + serialized.Length;
            journal?.AddImage(path, metadata, png.Length + serialized.Length);
            progress?.Report(new("Writing", files.Count, bytesWritten, target.Owner));
        }
        return new(files.Count, bytesWritten, files);
    }

    public static string PixelFingerprint(PixelImage image)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> dimensions = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(dimensions, image.Width);
        BinaryPrimitives.WriteInt32LittleEndian(dimensions[4..], image.Height);
        hash.AppendData(dimensions);
        hash.AppendData(MemoryMarshal.AsBytes(image.Pixels.AsSpan()));
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>Sidecar and embedded identities must agree; callers also compare canonical input paths.</summary>
    public static TextureExchangeMetadata? ReadMetadata(string imagePath)
    {
        string sidecarPath = imagePath + ".texture.json";
        TextureExchangeMetadata? sidecar = null;
        if (File.Exists(sidecarPath))
        {
            FileInfo info = new(sidecarPath);
            if (info.Length > MaximumMetadataBytes)
            {
                throw new InvalidDataException("Texture metadata exceeds its size limit.");
            }
            sidecar = TextureExchangeMetadata.Parse(File.ReadAllBytes(sidecarPath));
        }
        using FileStream input = File.OpenRead(imagePath);
        TextureExchangeMetadata? embedded = ReadEmbeddedMetadata(input);
        if (sidecar is not null && embedded is not null && (sidecar.Target != embedded.Target || sidecar.Contract != embedded.Contract))
        {
            throw new InvalidDataException($"Texture metadata conflicts between {imagePath} and its sidecar. Select the intended target explicitly.");
        }
        return sidecar ?? embedded;
    }

    public static TextureExchangeMetadata? TryReadEmbeddedMetadata(byte[] png)
    {
        using MemoryStream input = new(png, false);
        return ReadEmbeddedMetadata(input);
    }

    private static TextureExchangeMetadata? ReadEmbeddedMetadata(Stream input)
    {
        Span<byte> signature = stackalloc byte[8];
        if (input.Read(signature) != 8 || !signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            return null;
        }
        TextureExchangeMetadata? result = null;
        Span<byte> header = stackalloc byte[8];
        while (input.Position < input.Length)
        {
            input.ReadExactly(header);
            uint length = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (length > input.Length - input.Position - 4)
            {
                throw new InvalidDataException("The PNG chunk exceeds its container.");
            }
            if (header[4..].SequenceEqual("iTXt"u8) && length <= MaximumMetadataBytes)
            {
                byte[] data = new byte[length];
                input.ReadExactly(data);
                if (data.AsSpan().StartsWith(MetadataKeyword))
                {
                    int start = MetadataKeyword.Length;
                    if (data.Length < start + 4 || !data.AsSpan(start, 4).SequenceEqual(new byte[4]))
                    {
                        throw new InvalidDataException("Compressed or translated texture metadata is unsupported.");
                    }
                    TextureExchangeMetadata parsed = TextureExchangeMetadata.Parse(data[(start + 4)..]);
                    if (result is not null)
                    {
                        throw new InvalidDataException("The PNG contains duplicate texture identities.");
                    }
                    result = parsed;
                }
            }
            else
            {
                input.Seek(length, SeekOrigin.Current);
            }
            input.Seek(4, SeekOrigin.Current);
            if (header[4..].SequenceEqual("IEND"u8))
            {
                break;
            }
        }
        return result;
    }

    private static byte[] EmbedMetadata(byte[] png, byte[] metadata)
    {
        if (metadata.Length > MaximumMetadataBytes)
        {
            throw new InvalidDataException("Texture metadata exceeds its size limit.");
        }
        using MemoryStream output = new();
        output.Write(png.AsSpan(0, 33));
        byte[] chunk = new byte[4 + MetadataKeyword.Length + 4 + metadata.Length];
        "iTXt"u8.CopyTo(chunk);
        MetadataKeyword.CopyTo(chunk, 4);
        metadata.CopyTo(chunk, 4 + MetadataKeyword.Length + 4);
        Span<byte> integer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(integer, chunk.Length - 4);
        output.Write(integer);
        output.Write(chunk);
        uint crc = uint.MaxValue;
        foreach (byte value in chunk)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = crc >> 1 ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
            }
        }
        BinaryPrimitives.WriteUInt32BigEndian(integer, ~crc);
        output.Write(integer);
        output.Write(png.AsSpan(33));
        return output.ToArray();
    }

    internal static string SafeOutput(string root, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidDataException("Texture output escapes the selected folder.");
        }
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(path)!); directory is not null && directory.FullName.Length >= prefix.Length - 1; directory = directory.Parent)
        {
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Texture output cannot traverse a linked directory.");
            }
        }
        return path;
    }

    internal static void WriteAtomic(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
