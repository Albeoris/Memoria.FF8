using System.Diagnostics;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Mods.EncodingProfiles;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

/// <summary>Graphics adapter over immutable profile namespaces and the editor's shared glyph layout.</summary>
internal sealed class NativeFontPresentation(GraphicsDevice device) : IDisposable
{
    private readonly Dictionary<(string ProfileId, string AtlasId), Texture2D> textures = [];
    private NativeFontProfileCatalog? current;
    private Task<NativeFontProfileCatalog>? loading;
    private string? loadingIdentity;
    private string requestedProfileId = "Auto";
    private string? targetDataset;
    private long nextProbe;
    public string? Diagnostic { get; private set; }
    public NativeFontProfile? Profile => current?.DefaultProfile(requestedProfileId, targetDataset);
    public float LineHeight => Profile?.LineHeight ?? 16;
    public bool Ready => current is not null;
    public long Revision { get; private set; }
    public IReadOnlyList<string> AvailableProfileIds => current?.AvailableProfileIds ?? ["Auto"];

    public NativeFontProfile? ProfileFor(NativeTextContext context, string role = "Target", string? dataset = null) => Select(context, role, dataset).Entry?.Profile;
    public float LineHeightFor(NativeTextContext context, string role = "Target", string? dataset = null) => ProfileFor(context, role, dataset)?.LineHeight ?? 16;

