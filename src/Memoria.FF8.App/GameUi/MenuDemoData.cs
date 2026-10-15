namespace Memoria.FF8.GameUi;

/// <summary>Explicit synthetic fixtures. These never project or seed game-owned state.</summary>
public static class MenuDemoData
{
    public static MenuData Create()
    {
        var data = new MenuData
        {
            Location = "Balamb Garden",
            Currency = 24580,
            PlayTime = TimeSpan.FromHours(12) + TimeSpan.FromMinutes(34),
            SeeDRank = 7,
            SaveAvailable = true,
            SourceDescription = "Synthetic mutable menu fixtures; no native rules or saved game state"
        };
        string[] names = ["Squall", "Rinoa", "Zell", "Selphie", "Quistis", "Irvine", "Laguna", "Kiros", "Ward"];
        string[] weapons = ["Revolver", "Pinwheel", "Metal Knuckle", "Flail", "Chain Whip", "Valiant", "Machine Gun", "Katal", "Harpoon"];
        string[] limits = ["Renzokuken", "Combine", "Duel", "Slot", "Blue Magic", "Shot", "Desperado", "Blood Pain", "Massive Anchor"];
        for (var i = 0; i < names.Length; i++)
        {
            var character = new MenuCharacter { Id = i.ToString(), Name = names[i], InParty = i < 3, Level = 18 + i, CurrentHp = 720 + i * 93, MaximumHp = 1300 + i * 111, Experience = 18000 + i * 500, NextLevelExperience = 500, Weapon = weapons[i], LimitBreak = limits[i] };
            character.Commands.AddRange(["Attack", "Magic", "GF", "Draw"]);
            character.Abilities.AddRange(["Str+20%", "HP+20%", "Mug"]);
            foreach (var stat in new[] { "Str", "Vit", "Mag", "Spr", "Spd", "Eva", "Hit", "Luck" })
            {
                character.Stats[stat] = 20 + i * 3;
            }
            character.Stats["HP"] = character.MaximumHp.Value;
            character.AtbProgress = i < 3 ? 0.3f + i * 0.3f : null;
            character.Junctions["HP"] = "Cure";
            character.Junctions["Str"] = "Fire";
            character.GuardianForceIds.Add(i.ToString());
            if (i == 2)
            {
                character.Conditions.Add("Poison");
            }
            data.Characters.Add(character);
        }
        string[] guardianForces = ["Quezacotl", "Shiva", "Ifrit", "Siren", "Brothers", "Diablos", "Carbuncle", "Leviathan", "Pandemona", "Cerberus", "Alexander", "Doomtrain", "Bahamut", "Cactuar", "Tonberry", "Eden"];
        for (var i = 0; i < guardianForces.Length; i++)
        {
            var gf = new MenuGuardianForce { Id = i.ToString(), Name = guardianForces[i], Level = 12 + i, CurrentHp = 800 + i * 100, MaximumHp = 900 + i * 100, Experience = 5000 + i * 100, AbilityPoints = 35, Learning = "HP-J", Element = i % 3 == 0 ? "Thunder" : i % 3 == 1 ? "Ice" : "Fire" };
            gf.Abilities.AddRange(["HP-J", "Str-J", "Vit-J", "Mag-J", "Spr-J", "Elem-Atk-J", "Elem-Def-J", "ST-Atk-J", "ST-Def-J", "Ability x3", "Boost", "GFHP+10%"]);
            data.GuardianForces.Add(gf);
        }
        string[] itemNames = ["Potion", "Hi-Potion", "X-Potion", "Mega-Potion", "Phoenix Down", "Mega Phoenix", "Antidote", "Soft", "Eye Drops", "Echo Screen", "Remedy", "Tent", "Fuel", "M-Stone Piece", "Magic Stone", "Wizard Stone", "Screw", "Steel Pipe", "Fish Fin", "Healing Ring"];
        for (var i = 0; i < itemNames.Length; i++)
        {
            data.Items.Add(new MenuItem { Id = i.ToString(), Name = itemNames[i], Quantity = 12 + i, DemoHealing = i < 4 ? (i + 1) * 200 : 0, Price = 100 + i * 50, Description = i < 4 ? "Fixture: restores HP to one character." : "Fixture item; native item effects are not implemented here." });
        }
        string[] magicNames = ["Fire", "Blizzard", "Thunder", "Cure", "Cura", "Curaga", "Life", "Full-life", "Esuna", "Scan", "Protect", "Shell", "Haste", "Slow", "Stop", "Sleep", "Blind", "Silence", "Bio", "Death", "Regen", "Double", "Triple", "Flare", "Holy", "Ultima"];
        foreach (var character in data.Characters)
        {
            for (var i = 0; i < magicNames.Length; i++)
            {
                data.Magic.Add(new MenuMagic { Id = i.ToString(), Name = magicNames[i], CharacterId = character.Id, Quantity = 40 + i * 2, DemoHealing = i is >= 3 and <= 5 ? 350 + i * 75 : 0, Description = "Synthetic stock. Junction strength and battle effects require native contracts." });
            }
        }
        string[] cardNames = ["Geezard", "Funguar", "Bite Bug", "Red Bat", "Blobra", "Gayla", "Gesper", "Fastitocalon-F", "Blood Soul", "Caterchipillar", "Cockatrice", "Grat", "Buel", "Mesmerize", "Glacial Eye", "Belhelmel", "Thrustaevis", "Anacondaur", "Creeps", "Grendel", "Jelleye", "Grand Mantis", "Ifrit", "Shiva", "Quezacotl", "Diablos", "Laguna", "Rinoa", "Squall"];
        for (var i = 0; i < cardNames.Length; i++)
        {
            data.Cards.Add(new MenuCard { Id = i.ToString(), Name = cardNames[i], Quantity = i < 22 ? 3 : 1, Level = 1 + i / 4, Ranks = $"{2 + i % 7} / {3 + i % 6} / {1 + i % 8} / {4 + i % 5}", Element = i % 4 == 0 ? "Thunder" : "None", Description = "Fixture card. Triple Triad rules are outside this menu demonstration.", RefinementItemId = i % 2 == 0 ? "13" : "14", RefinementQuantity = 1 + i % 3 });
        }
        data.Recipes.Add(new MenuRecipe("fire", "F Mag-RF", "13", 1, "0", "Fire", 5));
        data.Recipes.Add(new MenuRecipe("blizzard", "I Mag-RF", "14", 1, "1", "Blizzard", 5));
        data.Recipes.Add(new MenuRecipe("cure", "L Mag-RF", "19", 1, "3", "Cure", 10));
        for (var i = 0; i < 30; i++)
        {
            data.SaveSlots.Add(new MenuSaveSlot { Id = i.ToString(), Name = $"Slot {i + 1:00}", Summary = i == 0 ? "DEMO Squall Lv18 · Balamb Garden" : "Empty demo slot" });
        }
        foreach (var setting in MenuSettings.Definitions)
        {
            data.Settings[setting.Id] = setting.Values[0];
        }
        AddLimits(data);
        AddBattle(data);
        return data;
    }

