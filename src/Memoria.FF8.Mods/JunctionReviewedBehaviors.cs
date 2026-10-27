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
    InstantManualSkip,
    SaveAnywhere,
    DreamJunctionCarry
}

public static partial class JunctionModImporter
{
    // Adding an entry requires separate human approval of that exact payload and its engine semantics.
    // This registry recognizes bytes; it never interprets or executes HEXT instructions.
    private sealed record ReviewedJunctionPayload(JunctionBehavior? Behavior = null, JunctionGameplayOverride[]? GameplayOverrides = null, string? Denial = null)
    {
        public bool Intrinsic => Behavior is JunctionBehavior.InstantManualSkip or JunctionBehavior.SaveAnywhere or JunctionBehavior.DreamJunctionCarry;
    }

    private static readonly IReadOnlyDictionary<string, ReviewedJunctionPayload> ReviewedBehaviors = new Dictionary<string, ReviewedJunctionPayload>(StringComparer.Ordinal)
    {
        ["BD84FBA9655540ED4CDA283E9F1F9C6C483E013FD290F04393C5E13189162E49"] = new(JunctionBehavior.SkipPublisherMovie),
        ["672040B7877A32B96F1B58B13BC5FE5F0E13A9EF7C6AE7AC1564FF1932A5B009"] = new(JunctionBehavior.SkipSquareLogo),
        ["12E2FE18A160F948D5164D4A1A70927D7CC3AD958EB42C0BC06058A371750827"] = new(JunctionBehavior.SkipCreditsPause),
        ["AC025CD3C009C6DE6D32E2B1166984552E765BC27AF626ACDC12675FDF35C749"] = new(JunctionBehavior.SkipCreditsImages),
        ["A3F7E939A12F47E8E081CD91685DFF51B69A95346674A9269CC39AA9014CF7DC"] = new(JunctionBehavior.InstantManualSkip),
        ["5FEBC6B68104AF43C71CFEA1E4FFDECB32BE386D098DE16CF2D4287901F8A542"] = Setting(JunctionGameplaySetting.UnrestrictedScanRotation, 1),
        ["4AE6F4BFF59DDEC9D863C3F3C80E0C713083CC500B31517815BC8A8395B3EE1C"] = Denied("This reviewed behavior was not approved for engine emulation."),
        ["7B392B63153209CF4EB43866409241A7F8DA29577E6F597547D63978BCCAC196"] = Denied("This reviewed behavior was not approved for engine emulation."),
        ["B8A19FDB7A27FE8D7830EEE9194302AEE123C905823524D99F8F7DE61FE67801"] = Denied("This reviewed behavior was not approved for engine emulation."),
        ["69B542DB62A24F792DE1FB233FFB8CBACDBE82F561EDA91C165A4458AA167375"] = Denied("This reviewed behavior was not approved for engine emulation."),
        ["CF5D09B4ED568097FE4BC53B8D38B2ABD979A147FB85FE87BAEED24B81402494"] = Denied("This reviewed behavior was not approved for engine emulation."),
        ["6CF13A6DA31581CDD05BB6477F50B3308AD497F70DC5339970DDB4D3CAF624A5"] = Denied("This reviewed behavior was not approved for engine emulation."),
        ["4D2B44E846637A02542A70DFF04F66CBB810E566515C3DE6D8B4B8E23C2ABD26"] = new(GameplayOverrides: [new(JunctionGameplaySetting.RareItemFirst, 46.875m), new(JunctionGameplaySetting.RareItemSecond, 31.25m), new(JunctionGameplaySetting.RareItemThird, 11.71875m), new(JunctionGameplaySetting.RareItemFourth, 10.15625m)]),
        ["23929F707190078B02431D92D4F5A82B323F88940A926F00EDD1D501F5CEFF4B"] = Denied("This reviewed behavior was not approved for engine emulation."),
        ["F70590AC0315799D0B56DA438EC2A689339861877BDB10521C8F2BFC4FDA81A4"] = Setting(JunctionGameplaySetting.ExperienceMultiplier, 0),
        ["6D565374CC49E03145313B0FF91DAA760D6970605FD79595C072BC0BD9F5CEE4"] = Denied("This reviewed behavior was not approved for engine emulation."),
        ["5C73BAE0D1F5E7479B354F86AFE0FAF993CEC9C16107E9F773CDD605287052DD"] = Denied("The reviewed AP assignment has unresolved arithmetic and is not an approved gameplay rule."),
        ["04114651C4527D7C86D56C33E42AC2F247D34C24412DC1EA4254A8C4D3A62CFB"] = Setting(JunctionGameplaySetting.ApMultiplier, 2),
        ["D6F1A2AC603E3D6C0212E7404A571A66D759E7A85D585D850EC63CEC1AC36ABE"] = Setting(JunctionGameplaySetting.ApMultiplier, 4),
        ["37DF230EF5EAEB29876AA3DD910EDA621BBDD7138EEF95B5B9FEEF0EC543F6AD"] = Setting(JunctionGameplaySetting.ApMultiplier, 8),
        ["BE5EB0792F0FB229FC231D8DC38978EBE2FC79CD2D4253AB905D81EEFB7D4221"] = Setting(JunctionGameplaySetting.MaximumDamage, 65535),
        ["4E9556DB57270D0F0D3CFBAD21A80FCAFD258CF8DF326C0600547030C3DF4DC9"] = Setting(JunctionGameplaySetting.DrawBonus, 100),
        ["7D4722302FE67F9492AA402004DD99BCA28EE4C04D804627BCC34306E88202F7"] = Setting(JunctionGameplaySetting.DrawBonus, 100),
        ["F758412928297C413B32B23F4E4A4A466142261577569AC2DF6C07D687E08D2D"] = Setting(JunctionGameplaySetting.DrawBonus, 100),
        ["1E15E923336B093412C9E4B96EABD9CDEA11624A32137CDE87690DCF6417FAB6"] = Setting(JunctionGameplaySetting.DrawBonus, 100),
        ["1388735540B8FACF638FDB02298A62B34CD5622C8C707C94CD435A3202B7A4CE"] = Setting(JunctionGameplaySetting.DrawBonus, 100),
        ["A59DAB355E117451CDDE231D980C32871637CFEC3CD04CB62EE7D31D52E21EDC"] = Setting(JunctionGameplaySetting.UnlimitedMagic, 1),
        ["86169F41F49645F3C3C9CA0C0CEFE17493D1A52C8B785403F171FFC3B7759801"] = new(JunctionBehavior.DreamJunctionCarry),
        ["25095ECB387DD4B987696F0D916A35D67FF5C7B26FF640E669919F8DCC718D03"] = new(JunctionBehavior.SaveAnywhere),
        ["288F33FA4EEB02356B582A8ADE98EAF243C91EAB7BF88B0D2A58B70AE3D6B302"] = Setting(JunctionGameplaySetting.ExperienceMultiplier, 0),
        ["3180E5B37EBD6C42B1C5796BC6C01A1CD63E3A259C1B23D23D27FCA724603758"] = Setting(JunctionGameplaySetting.ExperienceMultiplier, 16),
        ["893B6A35AE196E41249FE299B3C70028A05D64C6A91640276929F72F20EF7FD0"] = Setting(JunctionGameplaySetting.ExperienceMultiplier, 2),
        ["A738D02E02359FA3181EF41E9C0074069D2A719FB50EE29B89B572B531AC5C6A"] = Setting(JunctionGameplaySetting.ExperienceMultiplier, 4),
        ["A9545DDA038E6E8DEFE85A34024639A15617AC7143876B5A3611ABC420099854"] = Setting(JunctionGameplaySetting.ExperienceMultiplier, 8)
    };

