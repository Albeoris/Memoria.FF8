using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.MimMap;
using Albeoris.Games.FF8.Imaging.Workspace;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed record TextureWorkspaceTile(TextureTarget Target, TextureTargetContract Contract, PixelImage Image);

/// <summary>The immutable source reference lives outside the editor's resampled hidden layers.</summary>
public sealed record FieldTextureWorkspaceMetadata(int SchemaVersion, string Field, string MimSha256, string MapSha256)
{
    public IReadOnlyDictionary<int, string> ExportedPixelSha256 { get; init; } = new Dictionary<int, string>();
}

public sealed partial class GameAssets
{
    private static readonly JsonSerializerOptions TextureWorkspaceJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };

    public void ExportFieldTextureWorkspace(string fieldName, string destination, CancellationToken cancellationToken = default, bool resolved = false)
    {
        ValidateTextureWorkspaceField(fieldName);
        cancellationToken.ThrowIfCancellationRequested();
        (byte[] mim, byte[] map) = ReadTextureWorkspaceSource(fieldName);
        string path = Path.GetFullPath(destination);
        if (File.Exists(path) || File.Exists(path + ".texture.json"))
        {
            throw new IOException("Export preserves existing field artwork. Choose a fresh workspace filename or working folder.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            IReadOnlyList<TextureBuildContext> tiles = ReadCatalogBackground(fieldName);
            Dictionary<int, string> baselines = [];
            MimMapRuntimeWorkspace.Export(MimMapFile.Read(mim, map), new(new(temporary, WorkspaceContainerFormat.OpenRaster)), index =>
            {
                PixelImage image = resolved ? resources.ResolveForExport(tiles[index]).Image : tiles[index].Original;
                return image;
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (MimMapRuntimeTile tile in MimMapRuntimeWorkspace.Import(new(new(temporary, WorkspaceContainerFormat.OpenRaster)), MimMapFile.Read(mim, map), cancellationToken))
            {
                baselines[tile.Index] = TextureExchange.PixelFingerprint(TexturePixelExchange.ToEditable(tile.Image));
            }
            FieldTextureWorkspaceMetadata metadata = new(1, fieldName, Convert.ToHexString(SHA256.HashData(mim)), Convert.ToHexString(SHA256.HashData(map))) { ExportedPixelSha256 = baselines };
            byte[] companion = JsonSerializer.SerializeToUtf8Bytes(metadata, TextureWorkspaceJson);
            TextureExchange.WriteAtomic(path + ".texture.json", companion, cancellationToken);
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

    public IReadOnlyList<TextureWorkspaceTile> ImportFieldTextureWorkspace(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string metadataPath = path + ".texture.json";
        if (!File.Exists(metadataPath))
        {
            throw new InvalidDataException("The field workspace source companion is missing. Keep the exported .texture.json beside the ORA document, or export the field again.");
        }
        if (new FileInfo(metadataPath).Length > 4 * 1024 * 1024)
        {
            throw new InvalidDataException("Field workspace source metadata exceeds its size limit.");
        }
        FieldTextureWorkspaceMetadata metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<FieldTextureWorkspaceMetadata>(File.ReadAllBytes(metadataPath), TextureWorkspaceJson) ?? throw new InvalidDataException("The field workspace has no source metadata.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The field workspace source metadata is malformed.", exception);
        }
        if (metadata.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported field workspace version {metadata.SchemaVersion}.");
        }
        ValidateTextureWorkspaceField(metadata.Field);
        (byte[] mim, byte[] map) = ReadTextureWorkspaceSource(metadata.Field);
        if (!string.Equals(metadata.MimSha256, Convert.ToHexString(SHA256.HashData(mim)), StringComparison.OrdinalIgnoreCase) || !string.Equals(metadata.MapSha256, Convert.ToHexString(SHA256.HashData(map)), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Field {metadata.Field} differs from the source used to export this workspace. Select the original source or export a compatible workspace.");
        }
        ResourceKey owner = new($"field/{metadata.Field}/{metadata.Field}.mim");
        IReadOnlyList<MimMapRuntimeTile> tiles = MimMapRuntimeWorkspace.Import(new(new(path, WorkspaceContainerFormat.OpenRaster)), MimMapFile.Read(mim, map), cancellationToken);
        return tiles.Select(tile => new TextureWorkspaceTile(TextureTarget.FromKey(new(owner, $"tile/{tile.Index}", tile.Palette)), new("classic-field-tile", 16, 16), tile.Image)).ToArray();
    }

    public static IReadOnlyDictionary<int, string> ReadFieldTextureWorkspaceBaselines(string path)
    {
        string companion = path + ".texture.json";
        if (!File.Exists(companion))
        {
            return new Dictionary<int, string>();
        }
        if (new FileInfo(companion).Length > 4 * 1024 * 1024)
        {
            throw new InvalidDataException("Field workspace source metadata exceeds its size limit.");
        }
        FieldTextureWorkspaceMetadata metadata = JsonSerializer.Deserialize<FieldTextureWorkspaceMetadata>(File.ReadAllBytes(companion), TextureWorkspaceJson) ?? throw new InvalidDataException("The field workspace has no source metadata.");
        if (metadata.SchemaVersion != 1 || metadata.ExportedPixelSha256.Count > 100000 || metadata.ExportedPixelSha256.Any(entry => entry.Key < 0 || entry.Value.Length != 64 || !entry.Value.All(char.IsAsciiHexDigit)))
        {
            throw new InvalidDataException("Field workspace export baselines are malformed or unsupported.");
        }
        return metadata.ExportedPixelSha256;
    }

    private (byte[] Mim, byte[] Map) ReadTextureWorkspaceSource(string field) => (resources.Read(new($"field/{field}/{field}.mim")), resources.Read(new($"field/{field}/{field}.map")));

    private void ValidateTextureWorkspaceField(string field)
    {
        if (string.IsNullOrWhiteSpace(field) || !fieldNames.Contains(field, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Select a field present in this installation's source catalog.");
        }
    }
}
