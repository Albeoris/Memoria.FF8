namespace Memoria.FF8.GameUi;

/// <summary>Menu information architecture. Native effects remain owned by the live provider.</summary>
public static class MenuCatalog
{
    public static void Register(MenuController controller)
    {
        void Add(string id, string title, string help, Func<MenuController, IReadOnlyList<MenuEntry>> entries, Func<MenuController, IReadOnlyList<string>>? details = null, MenuLayout layout = MenuLayout.List, bool main = false, int columns = 1)
        {
            controller.Register(new MenuScreenDefinition { Id = id, Title = title, Help = help, Entries = entries, Details = details ?? (c => Boundary(c)), Layout = id.StartsWith("battle-", StringComparison.Ordinal) ? MenuLayout.BattleHud : layout, MainCommand = main, Columns = columns });
        }

        Add("main", "Menu", "Select a command.", c => c.Definitions.Where(s => s.MainCommand).Select(s => new MenuEntry(s.Id, s.Title, s.Help, s.Id)).ToArray(), layout: MenuLayout.Main);
        Add("junction", "Junction", "Junction Guardian Forces, magic, commands and abilities.", _ =>
        [
            Link("junction-options", "Junction", "Junction a GF or magic."),
            Link("junction-off", "Off", "Remove magic or all junctions."),
            Link("junction-auto", "Auto", "Choose an Auto priority."),
            Link("junction-command", "Ability", "Set command and character abilities.")
        ], Stats, MenuLayout.Junction, true);
        Add("junction-options", "Junction", "Choose GF or Magic.", _ => [Link("junction-gf", "GF", "Select a Guardian Force to junction."), Link("junction-magic", "Magic", "Junction stocked magic to a stat."), Link("junction-element", "Elem-Atk/Def", "Elemental attack and defense junction slots."), Link("junction-status", "ST-Atk/Def", "Status attack and defense junction slots.")], Stats, MenuLayout.Junction);
        Add("junction-gf", "Junction · GF", "Choose a GF to junction or remove. Transferring another character's GF requires confirmation.", c => c.Data.GuardianForces.Select(g => new MenuEntry(g.Id, g.Name, c.GuardianForceOwner(g.Id) is { } owner && owner.Id != c.Character?.Id ? $"Transfer {g.Name} from {owner.Name} to {c.Character?.Name}." : $"Junction {g.Name} to {c.Character?.Name}.", Action: new MenuAction("junction-gf", g.Id))).ToArray(), c => ["Character: " + (c.Character?.Name ?? "Unavailable"), "Junctioned GF:", .. c.Data.GuardianForces.Where(g => c.Character?.GuardianForceIds.Contains(g.Id) == true).Select(g => g.Name).Take(10), .. Boundary(c)], MenuLayout.Junction);
        Add("junction-magic", "Junction · Magic", "Choose the stat, then the stocked spell.", c => Slots(c, ["HP", "Str", "Vit", "Mag", "Spr", "Spd", "Eva", "Hit", "Luck"]), Stats, MenuLayout.Junction);
        Add("junction-element", "Junction · Elemental", "Junction stocked magic to unlocked elemental attack and defense slots.", c => Slots(c, ["Elem-Atk", "Elem-Def 1", "Elem-Def 2", "Elem-Def 3", "Elem-Def 4"]), Stats, MenuLayout.Junction);
        Add("junction-status", "Junction · Status", "Junction stocked magic to unlocked status attack and defense slots.", c => Slots(c, ["ST-Atk", "ST-Def 1", "ST-Def 2", "ST-Def 3", "ST-Def 4"]), Stats, MenuLayout.Junction);
        Add("junction-stock", "Junction · Select magic", "Choose stocked magic. A spell can be junctioned to one slot at a time.", c => JunctionStock(c), c => [$"Slot: {c.JunctionSlot}", "Current: " + (c.Character?.Junctions.GetValueOrDefault(c.JunctionSlot) ?? "--")], MenuLayout.Junction);
        Add("junction-off", "Junction · Off", "Remove a junction category.", _ => [Action("magic", "Magic", "Remove stat / elemental / status magic junctions.", "junction-off", value: "Magic"), Action("all", "All", "Remove GF and magic junctions.", "junction-off", value: "All"), Action("party-magic", MagicUiText.Get("PartyOffLabel"), MagicUiText.Get("PartyOffHelp"), "junction-magic-off-all")], Stats, MenuLayout.Junction);
        Add("junction-auto", "Junction · Auto", "Choose an optimization priority.", c => c.AutoJunctionEntries(), Stats, MenuLayout.Junction);
        Add("junction-command", "Junction · Command", "Choose a configurable command slot.", c => c.CommandSlotEntries(), c => [MagicUiText.Get("AttackSlotHelp"), .. CharacterDetails(c)], MenuLayout.Junction);
        Add("junction-command-choice", "Junction · Select Command", "Choose a learned command from the junctioned GFs.", c => CommandChoices(c), c => [$"Selected command slot: {c.CommandSlot + 1}", .. CharacterDetails(c)], MenuLayout.Junction);
        Add("junction-ability", "Junction · Ability", "Choose a passive ability slot.", c => Enumerable.Range(0, c.IsDemo ? 3 : c.Character?.AbilitySlots ?? 2).Select(slot => new MenuEntry(slot.ToString(), c.Character?.Abilities.ElementAtOrDefault(slot) ?? "--", "Select a learned ability for this slot.", "junction-ability-choice", new MenuAction("select-ability-slot", slot.ToString()))).ToArray(), c => ["Assigned abilities:", .. (c.Character?.Abilities ?? []).Take(4)], MenuLayout.Junction);
        Add("junction-ability-choice", "Junction · Select Ability", "Choose a learned passive ability from the junctioned GFs.", c => PassiveChoices(c), c => [$"Selected ability slot: {c.AbilitySlot + 1}"], MenuLayout.Junction);

        Add("item", "Item", "Use and arrange inventory.", _ => [Link("item-use", "Use", "Choose an item and character target."), Link("item-rearrange", "Rearrange", "Exchange two inventory positions."), Action("sort", "Sort", "Sort the explicit fixture inventory by name.", "item-sort")], InventoryDetails, main: true);
        Add("item-use", "Item · Use", "Select an item.", c => c.Data.Items.Select(i => new MenuEntry(i.Id, $"{i.Name}   {i.Quantity}", i.NativeMagazinePages.Length > 0 ? i.NativeMagazinePageUnavailableReasons.GetValueOrDefault(i.NativeMagazinePages[0].PageId) ?? i.Description : i.Description, i.NativeFieldBattleUse ? null : i.NativeMagazinePages.Length > 0 ? "magazine" : "item-target", new MenuAction(i.NativeFieldBattleUse ? "item-battle" : "select-item", i.Id), c.IsDemo || (i.NativeMagazinePages.Length > 0 ? !i.NativeMagazinePageUnavailableReasons.ContainsKey(i.NativeMagazinePages[0].PageId) : i.NativeFieldUseSupported))).ToArray(), InventoryDetails);
        Add("item-target", "Item · Target", "Select the target character.", c => Targets(c, "item-use"), c => [$"Item: {c.Item?.Name ?? "Unavailable"}", $"Quantity: {c.Item?.Quantity.ToString() ?? "?"}", c.Item?.Description ?? "No item projection.", .. CharacterDetails(c)]);
        Add("item-forget", "Item · Forget Ability", "Choose the selected GF's ability to forget.", c => c.GuardianForce?.LearningChoices.Select(choice => Action(choice.Id, choice.Name, "Use one Amnesia Greens to forget this ability.", "item-use", target: $"item-target-{c.GuardianForce.NativeId + 16}", value: choice.Id)).ToArray() ?? [], c => [$"Item: {c.Item?.Name}", $"GF: {c.GuardianForce?.Name}"]);
        Add("item-rearrange", "Item · Rearrange", "Choose the item to move.", c => c.Data.Items.Select(i => new MenuEntry(i.Id, $"{i.Name}   {i.Quantity}", "Choose its destination position next.", "item-position", new MenuAction("select-item", i.Id))).ToArray(), InventoryDetails);
        Add("item-position", "Item · Destination", "Choose the inventory position to exchange.", c => c.Data.Items.Select(i => Action(i.Id, i.Name, "Exchange the fixture item positions.", "item-move", target: i.Id)).ToArray(), c => [$"Moving: {c.Item?.Name}", .. Boundary(c)]);

        Add("magic", "Magic", "Use, rearrange and exchange stocked magic.", c => [Link("magic-use", "Use", "Choose field magic and a target."), Link("magic-stock", "Stock", "Browse the selected character's magic stock."), new MenuEntry("magic-exchange", "Exchange", "Transfer stock to another character.", "magic-exchange", Enabled: !c.Data.SharedMagicStock), Action("sort", "Rearrange", "Sort explicit fixture magic stocks by name.", "magic-sort")], MagicDetails, MenuLayout.Character, true);
        Add("magic-use", "Magic · Use", "Select a field spell.", c => MagicEntries(c, "magic-target"), MagicDetails, MenuLayout.Character);
        Add("magic-stock", "Magic · Stock", "Stock list and descriptions.", c => MagicEntries(c, "magic-detail"), MagicDetails, MenuLayout.Character);
        Add("magic-detail", "Magic · Details", "Inspect selected magic stock.", _ => [], c => [$"Spell: {c.Magic?.Name ?? "Unavailable"}", $"Stock: {c.Magic?.Quantity.ToString() ?? "?"}", c.Magic?.Description ?? "No stock owner projection.", .. Boundary(c)], MenuLayout.Character);
        Add("magic-target", "Magic · Target", "Choose a character to receive the spell.", c => Targets(c, "magic-use"), c => [$"Spell: {c.Magic?.Name ?? "Unavailable"}", .. CharacterDetails(c)], MenuLayout.Character);
        Add("magic-exchange", "Magic · Exchange", "Choose the stock to transfer.", c => MagicEntries(c, "magic-exchange-target"), MagicDetails, MenuLayout.Character);
        Add("magic-exchange-target", "Magic · Transfer Target", "Choose the recipient, then transfer quantity.", c => c.Data.Characters.Select(ch => new MenuEntry(ch.Id, $"{ch.Name}   HP {MenuController.Hp(ch)}", ch.Id == c.Character?.Id ? "Choose another character." : "Choose the recipient's transfer quantity next.", "magic-exchange-quantity", new MenuAction("select-exchange", ch.Id), ch.Id != c.Character?.Id)).ToArray(), MagicDetails, MenuLayout.Character);
        Add("magic-exchange-quantity", "Magic · Transfer Quantity", "Choose quantity; stock is conserved and capped at 100.", _ => new[] { "1", "10", "100", "All" }.Select(amount => Action(amount, amount == "All" ? "All available" : "Transfer " + amount, "Transfer up to the available source stock and recipient capacity.", "magic-transfer", value: amount)).ToArray(), c => [$"Spell: {c.Magic?.Name ?? "Unavailable"}", $"Source stock: {c.Magic?.Quantity.ToString() ?? "?"}", "Quantity choices: 1 / 10 / 100 / All", "A character can hold 32 different spells, up to 100 of each.", .. Boundary(c)], MenuLayout.Character);

        Add("status", "Status", "Character status pages and Limit Breaks.", _ => [Link("status-stats", "Stats", "View stats and equipped junctions."), Link("status-info", "Information", "Weapon, experience and conditions."), Link("status-limit", "Limit Break", "Limit Break overview. S opens this page.")], Stats, MenuLayout.Character, true);
        Add("status-stats", "Status · Stats", "Browse current projected character stats.", _ => [], Stats, MenuLayout.Character);
        Add("status-info", "Status · Information", "Character experience and equipment.", _ => [], CharacterDetails, MenuLayout.Character);
        Add("status-limit", "Status · Limit Break", "Browse Limit Break information; setup requires native contracts.", c => c.Character?.LimitBreak switch
        {
            "Renzokuken" => [Link("status-renzokuken", "Renzokuken", "Gunblade trigger and finishing move options.")],
            "Combine" => [Link("status-angelo", "Angelo", "Angelo learned moves and progress."), Link("status-angel-wing", "Angel Wing", "Angel Wing details.")],
            "Duel" => [Link("status-duel", "Duel", "Duel input sequence reference.")],
            "Slot" => [Link("status-slot", "Slot", "Slot limit overview.")],
            "Blue Magic" => [Link("status-blue-magic", "Blue Magic", "Learned Blue Magic overview.")],
            "Shot" => [Link("status-shot", "Shot", "Ammo and shooting overview.")],
            _ => []
        }, c => [$"Limit: {c.Character?.LimitBreak ?? "Unavailable"}", "Learned moves / limit conditions require native data.", .. Boundary(c)], MenuLayout.Character);
        Add("status-renzokuken", "Limit · Renzokuken", "Auto / Indicator setup and finishing moves.", c =>
        {
            var owner = LimitCharacter(c, "Renzokuken");
            return [.. new[] { "Auto", "Indicator" }.Select(setting => Action(setting, $"{setting}   {owner?.LimitSettings.GetValueOrDefault(setting, "Unavailable") ?? "Unavailable"}", "Toggle the isolated fixture option.", "limit-setting", setting, owner?.Id ?? "", owner?.LimitSettings.GetValueOrDefault(setting) == "On" ? "Off" : "On")), .. LimitEntries(c, "Renzokuken")];
        }, c => LimitDetails(c, "Renzokuken"), MenuLayout.Character);
        Add("status-angelo", "Limit · Angelo", "Inspect learned trick progress; choose a fixture learning target.", c => LimitEntries(c, "Combine", learn: true), c => LimitDetails(c, "Combine"), MenuLayout.Character);
        Add("status-angel-wing", "Limit · Angel Wing", "Inspect Angel Wing's separate state.", c => [Action("wing", "Angel Wing preview", "Toggle the explicit fixture mode; no battle state changes.", "limit-setting", "Angel Wing", LimitCharacter(c, "Combine")?.Id ?? "", LimitCharacter(c, "Combine")?.LimitSettings.GetValueOrDefault("Angel Wing") == "On" ? "Off" : "On")], c => [$"Fixture mode: {LimitCharacter(c, "Combine")?.LimitSettings.GetValueOrDefault("Angel Wing", c.IsDemo ? "Off" : "Unavailable") ?? "Unavailable"}", "Native story availability and battle behavior unsupported.", .. Boundary(c)], MenuLayout.Character);
        Add("status-duel", "Limit · Duel", "Inspect learned moves and their explicit fixture input sequences.", c => LimitEntries(c, "Duel"), c => LimitDetails(c, "Duel"), MenuLayout.Character);
        Add("status-slot", "Limit · Slot", "Inspect a spell preview, Select or Do Over.", c => [Action("select", "Select", "Select only the fixture preview; no spell is cast.", "slot-select"), Action("do-over", "Do Over", "Advance the deterministic fixture preview.", "slot-do-over", target: LimitCharacter(c, "Slot")?.Id ?? "")], SlotDetails, MenuLayout.Character);
        Add("status-blue-magic", "Limit · Blue Magic", "Inspect the explicit learned-skill list.", c => LimitEntries(c, "Blue Magic"), c => LimitDetails(c, "Blue Magic"), MenuLayout.Character);
        Add("status-shot", "Limit · Shot", "Inspect ammo stocks and shot controls.", c => LimitEntries(c, "Shot"), c => LimitDetails(c, "Shot"), MenuLayout.Character);

        Add("gf", "GF", "Choose a Guardian Force.", c => c.Data.GuardianForces.Select(g => new MenuEntry(g.Id, g.Name, $"Inspect {g.Name}.", "gf-detail", new MenuAction("select-gf", g.Id))).ToArray(), SelectedGfDetails, MenuLayout.GuardianForceGrid, true, 8);
        Add("gf-detail", "GF · Details", "GF stats, junction owner and learned abilities.", _ => [Link("gf-learn", "Learn", "Choose the GF ability learning target."), Link("gf-abilities", "Abilities", "Browse ability catalogue.")], GfDetails);
        Add("gf-learn", "GF · Learn", "Choose an available unlearned ability. AP progress is retained when changing targets.", c => (c.GuardianForce?.LearningChoices ?? []).Select(a => new MenuEntry(a.Id, a.Name, a.Description, Action: new MenuAction("gf-learn", a.Id, Value: c.IsDemo ? a.Name : ""), Enabled: !a.Learned)).ToArray(), GfDetails);
        Add("gf-abilities", "GF · Abilities", "Learned and available abilities.", c => (c.GuardianForce?.LearningChoices ?? []).Select(a => new MenuEntry(a.Id, a.Name, a.Description)).ToArray(), GfDetails);

        Add("ability", "Ability", "Refine items, magic and cards with learned menu abilities.", c => [.. c.Data.Recipes.Select(r => r.Ability).Distinct().Select(a => new MenuEntry(a, a, "Browse learned refinement recipes.", "ability-recipes", new MenuAction("select-recipe", c.Data.Recipes.First(r => r.Ability == a).Id))), .. (c.IsDemo ? new[] { Link("ability-card-mod", "Card Mod", "Select a card and inspect its refinement result.") } : [])], c => ["Choose a refinement ability.", "Select a recipe before choosing quantity."], main: true);
        Add("ability-recipes", "Ability · Recipes", "Select a recipe.", c => c.Data.Recipes.Where(r => r.Ability == c.Recipe?.Ability).Select(r => new MenuEntry(r.Id, $"{r.IngredientQuantity} {r.IngredientName ?? r.IngredientId} → {r.ResultQuantity} {r.ResultName}", "Inspect the recipe before executing.", "ability-refine", new MenuAction("select-recipe", r.Id))).ToArray(), RecipeDetails);
        Add("ability-refine", "Ability · Refine Quantity", "Choose recipe batches for the selected character stock.", c => c.IsDemo ? new[] { "1", "5", "10", "All" }.Select(amount => Action(amount, amount == "All" ? "All possible" : "Refine ×" + amount, "The fixture clamps batches to ingredient stock and output capacity.", "refine", value: amount)).ToArray() : NativeRefinementMenuProjection.Quantities(c), RecipeDetails, MenuLayout.Character);
        Add("ability-card-mod", "Ability · Card Mod", "Select a card to inspect its refinement result.", c => c.Data.Cards.Select(card => new MenuEntry(card.Id, $"{card.Name} ×{card.Quantity}", "Inspect the card's explicitly supplied recipe.", "ability-card-result", new MenuAction("select-card", card.Id))).ToArray(), c => CardDetails(c.Card));
        Add("ability-card-result", "Ability · Card Mod Result", "Confirm the card conversion.", c => [Action("refine", "Refine", "Convert one recipe batch into items.", "card-mod", c.Card?.Id ?? "")], c => [$"Card: {c.Card?.Name ?? "Unavailable"} ×{c.Card?.Quantity.ToString() ?? "?"}", $"Required: {c.Card?.RefinementRequiredCards?.ToString() ?? "1"}", $"Result: {c.Card?.RefinementQuantity.ToString() ?? "?"} × {c.Card?.RefinementItemName ?? c.Data.Items.FirstOrDefault(i => i.Id == c.Card?.RefinementItemId)?.Name ?? "Unavailable"}", c.IsDemo ? "Synthetic demonstration recipe." : "Original Card Mod recipe."]);

        Add("switch", "Switch", "Change party members or exchange junction packages.", c => [new MenuEntry("switch-party", "Party", c.IsDemo || c.Data.SupportedActions.Contains("party-switch") ? "Choose source and target party members." : "Party changes are unavailable in this menu.", "switch-party", Enabled: c.IsDemo || c.Data.SupportedActions.Contains("party-switch")), Link("switch-junction", "Junction Exchange", "Exchange GF, magic, commands and abilities.")], CharacterDetails, main: true);
        Add("switch-party", "Switch · Party", "Select the source character.", c => c.Data.Characters.Select(ch => new MenuEntry(ch.Id, $"{ch.Name} {(ch.InParty ? "[Party]" : "[Reserve]")}", "Select the other member next.", "switch-party-target", new MenuAction("select-exchange", ch.Id))).ToArray(), CharacterDetails);
        Add("switch-party-target", "Switch · Target", "Choose the other party / reserve member.", c => Targets(c, "party-switch"), CharacterDetails);
        Add("switch-junction", "Switch · Junction Exchange", "Choose the source junction package.", c => c.Data.Characters.Select(ch => new MenuEntry(ch.Id, ch.Name, "Choose the destination character next.", "switch-junction-target", new MenuAction("select-exchange", ch.Id))).ToArray(), CharacterDetails);
        Add("switch-junction-target", "Switch · Junction Target", "Choose the destination for the exchange.", c => Targets(c, "junction-exchange"), CharacterDetails);

        Add("card", "Card", "Browse Triple Triad collection.", c => c.Data.Cards.Select(card => new MenuEntry(card.Id, $"{card.Name} x{card.Quantity}", "Inspect card values and ownership.", "card-detail", new MenuAction("select-card", card.Id))).ToArray(), SelectedCardDetails, MenuLayout.CardGrid, true, 1);
        Add("card-detail", "Card · Details", "Card rank, element and collection metadata.", _ => [], c => CardDetails(c.Card));

        Add("config", "Config", "Choose a settings category.", c => c.ConfigEntries(), main: true);
        foreach (var category in new[] { ("gameplay", "Gameplay"), ("display", "Display"), ("graphics", "Graphics"), ("audio", "Audio"), ("interface", "Interface"), ("controls", "Controls") })
        {
            Add("config-" + category.Item1, category.Item2, "Select a setting.", c => c.ConfigEntries());
        }
        Add("new-game-options", "New Game", "Choose your ATB mode and whether to show rare Triple Triad card owners. More settings are available in Config.", c => c.ConfigEntries());
        controller.RegisterControlsScreens();

        Add("tutorial", "Tutorial", "Reference topics and SeeD test entry.", _ => [Link("tutorial-junction", "Junction", "GF, magic and ability junction concepts."), Link("tutorial-battle", "Battle", "Battle command concepts."), Link("tutorial-gf", "GF", "GF learning and junction concepts."), Link("tutorial-card", "Card", "Triple Triad concepts."), Link("tutorial-menu", "Menu", "Menu control reference."), Link("tutorial-seed", "SeeD test", "Original test bank is unsupported; isolated demo questions available.")], _ => ["Concept reference pages are rewritten summaries.", "These pages do not reproduce original tutorial scripts."], main: true);
        Add("tutorial-junction", "Tutorial · Junction", "Junction concept reference.", _ => [], _ => ["Junction a GF to unlock supported junction slots.", "Stocked magic can be assigned to enabled stats.", "The spell and stock affect the original stat bonus.", "Commands and passive abilities use separate slots.", "This UI does not guess native stat formulas."]);
        Add("tutorial-battle", "Tutorial · Battle", "Battle concept reference.", _ => [], _ => ["Attack uses the equipped weapon.", "Magic casts stocked spells.", "Draw obtains available spells from a target.", "GF summons the junctioned Guardian Force.", "Item uses an available battle item.", "Limit Break availability depends on battle state."]);
        Add("tutorial-gf", "Tutorial · GF", "Guardian Force concept reference.", _ => [], _ => ["Guardian Forces earn EXP and AP.", "Choose an ability learning target in Learn.", "Learned abilities enable commands and junctions.", "Learn shows available abilities and their saved AP progress."]);
        Add("tutorial-card", "Tutorial · Card", "Triple Triad concept reference.", _ => [], _ => ["Cards carry values on four sides; A means 10.", "Place a card next to a lower opposing side to capture it.", "Same and Plus can trigger chain captures.", "Elemental changes normal side comparisons by +1 or -1.", "Random chooses your hand. Sudden Death replays a draw with the captured cards.", "One, Diff, Direct and All determine the cards exchanged."]);
        Add("tutorial-menu", "Tutorial · Menu", "Menu reference.", _ => [], _ => ["The menu contains character status, junctions, items and magic.", "Character pages show the selected party member.", "Status includes Limit Break information."]);
        Add("tutorial-seed", "Tutorial · SeeD Test", "Start the isolated test fixture.", _ => [new MenuEntry("test", "Start fixture test", "Ten synthetic questions; no native rank mutation.", "seed-test", new MenuAction("test-reset"))], c => [$"Projected SeeD rank: {MenuController.Number(c.Data.SeeDRank)}", "Original numbered tests and eligibility are unsupported.", "The debug fixture tests navigation and answer recording."]);
        Add("seed-test", "SeeD Test · Fixture", "Synthetic navigation exercise; not the original question bank.", _ => [Action("yes", "Yes", "Record a fixture Yes answer.", "test-answer", value: "Yes"), Action("no", "No", "Record a fixture No answer.", "test-answer", value: "No"), Action("restart", "Restart", "Reset this synthetic test.", "test-reset")], c => [c.TestQuestion >= 10 ? "Fixture completed" : $"Question {c.TestQuestion + 1} / 10", c.TestQuestion >= 10 ? $"Score: {c.TestCorrect}/10" : c.TestQuestion % 2 == 0 ? "Fixture question: does Confirm select an entry?" : "Fixture question: does Cancel increase your gil?", "This is not an original FF8 SeeD test.", "No game rank or salary changes."]);

        Add("save", "Save", "Select a slot, then confirm.", c => c.IsDemo ? c.Data.SaveSlots.Select(s => new MenuEntry(s.Id, $"{s.Name}   {s.Summary}", "Review this slot before saving.", "save-confirm", new MenuAction("select-save-slot", s.Id))).ToArray() : [new MenuEntry("live-save", "Open save slots", c.Data.SaveAvailable ? "Open the authentic host save flow." : "Saving is not currently available.", Action: new MenuAction("save"), Enabled: c.Data.SaveAvailable)], c => [$"Slot: {c.SaveSlot?.Name ?? "Unavailable"}", c.SaveSlot?.Summary ?? "Select a slot."], main: true);
        Add("save-confirm", "Save · Confirm", "Save to the selected slot?", c => [Action("yes", "Yes", "Confirm the selected save slot.", "save", c.SaveSlot?.Id ?? ""), Action("no", "No", "Return to slot selection.", "menu-back")], c => [$"Slot: {c.SaveSlot?.Name ?? "Unavailable"}", c.SaveSlot?.Summary ?? "Unavailable", "Confirm Yes to save, or No to return."]);
        Add("load", "Load", "Select a slot, then confirm.", c => c.Data.SaveSlots.Select(s => new MenuEntry(s.Id, $"{s.Name}   {s.Summary}", "Review the selected slot.", "load-confirm", new MenuAction("select-save-slot", s.Id))).ToArray(), c => [$"Slot: {c.SaveSlot?.Name ?? "Unavailable"}", c.SaveSlot?.Summary ?? "Slot metadata unavailable."]);
        Add("load-confirm", "Load · Confirm", "Load the selected slot?", c => [Action("yes", "Yes", "Confirm the selected load slot.", "load", c.SaveSlot?.Id ?? ""), Action("no", "No", "Return to slot selection.", "menu-back")], c => [$"Slot: {c.SaveSlot?.Name ?? "Unavailable"}", c.SaveSlot?.Summary ?? "Unavailable", c.IsDemo ? "Fixture confirmation only; no game save is loaded." : "Loading requires the authentic host save owner."]);
        Add("shop", "Shop", "Reusable buy / sell menu; shop inventory must be supplied by its owner.", c => c.IsDemo ? [Link("shop-buy", "Buy", "Inspect items and prices."), Link("shop-sell", "Sell", "Sell an inventory item."), Link("shop-junk", "Junk shop", "Weapon remodeling boundary.")] : [Link("shop-buy", "Buy", "Inspect items and prices."), Link("shop-sell", "Sell", "Sell an inventory item.")], InventoryDetails);
        Add("shop-buy", "Shop · Buy", "Choose an item to purchase.", c => c.Data.Items.Where(i => i.Price.HasValue).Select(i => new MenuEntry(i.Id, $"{i.Name}   {i.Price} gil", $"Buy one {i.Name}.", Action: new MenuAction("shop-buy", i.Id, Value: "1"), Enabled: c.Data.Currency >= i.Price && i.Quantity < 100)).ToArray(), InventoryDetails);
        Add("shop-sell", "Shop · Sell", "Select an item to sell.", c => c.Data.Items.Where(i => (i.SalePrice ?? i.Price / 2).HasValue && i.Quantity > 0).Select(i => new MenuEntry(i.Id, $"{i.Name} x{i.Quantity}   {i.SalePrice ?? i.Price / 2} gil", $"Sell one {i.Name}.", Action: new MenuAction("shop-sell", i.Id, Value: "1"))).ToArray(), InventoryDetails);
        Add("shop-junk", "Shop · Junk Shop", "Select a character or remodel a weapon.", c => c.Data.Characters.Select(character => new MenuEntry(character.Id, character.Name + (character.InParty ? " · Selected" : ""), "Inspect this character's recipes.", Action: new MenuAction("weapon-character", character.Id))).Concat(c.Data.Items.Select(item => new MenuEntry(item.Id, $"{item.Name}   {item.Price} gil", item.Description, Action: new MenuAction("weapon-remodel", item.Id), Enabled: c.Data.SupportedActions.Contains($"weapon-remodel:{item.Id}")))).ToArray(), c => ["Select a character to inspect recipes, or a weapon to remodel."]);
        Add("battle-command", "Battle · Commands", "Choose a battle command.", c => (c.Character?.Commands ?? []).Select(command => new MenuEntry(command, command, "Choose the command's spell, item, GF or target.", command switch { "Magic" => "battle-magic", "Item" => "battle-item", "GF" => "battle-gf", _ => "battle-target" }, new MenuAction("select-battle-command", command))).ToArray(), CharacterDetails);
        Add("battle-magic", "Battle · Magic", "Reusable magic stock list; battle resolution unsupported.", c => MagicEntries(c, "battle-target"), MagicDetails, MenuLayout.Character);
        Add("battle-item", "Battle · Item", "Reusable item stock list; battle resolution unsupported.", c => c.Data.Items.Select(i => new MenuEntry(i.Id, $"{i.Name} x{i.Quantity}", "Inspect a projected battle item.", "battle-target", new MenuAction("select-item", i.Id))).ToArray(), InventoryDetails);
        Add("battle-gf", "Battle · GF", "Choose a Guardian Force.", c => c.Data.GuardianForces.Where(g => c.Character?.GuardianForceIds.Contains(g.Id) == true).Select(g => new MenuEntry(g.Id, g.Name, "Choose a target for the GF preview.", "battle-target", new MenuAction("select-battle-gf", g.Id))).ToArray(), GfDetails);
        Add("battle-draw", "Battle · Draw", "Choose a spell from the selected target's draw list.", c => (c.BattleTarget?.DrawSpells ?? []).Select(m => new MenuEntry(m.Id, m.Name, "Choose Stock or Cast.", "battle-draw-action", new MenuAction("select-magic", m.Id))).ToArray(), c => [$"Target: {c.BattleTarget?.Name ?? "Unavailable"}", "Select a spell, then choose Stock or Cast."]);
        Add("battle-draw-action", "Battle · Draw Action", "Choose Stock or Cast.", _ => [Action("stock", "Stock", "Add the spell to stock in the explicit fixture.", "battle-draw", value: "Stock"), Action("cast", "Cast", "Preview drawing and casting this spell.", "battle-draw", value: "Cast")], c => [$"Spell: {c.Magic?.Name ?? "Unavailable"}", $"Stock: {c.Magic?.Quantity.ToString() ?? "?"}", "Stock adds three spells in the fixture.", "Cast previews an action without combat effects."]);
        Add("battle-target", "Battle · Target", "Select a target.", c => c.BattleCommand == "Draw" ? c.Data.BattleTargets.Select(t => new MenuEntry(t.Id, t.Name, "Inspect this target's draw spells.", "battle-draw", new MenuAction("select-battle-target", t.Id))).ToArray() : [.. c.Data.BattleTargets.Select(t => Action(t.Id, $"{t.Name}   HP {MenuController.Number(t.CurrentHp)} / {MenuController.Number(t.MaximumHp)}", "Preview this combat target.", "battle-preview", target: t.Id, value: c.BattleCommand)), .. c.Data.Characters.Where(ch => ch.InParty).Select(ch => Action(ch.Id, $"{ch.Name}   HP {MenuController.Hp(ch)}", "Preview this party target.", "battle-preview", target: ch.Id, value: c.BattleCommand))], c => [$"Command: {c.BattleCommand}", c.IsDemo ? "Choose a target for the fixture action preview." : "Native combat target execution is unavailable."]);
        Add("battle-victory", "Battle · Results", "Review battle rewards.", _ => [Link("battle-victory-growth", "EXP / AP", "Review character EXP and GF AP rewards."), Link("battle-victory-items", "Items / Gil", "Review recovered items and gil.")], c => [$"EXP recipients: {c.Data.BattleRewards?.CharacterExperience.Count.ToString() ?? "Unavailable"}", $"AP: {MenuController.Number(c.Data.BattleRewards?.AbilityPoints)}", $"Gil: {MenuController.Number(c.Data.BattleRewards?.Currency)}", c.IsDemo ? "Fixture reward display; rewards are not applied." : "Battle result owner is unavailable."]);
        Add("battle-victory-growth", "Battle · EXP / AP", "Character experience and GF ability-point rewards.", c => c.Data.BattleRewards == null ? [] : [.. c.Data.BattleRewards.CharacterExperience.Select(pair => new MenuEntry(pair.Key, $"{c.Data.Characters.FirstOrDefault(ch => ch.Id == pair.Key)?.Name ?? pair.Key}   EXP +{pair.Value}", "Experience reward preview.")), .. c.Data.GuardianForces.Where(g => c.Data.Characters.Any(ch => ch.InParty && ch.GuardianForceIds.Contains(g.Id))).Select(g => new MenuEntry(g.Id, $"{g.Name}   AP +{c.Data.BattleRewards.AbilityPoints}", "GF ability-point reward preview."))], c => ["Review character EXP and junctioned GF AP.", c.IsDemo ? "Explicit fixture values; no progression is changed." : "Native reward application is unsupported."]);
        Add("battle-victory-items", "Battle · Items / Gil", "Recovered item and gil list.", c => c.Data.BattleRewards?.Items.Select(i => new MenuEntry(i.Id, $"{i.Name} ×{i.Quantity}", "Recovered item reward preview.")).ToArray() ?? [], c => [$"Gil: {MenuController.Number(c.Data.BattleRewards?.Currency)}", c.IsDemo ? "Fixture result display; inventory is unchanged." : "Native reward application is unsupported."]);

        Add("debug-battle", "Debug Room · Battle", "Battle settings apply immediately to the running game.", c => c.BattleSettingsEntries(), c => [c.SelectedEntry?.Help ?? "Battle settings", "Changes apply immediately and save automatically.", "Outcome shortcuts require an active battle."]);
        Add("debug-ui", "Debug Room · UI", "Interface presentation preferences save automatically.", c => c.HostSettingsEntries("Text scale", "UI font", "UI font size", "Dialogue font", "Dialogue font size", "Rare card markers", "Show FPS"), c => [c.SelectedEntry?.Help ?? "Interface settings", "Left / Right: adjust", "Changes save automatically."]);
        Add("debug-audio", "Debug Room · Audio", "Audio output preferences save automatically.", c => c.HostSettingsEntries("Music volume", "Effect volume"), c => [c.SelectedEntry?.Help ?? "Audio settings", "Left / Right: adjust", "Changes save automatically."]);
        Add("debug-controls", "Debug Room · Controls", "Edit persisted host controls.", _ => [Link("config-keyboard", "Keyboard", "Edit keyboard bindings."), Link("config-controller", "Controller", "Edit controller bindings.")], _ => ["Local control mappings save automatically.", "Select a device to edit its bindings."]);
        Add("debug-gallery", "Debug Room · Menu Gallery", "Inspect isolated registered menu fixtures.", c => [Action("reset", "Reset all fixture data", "Reset only this isolated menu fixture.", "demo-reset"), .. c.Definitions.Where(s => !s.Id.StartsWith("debug-", StringComparison.Ordinal) && s.Id != "name-input").Select(s => new MenuEntry(s.Id, s.Title, $"Preview {s.Title}. {s.Help}", s.Id, PreviewCharacter(c, s.Id)))], c => [$"Registered screens: {c.Definitions.Count}", "Menu fixture writes stay in memory.", "Cancel returns to Debug Room."], MenuLayout.Gallery);
        Add("debug-autoplay", "Automatic play", "Run automatic play and inspect saved reproductions.", c => c.AutoPlayEntries(), c => c.AutoPlayDetails());
        Add("debug-room", "Debug Room", "Choose a settings section or the isolated menu gallery.", c => [new MenuEntry("battle-laboratory", "Battle laboratory", "Configure isolated battle scenarios and manage explicit presets.", Action: new MenuAction("open-battle-debug"), Enabled: c.BattleDebugAvailable), new MenuEntry("scene-inspector", "Scene inspector", "Inspect the loaded field and temporarily toggle its layers, objects and event scripts.", Action: new MenuAction("open-scene-inspector"), Enabled: c.SceneInspectorAvailable), Link("debug-battle", "Battle", "Apply combat, encounter and ATB settings to the current game; force an active battle outcome."), Link("debug-ui", "UI", "Interface size and presentation preferences."), Link("debug-audio", "Audio", "Music and effect volumes."), Link("debug-controls", "Controls", "Persisted keyboard and controller mappings."), new MenuEntry("debug-names", "Names", "Rename characters and Guardian Forces in the current game. Available in a field or on the world map.", "debug-names", Enabled: c.NamingAvailable), Link("debug-gallery", "Menu gallery", "Inspect and reset isolated menu fixtures."), new MenuEntry("debug-autoplay", "Automatic play", "Start, pause or stop automatic play and inspect its saved reproduction.", "debug-autoplay", Enabled: c.AutoPlayAvailable)], _ => ["Battle · UI · Audio · Controls", "Settings save automatically.", "Names are kept in the game save.", "Battle presets use explicit Save / Load.", "Menu gallery contains isolated fixtures."]);
    }

