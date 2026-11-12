namespace Memoria.FF7.Resources;

public enum Ff7InstallationLayout
{
    ClassicPc,
    SteamWorkingDirectory
}

public enum Ff7ResourceRole
{
    FieldArchive,
    CharacterArchive,
    BattleArchive,
    MagicArchive,
    WorldArchive,
    MenuArchive,
    Kernel,
    Kernel2,
    BattleScenes,
    SoundData,
    SoundIndex,
    Movies,
    Music,
    MovieCameraArchive,
    Window,
    BattleCamera0,
    BattleCamera1,
    BattleCamera2,
    BattleShadow
}

public sealed record Ff7ResourceLocation(Ff7ResourceRole Role, string Path);

public sealed class Ff7Installation
{
    private Ff7Installation(string root, string data, string language, Ff7InstallationLayout layout, List<Ff7ResourceLocation> resources)
    {
        RootDirectory = root;
        DataDirectory = data;
        Language = language;
        Layout = layout;
        Resources = resources.AsReadOnly();
    }

    public string RootDirectory { get; }
    public string DataDirectory { get; }
    public string Language { get; }
    public Ff7InstallationLayout Layout { get; }
    public IReadOnlyList<Ff7ResourceLocation> Resources { get; }

    public Ff7ResourceLocation GetRequired(Ff7ResourceRole role)
    {
        return Resources.SingleOrDefault(resource => resource.Role == role) ?? throw new InvalidDataException($"FF7 resource role {role} is absent in profile {Layout}/{Language}.");
    }

    public static Ff7Installation Discover(string root, string language = "en")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        (string field, string menu, string world) = language.ToLowerInvariant() switch
        {
            "en" => ("flevel.lgp", "menu_us.lgp", "world_us.lgp"),
            "de" => ("gflevel.lgp", "menu_gm.lgp", "world_gm.lgp"),
            "fr" => ("fflevel.lgp", "menu_fr.lgp", "world_fr.lgp"),
            "es" => ("sflevel.lgp", "menu_sp.lgp", "world_sp.lgp"),
            "ja" => ("jfleve.lgp", "menu_ja.lgp", "world_ja.lgp"),
            _ => throw new NotSupportedException($"FF7 resource language '{language}' is unsupported.")
        };
        string fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException("The selected FF7 resource root does not exist.");
        }
        string nested = Path.Combine(fullRoot, "ff7", "workingdir", "data");
        Ff7InstallationLayout layout = Directory.Exists(nested) ? Ff7InstallationLayout.SteamWorkingDirectory : Ff7InstallationLayout.ClassicPc;
        string data = layout == Ff7InstallationLayout.SteamWorkingDirectory ? nested : Path.Combine(fullRoot, "data");
        List<Ff7ResourceLocation> resources = new();
        AddFile(Ff7ResourceRole.FieldArchive, "field", field);
        AddFile(Ff7ResourceRole.CharacterArchive, "field", "char.lgp");
        AddFile(Ff7ResourceRole.BattleArchive, "battle", "battle.lgp");
        AddFile(Ff7ResourceRole.MagicArchive, "battle", "magic.lgp");
        AddFile(Ff7ResourceRole.BattleShadow, "battle", "kage.dat");
        AddFile(Ff7ResourceRole.WorldArchive, "wm", world);
        AddFile(Ff7ResourceRole.MenuArchive, "menu", menu);
        AddFile(Ff7ResourceRole.MovieCameraArchive, "cd", "moviecam.lgp");
        string localized = Path.Combine(data, $"lang-{language.ToLowerInvariant()}");
        AddLocalized(Ff7ResourceRole.Kernel, "kernel", "kernel.bin");
        AddLocalized(Ff7ResourceRole.Kernel2, "kernel", "kernel2.bin");
        AddLocalized(Ff7ResourceRole.Window, "kernel", "window.bin");
        AddLocalized(Ff7ResourceRole.BattleScenes, "battle", "scene.bin");
        AddLocalized(Ff7ResourceRole.BattleCamera0, "battle", "camdat0.bin");
        AddLocalized(Ff7ResourceRole.BattleCamera1, "battle", "camdat1.bin");
        AddLocalized(Ff7ResourceRole.BattleCamera2, "battle", "camdat2.bin");
        AddFile(Ff7ResourceRole.SoundData, "sound", "audio.dat");
        AddFile(Ff7ResourceRole.SoundIndex, "sound", "audio.fmt");
        AddDirectory(Ff7ResourceRole.Movies, Path.Combine(data, "movies"));
        AddDirectory(Ff7ResourceRole.Music, Directory.Exists(Path.Combine(data, "music_ogg")) ? Path.Combine(data, "music_ogg") : Path.Combine(data, "music"));
        Ff7Installation result = new(fullRoot, data, language.ToLowerInvariant(), layout, resources);
        result.GetRequired(Ff7ResourceRole.FieldArchive);
        result.GetRequired(Ff7ResourceRole.CharacterArchive);
        return result;

        void AddFile(Ff7ResourceRole role, string directory, string name)
        {
            string path = Path.Combine(data, directory, name);
            if (File.Exists(path))
            {
                resources.Add(new Ff7ResourceLocation(role, path));
            }
        }

        void AddLocalized(Ff7ResourceRole role, string directory, string name)
        {
            string path = Path.Combine(localized, directory, name);
            if (File.Exists(path))
            {
                resources.Add(new Ff7ResourceLocation(role, path));
            }
            else
            {
                AddFile(role, directory, name);
            }
        }

        void AddDirectory(Ff7ResourceRole role, string path)
        {
            if (Directory.Exists(path))
            {
                resources.Add(new Ff7ResourceLocation(role, path));
            }
        }
    }
}
