using System.Globalization;
using System.Xml.Linq;

namespace Memoria.FF8.Mods;

/// <summary>Declarative resource settings and package relationships selected from a Junction manifest.</summary>
public sealed record JunctionSettingConstraint(string? MyId, int? MyValue, Guid ModId, string TheirId, int? Require, IReadOnlyList<int> Forbid);

public sealed record JunctionManifestMetadata
{
    public Guid? Id { get; init; }
    public bool? ContainsMovies { get; init; }
    public bool? ContainsMusic { get; init; }
    public bool? EnableFfmpegVideos { get; init; }
    public string? VideoExtension { get; init; }
    public bool? UseExternalMusic { get; init; }
    public IReadOnlyDictionary<string, int> Options { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<JunctionBehavior> Behaviors { get; init; } = Array.Empty<JunctionBehavior>();
    public IReadOnlyList<JunctionSettingConstraint> SettingConstraints { get; init; } = Array.Empty<JunctionSettingConstraint>();
    public IReadOnlyList<Guid> RequiredMods { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> Before { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> After { get; init; } = Array.Empty<Guid>();
}

public static partial class JunctionModImporter
{
    private static JunctionManifestMetadata ReadManifestMetadata(XElement root)
    {
        Guid? id = null;
        if (root.Element("ID") is XElement identity && !string.IsNullOrWhiteSpace(identity.Value))
        {
            id = ParseModId(identity.Value);
        }
        List<JunctionSettingConstraint> settings = [];
        List<Guid> required = [];
        List<Guid> before = [];
        List<Guid> after = [];
        foreach (XElement compatibility in root.Elements("Compatibility"))
        {
            RequireAttributes(compatibility);
            foreach (XElement requirement in compatibility.Elements())
            {
                if (requirement.Name == "Setting")
                {
                    settings.Add(ReadSettingConstraint(requirement));
                    continue;
                }
                if (requirement.Name != "Require")
                {
                    throw Unsupported(requirement);
                }
                RequireAttributes(requirement, "ModID");
                RequireLeaf(requirement);
                required.Add(ParseModId(RequiredAttribute(requirement, "ModID")));
            }
        }
        foreach (XElement constraints in root.Elements("OrderConstraints"))
        {
            RequireAttributes(constraints);
            foreach (XElement constraint in constraints.Elements())
            {
                RequireAttributes(constraint);
                RequireLeaf(constraint);
                if (constraint.Name == "Before")
                {
                    before.Add(ParseModId(constraint.Value));
                }
                else if (constraint.Name == "After")
                {
                    after.Add(ParseModId(constraint.Value));
                }
                else
                {
                    throw Unsupported(constraint);
                }
            }
        }
        return new JunctionManifestMetadata
        {
            Id = id,
            ContainsMovies = ReadInformationFlag(root, "ContainsMovies"),
            ContainsMusic = ReadInformationFlag(root, "ContainsMusic"),
            SettingConstraints = settings.AsReadOnly(),
            RequiredMods = Array.AsReadOnly(required.Distinct().ToArray()),
            Before = Array.AsReadOnly(before.Distinct().ToArray()),
            After = Array.AsReadOnly(after.Distinct().ToArray())
        };
    }

    private static JunctionSettingConstraint ReadSettingConstraint(XElement element)
    {
        RequireAttributes(element);
        string[] allowed = ["MyID", "MyValue", "ModID", "TheirID", "Require", "Forbid"];
        foreach (XElement child in element.Elements())
        {
            if (!allowed.Contains(child.Name.LocalName, StringComparer.Ordinal) || child.Name.Namespace != XNamespace.None)
            {
                throw Unsupported(child);
            }
            RequireAttributes(child);
            RequireLeaf(child);
        }
        string? Read(string name, bool required = false)
        {
            XElement[] nodes = element.Elements(name).ToArray();
            if (nodes.Length > 1 || required && nodes.Length == 0)
            {
                throw new InvalidDataException($"Junction setting constraint requires one {name} field.");
            }
            return nodes.FirstOrDefault()?.Value.Trim();
        }
        int Parse(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : throw new InvalidDataException("Junction setting constraint values must be integers.");
        }
        string? myId = Read("MyID");
        string? myValue = Read("MyValue");
        if ((myId is null) != (myValue is null))
        {
            throw new InvalidDataException("Junction setting constraint MyID and MyValue must be supplied together.");
        }
        string theirId = Read("TheirID", true)!;
        if (theirId.Length == 0)
        {
            throw new InvalidDataException("Junction setting constraint requires a target option ID.");
        }
        string? require = Read("Require");
        return new(string.IsNullOrWhiteSpace(myId) ? null : myId, myValue is null ? null : Parse(myValue), ParseModId(Read("ModID", true)!), theirId, require is null ? null : Parse(require), element.Elements("Forbid").Select(child => Parse(child.Value.Trim())).ToArray());
    }

    private static JunctionManifestMetadata ReadResourceSettings(XElement root, IReadOnlyDictionary<string, int> options, JunctionManifestMetadata metadata, List<string> diagnostics)
    {
        HashSet<string> selected = new(StringComparer.Ordinal);
        foreach (XElement setting in root.Elements("FFNxConfig").Elements())
        {
            RequireLeaf(setting);
            if (setting.Name != "enable_ffmpeg_videos" && setting.Name != "ffmpeg_video_ext" && setting.Name != "use_external_music")
            {
                throw new NotSupportedException($"Unsupported Junction FFNx setting: {setting.Name}.");
            }
            bool active = true;
            foreach (XAttribute attribute in setting.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
            {
                if (attribute.Name.Namespace != XNamespace.None || !options.TryGetValue(attribute.Name.LocalName, out int actual) || !int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int expected))
                {
                    throw Unsupported(setting);
                }
                active &= actual == expected;
            }
            string value = setting.Value.Trim();
            bool? flag = null;
            if (setting.Name == "ffmpeg_video_ext")
            {
                if (value.Length == 0 || value.Any(character => !char.IsAsciiLetterOrDigit(character)))
                {
                    throw new InvalidDataException("Junction movie extension must contain only ASCII letters and digits.");
                }
            }
            else
            {
                flag = ParseFlag(value);
            }
            if (!active)
            {
                continue;
            }
            if (!selected.Add(setting.Name.LocalName))
            {
                throw new InvalidDataException($"Duplicate active Junction FFNx setting: {setting.Name}.");
            }
            if (setting.Name == "ffmpeg_video_ext")
            {
                metadata = metadata with { VideoExtension = value.ToLowerInvariant() };
            }
            else if (setting.Name == "use_external_music")
            {
                metadata = metadata with { UseExternalMusic = flag };
            }
            else
            {
                metadata = metadata with { EnableFfmpegVideos = flag };
                if (flag == false)
                {
                    diagnostics.Add("Junction FFNx setting enable_ffmpeg_videos disables FFmpeg videos, which is not supported; the engine's FFmpeg movie pipeline remains enabled.");
                }
            }
        }
        return metadata;
    }

    private static Guid ParseModId(string value)
    {
        return Guid.TryParse(value.Trim(), out Guid id) ? id : throw new InvalidDataException("Junction mod identity must be a GUID.");
    }

    private static bool? ReadInformationFlag(XElement root, string name)
    {
        XElement[] elements = root.Elements(name).ToArray();
        if (elements.Length > 1)
        {
            throw new InvalidDataException($"Duplicate Junction metadata field: {name}.");
        }
        if (elements.Length == 0)
        {
            return null;
        }
        RequireAttributes(elements[0]);
        RequireLeaf(elements[0]);
        return ParseFlag(elements[0].Value.Trim());
    }

    private static bool ParseFlag(string value)
    {
        if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        throw new InvalidDataException($"Junction resource flag must be true, false, 1 or 0: {value}.");
    }

    private static string ResourceFolderPrefix(string folder)
    {
        string first = folder.Split('/')[0];
        return first.Equals("direct", StringComparison.OrdinalIgnoreCase) || first.Equals("data", StringComparison.OrdinalIgnoreCase) || first.Equals("mods", StringComparison.OrdinalIgnoreCase) || first.Equals("movies", StringComparison.OrdinalIgnoreCase) || first.Equals("music", StringComparison.OrdinalIgnoreCase) || first.Equals("field", StringComparison.OrdinalIgnoreCase) || first.Equals("battle", StringComparison.OrdinalIgnoreCase) || first.Equals("textures", StringComparison.OrdinalIgnoreCase) ? folder + "/" : "";
    }
}
