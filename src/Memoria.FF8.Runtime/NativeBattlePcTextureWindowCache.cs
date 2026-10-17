using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePcTextureWindowRegion([property: JsonRequired] uint WindowBits, [property: JsonRequired] NativeBattlePrimitiveTextureWindow Rectangle, [property: JsonRequired] bool CurrentPixels);
public sealed record NativeBattlePcTextureWindowRow([property: JsonRequired] ImmutableArray<NativeBattlePcTextureWindowRegion> Regions, [property: JsonRequired] uint UsedMask, [property: JsonRequired] uint AlternateMask, [property: JsonRequired] uint DirtyMask, [property: JsonRequired] ushort AlternateClut)
{
    public int Count => Regions.Length;
}
public sealed record NativeBattlePcTextureWindowUpload(int Row, int Slot, NativeBattlePrimitiveTextureWindow Rectangle);
public sealed record NativeBattlePcTextureWindowPreparation(NativeBattlePcTextureWindowCache Cache, ImmutableArray<NativeBattlePcTextureWindowUpload> Uploads);
public sealed record NativeBattlePcTextureWindowSelection([property: JsonRequired] int Row, [property: JsonRequired] int Slot, [property: JsonRequired] int X, [property: JsonRequired] int Y, [property: JsonRequired] int Width, [property: JsonRequired] int Height, [property: JsonRequired] float ScaleU, [property: JsonRequired] float ScaleV, [property: JsonRequired] bool Windowed, [property: JsonRequired] bool Fallback)
{
    public void Validate()
    {
        if (Row is < 0 or >= 96 || Slot is < 0 or >= 8 || X is < 0 or > 248 || Y is < 0 or > 248 || Width is < 1 or > 256 || Height is < 1 or > 256 || X + Width > 256 || Y + Height > 256 || !float.IsFinite(ScaleU) || !float.IsFinite(ScaleV) || ScaleU <= 0 || ScaleV <= 0 || Fallback && (!Windowed || Slot != 0))
        {
            throw new InvalidDataException("Native PC texture selection requires its actual bounded region and positive finite UV scales.");
        }
    }
}
public sealed record NativeBattlePcTextureWindowResolve(NativeBattlePcTextureWindowCache Cache, NativeBattlePcTextureWindowSelection Selection);

/// <summary>4647A0/464B60/464BD0/465CE0's96 page-depth rows, eight regions and retained global UV scales.</summary>
public sealed record NativeBattlePcTextureWindowCache([property: JsonRequired] ImmutableArray<NativeBattlePcTextureWindowRow> Rows, [property: JsonRequired] float? ScaleU, [property: JsonRequired] float? ScaleV)
{
    public static NativeBattlePcTextureWindowCache Reset4647A0(NativeBattlePcTextureWindowCache? prior)
    {
        prior?.Validate();
        NativeBattlePcTextureWindowRow empty = new([], 0, 0, 0, 0);
        return new(Enumerable.Repeat(empty, 96).ToImmutableArray(), prior?.ScaleU, prior?.ScaleV);
    }

    public void Validate()
    {
        if (Rows.IsDefault || Rows.Length != 96 || (ScaleU is null) != (ScaleV is null) || ScaleU is float u && !float.IsFinite(u) || ScaleV is float v && !float.IsFinite(v))
        {
            throw new InvalidDataException("Native PC window cache requires96 rows and paired retained UV scale DWORDs.");
        }
        foreach (NativeBattlePcTextureWindowRow row in Rows)
        {
            if (row is null || row.Regions.IsDefault || row.Regions.Length > 8 || (row.UsedMask & ~255u) != 0 || (row.AlternateMask & ~255u) != 0 || row.Regions.Any(region => region is null || region.WindowBits > 0xFFFFFF || region.Rectangle is null || region.Rectangle.X is < 0 or > 248 || region.Rectangle.Y is < 0 or > 248 || region.Rectangle.Width is < 1 or > 256 || region.Rectangle.Height is < 1 or > 256 || region.Rectangle.X + region.Rectangle.Width > 256 || region.Rectangle.Y + region.Rectangle.Height > 256))
            {
                throw new InvalidDataException("Native PC window row requires its actual bounded region identities and flags.");
            }
            if (row.Count != 0 && (row.Regions[0].WindowBits != 0 || row.Regions[0].Rectangle != new NativeBattlePrimitiveTextureWindow(0, 0, 256, 256)))
            {
                throw new InvalidDataException("Native PC window row0 must retain its prepared full-page texture.");
            }
        }
    }

    public NativeBattlePcTextureWindowCache Preprocess462F90(ushort texturePage, ushort clut)
    {
        Validate();
        int index = RowIndex(texturePage);
        NativeBattlePcTextureWindowRow row = Rows[index];
        return this with { Rows = Rows.SetItem(index, Mark(row, 0, texturePage, clut)) };
    }

