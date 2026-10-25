using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Memoria.FF8.Mods;

public sealed record JunctionModPackage(string Name, string SourcePath, IReadOnlyList<JunctionReplacement> Files, IReadOnlyList<string> Diagnostics)
{
    public JunctionManifestMetadata Manifest { get; init; } = new();
}

public sealed class JunctionReplacement
{
    private readonly Func<Stream> openRead;

    internal JunctionReplacement(string relativePath, string sourcePath, long? length, Func<Stream> openRead, ModFileSegment? fileSegment = null)
    {
        RelativePath = relativePath;
        SourcePath = sourcePath;
        Length = length;
        this.openRead = openRead;
        FileSegment = fileSegment;
    }

    public string RelativePath { get; }
    public string SourcePath { get; }
    public long? Length { get; }
    public ModFileSegment? FileSegment { get; }

    public Stream OpenRead()
    {
        return openRead();
    }

    public void CopyTo(Stream destination)
    {
        using Stream input = OpenRead();
        input.CopyTo(destination);
    }
}

/// <summary>Imports static file replacements using manifest defaults and validated option selections. Executable and dynamic mod behavior fails explicitly.</summary>
public static partial class JunctionModImporter
{
    private const int ManifestLimit = 4 * 1024 * 1024;
    private static readonly HashSet<string> MetadataElements = new(StringComparer.Ordinal)
    {
        "ID", "Name", "Author", "Version", "ReleaseDate", "Category", "Description", "ReleaseNotes", "Link", "DonationLink", "PreviewFile"
    };
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase) { ".hext", ".dll", ".exe", ".com", ".bat", ".cmd", ".ps1", ".asi" };
    private static readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase) { "EN", "FR", "DE", "ES", "IT", "JA", "ANY" };

    public static JunctionModPackage Open(string path, IReadOnlyDictionary<string, int>? optionOverrides = null)
    {
        string sourcePath = Path.GetFullPath(path);
        IReadOnlyList<JunctionReplacement> sourceFiles = Directory.Exists(sourcePath) ? ReadFolder(sourcePath) : ReadArchive(sourcePath);
        JunctionReplacement manifest = sourceFiles.SingleOrDefault(file => file.SourcePath.Equals("mod.xml", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidDataException("Junction packages require mod.xml at the package root.");
        using Stream manifestStream = manifest.OpenRead();
        byte[] bytes = ReadBounded(manifestStream, ManifestLimit);
        using MemoryStream xml = new(bytes, false);
        using XmlReader reader = XmlReader.Create(xml, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = ManifestLimit });
        XElement root = XDocument.Load(reader).Root ?? throw new InvalidDataException("Junction mod.xml is empty.");
        if (root.Name != "ModInfo" || root.Attributes().Any(attribute => !attribute.IsNamespaceDeclaration))
        {
            throw new InvalidDataException("Junction mod.xml requires an unqualified ModInfo root.");
        }
        string name = root.Element("Name")?.Value.Trim() ?? Path.GetFileNameWithoutExtension(sourcePath);
        Dictionary<string, int> options = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> previews = new(StringComparer.OrdinalIgnoreCase);
        List<string> diagnostics = [];
        JunctionManifestMetadata metadata = ReadManifestMetadata(root);
        foreach (XElement element in root.Elements())
        {
            string tag = element.Name.LocalName;
            if (element.Name.Namespace != XNamespace.None)
            {
                throw Unsupported(element);
            }
            if (MetadataElements.Contains(tag))
            {
                RequireAttributes(element);
                RequireLeaf(element);
                if (tag == "PreviewFile" && !string.IsNullOrWhiteSpace(element.Value))
                {
                    previews.Add(NormalizePath(element.Value.Trim()));
                }
            }
            else if (tag == "ConfigOption")
            {
                ReadOption(element, options, previews);
            }
            else if (tag == "GameLanguage")
            {
                RequireAttributes(element);
                RequireLeaf(element);
                ParseLanguages(element.Value);
            }
            else if (tag == "ModFolder")
            {
                RequireAttributes(element, "Folder", "ActiveWhen");
                if (element.Elements().Any(child => child.Name != "ActiveWhen") || element.Elements("ActiveWhen").Count() > 1)
                {
                    throw Unsupported(element);
                }
                NormalizePath(RequiredAttribute(element, "Folder"));
            }
            else if (tag == "FFNxConfig")
            {
                RequireAttributes(element);
            }
            else if (tag is "ContainsMovies" or "ContainsMusic" or "Compatibility" or "OrderConstraints")
            {
                // Parsed and validated by ReadManifestMetadata.
            }
            else if (tag == "Variables" && !element.HasElements && !element.HasAttributes && string.IsNullOrWhiteSpace(element.Value))
            {
                // Older packs include this empty informational placeholder; actual Variable nodes are unsupported.
            }
            else
            {
                throw Unsupported(element);
            }
        }
        ApplyOptionOverrides(root, options, optionOverrides);
        metadata = ReadResourceSettings(root, options, metadata, diagnostics);
        string[] supportedLanguages = root.Elements("GameLanguage").SelectMany(element => ParseLanguages(element.Value)).ToArray();
        if (supportedLanguages.Length > 0 && !supportedLanguages.Any(language => language.Equals("EN", StringComparison.OrdinalIgnoreCase) || language.Equals("ANY", StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotSupportedException("Junction package does not support the current English game language.");
        }
        foreach (JunctionReplacement file in sourceFiles)
        {
            if (file.SourcePath.Split('/').Any(part => part.Equals("hext", StringComparison.OrdinalIgnoreCase)) || CodeExtensions.Contains(Path.GetExtension(file.SourcePath)))
            {
                throw new NotSupportedException($"Junction executable or HEXT content is unsupported: {file.SourcePath}.");
            }
        }
        List<string> folders = [];
        foreach (XElement folder in root.Elements("ModFolder"))
        {
            string prefix = NormalizePath(RequiredAttribute(folder, "Folder"));
            bool active = EvaluateFolder(folder, options);
            if (active)
            {
                folders.Add(prefix);
            }
        }
        if (!root.Elements("ModFolder").Any())
        {
            folders.Add("");
        }
        Dictionary<string, JunctionReplacement> replacements = new(StringComparer.OrdinalIgnoreCase);
        foreach (string folder in folders)
        {
            foreach (JunctionReplacement file in sourceFiles)
            {
                string prefix = folder.Length == 0 ? "" : folder + "/";
                if (!file.SourcePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string relativePath = ResourceFolderPrefix(folder) + file.SourcePath[prefix.Length..];
                if (relativePath.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = relativePath[5..];
                }
                if (relativePath.Length == 0 || IsMetadata(file.SourcePath, relativePath, previews))
                {
                    continue;
                }
                // Junction keeps the first selected folder candidate for a logical path.
                replacements.TryAdd(relativePath, new JunctionReplacement(relativePath, file.SourcePath, file.Length, file.OpenRead, file.FileSegment));
            }
        }
        return new JunctionModPackage(name, sourcePath, Array.AsReadOnly(replacements.Values.ToArray()), diagnostics.AsReadOnly()) { Manifest = metadata };
    }

    internal static string NormalizePath(string value)
    {
        string path = value.Replace('\\', '/');
        string[] parts = path.Split('/');
        if (path.Length == 0 || parts.Any(part => part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.Any(character => character < 32 || "<>:\"|?*".Contains(character))))
        {
            throw new InvalidDataException($"Unsafe Junction package path: {value}.");
        }
        return path;
    }

    private static IReadOnlyList<JunctionReplacement> ReadArchive(string path)
    {
        JunctionArchive archive = new(path);
        return archive.Entries.Select(entry => new JunctionReplacement(entry.RelativePath, entry.RelativePath, entry.Length, entry.OpenRead, entry.FileSegment)).ToArray();
    }

    private static IReadOnlyList<JunctionReplacement> ReadFolder(string path)
    {
        List<JunctionReplacement> files = [];
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        Stack<string> pending = new();
        pending.Push(path);
        while (pending.TryPop(out string? directory))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new NotSupportedException("Junction folder packages cannot contain symbolic links or junctions.");
            }
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.OrdinalIgnoreCase))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new NotSupportedException($"Junction folder packages cannot contain symbolic links: {entry}.");
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                    continue;
                }
                string relativePath = NormalizePath(Path.GetRelativePath(path, entry));
                if (!paths.Add(relativePath))
                {
                    throw new InvalidDataException($"Duplicate Junction folder path: {relativePath}.");
                }
                files.Add(new JunctionReplacement(relativePath, relativePath, new FileInfo(entry).Length, () => File.OpenRead(entry)));
            }
        }
        return files;
    }

    private static void ReadOption(XElement element, Dictionary<string, int> options, HashSet<string> previews)
    {
        RequireAttributes(element);
        HashSet<string> allowed = new(StringComparer.Ordinal) { "Type", "Default", "ID", "Name", "Description", "Option" };
        foreach (XElement child in element.Elements())
        {
            if (child.Name.Namespace != XNamespace.None || !allowed.Contains(child.Name.LocalName))
            {
                throw Unsupported(child);
            }
            RequireLeaf(child);
            if (child.Name == "Option")
            {
                RequireAttributes(child, "Value", "Name", "PreviewFile", "PreviewAudio");
                if (!int.TryParse(RequiredAttribute(child, "Value"), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    throw new InvalidDataException("Junction option value must be an integer.");
                }
                if (child.Attribute("PreviewFile") is XAttribute preview && !string.IsNullOrWhiteSpace(preview.Value))
                {
                    previews.Add(NormalizePath(preview.Value));
                }
                if (child.Attribute("PreviewAudio") is XAttribute audio && !string.IsNullOrWhiteSpace(audio.Value))
                {
                    previews.Add(NormalizePath(audio.Value));
                }
            }
            else
            {
                RequireAttributes(child);
                if (element.Elements(child.Name).Count() > 1)
                {
                    throw new InvalidDataException($"Duplicate Junction option field {child.Name}.");
                }
            }
        }
        string id = element.Element("ID")?.Value.Trim() ?? "";
        string type = element.Element("Type")?.Value.Trim() ?? "";
        if (id.Length == 0 || type is not ("Bool" or "List") || !int.TryParse(element.Element("Default")?.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || (type == "Bool" && value is not (0 or 1)) || !options.TryAdd(id, value))
        {
            throw new InvalidDataException("Junction configuration requires a unique ID, supported type and valid default.");
        }
        if (type == "List" && !element.Elements("Option").Any(option => int.Parse(RequiredAttribute(option, "Value"), CultureInfo.InvariantCulture) == value))
        {
            throw new InvalidDataException($"Junction list default is not a declared option: {id}.");
        }
    }

    private static void ApplyOptionOverrides(XElement root, Dictionary<string, int> options, IReadOnlyDictionary<string, int>? overrides)
    {
        foreach ((string id, int value) in overrides ?? new Dictionary<string, int>())
        {
            if (!options.ContainsKey(id))
            {
                throw new InvalidDataException($"Unknown Junction configuration option: {id}.");
            }
            XElement declaration = root.Elements("ConfigOption").Single(option => string.Equals(option.Element("ID")?.Value.Trim(), id, StringComparison.OrdinalIgnoreCase));
            string type = declaration.Element("Type")!.Value.Trim();
            bool valid = type == "Bool" ? value is 0 or 1 : declaration.Elements("Option").Any(option => int.Parse(RequiredAttribute(option, "Value"), CultureInfo.InvariantCulture) == value);
            if (!valid)
            {
                throw new InvalidDataException($"Invalid value {value} for Junction configuration option {id}.");
            }
            options[id] = value;
        }
    }

    private static bool EvaluateFolder(XElement folder, IReadOnlyDictionary<string, int> options)
    {
        XAttribute? attribute = folder.Attribute("ActiveWhen");
        XElement? nested = folder.Element("ActiveWhen");
        if (attribute is not null && nested is not null)
        {
            throw new InvalidDataException("Junction ModFolder has ambiguous ActiveWhen conditions.");
        }
        if (attribute is not null)
        {
            return EvaluateOption(attribute.Value, options);
        }
        if (nested is null)
        {
            return true;
        }
        RequireAttributes(nested);
        if (nested.Elements().Count() != 1)
        {
            throw new InvalidDataException("Junction ActiveWhen requires one expression.");
        }
        return EvaluateExpression(nested.Elements().Single(), options);
    }

    private static bool EvaluateExpression(XElement expression, IReadOnlyDictionary<string, int> options)
    {
        RequireAttributes(expression);
        if (expression.Name == "Option")
        {
            RequireLeaf(expression);
            return EvaluateOption(expression.Value, options);
        }
        if (expression.Name == "GameLanguage")
        {
            RequireLeaf(expression);
            return ParseLanguages(expression.Value).Any(language => language.Equals("EN", StringComparison.OrdinalIgnoreCase) || language.Equals("ANY", StringComparison.OrdinalIgnoreCase));
        }
        XElement[] children = expression.Elements().ToArray();
        // Evaluate every branch so unsupported behavior is rejected even if another branch decides the result.
        bool[] values = children.Select(child => EvaluateExpression(child, options)).ToArray();
        if (expression.Name == "And" && values.Length > 0)
        {
            return values.All(value => value);
        }
        if (expression.Name == "Or" && values.Length > 0)
        {
            return values.Any(value => value);
        }
        if (expression.Name == "Not" && values.Length == 1)
        {
            return !values[0];
        }
        throw Unsupported(expression);
    }

    private static bool EvaluateOption(string condition, IReadOnlyDictionary<string, int> options)
    {
        if (condition.Trim() is "0" or "1")
        {
            return condition.Trim() == "1";
        }
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }
        Match match = OptionCondition().Match(condition);
        if (!match.Success || !int.TryParse(match.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int expected))
        {
            throw new NotSupportedException($"Unsupported Junction option condition: {condition}.");
        }
        if (match.Groups[1].Value.StartsWith("ffnx_", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Junction driver configuration selectors require an unsupported live FFNx configuration source: {condition}.");
        }
        if (!options.TryGetValue(match.Groups[1].Value, out int actual))
        {
            // An undeclared static option has no value; every comparison is inactive.
            return false;
        }
        return match.Groups[2].Value switch
        {
            "=" or "==" => actual == expected,
            "!=" or "<>" => actual != expected,
            ">" => actual > expected,
            "<" => actual < expected,
            ">=" => actual >= expected,
            "<=" => actual <= expected,
            _ => throw new NotSupportedException($"Unsupported Junction comparison: {condition}.")
        };
    }

    private static bool IsMetadata(string path, string resourcePath, HashSet<string> previews)
    {
        bool resource = resourcePath.StartsWith("mods/textures/", StringComparison.OrdinalIgnoreCase) || resourcePath.StartsWith("textures/", StringComparison.OrdinalIgnoreCase) || resourcePath.StartsWith("movies/", StringComparison.OrdinalIgnoreCase) || resourcePath.StartsWith("music/", StringComparison.OrdinalIgnoreCase) || resourcePath.StartsWith("field/", StringComparison.OrdinalIgnoreCase) || resourcePath.StartsWith("battle/", StringComparison.OrdinalIgnoreCase);
        return path.Equals("mod.xml", StringComparison.OrdinalIgnoreCase) || path.Equals("mod.ini", StringComparison.OrdinalIgnoreCase) || path.Equals("Readme.txt", StringComparison.OrdinalIgnoreCase) || !resource && (path.StartsWith("Preview/", StringComparison.OrdinalIgnoreCase) || previews.Contains(path));
    }

    private static string[] ParseLanguages(string value)
    {
        string[] languages = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (languages.Any(language => !Languages.Contains(language)))
        {
            throw new NotSupportedException($"Unsupported Junction game language: {value}.");
        }
        return languages;
    }

    private static string RequiredAttribute(XElement element, string name)
    {
        return element.Attribute(name)?.Value ?? throw new InvalidDataException($"Junction {element.Name} requires {name}.");
    }

    private static void RequireAttributes(XElement element, params string[] allowed)
    {
        if (element.Attributes().Any(attribute => !attribute.IsNamespaceDeclaration && (attribute.Name.Namespace != XNamespace.None || !allowed.Contains(attribute.Name.LocalName, StringComparer.Ordinal))))
        {
            throw Unsupported(element);
        }
    }

    private static void RequireLeaf(XElement element)
    {
        if (element.HasElements)
        {
            throw Unsupported(element);
        }
    }

    private static NotSupportedException Unsupported(XElement element)
    {
        return new NotSupportedException($"Unsupported Junction manifest behavior: {element.Name}.");
    }

    private static byte[] ReadBounded(Stream stream, int limit)
    {
        using MemoryStream output = new();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            if (output.Length + count > limit)
            {
                throw new InvalidDataException("Junction mod.xml exceeds the supported 4 MiB limit.");
            }
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    [GeneratedRegex(@"^\s*([^\s=<>!]+)\s*(==|!=|<>|>=|<=|=|>|<)\s*(-?\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex OptionCondition();
}