    private static ReviewedJunctionPayload Setting(JunctionGameplaySetting setting, decimal value) => new(GameplayOverrides: [new(setting, value)]);
    private static ReviewedJunctionPayload Denied(string reason) => new(Denial: reason);

    private static Dictionary<string, ReviewedJunctionPayload> ReadReviewedBehaviors(IReadOnlyList<JunctionReplacement> files)
    {
        Dictionary<string, ReviewedJunctionPayload> reviewed = new(StringComparer.OrdinalIgnoreCase);
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
            if (!ReviewedBehaviors.TryGetValue(hash, out ReviewedJunctionPayload? behavior))
            {
                throw new NotSupportedException($"Junction HEXT requires individual review and human approval of its exact bytes and engine behavior: {file.SourcePath} (SHA256 {hash}).");
            }
            reviewed.Add(file.SourcePath, behavior);
        }
        return reviewed;
    }

    private static IReadOnlyDictionary<string, int> ExcludeIntrinsicOptions(XElement root, IReadOnlyList<JunctionReplacement> files, IReadOnlyDictionary<string, ReviewedJunctionPayload> reviewed, Dictionary<string, int> options, JunctionManifestMetadata metadata)
    {
        HashSet<string> intrinsic = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> other = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement folder in root.Elements("ModFolder"))
        {
            string prefix = NormalizePath(RequiredAttribute(folder, "Folder")) + "/";
            JunctionReplacement[] contents = files.Where(file => file.SourcePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
            bool intrinsicOnly = contents.Length > 0 && contents.All(file => reviewed.TryGetValue(file.SourcePath, out ReviewedJunctionPayload? behavior) && behavior.Intrinsic);
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