    public NativeBattlePcTextureWindowPreparation Prepare464BD0()
    {
        Validate();
        ImmutableArray<NativeBattlePcTextureWindowRow>.Builder rows = Rows.ToBuilder();
        ImmutableArray<NativeBattlePcTextureWindowUpload>.Builder uploads = ImmutableArray.CreateBuilder<NativeBattlePcTextureWindowUpload>();
        for (int index = 0; index < rows.Count; index++)
        {
            NativeBattlePcTextureWindowRow row = rows[index];
            for (int slot = 0; slot < 8; slot++)
            {
                uint bit = 1u << slot;
                if ((row.UsedMask & bit) == 0)
                {
                    continue;
                }
                if (slot >= row.Count)
                {
                    if (slot != 0 || row.Count != 0)
                    {
                        throw new NotSupportedException("Native464BD0 requires the original resource allocation for a used non-base window slot.");
                    }
                    NativeBattlePcTextureWindowRegion region = new(0, new(0, 0, 256, 256), true);
                    row = row with { Regions = [region] };
                    uploads.Add(new(index, slot, region.Rectangle));
                }
                else if ((row.DirtyMask & bit) != 0)
                {
                    NativeBattlePcTextureWindowRegion region = row.Regions[slot] with { CurrentPixels = true };
                    row = row with { Regions = row.Regions.SetItem(slot, region) };
                    uploads.Add(new(index, slot, region.Rectangle));
                }
                //464BD0 clears this dirty bit only for a used slot, including a newly initialized base.
                row = row with { DirtyMask = row.DirtyMask & ~bit };
            }
            rows[index] = row;
        }
        return new(this with { Rows = rows.ToImmutable() }, uploads.ToImmutable());
    }

    public NativeBattlePcTextureWindowCache Flush465930()
    {
        Validate();
        return this with { Rows = Rows.Select(row => row with { UsedMask = 0, AlternateMask = 0 }).ToImmutableArray() };
    }

    public NativeBattlePcTextureWindowCache Invalidate464850(int left, int top, int right, int bottom)
    {
        Validate();
        if (left is < 0 or >= 1024 || right < left - 1 || right >= 1024 || top is < 0 or >= 512 || bottom < top - 1 || bottom >= 512)
        {
            throw new NotSupportedException("Native464850 window invalidation requires its actual inclusive VRAM rectangle, including origin-minus-one ends for empty transfers.");
        }
        ImmutableArray<NativeBattlePcTextureWindowRow>.Builder rows = Rows.ToBuilder();
        for (int y = top / 256; y <= bottom / 256; y++)
        {
            for (int x = left / 64; x <= right / 64; x++)
            {
                for (int depth = 0; depth < 3; depth++)
                {
                    Invalidate(depth * 32 + y * 16 + x);
                }
                //46495C..464980 additionally invalidates the even depth1 page of each touched column pair.
                Invalidate(32 + y * 16 + (x & ~1));
            }
        }
        return this with { Rows = rows.ToImmutable() };

        void Invalidate(int index)
        {
            NativeBattlePcTextureWindowRow row = rows[index];
            rows[index] = row with { DirtyMask = uint.MaxValue, Regions = row.Regions.Select(region => region with { CurrentPixels = false }).ToImmutableArray() };
        }
    }

    public NativeBattlePcTextureWindowResolve Resolve465CE0(ushort texturePage, ushort clut, uint windowBits)
    {
        Validate();
        int index = RowIndex(texturePage);
        NativeBattlePcTextureWindowRow row = Rows[index];
        if (row.Count == 0)
        {
            throw new NotSupportedException("Native465CE0 requires its462F90/464BD0 prepared full-page resource.");
        }
        windowBits &= 0xFFFFFF;
        int slot = 0;
        bool fallback = false;
        float? scaleU = ScaleU;
        float? scaleV = ScaleV;
        if (windowBits != 0)
        {
            slot = 1;
            while (slot < row.Count && row.Regions[slot].WindowBits != windowBits)
            {
                slot++;
            }
            if (slot == row.Count && row.Count == 8)
            {
                //465E88..465F28 chooses the base page and bypasses BOTH global scale writes.
                slot = 0;
                fallback = true;
            }
            else
            {
                if (slot == row.Count)
                {
                    NativeBattlePrimitiveTextureWindow rectangle = NativeBattlePrimitiveTextureWindow.Decode45BC60(windowBits);
                    //The native synchronous successful resource path copies current VRAM when this region is created.
                    row = row with { Regions = row.Regions.Add(new(windowBits, rectangle, true)) };
                }
                row = Mark(row, slot, texturePage, clut);
                scaleU = 1f / row.Regions[slot].Rectangle.Width;
                scaleV = 1f / row.Regions[slot].Rectangle.Height;
            }
        }
        NativeBattlePcTextureWindowRegion selected = row.Regions[slot];
        if (!selected.CurrentPixels)
        {
            throw new NotSupportedException("Native465CE0 selected a dirty cached texture region before464BD0 refreshed it; its retained prior pixel bytes are required.");
        }
        float actualU = windowBits == 0 ? 1f / 256 : scaleU ?? throw new NotSupportedException("Native465CE0 fallback requires its retained1CB603C U scale DWORD.");
        float actualV = windowBits == 0 ? 1f / 256 : scaleV ?? throw new NotSupportedException("Native465CE0 fallback requires its retained1CB6038 V scale DWORD.");
        NativeBattlePrimitiveTextureWindow result = selected.Rectangle;
        NativeBattlePcTextureWindowSelection selection = new(index, slot, result.X, result.Y, result.Width, result.Height, actualU, actualV, windowBits != 0, fallback);
        return new(this with { Rows = Rows.SetItem(index, row), ScaleU = scaleU, ScaleV = scaleV }, selection);
    }

    private static int RowIndex(ushort texturePage)
    {
        int depth = texturePage >> 7 & 3;
        if (depth == 3)
        {
            throw new NotSupportedException("Native465CE0 does not select a texture resource for TPAGE depth3.");
        }
        return depth * 32 + (texturePage & 31);
    }

    private static NativeBattlePcTextureWindowRow Mark(NativeBattlePcTextureWindowRow row, int slot, ushort texturePage, ushort clut)
    {
        uint bit = 1u << slot;
        return (texturePage & 0x60) == 0x40 ? row with { UsedMask = row.UsedMask | bit, AlternateMask = row.AlternateMask | bit, AlternateClut = (ushort)(clut & 0x7FFF) } : row with { UsedMask = row.UsedMask | bit };
    }
}
