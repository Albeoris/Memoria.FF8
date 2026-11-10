using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed record CharacterAnimationGap(string Resource, string Reason);
internal sealed record CharacterCompatibility(int SupportedClips, int TotalClips, IReadOnlyList<CharacterAnimationGap> Missing)
{
    internal IReadOnlyList<CharacterAnimationGap> ExcludedSources { get; init; } = [];
    internal double Percentage => TotalClips == 0 ? 0 : 100d * SupportedClips / TotalClips;
    internal string Summary => $"{Percentage:0.#}% ({SupportedClips}/{TotalClips} native field body clips)";
    internal static string ColorName(double percentage) => percentage == 100 ? "Cyan" : percentage > 75 ? "Green" : percentage >= 50 ? "Yellow" : percentage >= 25 ? "Orange" : "Red";
}

/// <summary>Installed-resource coverage. A supported clip means every original integer sample can execute the shared retarget adapter; it is not artistic certification.</summary>
internal sealed class CrossoverCharacterCatalog(Ff7Installation installation, GameAssets assets, CancellationToken cancellationToken = default)
{
    private readonly Dictionary<(int Story, int Appearance), CharacterCompatibility> compatibility = [];
    private readonly Dictionary<int, IReadOnlyList<PcFieldModelDescriptor>> storyDescriptors = [];
    private bool discovered;
    private readonly List<CharacterAnimationGap> excludedSources = [];

    internal static readonly string[] StoryNames = ["Cloud", "Barret", "Tifa", "Aerith", "Red XIII", "Yuffie", "Cait Sith", "Vincent", "Cid"];
    internal static readonly string[] AppearanceNames = ["Squall", "Zell", "Irvine", "Quistis", "Rinoa", "Selphie", "Seifer", "Edea", "Laguna", "Kiros", "Ward"];

    internal static int IdentifyStoryCharacter(PcFieldModelDescriptor descriptor)
    {
        string hrc = Path.GetFileNameWithoutExtension(descriptor.Hrc).ToLowerInvariant();
        int known = hrc switch
        {
            "aaaa" or "clgd" => 0,
            "acgd" => 1,
            "aagb" => 2,
            "auff" => 3,
            "adda" => 4,
            "abjb" => 5,
            "aebc" => 6,
            "aehd" => 7,
            "abda" => 8,
            _ => -1
        };
        if (known >= 0)
        {
            return known;
        }
        int marker = descriptor.Name.IndexOf("main_", StringComparison.OrdinalIgnoreCase);
        return marker < 0 ? -1 : IdentifyStoryCharacter(descriptor.Name[marker..]);
    }

    internal static int IdentifyStoryCharacter(string name)
    {
        string value = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
        string[][] names = [["cloud", "cloudup"], ["barret", "barre", "ballet"], ["tifa"], ["aerith", "aeris", "earith", "earis"], ["red13", "redxiii", "red"], ["yuffie", "yufi", "yuffi"], ["caitsith", "cait", "ketto", "ketcy"], ["vincent", "vinc"], ["cid"]];
        for (int character = 0; character < names.Length; character++)
        {
            if (names[character].Any(value.Contains))
            {
                return character;
            }
        }
        return -1;
    }

    internal CharacterCompatibility GetCompatibility(int story, int appearance)
    {
        if (compatibility.TryGetValue((story, appearance), out CharacterCompatibility? known))
        {
            return known;
        }
        Discover();
        List<CharacterAnimationGap> missing = [];
        int total = 0;
        int supported = 0;
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        foreach (var group in (storyDescriptors.GetValueOrDefault(story) ?? []).GroupBy(descriptor => Path.GetFileName(descriptor.Hrc), StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] animations = group.SelectMany(descriptor => descriptor.Animations).DistinctBy(animation => Path.GetFileNameWithoutExtension(animation), StringComparer.OrdinalIgnoreCase).ToArray();
            PcFieldModelDescriptor selected = group.First() with { Animations = [group.First().Animations[0]] };
            total += animations.Length;
            try
            {
                Ff7FieldModelResources original = Ff7FieldModelResources.Load(archive, selected);
                Ff7ModelAppearance profile = Ff7ModelAppearance.CreateFieldCharacter(assets, appearance, selected, original);
                foreach (string animation in animations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string identity = Path.GetFileName(selected.Hrc).ToLowerInvariant() + "/" + Path.ChangeExtension(Path.GetFileName(animation), ".a").ToLowerInvariant();
                    try
                    {
                        string member = Path.ChangeExtension(Path.GetFileName(animation), ".a");
                        IReadOnlyList<LgpEntry> entries = archive.Find(member);
                        if (entries.Count != 1)
                        {
                            throw new InvalidDataException($"Native animation '{member}' does not resolve unambiguously.");
                        }
                        Ff7FieldModelResources clipOwner = original;
                        PcFieldAnimation clip;
                        if (member.Equals("bzbc.a", StringComparison.OrdinalIgnoreCase))
                        {
                            clipOwner = Ff7FieldModelResources.Load(archive, selected with { Animations = new[] { selected.Animations[0], animation }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() });
                            clip = clipOwner.Animations[member];
                        }
                        else
                        {
                            clip = PcFieldAnimation.Read(archive.ReadEntry(entries[0]));
                        }
                        profile.ValidateNativeClip(clipOwner, clip, member);
                        supported++;
                    }
                    catch (Exception exception) when (CoverageError(exception))
                    {
                        missing.Add(new(identity, exception.Message));
                    }
                }
            }
            catch (Exception exception) when (CoverageError(exception))
            {
                foreach (string animation in animations)
                {
                    missing.Add(new(Path.GetFileName(selected.Hrc).ToLowerInvariant() + "/" + Path.ChangeExtension(Path.GetFileName(animation), ".a").ToLowerInvariant(), exception.Message));
                }
            }
        }
        if (total == 0)
        {
            missing.Add(new(StoryNames[story], "No named native field descriptor was discovered; coverage is unknown, represented as 0%."));
        }
        CharacterCompatibility result = new(supported, total, missing.AsReadOnly()) { ExcludedSources = excludedSources.AsReadOnly() };
        compatibility.Add((story, appearance), result);
        return result;
    }

    private static bool CoverageError(Exception exception) => exception is IOException or InvalidDataException or NotSupportedException or InvalidOperationException or ArgumentException;

    private void Discover()
    {
        if (discovered)
        {
            return;
        }
        Dictionary<int, List<PcFieldModelDescriptor>> descriptors = [];
        using LgpArchive fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        foreach (LgpEntry entry in fields.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                PcFieldFile field = PcFieldFile.ReadCompressed(fields.ReadEntry(entry));
                PcFieldModels models = PcFieldModels.Read(field.GetSection(PcFieldSectionKind.Models).Span);
                foreach (PcFieldModelDescriptor descriptor in models.Models)
                {
                    string name = descriptor.Name;
                    string fieldName = Path.GetFileNameWithoutExtension(entry.FullPath);
                    if (name.StartsWith(fieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        name = name[fieldName.Length..];
                    }
                    int story = IdentifyStoryCharacter(name);
                    if (story >= 0)
                    {
                        if (!descriptors.TryGetValue(story, out List<PcFieldModelDescriptor>? list))
                        {
                            descriptors.Add(story, list = []);
                        }
                        list.Add(descriptor);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or ArgumentException)
            {
                excludedSources.Add(new(entry.FullPath, exception.Message));
            }
        }
        foreach (var pair in descriptors)
        {
            storyDescriptors.Add(pair.Key, pair.Value.AsReadOnly());
        }
        discovered = true;
    }
}
