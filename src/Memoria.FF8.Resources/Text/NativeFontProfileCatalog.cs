using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources.Text;


namespace Memoria.FF8.Resources.Text;

public sealed record NativeFontProfileEntry(NativeFontProfile Profile, IReadOnlyDictionary<string, PixelImage> Atlases, string Revision);
public sealed record NativeFontSelection(NativeFontProfileEntry? Entry, string? Diagnostic)
{
    public bool Success => Entry is not null;
}

/// <summary>Validated immutable namespaces and explicit role/context assignment selection, independent of graphics state.</summary>
public sealed class NativeFontProfileCatalog
{
    private readonly IReadOnlyDictionary<string, NativeFontProfileEntry> entries;
    private readonly NativeFontProfileEntry? installed;
    private readonly string? installedFailure;
    public string Revision { get; }
    public IReadOnlyList<string> AvailableProfileIds { get; }

    public NativeFontProfileCatalog(IEnumerable<NativeFontProfileEntry> profiles, NativeFontProfileEntry? installed, string revision, string? installedFailure = null)
    {
        Dictionary<string, NativeFontProfileEntry> result = new(StringComparer.Ordinal);
        foreach (NativeFontProfileEntry entry in profiles)
        {
            IReadOnlyList<NativeTextDiagnostic> errors = NativeFontProfileValidation.Validate(entry.Profile);
            if (errors.Count != 0)
            {
                throw new InvalidDataException(string.Join(Environment.NewLine, errors.Select(error => error.Message)));
            }
            if (entry.Profile.Id == "Auto")
            {
                throw new InvalidDataException("Profile ID 'Auto' is reserved for contextual selection; choose a stable profile ID.");
            }
            if (result.TryGetValue(entry.Profile.Id, out NativeFontProfileEntry? existing) && existing.Revision != entry.Revision)
            {
                throw new InvalidDataException($"Native font profile ID '{entry.Profile.Id}' has incompatible definitions/dependencies.");
            }
            result[entry.Profile.Id] = entry;
        }
        if (result.Values.SelectMany(entry => entry.Profile.Assignments).Any(assignment => !result.ContainsKey(assignment.ProfileId)))
        {
            throw new InvalidDataException("An encoding assignment refers to a profile outside the enabled catalog.");
        }
        entries = result;
        this.installed = installed;
        this.installedFailure = installedFailure;
        Revision = revision;
        AvailableProfileIds = new[] { "Auto" }.Concat(result.Keys.Order(StringComparer.Ordinal)).ToArray();
    }

    public NativeFontSelection Resolve(string requestedId, NativeTextContext context, string role, string? dataset)
    {
        if (role is not ("Target" or "Reference" or "Source"))
        {
            return new(null, $"Unknown text dataset role '{role}'.");
        }
        NativeFontProfileEntry? selected = null;
        if (role == "Target" && requestedId != "Auto")
        {
            if (!entries.TryGetValue(requestedId, out selected))
            {
                return new(null, $"Native font profile '{requestedId}' is not declared by an enabled package.");
            }
        }
        else
        {
            var applicable = entries.Values.SelectMany(entry => entry.Profile.Assignments).Where(assignment => assignment.Role == role && assignment.Dataset == dataset && Matches(assignment.Context, context)).Select(assignment => (Assignment: assignment, Specificity: (assignment.Context.Field is null ? 0 : 1) + (assignment.Context.DictionaryLanguage is null ? 0 : 1))).ToArray();
            if (applicable.Length != 0)
            {
                int specificity = applicable.Max(item => item.Specificity);
                string[] ids = applicable.Where(item => item.Specificity == specificity).Select(item => item.Assignment.ProfileId).Distinct(StringComparer.Ordinal).ToArray();
                if (ids.Length != 1)
                {
                    return new(null, $"Ambiguous {role} profile assignment for dataset '{dataset}', resource '{context.ResourceFamily}', field '{context.Field}': {string.Join(", ", ids)}.");
                }
                selected = entries[ids[0]];
            }
            else
            {
                selected = installed;
            }
        }
        if (selected is null)
        {
            return new(null, $"No {role} native font profile is assigned to dataset '{dataset}' and resource '{context.ResourceFamily}'. {installedFailure}");
        }
        if (selected.Profile.Domains.Count != 0 && !selected.Profile.Domains.Any(domain => domain.Matches(context)))
        {
            return new(null, $"Profile '{selected.Profile.Id}' does not support edition '{context.Edition}', resource '{context.ResourceFamily}', field '{context.Field}', dictionary '{context.DictionaryLanguage}'.");
        }
        return new(selected, null);
    }

    public NativeFontProfile? DefaultProfile(string requestedId, string? target)
    {
        if (requestedId != "Auto")
        {
            return entries.GetValueOrDefault(requestedId)?.Profile;
        }
        string[] ids = entries.Values.SelectMany(entry => entry.Profile.Assignments).Where(assignment => assignment.Role == "Target" && assignment.Dataset == target).Select(assignment => assignment.ProfileId).Distinct(StringComparer.Ordinal).ToArray();
        return ids.Length == 1 ? entries.GetValueOrDefault(ids[0])?.Profile : ids.Length == 0 ? installed?.Profile : null;
    }

    private static bool Matches(NativeTextContext assignment, NativeTextContext context) => assignment.Edition == context.Edition && assignment.ResourceFamily == context.ResourceFamily && (assignment.Field is null || assignment.Field == context.Field) && (assignment.DictionaryLanguage is null || assignment.DictionaryLanguage == context.DictionaryLanguage);
}

