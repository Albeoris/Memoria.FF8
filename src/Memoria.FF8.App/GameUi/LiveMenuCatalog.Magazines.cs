using System.Security.Cryptography;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class LiveMenuCatalog
{
    private readonly Dictionary<int, NativeMagazinePage[]> magazines = [];

    public NativeMagazinePage[] MagazinePages(int itemId) => magazines.TryGetValue(itemId, out NativeMagazinePage[]? pages) ? pages.Select(page => page with { Text = page.Text.ToArray() }).ToArray() : [];

    public NativeMagazinePage MagazinePage(int itemId, int pageId) => MagazinePages(itemId).SingleOrDefault(page => page.PageId == pageId) ?? throw new ArgumentOutOfRangeException(nameof(pageId), "Magazine page lies outside its original item's inclusive range.");

    public Dictionary<byte, string> MagazineUnavailableReasons(int itemId, GameState state)
    {
        Dictionary<byte, string> reasons = [];
        foreach (NativeMagazinePage page in MagazinePages(itemId))
        {
            try
            {
                page.View(state.Economy ?? throw new NotSupportedException("Magazine viewing requires authentic saved inventory."), state.WeaponRecipes, state.LimitProgress);
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or InvalidOperationException)
            {
                reasons.Add(page.PageId, exception.Message);
            }
        }
        return reasons;
    }

    private void ReadMagazines(byte[] bytes, MngrpStringTable text, NativeWeaponShopDefinitions weapons)
    {
        if (bytes.Length != 4692 || Convert.ToHexString(SHA256.HashData(bytes)) != "0D9D48BB57F25CBFD4A626A121B1C24FCE37F2EE2ED11884757166B3C995905A" || text.Entries.Count != 111)
        {
            throw new InvalidDataException("Magazine viewing requires the original mmag page rows and English section87 text.");
        }
        for (int itemId = 1; itemId < fieldItems.Length / 4; itemId++)
        {
            if (fieldItems[itemId * 4] != 9)
            {
                continue;
            }
            int first = fieldItems[itemId * 4 + 2];
            int last = fieldItems[itemId * 4 + 3];
            if (first > last || last >= bytes.Length / 68)
            {
                throw new InvalidDataException("Magazine item range exceeds its original page rows.");
            }
            List<NativeMagazinePage> pages = [];
            for (int id = first; id <= last; id++)
            {
                ReadOnlySpan<byte> row = bytes.AsSpan(id * 68, 68);
                List<string> lines = [];
                for (int slot = 0; slot < 4; slot++)
                {
                    byte textId = row[52 + slot * 4 + 3];
                    if (textId != 255)
                    {
                        lines.Add(DialogueTextFormatter.Format(text[textId] ?? throw new InvalidDataException("Magazine page refers to an absent English text entry.")));
                    }
                }
                if (row[24] != 255)
                {
                    NativeWeaponRecipe recipe = weapons.Recipes[row[24]];
                    lines.Add(recipe.Name);
                    lines.Add(string.Join(", ", recipe.Materials.Where(material => material.ItemId != 0).Select(material => $"{ItemName(material.ItemId)} ×{material.Count}")));
                }
                pages.Add(new NativeMagazinePage(itemId, (byte)id, row[24], row[26], row[27], lines.ToArray()));
            }
            magazines.Add(itemId, pages.ToArray());
        }
    }
}
