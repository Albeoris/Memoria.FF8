using System.Buffers.Binary;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Albeoris.Games.FF8.NamedicBin;
using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Assets.Localization;

/// <summary>Reads installed source text without changing gameplay owners or original resources.</summary>
public static partial class LocalizationSourceExporter
{
    private static readonly string[] Languages = ["en", "fr", "de", "es", "it", "jp"];

    public static IReadOnlyList<LocalizationSourceAvailability> ProbeLanguages(string gameDirectory, CancellationToken cancellation = default)
    {
        List<LocalizationSourceAvailability> result = [];
        foreach (string language in Languages)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                using IResourceSource source = Open(gameDirectory, language);
                using IFlArchive field = ((IGameArchiveSource)source).OpenArchive("field");
                using IFlArchive mapdata = Nested(field, "mapdata");
                string[] names = Encoding.ASCII.GetString(Bytes(Entry(mapdata, "maplist"))).Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
                bool available = names.Any(name => source.Find(new ResourceKey("field/" + name + "/" + name + ".msd")) is not null);
                result.Add(new(language, available, available ? null : "No localized field text bank is present."));
            }
            catch (Exception exception) when (ResourceError(exception))
            {
                result.Add(new(language, false, exception.Message));
            }
        }
        return result;
    }

    public static LocalizationSourceCatalog Read(string gameDirectory, string language, CancellationToken cancellation = default)
    {
        string normalized = FieldLocalizationKey.CanonicalLanguage(language);
        using IResourceSource source = Open(gameDirectory, normalized);
        return Read(source, (IGameArchiveSource)source, normalized, cancellation);
    }

    public static LocalizationSourceCatalog ReadNonFields(string gameDirectory, string language, CancellationToken cancellation = default)
    {
        string normalized = FieldLocalizationKey.CanonicalLanguage(language);
        using IResourceSource source = Open(gameDirectory, normalized);
        List<LocalizationSourceEntry> entries = [];
        List<LocalizationSourceGap> gaps = [];
        new Reader(source, (IGameArchiveSource)source, normalized, entries, gaps, cancellation).NonFields();
        return new(entries.AsReadOnly(), gaps.AsReadOnly(), [new(normalized, entries.Count > 0, entries.Count > 0 ? null : "No non-field source text was decoded.")]);
    }

    public static LocalizationSourceCatalog Read(IResourceSource source, IGameArchiveSource archives, string language, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(archives);
        string normalized = FieldLocalizationKey.CanonicalLanguage(language);
        List<LocalizationSourceEntry> entries = [];
        List<LocalizationSourceGap> gaps = [];
        Reader reader = new(source, archives, normalized, entries, gaps, cancellation);
        reader.Fields();
        reader.NonFields();
        return new(entries.AsReadOnly(), gaps.AsReadOnly(), [new(normalized, entries.Count > 0, entries.Count > 0 ? null : "No source text was decoded.")]);
    }

    public static string IndexedKey(string language, string table, int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        return "$" + FieldLocalizationKey.CanonicalLanguage(language) + "_" + table + "_" + ordinal.ToString("D4", CultureInfo.InvariantCulture);
    }

    public static string KernelTable(string collection, string role) => "kernel_" + collection.ToLowerInvariant() + "_" + role.ToLowerInvariant();

    private static IResourceSource Open(string directory, string language)
    {
        if (File.Exists(Path.Combine(directory, "main.zzz")))
        {
            return new RemasteredGameDataSource(directory, language == "jp" ? "ja" : language);
        }
        if (language != "en")
        {
            throw new NotSupportedException("Classic source selection currently supports the installed English archive layout only.");
        }
        return new ClassicGameDataSource(directory);
    }

    private static bool ResourceError(Exception exception) => exception is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException or IndexOutOfRangeException or OverflowException or FormatException;
    private static string Basename(string path) => path.Replace('\\', '/').Split('/')[^1];
    private static byte[] Bytes(IFlArchiveEntry entry)
    {
        using Stream stream = entry.OpenForRead();
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
    private static IFlArchiveEntry Entry(IFlArchive archive, string name) => archive.Entries.SingleOrDefault(entry => Basename(entry.RelativePath).Equals(name, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("Missing archive member: " + name);
    private static IFlArchive Nested(IFlArchive archive, string name) => FlArchive.Open(new MemoryStream(Bytes(Entry(archive, name + ".fl")), false), new MemoryStream(Bytes(Entry(archive, name + ".fi")), false), new MemoryStream(Bytes(Entry(archive, name + ".fs")), false), false, FlArchiveRepresentation.Files);

    private sealed partial class Reader(IResourceSource source, IGameArchiveSource archives, string language, List<LocalizationSourceEntry> entries, List<LocalizationSourceGap> gaps, CancellationToken cancellation)
    {
        private readonly FF8Encoding encoding = FF8Encoding.CreateEuropean();
        private readonly Dictionary<string, byte[]?> resourceBytes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> resourceHashes = new(StringComparer.OrdinalIgnoreCase);
        private byte[]? Resource(string path)
        {
            cancellation.ThrowIfCancellationRequested();
            if (resourceBytes.TryGetValue(path, out byte[]? cached))
            {
                return cached;
            }
            ResourceContent? content = source.Find(new ResourceKey(path));
            if (content is null)
            {
                resourceBytes.Add(path, null);
                return null;
            }
            using Stream stream = content.OpenRead();
            using MemoryStream buffer = new();
            stream.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            resourceBytes.Add(path, bytes);
            return bytes;
        }
        private string ResourceHash(string path)
        {
            if (!resourceHashes.TryGetValue(path, out string? hash))
            {
                hash = Convert.ToHexString(SHA256.HashData(Required(path)));
                resourceHashes.Add(path, hash);
            }
            return hash;
        }
        private byte[] Required(string path) => Resource(path) ?? throw new FileNotFoundException("Missing source resource: " + path);
        private void Attempt(string category, string resource, Action action)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                action();
            }
            catch (Exception exception) when (ResourceError(exception))
            {
                gaps.Add(new(category, resource, exception.GetType().Name + ": " + exception.Message));
            }
        }
        private void Add(string category, string table, int ordinal, string text, string resource, IReadOnlyDictionary<string, string?>? metadata = null)
        {
            Dictionary<string, string?> values = metadata is null ? [] : new(metadata, StringComparer.Ordinal);
            values["Resource"] = resource;
            if (resourceBytes.TryGetValue(resource, out byte[]? bytes) && bytes is not null)
            {
                values["Source SHA256"] = ResourceHash(resource);
            }
            if (resource == "menu/mngrp.bin")
            {
                values["Companion Resource"] = "menu/mngrphd.bin";
                values["Companion SHA256"] = ResourceHash("menu/mngrphd.bin");
            }
            values["Source Ordinal"] = ordinal.ToString(CultureInfo.InvariantCulture);
            values["Correspondence"] = "SourceLanguageOnly";
            entries.Add(new(category, table, IndexedKey(language, table, ordinal), text, language, values));
        }

        public void Fields()
        {
            Attempt("Fields", "field/mapdata/maplist", () =>
            {
                using IFlArchive outer = archives.OpenArchive("field");
                using IFlArchive mapdata = Nested(outer, "mapdata");
                string[] names = Encoding.ASCII.GetString(Bytes(Entry(mapdata, "maplist"))).Replace("\r", "").Split('\n');
                List<FieldLocationSource> scripts = [];
                Dictionary<string, byte[]> textBanks = new(StringComparer.Ordinal);
                HashSet<string> discovered = names.Where(name => name.Length > 0).ToHashSet(StringComparer.Ordinal);
                foreach (IFlArchiveEntry entry in outer.Entries.Where(entry => entry.RelativePath.EndsWith(".fl", StringComparison.OrdinalIgnoreCase)))
                {
                    string name = Path.GetFileNameWithoutExtension(Basename(entry.RelativePath));
                    if (name is not ("mapdata" or "main_chr"))
                    {
                        discovered.Add(name);
                    }
                }
                int extraId = names.Length;
                foreach (string field in discovered.Order(StringComparer.Ordinal))
                {
                    cancellation.ThrowIfCancellationRequested();
                    int id = Array.IndexOf(names, field);
                    id = id < 0 ? extraId++ : id;
                    string resource = "field/" + field + "/" + field;
                    Attempt("Fields", resource, () =>
                    {
                        byte[]? jsm = Resource(resource + ".jsm");
                        scripts.Add(new(id, field, jsm));
                        byte[]? msd = Resource(resource + ".msd");
                        if (msd is null)
                        {
                            gaps.Add(new("Fields", resource + ".msd", "Localized text bank is absent."));
                        }
                        else
                        {
                            textBanks.Add(field, msd);
                        }
                    });
                }
                string[] places = [];
                if (language != "jp")
                {
                    Attempt("Locations", "menu/areames.dc1", () => places = NamedicBin.ReadStrings(Required("menu/areames.dc1"), encoding));
                }
                FieldLocationGraphResult graph = FieldLocationGraph.Build(scripts, places);
                Dictionary<string, FieldLocationResolution> resolutions = graph.Resolutions.ToDictionary(row => row.Field);
                foreach (FieldLocationSource script in scripts)
                {
                    if (!textBanks.TryGetValue(script.Field, out byte[]? bytes))
                    {
                        continue;
                    }
                    Attempt("Fields", script.Field, () =>
                    {
                        FieldAuditDecodedBank bank = FieldAuditDecoder.Decode(bytes, language == "jp" ? "ja" : language, script.Field);
                        FieldCompatibilityInventory inventory = FieldCompatibilityAudit.Prepare(new(script.Field, language, script.Field, script.Jsm, bank.Rows.Select(row => new FieldCompatibilityText(row.Ordinal, row.Text ?? "", row.Tokens)).ToArray(), bank.Rows.Where(row => row.Error is not null).Select(row => row.Error!).ToArray(), bank.Rows.Length)).Inventory;
                        foreach (FieldCompatibilityContext context in inventory.Contexts.Where(context => context.IsMessage && context.Dynamic))
                        {
                            gaps.Add(new("Fields", script.Field + "/operation" + context.Position.OperationIndex, "Dynamic message, channel, position, or choice operands remain native and cannot be materialized as a static translation prefix."));
                        }
                        foreach (string problem in inventory.Problems)
                        {
                            gaps.Add(new("Fields", script.Field, problem));
                        }
                        FieldLocationResolution resolution = resolutions[script.Field];
                        string sourceHash = Convert.ToHexString(SHA256.HashData(bytes));
                        foreach (FieldAuditDecodedRow row in bank.Rows)
                        {
                            if (row.Error is not null)
                            {
                                gaps.Add(new("Fields", script.Field + "/" + row.Ordinal, row.Error));
                            }
                            string key = FieldLocalizationKey.Create(language, script.Field, row.Ordinal);
                            Dictionary<string, string?> metadata = new(StringComparer.Ordinal)
                            {
                                ["Resource"] = "field/" + script.Field + "/" + script.Field + ".msd",
                                ["Source Ordinal"] = row.Ordinal.ToString(CultureInfo.InvariantCulture),
                                ["Source Offset"] = row.Offset.ToString(CultureInfo.InvariantCulture),
                                ["Source Length"] = row.Length.ToString(CultureInfo.InvariantCulture),
                                ["Source SHA256"] = sourceHash,
                                ["Source MSD SHA256"] = sourceHash,
                                ["JSM SHA256"] = inventory.JsmSha256,
                                ["Decoder"] = bank.Decoder,
                                ["Character Table SHA256"] = bank.CharacterTableSha256,
                                ["Field Name"] = string.Join(" / ", resolution.Names),
                                ["Field Name Status"] = resolution.Status.ToString(),
                                ["Place IDs"] = JsonSerializer.Serialize(resolution.PlaceIds),
                                ["Call Contexts"] = JsonSerializer.Serialize(inventory.Contexts.Where(context => context.MessageId == row.Ordinal || context.CandidateOrdinals.Contains(row.Ordinal))),
                                ["Source Present"] = row.Error is null ? "true" : "false",
                                ["Decode Error"] = row.Error,
                                ["Mapping Version"] = FieldCorrespondenceCatalog.Version,
                                ["Mapping SHA256"] = FieldCorrespondenceCatalog.Fingerprint
                            };
                            LocalizationSourceEntry entry = new("Fields", script.Field, key, row.Text ?? "", language, metadata);
                            entries.AddRange(row.Error is null ? LocalizationSourceFieldContexts.Expand(entry, inventory) : [entry]);
                        }
                    });
                }
            });
        }
    }
}
