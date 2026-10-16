using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleFaceMaterialBinding
{
    /// <summary>507550 rewrites the actual polygon CLUT/page and high bit of every V coordinate after507400.</summary>
    public static NativeBattleFaceMaterial Apply507550(NativeBattleFaceMaterial material, NativeBattleModelTextureBinding binding)
    {
        if (binding.PaletteRows.IsDefault || binding.PaletteRows.Length != 16)
        {
            throw new InvalidDataException("Native507550 requires its complete original-to-allocated palette row map.");
        }
        int row = material.Clut >> 6;
        if ((material.Clut & 63) >= 16 || row is < 224 or >= 240)
        {
            return material;
        }
        byte slot = binding.PaletteRows[row - 224] ?? throw new NotSupportedException("Native polygon CLUT row has no executed507400 texture allocation.");
        if (slot >= 15)
        {
            throw new InvalidDataException("Native507550 palette row exceeds its original successful allocation range.");
        }
        int page = slot < 12 ? slot / 2 + 10 : slot - 2;
        int y = slot < 12 ? (slot & 1) << 7 : 128;
        ushort clut = (ushort)((material.Clut & 0x803F) | ((slot + 224) << 6));
        ushort texturePage = (ushort)((material.TexturePage & 0xFFE0) | page);
        Vector2[] uv = material.Uv.Select(value => new Vector2(value.X, ((int)value.Y & 127) | y)).ToArray();
        return new(texturePage, clut, uv);
    }
}
