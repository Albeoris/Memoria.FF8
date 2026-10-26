using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tex;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Localization;

public sealed record LocalizationArtworkSource(TextureKey Key, string SourceSha256, int Width, int Height, byte[] Png);

/// <summary>Exports editable bitmap resources without interpreting their embedded lettering as text.</summary>
public static class LocalizationArtworkExporter
{
    public static IReadOnlyList<LocalizationArtworkSource> Read(string gameDirectory, string language, CancellationToken cancellation = default) => LocalizationSourceExporter.ReadArtwork(gameDirectory, language, cancellation);

    public static IReadOnlyList<LocalizationArtworkSource> Read(IGameArchiveSource archives, CancellationToken cancellation = default)
    {
        using IFlArchive menu = archives.OpenArchive("menu");
        byte[] ReadEntry(IFlArchiveEntry entry)
        {
            using Stream input = entry.OpenForRead();
            using MemoryStream output = new();
            input.CopyTo(output);
            return output.ToArray();
        }
        List<LocalizationArtworkSource> result = [];
        IFlArchiveEntry? header = menu.Entries.SingleOrDefault(entry => Path.GetFileName(entry.RelativePath).Equals("mngrphd.bin", StringComparison.OrdinalIgnoreCase));
        IFlArchiveEntry? content = menu.Entries.SingleOrDefault(entry => Path.GetFileName(entry.RelativePath).Equals("mngrp.bin", StringComparison.OrdinalIgnoreCase));
        if (header is not null && content is not null)
        {
            result.AddRange(ReadMenuSections(ReadEntry(header), ReadEntry(content), cancellation));
        }
        foreach (IFlArchiveEntry entry in menu.Entries.Where(entry => Path.GetExtension(entry.RelativePath).Equals(".tex", StringComparison.OrdinalIgnoreCase)).OrderBy(entry => entry.RelativePath, StringComparer.Ordinal))
        {
            cancellation.ThrowIfCancellationRequested();
            byte[] bytes = ReadEntry(entry);
            TexFile texture = TexFile.Read(bytes);
            for (int palette = 0; palette < Math.Max(1, texture.Palettes.Count); palette++)
            {
                PixelImage image = texture.Decode(palette);
                result.Add(Create(new(new ResourceKey("menu/" + Path.GetFileName(entry.RelativePath).ToLowerInvariant()), "atlas", palette), bytes, image));
            }
        }
        result.AddRange(ReadStartup(archives, cancellation));
        result.AddRange(ReadCardOutcomeBanners(archives, cancellation));
        return result;
    }

    private static IReadOnlyList<LocalizationArtworkSource> ReadCardOutcomeBanners(IGameArchiveSource archives, CancellationToken cancellation)
    {
        if (archives is not IResourceSource source)
        {
            return [];
        }
        IGameNativeMemorySource memory;
        NativeTripleTriadArtwork artwork;
        try
        {
            memory = GameDataResources.ReadNativeMemory(new ResourceService(source));
            artwork = NativeTripleTriadArtwork.Read(memory);
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException)
        {
            return [];
        }
        List<LocalizationArtworkSource> result = [];
        for (int outcome = 0; outcome < 3; outcome++)
        {
            cancellation.ThrowIfCancellationRequested();
            PixelImage image = artwork.GetOutcomeBanner(outcome);
            result.Add(new(new(new ResourceKey("cards/cards"), $"outcome/{outcome}"), memory.SourceHash, image.Width, image.Height, EncodePng(image)));
        }
        return result;
    }

    private static IReadOnlyList<LocalizationArtworkSource> ReadStartup(IGameArchiveSource archives, CancellationToken cancellation)
    {
        IFlArchive main;
        try
        {
            main = archives.OpenArchive("main");
        }
        catch (FileNotFoundException)
        {
            return [];
        }
        using (main)
        {
            List<LocalizationArtworkSource> result = [];
            for (int index = 4; index <= 33; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                string name = NativeStartupArtwork.ResourceName(index);
                IFlArchiveEntry? entry = main.Entries.SingleOrDefault(entry => Path.GetFileName(entry.RelativePath).Equals(name, StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    continue;
                }
                using Stream input = entry.OpenForRead();
                using MemoryStream output = new();
                input.CopyTo(output);
                byte[] bytes = output.ToArray();
                result.Add(Create(new(new ResourceKey("main/" + name), "atlas"), bytes, NativeStartupArtwork.Decode(bytes)));
            }
            return result;
        }
    }

    public static IReadOnlyList<LocalizationArtworkSource> ReadMenuSections(byte[] header, byte[] content, CancellationToken cancellation = default)
    {
        if (header.Length % 8 != 0)
        {
            throw new InvalidDataException("Menu artwork directory requires complete offset/length records.");
        }
        List<LocalizationArtworkSource> result = [];
        for (int slot = 0; slot < header.Length / 8; slot++)
        {
            cancellation.ThrowIfCancellationRequested();
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(slot * 8));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(slot * 8 + 4));
            if (offset is 0 or uint.MaxValue || size < 8)
            {
                continue;
            }
            if ((ulong)offset - 1 + size > (ulong)content.Length)
            {
                throw new InvalidDataException("Menu artwork section is outside its original resource.");
            }
            byte[] bytes = content.AsSpan(checked((int)offset - 1), checked((int)size)).ToArray();
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x10 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) is not (2 or 3 or 8 or 9))
            {
                continue;
            }
            var tim = WorldAssetReader.ReadTim(bytes, 0).Image;
            int palettes = tim.Clut?.GetPalettes(tim.PixelMode).Count ?? 1;
            for (int palette = 0; palette < palettes; palette++)
            {
                result.Add(Create(new(new ResourceKey("menu/mngrp.bin"), $"slot{slot:000}", palette), bytes, tim.Decode(palette)));
            }
        }
        return result;
    }

    private static LocalizationArtworkSource Create(TextureKey key, byte[] nativeBytes, PixelImage image) => new(key, Convert.ToHexString(SHA256.HashData(nativeBytes)), image.Width, image.Height, EncodePng(image));

    public static byte[] EncodePng(PixelImage image)
    {
        using MagickImage png = new();
        png.ReadPixels(MemoryMarshal.AsBytes(image.Pixels.AsSpan()).ToArray(), new PixelReadSettings((uint)image.Width, (uint)image.Height, StorageType.Char, PixelMapping.RGBA));
        return png.ToByteArray(MagickFormat.Png);
    }
}