    public void Prepare(ResourceService resources, IReadOnlyList<ModPackage> packages, string profileId, string? target)
    {
        requestedProfileId = profileId;
        targetDataset = target;
        string identity = resources.CatalogGeneration + ":" + resources.ResourceLanguage + ":" + string.Join("|", packages.Where(package => package.Status == ModEffectiveStatus.Active).Select(package => package.Id + ":" + package.Priority));
        if (loading is { IsCompleted: true } completed)
        {
            loading = null;
            try
            {
                NativeFontProfileCatalog candidate = completed.GetAwaiter().GetResult();
                if (loadingIdentity == identity && candidate.Revision != current?.Revision)
                {
                    current = candidate;
                    ClearTextures();
                    Revision++;
                }
                if (loadingIdentity == identity)
                {
                    Diagnostic = null;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
            {
                Diagnostic = (current is null ? "Native font catalog unavailable: " : "Font reload rejected; retaining the last validated catalog: ") + exception.Message;
            }
        }
        long now = Stopwatch.GetTimestamp();
        if (loading is null && (loadingIdentity != identity || now >= nextProbe))
        {
            loadingIdentity = identity;
            nextProbe = now + Stopwatch.Frequency;
            if (current is null)
            {
                Diagnostic = "Loading native font profiles…";
            }
            ModPackage[] active = packages.Where(package => package.Status == ModEffectiveStatus.Active).OrderBy(package => package.Priority).ToArray();
            NativeFontProfileCatalog? previous = current;
            loading = Task.Run(() => LoadCatalog(resources, active, previous));
        }
    }

    internal static NativeFontProfileCatalog LoadCatalog(ResourceService resources, IReadOnlyList<ModPackage> packages, NativeFontProfileCatalog? previous = null)
    {
        List<(NativeFontProfile Profile, IReadOnlyDictionary<string, byte[]> Files, string Revision)> profiles = [];
        using IncrementalHash catalogHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Hash(string value) => catalogHash.AppendData(System.Text.Encoding.UTF8.GetBytes(value + "\0"));
        foreach (ModPackage package in packages)
        {
            Hash(package.Id);
            Hash(package.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string directory = NativeFontProfileIO.ResolveDependency(package.RootDirectory, "Encoding");
            if (!Directory.Exists(directory))
            {
                continue;
            }
            foreach (string child in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
            {
                string name = Path.GetFileName(child);
                if (name.StartsWith('.'))
                {
                    continue;
                }
                string profilePath = NativeFontProfileIO.ResolveDependency(package.RootDirectory, "Encoding/" + name + "/profile.json");
                string profileRoot = Path.GetDirectoryName(profilePath)!;
                byte[] json = File.ReadAllBytes(profilePath);
                NativeFontProfile profile = NativeFontProfileIO.Parse(System.Text.Encoding.UTF8.GetString(json));
                Dictionary<string, byte[]> files = new(StringComparer.Ordinal) { ["profile.json"] = json };
                Dictionary<string, string> casing = new(StringComparer.OrdinalIgnoreCase);
                foreach ((string path, string? expectedHash) in profile.Atlases.Select(atlas => (atlas.Path, atlas.Sha256)).Concat(profile.Dependencies.Select(dependency => (dependency.Path, dependency.Sha256))))
                {
                    if (path.Equals("profile.json", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("A profile dependency cannot replace profile.json.");
                    }
                    if (casing.TryGetValue(path, out string? previousCase) && previousCase != path)
                    {
                        throw new InvalidDataException($"Profile dependency '{path}' has a case collision.");
                    }
                    casing[path] = path;
                    if (!files.TryGetValue(path, out byte[]? bytes))
                    {
                        bytes = File.ReadAllBytes(NativeFontProfileIO.ResolveDependency(profileRoot, path));
                        files.Add(path, bytes);
                    }
                    if (expectedHash is not null && !Convert.ToHexString(SHA256.HashData(bytes)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Profile dependency '{path}' failed its SHA-256 check.");
                    }
                }
                using IncrementalHash profileHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                foreach ((string path, byte[] bytes) in files.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    profileHash.AppendData(System.Text.Encoding.UTF8.GetBytes(path + "\0"));
                    profileHash.AppendData(SHA256.HashData(bytes));
                }
                string revision = Convert.ToHexString(profileHash.GetHashAndReset());
                Hash(name);
                Hash(revision);
                profiles.Add((profile, files, revision));
            }
        }
        byte[]? tdw = null;
        byte[]? texture = null;
        string? installedFailure = null;
        if (resources.ResourceLanguage is "jp" or "ja")
        {
            installedFailure = "Automatic installed Japanese glyph-image placement is unsupported; assign an explicit escaped-page/field font profile.";
        }
        else
        {
            try
            {
                tdw = resources.Read(new("main/sysfnt.tdw"));
                texture = resources.Read(new("menu/sysfnt.tex"));
                catalogHash.AppendData(SHA256.HashData(tdw));
                catalogHash.AppendData(SHA256.HashData(texture));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException)
            {
                installedFailure = exception.Message;
            }
        }
        Hash(resources.Source.Edition.ToString());
        Hash(resources.ResourceLanguage);
        Hash(installedFailure ?? "");
        string catalogRevision = Convert.ToHexString(catalogHash.GetHashAndReset());
        if (previous?.Revision == catalogRevision)
        {
            return previous;
        }
        List<NativeFontProfileEntry> entries = [];
        foreach (var item in profiles)
        {
            Dictionary<string, PixelImage> atlases = [];
            foreach (NativeFontAtlas atlas in item.Profile.Atlases)
            {
                PixelImage image = NativeInstalledFontProfile.DecodeAtlas(item.Files[atlas.Path], atlas.Path, atlas.Palette);
                if (image.Width != atlas.Width || image.Height != atlas.Height)
                {
                    throw new InvalidDataException($"Atlas '{atlas.Id}' changed dimensions; its glyph assignments require review.");
                }
                atlases.Add(atlas.Id, image);
            }
            entries.Add(new(item.Profile, atlases, item.Revision));
        }
        NativeFontProfileEntry? installed = null;
        if (tdw is not null && texture is not null && installedFailure is null)
        {
            PixelImage image = NativeInstalledFontProfile.DecodeAtlas(texture, "sysfnt.tex");
            NativeFontProfile font = NativeInstalledFontProfile.CreateEuropean("installed:" + resources.ResourceLanguage, "Installed native font", resources.ResourceLanguage, new(resources.Source.Edition.ToString(), "Fields", DictionaryLanguage: resources.ResourceLanguage), "sysfnt.tex", image, tdw);
            font = font with { Domains = new[] { "Fields", "World", "System", "Menus", "Battle" }.Select(family => new NativeTextDomain(resources.Source.Edition.ToString(), family, DictionaryLanguage: resources.ResourceLanguage)).ToArray() };
            installed = new(font, new Dictionary<string, PixelImage> { ["native"] = image }, catalogRevision);
        }
        return new(entries, installed, catalogRevision, installedFailure);
    }

    private NativeFontSelection Select(NativeTextContext context, string role, string? dataset)
    {
        NativeFontSelection selection = current?.Resolve(requestedProfileId, context, role, dataset ?? (role == "Target" ? targetDataset : null)) ?? new(null, Diagnostic ?? "Native font catalog is unavailable.");
        if (!selection.Success)
        {
            Diagnostic = selection.Diagnostic;
        }
        return selection;
    }

    public NativeFontLayoutResult Layout(string value, NativeTextContext context, float maximumWidth = float.PositiveInfinity, string role = "Target", string? dataset = null, float maximumHeight = float.PositiveInfinity)
    {
        NativeFontSelection selection = Select(context, role, dataset);
        return selection.Entry is { } entry ? NativeFontLayout.Layout(entry.Profile, value, context, maximumWidth, maximumHeight) : new([], 0, 16, 1, [new("font-unavailable", selection.Diagnostic ?? "Native font is unavailable.")]);
    }

    /// <summary>Returns pen advance; painted bounds remain available through Layout.Width.</summary>
    public float Measure(string value, NativeTextContext context, string role = "Target", string? dataset = null) => Layout(value, context, role: role, dataset: dataset).FinalAdvance;

    public bool Draw(SpriteBatch batch, string value, Vector2 origin, float scale, Color color, NativeTextContext context, Rectangle clip, float maximumWidth = float.PositiveInfinity, string role = "Target", string? dataset = null, float maximumHeight = float.PositiveInfinity)
    {
        if (!float.IsFinite(scale) || scale <= 0)
        {
            Diagnostic = "Native font scale must be positive and finite.";
            return false;
        }
        NativeFontSelection selection = Select(context, role, dataset);
        if (selection.Entry is not { } entry)
        {
            return false;
        }
        NativeFontLayoutResult layout = NativeFontLayout.Layout(entry.Profile, value, context, maximumWidth, maximumHeight);
        if (!layout.Success)
        {
            Diagnostic = string.Join("; ", layout.Diagnostics.Select(error => error.Position < 0 ? error.Message : $"{error.Message} (position {error.Position})"));
            return false;
        }
        foreach (NativeGlyphRun glyph in layout.Glyphs.Where(glyph => glyph.Page == 0))
        {
            NativeGlyphImage image = glyph.Image;
            var key = (entry.Profile.Id, image.AtlasId);
            if (!textures.TryGetValue(key, out Texture2D? atlas))
            {
                PixelImage pixels = entry.Atlases[image.AtlasId];
                atlas = new Texture2D(device, pixels.Width, pixels.Height);
                atlas.SetData(pixels.Pixels.Select(pixel => Color.FromNonPremultiplied(pixel.R, pixel.G, pixel.B, pixel.A)).ToArray());
                textures.Add(key, atlas);
            }
            Rectangle destination = new((int)MathF.Round(origin.X + glyph.X * scale), (int)MathF.Round(origin.Y + glyph.Y * scale), (int)MathF.Ceiling(image.Width * scale), (int)MathF.Ceiling(image.Height * scale));
            if (NativeGlyphClipping.Clip(destination, new(image.X, image.Y, image.Width, image.Height), clip) is { } placement)
            {
                batch.Draw(atlas, placement.Position, placement.Source, color, 0, Vector2.Zero, placement.Scale, SpriteEffects.None, 0);
            }
        }
        return true;
    }

    private void ClearTextures()
    {
        foreach (Texture2D atlas in textures.Values)
        {
            atlas.Dispose();
        }
        textures.Clear();
    }

    public void Dispose()
    {
        if (loading is { } pending)
        {
            try
            {
                pending.GetAwaiter().GetResult();
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                Diagnostic = exception.Message;
            }
            loading = null;
        }
        ClearTextures();
    }
}
