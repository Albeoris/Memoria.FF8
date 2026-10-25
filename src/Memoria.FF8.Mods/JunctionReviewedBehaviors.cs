using System.Security.Cryptography;
using System.Xml.Linq;

namespace Memoria.FF8.Mods;

/// <summary>Engine behavior described by an individually reviewed external payload.</summary>
public enum JunctionBehavior
{
    SkipPublisherMovie,
    SkipSquareLogo,
    SkipCreditsPause,
    SkipCreditsImages,
    // Informational: immediate manual skipping is intrinsic and cannot be disabled.
    InstantManualSkip
}

public static partial class JunctionModImporter
{
    // Adding an entry requires separate human approval of that exact payload and its engine semantics.
    // This registry recognizes bytes; it never interprets or executes HEXT instructions.
    private static readonly IReadOnlyDictionary<string, JunctionBehavior> ReviewedBehaviors = new Dictionary<string, JunctionBehavior>(StringComparer.Ordinal)
    {
        ["BD84FBA9655540ED4CDA283E9F1F9C6C483E013FD290F04393C5E13189162E49"] = JunctionBehavior.SkipPublisherMovie,
        ["672040B7877A32B96F1B58B13BC5FE5F0E13A9EF7C6AE7AC1564FF1932A5B009"] = JunctionBehavior.SkipSquareLogo,
        ["12E2FE18A160F948D5164D4A1A70927D7CC3AD958EB42C0BC06058A371750827"] = JunctionBehavior.SkipCreditsPause,
        ["AC025CD3C009C6DE6D32E2B1166984552E765BC27AF626ACDC12675FDF35C749"] = JunctionBehavior.SkipCreditsImages,
        ["A3F7E939A12F47E8E081CD91685DFF51B69A95346674A9269CC39AA9014CF7DC"] = JunctionBehavior.InstantManualSkip
    };

    private static Dictionary<string, JunctionBehavior> ReadReviewedBehaviors(IReadOnlyList<JunctionReplacement> files)
    {
        Dictionary<string, JunctionBehavior> reviewed = new(StringComparer.OrdinalIgnoreCase);
        foreach (JunctionReplacement file in files)
        {
            string[] parts = file.SourcePath.Split('/');
            bool hext = parts.Contains("hext", StringComparer.OrdinalIgnoreCase) || Path.GetExtension(file.SourcePath).Equals(".hext", StringComparison.OrdinalIgnoreCase);
            if (!hext)
            {
                if (CodeExtensions.Contains(Path.GetExtension(file.SourcePath)))
                {
                    throw new NotSupportedException($"Junction executable content is unsupported: {file.SourcePath}.");
                }
                continue;
            }
            if (parts.Length < 4 || !parts[^4].Equals("hext", StringComparison.OrdinalIgnoreCase) || !parts[^3].Equals("ff8", StringComparison.OrdinalIgnoreCase) || !parts[^2].Equals("en_nv", StringComparison.OrdinalIgnoreCase) || !Path.GetExtension(file.SourcePath).Equals(".hext", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Junction HEXT requires individual review for this target profile: {file.SourcePath}.");
            }
            const int limit = 64 * 1024;
            if (file.Length > limit)
            {
                throw new NotSupportedException($"Junction HEXT requires individual review (payload exceeds recognition limit): {file.SourcePath}.");
            }
            using Stream stream = file.OpenRead();
            using MemoryStream bytes = new();
            byte[] buffer = new byte[8192];
            int count;
            while ((count = stream.Read(buffer)) > 0)
            {
                if (bytes.Length + count > limit)
                {
                    throw new NotSupportedException($"Junction HEXT requires individual review (payload exceeds recognition limit): {file.SourcePath}.");
                }
                bytes.Write(buffer, 0, count);
            }
            string hash = Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length))));
            if (!ReviewedBehaviors.TryGetValue(hash, out JunctionBehavior behavior))
            {
                throw new NotSupportedException($"Junction HEXT requires individual review and human approval of its exact bytes and engine behavior: {file.SourcePath} (SHA256 {hash}).");
            }
            reviewed.Add(file.SourcePath, behavior);
        }
        return reviewed;
    }

    private static IReadOnlyDictionary<string, int> ExcludeIntrinsicOptions(XElement root, IReadOnlyList<JunctionReplacement> files, IReadOnlyDictionary<string, JunctionBehavior> reviewed, Dictionary<string, int> options, JunctionManifestMetadata metadata)
    {
        HashSet<string> intrinsic = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> other = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement folder in root.Elements("ModFolder"))
        {
            string prefix = NormalizePath(RequiredAttribute(folder, "Folder")) + "/";
            JunctionReplacement[] contents = files.Where(file => file.SourcePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
            bool intrinsicOnly = contents.Length > 0 && contents.All(file => reviewed.TryGetValue(file.SourcePath, out JunctionBehavior behavior) && behavior == JunctionBehavior.InstantManualSkip);
            IEnumerable<string> conditions = folder.Descendants("Option").Select(element => element.Value);
            if (folder.Attribute("ActiveWhen") is XAttribute attribute)
            {
                conditions = conditions.Append(attribute.Value);
            }
            foreach (string condition in conditions)
            {
                System.Text.RegularExpressions.Match match = OptionCondition().Match(condition);
                if (match.Success)
                {
                    (intrinsicOnly ? intrinsic : other).Add(match.Groups[1].Value);
                }
            }
        }
        foreach (XAttribute attribute in root.Elements("FFNxConfig").Elements().Attributes())
        {
            other.Add(attribute.Name.LocalName);
        }
        foreach (JunctionSettingConstraint constraint in metadata.SettingConstraints)
        {
            if (constraint.MyId is string myId)
            {
                other.Add(myId);
            }
            if (constraint.ModId == metadata.Id)
            {
                other.Add(constraint.TheirId);
            }
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(options.Where(pair => !intrinsic.Contains(pair.Key) || other.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
    }
}