    private static void AddBattle(MenuData data)
    {
        string[] names = ["Grat A", "Grat B", "T-Rexaur"];
        for (var i = 0; i < names.Length; i++)
        {
            var target = new MenuBattleTarget { Id = $"enemy-{i}", Name = names[i], CurrentHp = 320 + i * 900, MaximumHp = 500 + i * 1000 };
            foreach (var magic in data.Magic.Where(m => m.CharacterId == "0").Take(4))
            {
                target.DrawSpells.Add(new MenuMagic { Id = magic.Id, Name = magic.Name, Description = "Explicit fixture draw list; native enemy spell tables are not projected." });
            }
            data.BattleTargets.Add(target);
        }
        var rewards = new MenuBattleRewards { AbilityPoints = 8, Currency = 240 };
        foreach (var character in data.Characters.Where(c => c.InParty))
        {
            rewards.CharacterExperience[character.Id] = 120 + int.Parse(character.Id) * 20;
        }
        rewards.Items.Add(new MenuItem { Id = "0", Name = "Potion", Quantity = 2 });
        rewards.Items.Add(new MenuItem { Id = "13", Name = "M-Stone Piece", Quantity = 1 });
        data.BattleRewards = rewards;
    }

    private static void AddLimits(MenuData data)
    {
        var squall = data.Characters[0];
        squall.LimitSettings["Auto"] = "Off";
        squall.LimitSettings["Indicator"] = "On";
        AddMoves(squall, ["Rough Divide", "Fated Circle", "Blasting Zone", "Lion Heart"]);
        var rinoa = data.Characters[1];
        AddMoves(rinoa, ["Angelo Cannon", "Angelo Strike", "Angelo Rush", "Angelo Recover", "Angelo Reverse", "Angelo Search", "Invincible Moon", "Wishing Star"]);
        for (var i = 0; i < rinoa.LimitMoves.Count; i++)
        {
            rinoa.LimitMoves[i].LearningProgress = i < 3 ? 100 : 20 + i * 7;
            rinoa.LimitMoves[i].Learned = i < 3;
        }
        var zell = data.Characters[2];
        AddMoves(zell, ["Punch Rush", "Booya", "Heel Drop", "Mach Kick", "Dolphin Blow", "Meteor Strike", "Burning Rave", "Meteor Barret", "Different Beat", "My Final Heaven"]);
        string[] sequences = ["Circle → Cross", "Right → Left", "Up → Down", "Left → Left → Circle", "L1 → R1 → L1 → R1", "Down → Circle → Up → Circle"];
        for (var i = 0; i < zell.LimitMoves.Count; i++)
        {
            zell.LimitMoves[i].InputSequence = sequences[i % sequences.Length];
            zell.LimitMoves[i].Description = "Synthetic input sequence fixture; original sequence parity has not been validated.";
        }
        var selphie = data.Characters[3];
        selphie.SlotPreview.AddRange(["Cure × 3", "Wall × 1", "Rapture × 1", "The End × 1"]);
        selphie.LimitSettings["Preview index"] = "0";
        AddMoves(data.Characters[4], ["Laser Eye", "Ultra Waves", "Electrocute", "LV?Death", "Degenerator", "Aqua Breath", "Micro Missiles", "Acid", "Gatling Gun", "Fire Breath", "Bad Breath", "White Wind", "Homing Laser", "Mighty Guard", "Ray Bomb", "Shockwave Pulsar"]);
        var irvine = data.Characters[5];
        AddMoves(irvine, ["Normal Shot", "Shotgun Shot", "Dark Shot", "Fire Shot", "Canister Shot", "Quick Shot", "Armor Shot", "Hyper Shot"]);
        for (var i = 0; i < irvine.LimitMoves.Count; i++)
        {
            irvine.LimitMoves[i].Stock = 12 + i * 3;
            irvine.LimitMoves[i].InputSequence = "R1 / RB: fixture shoot input";
        }
    }