    private static MenuEntry Link(string destination, string label, string help) => new(destination, label, help, destination);
    private static MenuEntry Action(string id, string label, string help, string kind, string subject = "", string target = "", string value = "") => new(id, label, help, Action: new MenuAction(kind, subject, target, value));
    private static IReadOnlyList<string> Boundary(MenuController c) => c.ScreenId == "debug-room" ? [c.Data.SourceDescription] : [];
    private static IEnumerable<MenuMagic> CharacterMagic(MenuController c) => c.Data.Magic.Where(m => m.CharacterId == c.Character?.Id);
    private static IReadOnlyList<MenuEntry> MagicEntries(MenuController c, string destination) => CharacterMagic(c).Select(m => new MenuEntry(m.Id, m.Name, m.Description, destination, new MenuAction("select-magic", m.Id), m.Quantity > 0 && (destination != "magic-target" || c.IsDemo || c.Data.SupportedActions.Contains("magic-use") || c.Data.SupportedActions.Contains($"magic-use:{m.Id}")))).ToArray();
    private static IReadOnlyList<MenuEntry> CommandChoices(MenuController c)
    {
        if (c.IsDemo)
        {
            return (c.Character?.AvailableCommands ?? []).Select(choice => Action(choice.Id, choice.Name, choice.Description, "junction-command", subject: choice.Id, value: choice.Name)).ToArray();
        }
        return new[] { new MenuEntry("0", "Empty", "Remove the command from this slot.", Action: new MenuAction("junction-command", "0")) }.Concat((c.Character?.AvailableCommands ?? []).Select(choice => new MenuEntry(choice.Id, choice.Name, choice.Description, Action: new MenuAction("junction-command", choice.Id)))).ToArray();
    }

