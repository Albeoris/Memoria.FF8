using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods.EncodingProfiles;
using Memoria.FF8.Resources.Text;

namespace Memoria.Launcher.EncodingProfiles;

internal sealed class NativeFontPreview : Control, IDisposable
{
    private readonly Dictionary<string, WriteableBitmap> atlases = new(StringComparer.Ordinal);
    private NativeFontProfile? profile;
    private NativeFontLayoutResult? layout;
    private string? atlasId;
    private int? selectedImage;
    public Action<int>? SelectImage { get; set; }

    public NativeFontPreview()
    {
        MinHeight = 130;
        PointerPressed += (_, args) =>
        {
            if (profile is null || atlasId is null)
            {
                return;
            }
            Point point = args.GetPosition(this);
            NativeGlyphImage? image = profile.Images.FirstOrDefault(image => image.AtlasId == atlasId && point.X >= image.X * 2 && point.X < (image.X + image.Width) * 2 && point.Y >= image.Y * 2 && point.Y < (image.Y + image.Height) * 2);
            if (image is not null)
            {
                SelectImage?.Invoke(image.Index);
            }
        };
    }

    public void Load(NativeFontProfile value, string profilePath)
    {
        Dispose();
        profile = value;
        string root = Path.GetDirectoryName(Path.GetFullPath(profilePath))!;
        foreach (NativeFontAtlas atlas in value.Atlases)
        {
            string path = NativeFontProfileIO.ResolveDependency(root, atlas.Path);
            var pixels = NativeInstalledFontProfile.DecodeAtlas(File.ReadAllBytes(path), path, atlas.Palette);
            if (pixels.Width != atlas.Width || pixels.Height != atlas.Height)
            {
                throw new InvalidDataException($"Atlas '{atlas.Id}' changed dimensions; its image assignments must be reviewed.");
            }
            WriteableBitmap bitmap = new(new PixelSize(pixels.Width, pixels.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using (ILockedFramebuffer frame = bitmap.Lock())
            {
                byte[] rgba = MemoryMarshal.AsBytes(pixels.Pixels.AsSpan()).ToArray();
                for (int y = 0; y < pixels.Height; y++)
                {
                    Marshal.Copy(rgba, y * pixels.Width * 4, frame.Address + y * frame.RowBytes, pixels.Width * 4);
                }
            }
            atlases.Add(atlas.Id, bitmap);
        }
        InvalidateVisual();
    }

    public void ShowSample(NativeFontLayoutResult result)
    {
        layout = result;
        atlasId = null;
        Height = Math.Max(130, result.Height * 2 + 8);
        InvalidateVisual();
    }

    public void ShowAtlas(string? id, int? imageIndex)
    {
        atlasId = id;
        selectedImage = imageIndex;
        layout = null;
        Height = id is not null && atlases.TryGetValue(id, out WriteableBitmap? bitmap) ? bitmap.PixelSize.Height * 2 : 130;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.DarkSlateGray, new Rect(Bounds.Size));
        if (atlasId is not null && atlases.TryGetValue(atlasId, out WriteableBitmap? atlas))
        {
            context.DrawImage(atlas, new Rect(0, 0, atlas.PixelSize.Width * 2, atlas.PixelSize.Height * 2));
            if (profile is not null)
            {
                foreach (NativeGlyphImage image in profile.Images.Where(image => image.AtlasId == atlasId))
                {
                    context.DrawRectangle(null, new Pen(image.Index == selectedImage ? Brushes.HotPink : Brushes.Gray, image.Index == selectedImage ? 2 : 0.5), new Rect(image.X * 2, image.Y * 2, image.Width * 2, image.Height * 2));
                }
            }
        }
        else if (layout is not null)
        {
            foreach (NativeGlyphRun run in layout.Glyphs.Where(run => run.Page == 0))
            {
                if (atlases.TryGetValue(run.Image.AtlasId, out WriteableBitmap? bitmap))
                {
                    context.DrawImage(bitmap, new Rect(run.Image.X, run.Image.Y, run.Image.Width, run.Image.Height), new Rect(run.X * 2, run.Y * 2, run.Image.Width * 2, run.Image.Height * 2));
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (WriteableBitmap bitmap in atlases.Values)
        {
            bitmap.Dispose();
        }
        atlases.Clear();
    }
}
