using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public readonly record struct NativeBattlePcPaletteEntry(byte Index, ushort Word);

/// <summary>Exact native words consumed by one PC decode; unused CLUT entries are not observed.</summary>
public sealed record NativeBattlePcPaletteReceipt(ushort Clut, ImmutableArray<NativeBattlePcPaletteEntry> Entries)
{
    public int RetainedBytes => checked(sizeof(ushort) + Entries.Length * Unsafe.SizeOf<NativeBattlePcPaletteEntry>());

    public bool Matches(NativeBattleEffectTextureState state)
    {
        state.Validate();
        if (Entries.IsDefaultOrEmpty || Entries.Length > 256)
        {
            throw new InvalidDataException("A native palette receipt requires its actual consumed entries.");
        }
        int x = (Clut & 63) * 16;
        int y = (Clut & 0x7FFF) >> 6;
        int previous = -1;
        foreach (NativeBattlePcPaletteEntry entry in Entries)
        {
            if (entry.Index <= previous || x + entry.Index >= 1024)
            {
                throw new InvalidDataException("A native palette receipt requires ordered entries inside physical CLUT storage.");
            }
            previous = entry.Index;
            if (state.ReadVramWord(x + entry.Index, y) != entry.Word)
            {
                return false;
            }
        }
        return true;
    }
}
