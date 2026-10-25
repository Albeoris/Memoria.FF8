using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Albeoris.Games.FF8.NamedicBin;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Localization;

internal static class FieldLocationAudit
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static FieldLocationGraphResult Run(string gameDirectory, string language, IReadOnlyList<FieldLocationSource> fields, string cacheDirectory, List<string> problems, Action<string> progress)
    {
        string[] names;
        try
        {
            using RemasteredGameDataSource source = new(gameDirectory, language);
            ResourceContent content = source.Find(new ResourceKey("menu/areames.dc1")) ?? throw new InvalidDataException("Missing menu/areames.dc1.");
            using Stream input = content.OpenRead();
            byte[] bytes = RemasteredFieldAudit.ReadBounded(input);
            // The existing Namedic reader validates contiguous offsets and terminators;
            // Latin1 temporarily preserves bytes for the strict source-language decoder.
            string[] encodedNames = NamedicBin.ReadStrings(bytes, Encoding.Latin1);
            names = new string[encodedNames.Length];
            for (int index = 0; index < names.Length; index++)
            {
                byte[] raw = Encoding.Latin1.GetBytes(encodedNames[index]);
                byte[] bank = new byte[4 + raw.Length];
                BinaryPrimitives.WriteInt32LittleEndian(bank, 4);
                raw.CopyTo(bank, 4);
                FieldAuditDecodedBank decoded = FieldAuditDecoder.Decode(bank, language, "areames");
                FieldAuditDecodedRow row = decoded.Rows[0];
                names[index] = row.Text ?? "";
                if (row.Error is not null)
                {
                    problems.Add($"{language} location name {index}: {row.Error}");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or FormatException or NotSupportedException)
        {
            problems.Add($"{language} location-name bank: {exception.Message}");
            names = [];
        }
        string assemblyHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(FieldLocationGraph).Assembly.Location)));
        byte[] identity = JsonSerializer.SerializeToUtf8Bytes(new
        {
            FieldLocationGraph.Rule,
            Assembly = assemblyHash,
            Fields = fields.Select(field => new { field.FieldId, field.Field, Hash = field.Jsm is null ? null : Convert.ToHexString(SHA256.HashData(field.Jsm)) }),
            PlaceNames = names
        });
        string fingerprint = Convert.ToHexString(SHA256.HashData(identity));
        Directory.CreateDirectory(cacheDirectory);
        string path = Path.Combine(cacheDirectory, language + "-" + fingerprint + ".json");
        if (File.Exists(path) && new FileInfo(path).Length <= 64 * 1024 * 1024)
        {
            try
            {
                Cache? cached = JsonSerializer.Deserialize<Cache>(File.ReadAllBytes(path), Json);
                if (cached?.Graph is not null && cached.SourceFingerprint == fingerprint && cached.Graph.Rule == FieldLocationGraph.Rule)
                {
                    progress($"Reused {language} location graph {fingerprint[..12]}");
                    return cached.Graph;
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                progress($"Rebuilding unreadable {language} location graph cache: {exception.Message}");
            }
        }
        FieldLocationGraphResult graph = FieldLocationGraph.Build(fields, names);
        string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(staging, JsonSerializer.SerializeToUtf8Bytes(new Cache(fingerprint, graph), Json));
            File.Move(staging, path, true);
        }
        finally
        {
            File.Delete(staging);
        }
        progress($"Built {language} location graph: {graph.Nodes.Length} nodes; {graph.Resolutions.Count(resolution => resolution.Names.Length == 1)} single-name resolutions.");
        return graph;
    }

    private sealed record Cache(string SourceFingerprint, FieldLocationGraphResult Graph);
}