    private static IReadOnlyList<MenuEntry> PassiveChoices(MenuController c)
    {
        if (c.IsDemo)
        {
            return (c.Character?.AvailableAbilities ?? []).Select(choice => Action(choice.Id, choice.Name, choice.Description, "junction-ability", subject: choice.Id, value: choice.Name)).ToArray();
        }
        return new[] { new MenuEntry("0", "Empty", "Remove the ability from this slot.", Action: new MenuAction("junction-ability", "0")) }.Concat((c.Character?.AvailableAbilities ?? []).Select(choice => new MenuEntry(choice.Id, choice.Name, choice.Description, Action: new MenuAction("junction-ability", choice.Id)))).ToArray();
    }

    private static IReadOnlyList<MenuEntry> JunctionStock(MenuController c)
    {
        bool available = c.IsDemo || c.Character?.AvailableJunctionSlots.Contains(c.JunctionSlot) == true;
        MenuEntry[] spells = CharacterMagic(c).Where(magic => magic.Quantity > 0).Select(magic => new MenuEntry(magic.Id, magic.Name, $"Junction {magic.Name} to {c.JunctionSlot}. {magic.AccessPresentation}", Action: new MenuAction(magic.JunctionAvailable ? "junction-magic" : "junction-magic-reassign", magic.Id, Value: magic.Name), Enabled: available)).ToArray();
        return c.IsDemo ? spells : [new MenuEntry("0", "Empty", "Remove magic from this slot.", Action: new MenuAction("junction-magic", "0"), Enabled: available), .. spells];
    }

