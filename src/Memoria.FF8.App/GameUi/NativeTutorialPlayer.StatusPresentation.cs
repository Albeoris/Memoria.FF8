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
        string[] elements = [Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.fire", "Fire"), Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.ice", "Ice"), Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.thunder", "Thunder"), Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.earth", "Earth"), Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.poison", "Poison"), Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.wind", "Wind"), Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.water", "Water"), Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.holy", "Holy")];
        List<string> lines = [];
        for (int element = 0; element < elements.Length; element++)
        {
            int attack = (statistics.ElementAttack & (1 << element)) != 0 ? statistics.ElementAttackPercent : 0;
            int defense = statistics.ElementDefenses[element] - 800;
            lines.Add(Menu.FormatSystemText("tutorial.ui.tutorialelementstatusdetails.atk.def", "{0}   Atk {1}%   Def {2}%", elements[element], attack, (defense > 100 ? Menu.FormatSystemText("tutorial.status.absorb", "Absorb {0}", defense - 100) : defense.ToString(CultureInfo.InvariantCulture))));
        }
        (int Bit, string Name)[] statuses = [(0, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.death", "Death")), (1, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.poison", "Poison")), (2, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.petrify", "Petrify")), (3, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.darkness", "Darkness")), (4, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.silence", "Silence")), (5, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.berserk", "Berserk")), (6, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.zombie", "Zombie")), (8, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.sleep", "Sleep")), (10, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.slow", "Slow")), (11, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.stop", "Stop")), (17, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.curse", "Curse")), (22, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.drain", "Drain")), (23, Menu.SystemText("tutorial.ui.tutorialelementstatusdetails.apathy", "Apathy"))];
        int spellId = party.Records[row].Junction.MagicJunctions[10];
        int stock = spellId == 0 ? 0 : party.Records[row].MagicInventory.FirstOrDefault(stock => stock.SpellId == spellId)?.Count ?? 0;
        var spell = rules.MagicRecords[spellId];
        int statusAttackMask = spell[38] | spell[39] << 8;
        for (int index = 0; index < statuses.Length; index++)
        {
            int attack = (statusAttackMask & (1 << index)) != 0 ? spell[36] * stock / 100 : 0;
            lines.Add(Menu.FormatSystemText("tutorial.ui.tutorialelementstatusdetails.atk.def", "{0}   Atk {1}%   Def {2}%", statuses[index].Name, attack, resistances[statuses[index].Bit] - 100));
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
        return StatusKernel.ZellLimits.Select((move, index) => (Move: move, Index: index)).Where(entry => (limits.DuelFlags & (1 << entry.Index)) != 0).Select(entry => Menu.FormatNativeTutorialText(menuCatalog.KernelText("ZellLimits", "Name", entry.Index, entry.Move.Name ?? throw new InvalidDataException("Native Duel move has no original name.")), names)).ToArray();
    }

    private string OriginalAngeloName(int index)
    {
        if (index < 4)
        {
            string[] passiveNames = [Menu.SystemText("tutorial.ui.originalangeloname.rush", "Rush"), Menu.SystemText("tutorial.ui.originalangeloname.recover", "Recover"), Menu.SystemText("tutorial.ui.originalangeloname.reverse", "Reverse"), Menu.SystemText("tutorial.ui.originalangeloname.search", "Search")];
            return Menu.FormatNativeTutorialText("{Char Angelo1} " + passiveNames[index], names);
        }
        // Saved learning bits4..7 address the kernel's four Angelo battle attacks; the extra kernel row is Angel Wing.
        return Menu.FormatNativeTutorialText(menuCatalog.KernelText("RinoaAngeloAttacks", "Name", index - 4, StatusKernel.RinoaAngeloAttacks[index - 4].Name ?? throw new InvalidDataException("Native Angelo move has no original name.")), names);
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
            details.Add($"{OriginalAngeloName(index)}   {(learned ? Menu.SystemText("tutorial.angelo.complete", "Complete") : Menu.FormatSystemText("tutorial.angelo.counter", "Counter {0}", limits.TrainingCounters[index]))}{(selected ? Menu.SystemText("tutorial.angelo.learning", "   Learning") : "")}");
        }
        return details;
    }

    private UiElement BuildTutorialStatusPresentation(MenuController menu, string section)
    {
        UiGrid root = new() { Columns = 1, Gap = 8 };
        root.RowDefinitions.Add(UiGridLength.Pixel(82));
        root.RowDefinitions.Add(UiGridLength.Pixel(54));
        root.RowDefinitions.Add(UiGridLength.Star());
        root.Children.Add(new UiPanel { GridRow = 0, Caption = Menu.SystemText("tutorial.ui.buildtutorialstatuspresentation.status", "Status"), CaptionFontSize = UiTheme.DefaultCaptionFontSize * menu.TextScale, CaptionHeight = 26, Padding = new UiThickness(10), Children = { new UiLabel { Text = Menu.FormatSystemText("tutorial.ui.buildtutorialstatuspresentation.lv.hp", "{0}   Lv {1}   HP {2}/{3}", menu.Character?.Name, menu.Character?.Level, menu.Character?.CurrentHp, menu.Character?.MaximumHp), FontSize = 26 } } });
        UiList commands = new() { Id = "commands", GridRow = 1, Items = menu.Entries.Select(entry => entry.Label).ToList(), SelectedIndex = menu.SelectedIndex, RowHeight = 42, FontSize = 24, HighlightSelection = false, PointerSize = 20, TextInset = 35, SelectionChanged = index => menu.SelectEntry(menu.ScreenId, menu.Entries[index].Id), Confirmed = index => ConfirmTutorialStatusEntry(menu, index) };
        root.Children.Add(commands);
        var details = menu.CurrentScreen.Details(menu);
        UiGrid body = new() { GridRow = 2, Columns = section == "elements" ? 2 : 1, Gap = 16 };
        if (section == "elements")
        {
            body.Children.Add(StatusPanel(Menu.SystemText("tutorial.ui.buildtutorialstatuspresentation.element.atk.def", "Element Atk / Def"), details.Take(8).ToArray(), 0, menu.TextScale));
            body.Children.Add(StatusPanel(Menu.SystemText("tutorial.ui.buildtutorialstatuspresentation.status.atk.def", "Status Atk / Def"), details.Skip(8).ToArray(), 1, menu.TextScale));
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
