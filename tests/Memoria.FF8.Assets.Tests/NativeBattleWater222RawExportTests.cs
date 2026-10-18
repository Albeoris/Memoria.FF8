using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWater222RawExportTests
{
    [Fact]
    public void Optional_original_magic_files_export_uses_a_fresh_directory_and_exact_archive_bytes()
    {
        string? export = Environment.GetEnvironmentVariable("MEMORIA_FF8_WATER222_RAW_EXPORT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(export), "An explicit optional resource export directory is required.");
        string exportDirectory = export!;
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        Assert.False(Directory.Exists(exportDirectory) && Directory.EnumerateFileSystemEntries(exportDirectory).Any(), "Export requires a fresh empty directory.");
        using GameAssets game = new(directory);
        using var archive = game.OpenResourceArchive("magic");
        List<ExportFile> files = [];
        foreach (string name in new[] { "mag221_b.00", "mag221_b.01" })
        {
            var entry = Assert.Single(archive.Entries, entry => Path.GetFileName(entry.RelativePath.Replace('\\', '/')).Equals(name, StringComparison.OrdinalIgnoreCase));
            using Stream source = entry.OpenForRead();
            using MemoryStream bytes = new();
            source.CopyTo(bytes);
            Assert.True(bytes.Length > 0);
            files.Add(new(name, entry.RelativePath, bytes.ToArray()));
        }
        Directory.CreateDirectory(exportDirectory);
        foreach (ExportFile file in files)
        {
            using FileStream output = new(Path.Combine(exportDirectory, file.Name), FileMode.CreateNew);
            output.Write(file.Data);
        }
        using FileStream manifest = new(Path.Combine(exportDirectory, "manifest.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(manifest, files.Select(file => new { file.Name, OriginalName = file.RelativePath, Bytes = file.Data.Length, Sha256 = Convert.ToHexString(SHA256.HashData(file.Data)) }), new JsonSerializerOptions { WriteIndented = true });
    }

    private sealed record ExportFile(string Name, string RelativePath, byte[] Data);
}