    private static IReadOnlyList<MenuEntry> Slots(MenuController c, string[] slots) => slots.Select(slot => new MenuEntry(slot, $"{slot}   {c.Character?.Junctions.GetValueOrDefault(slot, "--") ?? "--"}", c.IsDemo || c.Character?.AvailableJunctionSlots.Contains(slot) == true ? "Choose stocked magic for this slot." : "Learn this junction ability from an equipped GF to unlock the slot.", "junction-stock", new MenuAction("select-slot", slot), c.IsDemo || c.Character?.AvailableJunctionSlots.Contains(slot) == true)).ToArray();
    private static IReadOnlyList<MenuEntry> Targets(MenuController c, string kind)
    {
        if (kind == "item-use" && c.Item?.NativeFieldNameConfirmation == true)
        {
            return [new MenuEntry("rename", "Change name", "Confirm before using this naming item.", "item-name-confirm")];
        }
        if (kind == "item-use" && c.Item?.NativeFieldInventoryTarget == true)
        {
            return [Action("use", "Use", "Use this item with the current party and inventory.", kind, target: "character-0")];
        }
        if (kind == "item-use" && c.Item?.NativeFieldForgetsAbility == true)
        {
            return c.Data.GuardianForces.Select(gf => new MenuEntry(gf.Id, gf.Name, "Choose the ability to forget next.", "item-forget", new MenuAction("select-gf", gf.Id))).ToArray();
        }
        if (kind == "item-use" && c.Item?.NativeFieldTargetsGuardianForce == true)
        {
            return c.Data.GuardianForces.Select(gf => Action(gf.Id, $"{gf.Name}   HP {MenuController.Number(gf.CurrentHp)} / {MenuController.Number(gf.MaximumHp)}", "Use the selected item on this GF.", kind, target: $"item-target-{gf.NativeId + 16}")).ToArray();
        }
        return c.Data.Characters.Where(ch => kind != "item-use" || c.IsDemo || ch.NativeFieldItemTargetAllowed && (c.Item?.NativeFieldQuistisOnly != true || ch.NativeId == 3)).Select(ch => new MenuEntry(ch.Id, $"{ch.Name}   HP {MenuController.Hp(ch)}", "Request this action on the selected target.", Action: new MenuAction(kind, TargetId: ch.Id), Enabled: c.IsDemo || (kind != "magic-use" || ch.CurrentHp is not null) && (kind != "party-switch" || c.Data.SupportedActions.Contains($"party-switch:{c.ExchangeId}:{ch.Id}")))).ToArray();
    }
    private static IReadOnlyList<string> CharacterDetails(MenuController c)
    {
        var ch = c.Character;
        return ch == null ? ["Character data unavailable.", .. Boundary(c)] : [ch.Name, $"Level {MenuController.Number(ch.Level)}", $"HP {MenuController.Hp(ch)}", $"EXP {MenuController.Number(ch.Experience)}", $"Next level {MenuController.Number(ch.NextLevelExperience)}", $"Weapon: {ch.Weapon}", $"Limit Break: {ch.LimitBreak}", "Commands: " + (ch.Commands.Count == 0 ? "Unavailable" : string.Join(" / ", ch.Commands)), "Conditions: " + (ch.Conditions.Count == 0 ? "Unavailable / none projected" : string.Join(" / ", ch.Conditions))];
    }
    private static IReadOnlyList<string> Stats(MenuController c)
    {
        var ch = c.Character;
        return ch == null ? ["Character stats unavailable.", .. Boundary(c)] : new[] { "HP", "Str", "Vit", "Mag", "Spr", "Spd", "Eva", "Hit", "Luck" }.Select(stat => $"{stat,-5} {(ch.Stats.TryGetValue(stat, out var value) ? value.ToString() : "?")}    {ch.Junctions.GetValueOrDefault(stat, "--")}").Concat(Boundary(c)).ToArray();
    }
    private static IReadOnlyList<string> InventoryDetails(MenuController c) => [$"Inventory entries: {c.Data.Items.Count}", $"Selected item: {c.SelectedEntry?.Label ?? c.Item?.Name ?? "Unavailable"}", c.Item?.Description ?? "Item descriptions unavailable.", $"Gil {MenuController.Number(c.Data.Currency)}", .. Boundary(c)];
    private static IReadOnlyList<string> MagicDetails(MenuController c) => [$"Character: {c.Character?.Name ?? "Unavailable"}", $"Stock entries: {CharacterMagic(c).Count()}", "Max stock: 100 in the explicit fixture", c.Magic?.Description ?? "Magic descriptions unavailable.", .. Boundary(c)];
    private static IReadOnlyList<string> GfDetails(MenuController c)
    {
        MenuGuardianForce? gf = c.GuardianForce;
        if (gf is null)
        {
            return ["No Guardian Forces available."];
        }
        List<string> details = [gf.Name, $"Lv {MenuController.Number(gf.Level)}", $"HP {MenuController.Number(gf.CurrentHp)} / {MenuController.Number(gf.MaximumHp)}", $"EXP {MenuController.Number(gf.Experience)}", $"Learning: {gf.Learning}"];
        if (gf.AbilityPoints is int points)
        {
            details.Add($"Learning AP: {points}");
        }
        if (gf.Element != "Unavailable")
        {
            details.Add($"Element: {gf.Element}");
        }
        details.Add("Junctioned to: " + (c.Data.Characters.FirstOrDefault(character => character.GuardianForceIds.Contains(gf.Id))?.Name ?? "--"));
        return details;
    }
    private static IReadOnlyList<string> SelectedGfDetails(MenuController c)
    {
        var gf = c.Data.GuardianForces.FirstOrDefault(g => g.Id == c.SelectedEntry?.Id);
        return gf == null ? ["No Guardian Forces available.", .. Boundary(c)] : [$"{gf.Name}    Lv {MenuController.Number(gf.Level)}    HP {MenuController.Number(gf.CurrentHp)} / {MenuController.Number(gf.MaximumHp)}", $"Learning: {gf.Learning}", .. Boundary(c)];
    }
    private static IReadOnlyList<string> SelectedCardDetails(MenuController c) => CardDetails(c.Data.Cards.FirstOrDefault(card => card.Id == c.SelectedEntry?.Id));
    private static IReadOnlyList<string> CardDetails(MenuCard? card) => card == null ? ["No card metadata is projected."] : [$"{card.Name}    Quantity {card.Quantity}    Level {MenuController.Number(card.Level)}", "Ranks (top / right / bottom / left): " + card.Ranks, "Element: " + card.Element, card.Description];
    private static IReadOnlyList<string> RecipeDetails(MenuController c)
    {
        var recipe = c.Recipe;
        return recipe == null ? ["No recipes are projected.", .. Boundary(c)] : [recipe.Ability, $"Ingredient: {recipe.IngredientQuantity} x {recipe.IngredientName ?? c.Data.Items.FirstOrDefault(i => i.Id == recipe.IngredientId)?.Name ?? recipe.IngredientId}", $"Result: {recipe.ResultQuantity} x {recipe.ResultName}", "Recipient: " + (recipe.NativeKind is 1 or 3 or 4 ? "Inventory" : c.Character?.Name ?? "Unavailable"), recipe.NativeAbility.HasValue ? "Original recipe; final batch output caps at 100." : "Fixture recipe.", .. Boundary(c)];
    }
    private static MenuCharacter? LimitCharacter(MenuController c, string family) => c.Character?.LimitBreak == family ? c.Character : c.IsDemo ? c.Data.Characters.FirstOrDefault(ch => ch.LimitBreak == family) : c.Character;
    private static IReadOnlyList<MenuEntry> LimitEntries(MenuController c, string family, bool learn = false)
    {
        var owner = LimitCharacter(c, family);
        return (owner?.LimitMoves ?? []).Select(move => new MenuEntry(move.Id, $"{move.Name}   {(move.Stock.HasValue ? "×" + move.Stock : move.LearningProgress.HasValue ? move.LearningProgress + "%" : move.Learned == true ? "Learned" : move.Learned == false ? "--" : "?")}", move.Description, Action: learn ? new MenuAction("limit-setting", "Learning", owner?.Id ?? "", move.Name) : null)).ToArray();
    }
    private static IReadOnlyList<string> LimitDetails(MenuController c, string family)
    {
        var owner = LimitCharacter(c, family);
        var move = owner?.LimitMoves.FirstOrDefault(m => m.Id == c.SelectedEntry?.Id) ?? owner?.LimitMoves.FirstOrDefault();
        return move == null ? ["No learned-move owner is projected.", .. Boundary(c)] : [$"{owner!.Name} · {move.Name}", $"Learned: {(move.Learned.HasValue ? move.Learned.Value ? "Yes" : "No" : "Unavailable")}", $"Progress: {(move.LearningProgress.HasValue ? move.LearningProgress + "%" : "Unavailable")}", "Inputs: " + move.InputSequence, $"Ammo stock: {MenuController.Number(move.Stock)}", "Learning: " + owner.LimitSettings.GetValueOrDefault("Learning", "--"), "Fixture state and sequences; native parity unproved.", .. Boundary(c)];
    }
    private static IReadOnlyList<string> SlotDetails(MenuController c)
    {
        var owner = LimitCharacter(c, "Slot");
        var index = int.TryParse(owner?.LimitSettings.GetValueOrDefault("Preview index"), out var preview) ? preview : 0;
        var spell = owner is { SlotPreview.Count: > 0 } ? owner.SlotPreview[index % owner.SlotPreview.Count] : "Unavailable";
        return [owner?.Name ?? "Unavailable", "Spell preview: " + spell, "Select accepts the fixture preview.", "Do Over cycles explicit fixture choices.", "Native random draws and battle casting unsupported.", .. Boundary(c)];
    }
    private static MenuAction? PreviewCharacter(MenuController c, string screenId)
    {
        if (!c.IsDemo)
        {
            return null;
        }
        var family = LimitFamilyForScreen(screenId);
        var character = c.Data.Characters.FirstOrDefault(ch => ch.LimitBreak == family);
        return character == null ? null : new MenuAction("select-character", character.Id);
    }
    internal static string? LimitFamilyForScreen(string screenId)
    {
        return screenId switch
        {
            "status-renzokuken" => "Renzokuken",
            "status-angelo" or "status-angel-wing" => "Combine",
            "status-duel" => "Duel",
            "status-slot" => "Slot",
            "status-blue-magic" => "Blue Magic",
            "status-shot" => "Shot",
            _ => null
        };
    }
}
