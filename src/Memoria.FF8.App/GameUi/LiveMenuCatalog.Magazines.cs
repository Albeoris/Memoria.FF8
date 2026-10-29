using System.Buffers.Binary;
using Memoria.FF8.Resources.Localization;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class LiveMenuCatalog
{
    private readonly Dictionary<int, NativeMagazinePage[]> magazines = [];
    private readonly Dictionary<byte, (Func<string>[] Lines, Func<string>[] Layout)> magazinePresentation = [];

    public NativeMagazinePage[] MagazinePages(int itemId) => magazines.TryGetValue(itemId, out NativeMagazinePage[]? pages) ? pages.Select(ProjectMagazinePage).ToArray() : [];

    private NativeMagazinePage ProjectMagazinePage(NativeMagazinePage page)
    {
        var presentation = magazinePresentation[page.PageId];
        return page with
        {
            Text = presentation.Lines.Select(read => read()).ToArray(),
            Layout = page.Layout is { } layout ? layout with { Texts = layout.Texts.Select((text, index) => text with { Text = presentation.Layout[index]() }).ToArray(), Images = layout.Images.ToArray(), Icons = layout.Icons.ToArray(), Buttons = layout.Buttons.ToArray() } : null
        };
    }

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

    private void ReadMagazines(byte[] bytes, MngrpStringTable text, NativeWeaponShopDefinitions weapons, byte[] imageIndex, byte[] paperTexture, byte[] itemIconKinds)
    {
        NativeMagazineDataReader.ValidateInventoryPages(bytes, fieldItems, text, weapons, kernel.ZellLimits.Count, imageIndex, itemIconKinds);
        var paper = NativeMagazineArtwork.GetPaperColor(paperTexture);
        if (itemIconKinds.Length != 22)
        {
            throw new InvalidDataException("Magazine ingredients require the original native item-type icon table.");
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
                List<Func<string>> linePresentation = [];
                List<Func<string>> layoutPresentation = [];
                List<NativeMagazineText> texts = [];
                List<NativeMagazineImage> images = [];
                List<NativeMagazineIcon> icons = [];
                List<NativeMagazineButton> buttons = [];
                for (int slot = 0; slot < 4; slot++)
                {
                    byte textId = row[52 + slot * 4 + 3];
                    if (textId != 255)
                    {
                        string template = text[textId] ?? throw new InvalidDataException("Magazine page refers to an absent source text entry.");
                        string value = FormatDescription(template)!;
                        lines.Add(value);
                        texts.Add(new NativeMagazineText(BinaryPrimitives.ReadUInt16LittleEndian(row[(52 + slot * 4)..]), row[54 + slot * 4], value));
                        Func<string> readText = () => FormatDescription(LocalizedTable("mngrp_slot087_table", textId, template))!;
                        linePresentation.Add(readText);
                        layoutPresentation.Add(readText);
                    }
                    int imageId = row[39 + slot * 4];
                    if (imageId != 255)
                    {
                        (int width, int height) = NativeMagazineArtwork.GetDimensions(imageIndex, imageId);
                        images.Add(new NativeMagazineImage(BinaryPrimitives.ReadUInt16LittleEndian(row[(36 + slot * 4)..]), row[38 + slot * 4], imageId, width, height));
                    }
                }
                if (row[24] != 255)
                {
                    NativeWeaponRecipe recipe = weapons.Recipes[row[24]];
                    lines.Add(recipe.Name);
                    lines.Add(LocalizationTextComposition.Join(", ", recipe.Materials.Where(material => material.ItemId != 0).Select(material => LocalizationTextComposition.Format("{0} ×{1}", ItemName(material.ItemId), material.Count))));
                    int recipeId = row[24];
                    linePresentation.Add(() => WeaponName(recipeId) ?? recipe.Name);
                    linePresentation.Add(() => LocalizationTextComposition.Join(", ", recipe.Materials.Where(material => material.ItemId != 0).Select(material => LocalizationTextComposition.Format("{0} ×{1}", ItemName(material.ItemId), material.Count))));
                    int y = row[30];
                    int x = BinaryPrimitives.ReadUInt16LittleEndian(row[28..]);
                    foreach (NativeItemStock material in recipe.Materials)
                    {
                        if (material.ItemId != 0)
                        {
                            int type = fieldItems[material.ItemId * 4];
                            if (type >= itemIconKinds.Length)
                            {
                                throw new InvalidDataException("Magazine material has an unsupported native item type.");
                            }
                            icons.Add(new NativeMagazineIcon(x, y - 2, 223 + itemIconKinds[type]));
                            texts.Add(new NativeMagazineText(x + 14, y, ItemName(material.ItemId) ?? throw new InvalidDataException("Magazine material lacks its original name.")));
                            texts.Add(new NativeMagazineText(x + row[31], y, material.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), RightAligned: true));
                            layoutPresentation.Add(() => ItemName(material.ItemId) ?? throw new InvalidDataException("Magazine material lacks its original name."));
                            layoutPresentation.Add(() => material.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            y += row[25];
                        }
                    }
                }
                if (row[26] != 255)
                {
                    var move = kernel.ZellLimits[row[26]];
                    ushort[] combo = [move.Combo1, move.Combo2, move.Combo3, move.Combo4, move.Combo5];
                    int x = BinaryPrimitives.ReadUInt16LittleEndian(row[32..]);
                    foreach (ushort mask in combo)
                    {
                        if (mask == ushort.MaxValue)
                        {
                            break;
                        }
                        uint button = (uint)mask & 0xF0FF;
                        if (button == 0)
                        {
                            throw new InvalidDataException("Magazine Duel combination has an empty native input operand.");
                        }
                        if (buttons.Count != 0)
                        {
                            icons.Add(new NativeMagazineIcon(x - 16, row[34], 55));
                        }
                        buttons.Add(new NativeMagazineButton(x, row[34], System.Numerics.BitOperations.TrailingZeroCount(button)));
                        x += 40;
                    }
                }
                int tintWidth = BinaryPrimitives.ReadUInt16LittleEndian(row[12..]);
                int tintHeight = BinaryPrimitives.ReadUInt16LittleEndian(row[14..]);
                NativeMagazineTint? tint = tintWidth > 0 && tintHeight > 0 ? new NativeMagazineTint(BinaryPrimitives.ReadUInt16LittleEndian(row[8..]), BinaryPrimitives.ReadUInt16LittleEndian(row[10..]), tintWidth, tintHeight, row[16], row[17], row[18]) : null;
                NativeMagazineLayout layout = new(BinaryPrimitives.ReadUInt16LittleEndian(row), BinaryPrimitives.ReadUInt16LittleEndian(row[2..]), BinaryPrimitives.ReadUInt16LittleEndian(row[4..]), BinaryPrimitives.ReadUInt16LittleEndian(row[6..]), texts.ToArray(), images.ToArray(), tint)
                {
                    PaperColor = new NativeMagazineColor(paper.R, paper.G, paper.B),
                    Icons = icons.ToArray(),
                    Buttons = buttons.ToArray()
                };
                pages.Add(new NativeMagazinePage(itemId, (byte)id, row[24], row[26], row[27], lines.ToArray()) { Layout = layout });
                magazinePresentation[(byte)id] = (linePresentation.ToArray(), layoutPresentation.ToArray());
            }
            magazines.Add(itemId, pages.ToArray());
        }
    }
}
