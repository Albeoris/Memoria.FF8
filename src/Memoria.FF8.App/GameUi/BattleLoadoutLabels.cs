using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

internal static class BattleLoadoutLabels
{
    internal static string? CurrentWorldUnavailableReason(bool ff7Active, Func<string, string, string>? localize = null)
    {
        return ff7Active ? SystemTextCatalog.Translate(localize, "debug.loadout.dormant-world", "FFVIII is dormant while the FFVII world is active. Return to FFVIII to edit its live loadouts; the laboratory remains isolated.") : null;
    }

    internal static string Capabilities(BattleWeaponCapabilities capabilities, Func<string, string, string>? localize = null)
    {
        (BattleWeaponCapabilities Flag, string Id, string Text)[] labels =
        [
            (BattleWeaponCapabilities.Gunblade, "debug.loadout.capability.gunblade", "Gunblade"),
            (BattleWeaponCapabilities.MachineGun, "debug.loadout.capability.machine-gun", "Machine gun"),
            (BattleWeaponCapabilities.ShotFirearm, "debug.loadout.capability.shot-firearm", "Shot firearm"),
            (BattleWeaponCapabilities.PairedBlades, "debug.loadout.capability.paired-blades", "Paired blades"),
            (BattleWeaponCapabilities.ThrowingSpear, "debug.loadout.capability.throwing-spear", "Throwing spear"),
            (BattleWeaponCapabilities.Unarmed, "debug.loadout.capability.unarmed", "Unarmed fighting")
        ];
        string[] selected = labels.Where(label => (capabilities & label.Flag) != 0).Select(label => SystemTextCatalog.Translate(localize, label.Id, label.Text)).ToArray();
        return selected.Length == 0 ? SystemTextCatalog.Translate(localize, "debug.loadout.capability.general", "General weapon style") : string.Join(", ", selected);
    }

    internal static string Family(BattleLimitFamily family, Func<string, string, string>? localize = null)
    {
        string original = family switch
        {
            BattleLimitFamily.FireCross => "Fire Cross / No Mercy",
            BattleLimitFamily.BloodPain => "Blood Pain",
            BattleLimitFamily.MassiveAnchor => "Massive Anchor",
            BattleLimitFamily.AngelWing => "Angel Wing",
            BattleLimitFamily.IceStrike => "Ice Strike",
            BattleLimitFamily.BlueMagic => "Blue Magic",
            _ => family.ToString()
        };
        return SystemTextCatalog.Translate(localize, "debug.loadout.family." + family, original);
    }

    internal static string[] CanonicalFacts(GameState state, BattleLimitFamily family, Func<string, string, string>? localize = null)
    {
        string Text(string key, string original) => SystemTextCatalog.Translate(localize, "debug.loadout." + key, original);
        string Format(string key, string original, params object?[] values) => SystemTextCatalog.Format(Text(key, original), original, values);
        NativeLimitProgress? progress = state.LimitProgress;
        switch (family)
        {
            case BattleLimitFamily.BlueMagic:
                return [progress is null ? Text("learning-unknown", "Saved learning is unavailable.") : Format("blue-learned", "Blue Magic learned: {0}/16.", System.Numerics.BitOperations.PopCount((uint)progress.BlueMagicFlags))];
            case BattleLimitFamily.Combine:
                List<string> angelo = [];
                if (progress is null)
                {
                    angelo.Add(Text("learning-unknown", "Saved learning is unavailable."));
                }
                else
                {
                    angelo.Add(Format("angelo-learned", "Angelo learned: {0}/8; attack moves: {1}/4; support moves: {2}/4.", System.Numerics.BitOperations.PopCount((uint)progress.AngeloFlags), System.Numerics.BitOperations.PopCount((uint)(progress.AngeloFlags & 0xF0)), System.Numerics.BitOperations.PopCount((uint)(progress.AngeloFlags & 0x0F))));
                    if ((progress.AngeloFlags & 0xF0) == 0)
                    {
                        angelo.Add(Text("angelo-no-attacks", "Combine has no learned attack move; learned support remains separate."));
                    }
                }
                angelo.Add(state.SavedBattleFlags is not { } support || (support.EffectiveKnownMask & 0x10) == 0 ? Text("angelo-unknown", "Angelo story availability is unknown.") : (support.Value & 0x10) != 0 ? Text("angelo-suppressed", "Angelo is suppressed by the current story state.") : Text("angelo-support", "Learned Angelo support follows eligible assigned Combine holders."));
                return angelo.ToArray();
            case BattleLimitFamily.AngelWing:
                return [state.SavedBattleFlags is not { } wing || (wing.EffectiveKnownMask & 0x20) == 0 ? Text("wing-unknown", "Angel Wing unlock state is unknown.") : (wing.Value & 0x20) != 0 ? Text("wing-unlocked", "Angel Wing is unlocked.") : Text("wing-locked", "Angel Wing is locked."), Text("wing-no-angelo", "Angel Wing alone grants no Angelo support.")];
            case BattleLimitFamily.Shot:
                if (state.ShotAvailability is not { } shot || state.Economy is not { } economy)
                {
                    return [Text("ammo-unknown", "Saved ammunition availability is unknown.")];
                }
                int[] unlockedItems = shot.AmmoItemIds.Where((_, index) => (shot.Flags770 & (1 << index)) != 0).Select(id => (int)id).ToArray();
                int rounds = economy.Items.Where(stock => unlockedItems.Contains(stock.ItemId)).Sum(stock => stock.Count);
                int usable = unlockedItems.Count(id => economy.Items.Any(stock => stock.ItemId == id && stock.Count > 0));
                return [Format("shot-stock", "Shot ammunition: {0} rounds in {1} usable unlocked types.", rounds, usable), Text("shot-spending", "Ammunition is spent only after committing Shot.")];
            case BattleLimitFamily.Duel:
                return [progress is null ? Text("learning-unknown", "Saved learning is unavailable.") : Format("duel-learned", "Learned Duel flags: {0}.", System.Numerics.BitOperations.PopCount((uint)progress.DuelFlags)), Text("duel-manual", "Manual combinations keep their native input rules.")];
            case BattleLimitFamily.Slot:
                return [state.GameplayPreferences.SelphieCardSlots ? Text("slot-cards", "Configured mode: card Slots.") : Text("slot-native", "Configured mode: native Slot.")];
            default:
                return [Text("no-learned-requirement", "No separate learned-action requirement.")];
        }
    }

    internal static string Style(int sourceCharacter, Func<string, string, string>? localize = null)
    {
        (string Id, string Text) label = sourceCharacter switch
        {
            0 or 6 => ("debug.loadout.style.gunblade", "Gunblade"),
            1 => ("debug.loadout.style.fighting", "Unarmed fighting"),
            2 => ("debug.loadout.style.shot", "Shot firearm"),
            3 => ("debug.loadout.style.whip", "Whip"),
            4 => ("debug.loadout.style.blaster-edge", "Blaster edge"),
            5 => ("debug.loadout.style.nunchaku", "Nunchaku"),
            7 => ("debug.loadout.style.sorcery", "Unarmed sorcery"),
            8 => ("debug.loadout.style.machine-gun", "Machine gun"),
            9 => ("debug.loadout.style.paired-blades", "Paired blades"),
            10 => ("debug.loadout.style.throwing-spear", "Throwing spear"),
            _ => throw new ArgumentOutOfRangeException(nameof(sourceCharacter))
        };
        return SystemTextCatalog.Translate(localize, label.Id, label.Text);
    }
}
