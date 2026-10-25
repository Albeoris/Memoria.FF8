using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private IReadOnlyDictionary<(ushort Page, ushort Clut), AssetHandle<TextureValue>> RegisterStagePages(ResourceKey owner, BattleStage stage)
    {
        Dictionary<(ushort Page, ushort Clut), AssetHandle<TextureValue>> pages = [];
        foreach (BattleFace face in stage.Meshes.SelectMany(mesh => mesh.Faces))
        {
            NativeBattleFaceMaterial material = face.NativeMaterial ?? throw new NotSupportedException("Stage page registration requires the original material identity.");
            ushort page = (ushort)(material.TexturePage & 0x19F);
            ushort clut = (ushort)(material.Clut & 0x7FFF);
            if ((page & 31) is < 16 or > 21 || pages.ContainsKey((page, clut)))
            {
                continue;
            }
            TextureSourceRegion region = stage.TextureRegions[face.Texture];
            int pixelsPerWord = 16 / region.BitsPerPixel;
            int sourceX = ((page & 15) * 64 - region.X) * pixelsPerWord;
            int sourceY = (page & 16) * 16 - region.Y;
            PixelImage original = stage.Textures[face.Texture];
            PixelImage image = new(256, 256);
            int left = Math.Max(0, -sourceX);
            int right = Math.Min(256, original.Width - sourceX);
            int top = Math.Max(0, -sourceY);
            int bottom = Math.Min(256, original.Height - sourceY);
            if (left >= right || top >= bottom)
            {
                continue;
            }
            for (int row = top; row < bottom; row++)
            {
                original.Pixels.AsSpan((sourceY + row) * original.Width + sourceX + left, right - left).CopyTo(image.Pixels.AsSpan(row * 256 + left));
            }
            TextureKey key = new(owner, $"page/{page & 31}", clut);
            pages.Add((page, clut), textureScope.Register(key, image, "native-battle-stage-page", new((page & 15) * 64, (page & 16) * 16, region.BitsPerPixel, 256, 256, (clut & 63) * 16, clut >> 6))!);
        }
        return pages;
    }
}