    private static void AddMoves(MenuCharacter character, string[] names)
    {
        for (var i = 0; i < names.Length; i++)
        {
            character.LimitMoves.Add(new MenuLimitMove { Id = i.ToString(), Name = names[i], Learned = i % 4 != 3, Description = "Synthetic learned-state fixture; no original unlock or combat effect is inferred." });
        }
    }
}

public sealed record MenuSetting(string Id, string Description, string[] Values);

public static class MenuSettings
{
    public static IReadOnlyList<MenuSetting> Definitions { get; } =
    [
        new("Sound", "Original stereo / mono output setting.", ["Stereo", "Mono"]),
        new("Controller", "Original normal / custom control mapping.", ["Normal", "Customize"]),
        new("Cursor", "Original initial / memory cursor position.", ["Initial", "Memory"]),
        new("ATB", "Original battle timing mode.", ["Active", "Wait"]),
        new("Battle ATB mode", "Normal advances continuously. TurnBased advances idle gauges with Advance Turn.", ["Normal", "TurnBased"]),
        new("Battle policy", "Policy for the next New Game. ResearchInstantVictory automatically wins research battles.", ["ResearchInstantVictory", "NewGameFullCombat"]),
        new("Initiative bar", "Show living party and enemy heads on the battle initiative bar.", ["On", "Off"]),
        new("Battle speed", "Original battle speed slider.", ["1", "2", "3", "4", "5"]),
        new("Battle message", "Original battle message speed slider.", ["1", "2", "3", "4", "5"]),
        new("Field message", "Original field message speed slider.", ["1", "2", "3", "4", "5"]),
        new("Scan", "Original Scan display behavior.", ["Always", "Once"]),
        new("Camera movement", "Original battle camera movement.", ["On", "Off"]),
        new("Battle control", "Original battle control prompts.", ["On", "Off"]),
        new("Vibration", "Original controller vibration.", ["On", "Off"]),
        new("Music volume", "Host music playback preference.", ["100%", "75%", "50%", "25%", "0%"]),
        new("Effect volume", "Host sound-effect playback preference.", ["100%", "75%", "50%", "25%", "0%"]),
        new("Text scale", "Host menu text scale.", ["100%", "125%", "150%"]),
        new("UI font", "Installed OS font for menus and interface text.", SystemFontCatalog.Families.ToArray()),
        new("UI font size", "Interface font size in design pixels; Text scale applies on top.", ["18", "20", "24", "28", "32"]),
        new("Dialogue font", "Installed OS font for field dialogue; missing scripts use OS fallback.", SystemFontCatalog.Families.ToArray()),
        new("Dialogue font size", "Field dialogue font size in field pixels.", ["6", "7", "8", "9", "10", "12", "14", "16"])
    ];
}
