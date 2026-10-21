using System.Globalization;
using Albeoris.Games.FF8.KernelBin;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class NativeTutorialPlayer
{
    private KernelData StatusKernel => catalog.ReadStatusKernel();

    private IReadOnlyList<string> TutorialElementStatusDetails()
    {
        int row = Menu.Character?.NativeId ?? definition.Descriptor.CharacterId;
        var statistics = catalog.ReadStatistics(party, row);
        var rules = catalog.ReadStatRules(party, row);
        var resistances = rules.ProjectStatusResistances(party.Records[row].MagicInventory);
        string[] elements = ["Fire", "Ice", "Thunder", "Earth", "Poison", "Wind", "Water", "Holy"];
        List<string> lines = [];
        for (int element = 0; element < elements.Length; element++)
        {
            int attack = (statistics.ElementAttack & (1 << element)) != 0 ? statistics.ElementAttackPercent : 0;
            int defense = statistics.ElementDefenses[element] - 800;
            lines.Add($"{elements[element]}   Atk {attack}%   Def {(defense > 100 ? "Absorb " + (defense - 100).ToString(CultureInfo.InvariantCulture) : defense.ToString(CultureInfo.InvariantCulture))}%");
        }
        (int Bit, string Name)[] statuses = [(0, "Death"), (1, "Poison"), (2, "Petrify"), (3, "Darkness"), (4, "Silence"), (5, "Berserk"), (6, "Zombie"), (8, "Sleep"), (10, "Slow"), (11, "Stop"), (17, "Curse"), (22, "Drain"), (23, "Apathy")];
        int spellId = party.Records[row].Junction.MagicJunctions[10];
        int stock = spellId == 0 ? 0 : party.Records[row].MagicInventory.FirstOrDefault(stock => stock.SpellId == spellId)?.Count ?? 0;
        var spell = rules.MagicRecords[spellId];
        int statusAttackMask = spell[38] | spell[39] << 8;
        for (int index = 0; index < statuses.Length; index++)
        {
            int attack = (statusAttackMask & (1 << index)) != 0 ? spell[36] * stock / 100 : 0;
            lines.Add($"{statuses[index].Name}   Atk {attack}%   Def {resistances[statuses[index].Bit] - 100}%");
        }
        return lines;
    }

    private IReadOnlyList<string> TutorialCompatibilityDetails(MenuController menu)
    {
        int row = menu.Character?.NativeId ?? definition.Descriptor.CharacterId;
        ushort[] compatibility = party.Records[row].GuardianForceCompatibility ?? throw new InvalidDataException("Tutorial GF compatibility requires its original sixteen saved values.");
        return menu.Data.GuardianForces.Select(gf => $"{gf.Name}   {compatibility[gf.NativeId!.Value]}").ToArray();
    }

    private IReadOnlyList<string> TutorialDuelDetails()
    {
        NativeLimitProgress limits = catalog.CreateIsolatedLimitProgress(definition.Id);
        return StatusKernel.ZellLimits.Select((move, index) => (Move: move, Index: index)).Where(entry => (limits.DuelFlags & (1 << entry.Index)) != 0).Select(entry => Menu.FormatNativeTutorialText(entry.Move.Name ?? throw new InvalidDataException("Native Duel move has no original name."), names)).ToArray();
    }

    private string OriginalAngeloName(int index)
    {
        if (index < 4)
        {
            string[] passiveNames = ["Rush", "Recover", "Reverse", "Search"];
            return Menu.FormatNativeTutorialText("{Char Angelo1} " + passiveNames[index], names);
        }
        // Saved learning bits4..7 address the kernel's four Angelo battle attacks; the extra kernel row is Angel Wing.
        return Menu.FormatNativeTutorialText(StatusKernel.RinoaAngeloAttacks[index - 4].Name ?? throw new InvalidDataException("Native Angelo move has no original name."), names);
    }

    private IReadOnlyList<string> TutorialAngeloDetails()
    {
        NativeLimitProgress limits = catalog.CreateIsolatedLimitProgress(definition.Id);
        List<string> details = [];
        for (int index = 0; index < 8; index++)
        {
            if ((limits.PetPalsFlags!.Value & (1 << index)) == 0)
            {
                continue;
            }
            bool learned = (limits.AngeloFlags & (1 << index)) != 0;
            bool selected = settings["Angelo learning"] == index.ToString(CultureInfo.InvariantCulture);
            details.Add($"{OriginalAngeloName(index)}   {(learned ? "Complete" : $"Counter {limits.TrainingCounters[index]}")}{(selected ? "   Learning" : "")}");
        }
        return details;
    }

    private UiElement BuildTutorialStatusPresentation(MenuController menu, string section)
    {
        UiGrid root = new() { Columns = 1, Gap = 8 };
        root.RowDefinitions.Add(UiGridLength.Pixel(82));
        root.RowDefinitions.Add(UiGridLength.Pixel(54));
        root.RowDefinitions.Add(UiGridLength.Star());
        root.Children.Add(new UiPanel { GridRow = 0, Caption = "Status", CaptionFontSize = UiTheme.DefaultCaptionFontSize * menu.TextScale, CaptionHeight = 26, Padding = new UiThickness(10), Children = { new UiLabel { Text = $"{menu.Character?.Name}   Lv {menu.Character?.Level}   HP {menu.Character?.CurrentHp}/{menu.Character?.MaximumHp}", FontSize = 26 } } });
        UiList commands = new() { Id = "commands", GridRow = 1, Items = menu.Entries.Select(entry => entry.Label).ToList(), SelectedIndex = menu.SelectedIndex, RowHeight = 42, FontSize = 24, HighlightSelection = false, PointerSize = 20, TextInset = 35, SelectionChanged = index => menu.SelectEntry(menu.ScreenId, menu.Entries[index].Id), Confirmed = index => ConfirmTutorialStatusEntry(menu, index) };
        root.Children.Add(commands);
        var details = menu.CurrentScreen.Details(menu);
        UiGrid body = new() { GridRow = 2, Columns = section == "elements" ? 2 : 1, Gap = 16 };
        if (section == "elements")
        {
            body.Children.Add(StatusPanel("Element Atk / Def", details.Take(8).ToArray(), 0, menu.TextScale));
            body.Children.Add(StatusPanel("Status Atk / Def", details.Skip(8).ToArray(), 1, menu.TextScale));
        }
        else
        {
            string caption = section switch { "gf" => Menu.FormatNativeTutorialText(catalog.StatusLabels[29].Value, names), "duel" => "Duel", "angelo" => "Angelo", _ => section };
            body.Children.Add(StatusPanel(caption, details, 0, menu.TextScale));
        }
        root.Children.Add(body);
        return root;
    }

    private static void ConfirmTutorialStatusEntry(MenuController menu, int index)
    {
        MenuEntry entry = menu.Entries[index];
        if (entry.Action is not null)
        {
            menu.Execute(entry.Action);
        }
        if (entry.Destination is not null)
        {
            menu.Push(entry.Destination);
        }
    }

    private static UiPanel StatusPanel(string caption, IReadOnlyList<string> details, int column, float textScale)
    {
        UiPanel panel = new() { Caption = caption, CaptionFontSize = UiTheme.DefaultCaptionFontSize * textScale, CaptionHeight = 30, Padding = new UiThickness(12), GridColumn = column };
        UiGrid lines = new() { Columns = 1, Gap = 4 };
        foreach (string detail in details)
        {
            int row = lines.Children.Count;
            lines.RowDefinitions.Add(UiGridLength.Pixel(29));
            lines.Children.Add(new UiLabel { GridRow = row, Text = detail, FontSize = 22 });
        }
        lines.RowDefinitions.Add(UiGridLength.Star());
        panel.Children.Add(lines);
        return panel;
    }
}
