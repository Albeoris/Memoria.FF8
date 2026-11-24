using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.GameUi;

/// <summary>Original inventory glyphs indexed by their native weapon definition IDs.</summary>
internal static class WeaponIconAtlas
{
    private const int CellSize = 48;
    private const int Columns = 8;
    private const int Rows = 5;
    private const int WeaponCount = 33;
    private const string AssetName = "Memoria.FF8.Artwork.WeaponIcons.dds";
    private static readonly int[] FirstWeaponByOwner = [0, 7, 11, 15, 19, 24, 28, 29, 30, 31, 32];

    private static readonly Lazy<PixelImage> Atlas = new(ReadAtlas);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Rectangle, PixelImage> Cells = new();

    internal static PixelImage ReadIcon(UiImageSource source)
    {
        return Cells.GetOrAdd(Bounds(source), bounds =>
        {
            PixelImage atlas = Atlas.Value;
            PixelImage cell = new(bounds.Width, bounds.Height);
            for (int y = 0; y < bounds.Height; y++)
            {
                atlas.Pixels.AsSpan((bounds.Y + y) * atlas.Width + bounds.X, bounds.Width).CopyTo(cell.Pixels.AsSpan(y * bounds.Width, bounds.Width));
            }
            return cell;
        });
    }

    internal static int? RequiredWeaponOwner(BattleLimitFamily family)
    {
        return BattleLimitFamilyRules.RequiredWeapon(family) switch
        {
            BattleWeaponCapabilities.None => null,
            BattleWeaponCapabilities.Gunblade => 0,
            _ => BattleLimitFamilyRules.SourceCharacter(family)
        };
    }

    internal static Rectangle Bounds(UiImageSource source)
    {
        int id = source.Id;
        if (source.Kind == UiImageKind.Weapon)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(id);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(id, FirstWeaponByOwner.Length);
            id = FirstWeaponByOwner[id];
        }
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(id, WeaponCount);
        return new Rectangle(id % Columns * CellSize, id / Columns * CellSize, CellSize, CellSize);
    }

    private static PixelImage ReadAtlas()
    {
        using Stream stream = typeof(WeaponIconAtlas).Assembly.GetManifestResourceStream(AssetName) ?? throw new InvalidOperationException("The weapon icon atlas is missing.");
        using MemoryStream data = new();
        stream.CopyTo(data);
        var image = TextureImageDecoder.Decode(data.ToArray(), AssetName).Image;
        if (image.Width != Columns * CellSize || image.Height != Rows * CellSize)
        {
            throw new InvalidDataException("The weapon icon atlas has unexpected dimensions.");
        }

        return image;
    }

    internal static Texture2D Load(GraphicsDevice device)
    {
        PixelImage image = Atlas.Value;
        Texture2D texture = new(device, image.Width, image.Height);
        try
        {
            texture.SetData(image.Pixels.Select(color => Color.FromNonPremultiplied(color.R, color.G, color.B, color.A)).ToArray());
            return texture;
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }
}
