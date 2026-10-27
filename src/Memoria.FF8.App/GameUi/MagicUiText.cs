using System.Globalization;

namespace Memoria.FF8.GameUi;

internal static class MagicUiText
{
    public static string Get(string key) => key switch
    {
        "Field" => "Field",
        "Battle" => "Battle",
        "Unbound" => "Unbound",
        "Exhausted" => "Exhausted",
        "UnavailableOwner" => "Owner unavailable",
        "MissingCommand" => "Magic command missing",
        "StatusRestricted" => "Caster status prevents magic",
        "Caster" => "Caster: {0}",
        "MemoriaDestination" => "Custom difficulty, magic rules, personal Attack choices or usage history require a Memoria save. Choose a Memoria slot; original FF8 files remain intact.",
        "AttackSlotHelp" => "The first slot remains Attack. Smart Attack unlocks a personal physical or magic preference.",
        "PhysicalOnly" => "Physical only",
        "SmartMagic" => "Smart magic",
        "AttackChoiceHelp" => "Choose physical attacks or automatic magic using the normal Magic command and casting rules.",
        "DrawOnceDescription" => "A successful Draw Stock or Cast exhausts that spell on that enemy for the whole party until battle ends. Failed Draw does not. Saved with this game; change outside battles only. Title sets the new-game default.",
        "SharedDescription" => "Use one party magic bank with exclusive Junction owners. Review quantities, overflow, bindings and HP before changing this save.",
        "InfiniteDescription" => "Preserve casting stock and spend limited battle or field uses. Junction bonuses still use stock; victory restores one field use.",
        "SmartDescription" => "Unlock personal automatic Attack choices. Uses hidden enemy information and favorable spell selection; ordinary costs and command restrictions apply.",
        "ReviewTitle" => "Magic Stock migration preview",
        "ReviewHelp" => "Review every change. Apply commits the entire migration; Cancel leaves the save unchanged.",
        "Apply" => "Apply",
        "Cancel" => "Cancel",
        "PhysicalOverride" => "Hold {0}: Physical Attack",
        "Moved" => "Moved: record {0} → {1}, {2} ×{3}",
        "Overflow" => "Overflow retained: record {0}, {1} ×{2}",
        "Removed" => "Removed Junction: record {0}, slot {1}, {2}",
        "HpChange" => "Record {0}: maximum HP {1} → {2}",
        "NoStockChanges" => "No stock, Junction or maximum HP changes.",
        "SmartLocked" => "Enable Smart Attack in Config to change this preference.",
        "SharedLabel" => "Shared magic stock",
        "InfiniteLabel" => "Limited magic",
        "SmartLabel" => "Smart Attack",
        "ConservativeHelp" => "Conservative Auto Junction preserves every other character's spell ownership.",
        "PartyOffLabel" => "Entire party magic",
        "PartyOffHelp" => "Remove all magic junctions from all eight characters, including reserves and unavailable characters. Preserve GF, commands, abilities and magic stock.",
        "CommandSlotHelp" => "Select a command for this slot.",
        "SettingBeginHelp" => "Start a new game with these gameplay rules. More settings are available in Config.",
        "DeferredReview" => "A magic mode change is pending. Review the migration before applying it.",
        "StockHelp" => "Browse stock, remaining field uses and Junction ownership.",
        "RefinementCost" => "Consume {0} {1}; receive {2} {3}. The complete batch ingredient cost is paid.",
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown magic interface text.")
    };

    public static string SettingLabel(string id) => id switch
    {
        "Shared magic stock" => Get("SharedLabel"),
        "Infinite magic" => Get("InfiniteLabel"),
        "Smart Attack" => Get("SmartLabel"),
        _ => id
    };

    public static string Format(string key, params object[] values) => string.Format(CultureInfo.InvariantCulture, Get(key), values);
}
