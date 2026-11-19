using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private string BattleLoadoutResourceRevision => resourceHost?.Mods.Catalog.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";

    private void ConnectBattleLoadoutEditor()
    {
        gameMenus!.ConnectBattleLoadoutEditor(ReadBattleLoadoutEditor, draft =>
        {
            session.ApplyBattleLoadoutDraft(draft, BattleLoadoutResourceRevision, (character, assignment) =>
            {
                int nativeWeapon = draft.ResolveWeapon(character);
                _ = PresentationBattleResources.ResolveBattleLoadout(session.State with { BattleLoadouts = draft.Overrides }, character, nativeWeapon);
            });
        }, () => ActiveBattle is not null ? "Loadouts cannot change during an active battle." : session.BattleLoadoutMutationUnavailableReason, DescribeBattleLoadout, () => session.State.Scene != SceneKind.Title && session.State.CharacterVitals is not null);
    }

    private BattleLoadoutEditorContext ReadBattleLoadoutEditor()
    {
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new InvalidOperationException("Start or load a game to edit current-game assignments. At Title, use the Battle laboratory.");
        EnsureLiveMenuCatalog();
        List<BattleLoadoutDefaults> defaults = [];
        List<BattleLoadoutEditorCharacter> characters = [];
        HashSet<int> identities = [];
        for (int row = 0; row < vitals.Records.Length; row++)
        {
            if (!vitals.IsRosterMember(row) || vitals.DreamEnabled && (row >= NativeCharacterVitals.NativeRecordCount || vitals.Records[row].CharacterId < 8))
            {
                continue;
            }
            NativeCharacterVital record = vitals.Records[row];
            int identity = record.CharacterId;
            if (!identities.Add(identity))
            {
                continue;
            }
            defaults.Add(new(identity, vitals.GetBattleWeapon(row), BattleLimitFamilyRules.NativeFamilies(identity)));
            string name = LiveMenuData.CharacterName(session.State, record.CharacterId, events?.Scene.Names, liveMenuCatalog);
            characters.Add(new(identity, name, "Edit this character's equipment and ordered Limit families."));
        }
        BattleLoadoutDraft draft = session.CreateBattleLoadoutDraft(defaults, BattleLoadoutResourceRevision);
        BattleLoadoutEditorWeapon[] weapons = PresentationBattleResources.WeaponDefinitions.Select(weapon => new BattleLoadoutEditorWeapon(weapon.Id, liveMenuCatalog?.WeaponName(weapon.Id) ?? weapon.Name, LiveMenuData.CharacterName(session.State, checked((byte)weapon.CharacterId), events?.Scene.Names, liveMenuCatalog), "Combat capabilities: " + PresentationBattleResources.ReadWeaponProfile(weapon.Id).Capabilities)).ToArray();
        return new(draft, characters.ToArray(), weapons);
    }

    private string[] DescribeBattleLoadout(BattleLoadoutDraft draft, int character)
    {
        try
        {
            NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new InvalidOperationException("Current-game character state is unavailable.");
            int row = Array.FindIndex(vitals.Records, record => record.CharacterId == character);
            if (vitals.DreamEnabled)
            {
                row = Array.FindIndex(vitals.Records, 0, NativeCharacterVitals.NativeRecordCount, record => record.CharacterId == character);
            }
            if (row < 0)
            {
                return ["Character binding changed. Reopen the draft."];
            }
            BattleResolvedLoadout loadout = PresentationBattleResources.ResolveBattleLoadout(session.State with { BattleLoadouts = draft.Overrides }, character, vitals.GetBattleWeapon(row));
            BattlePlayerStats stats = PresentationBattleResources.ReadPlayerStats(vitals, row, loadout.Weapon.Id);
            List<string> summary = [$"{loadout.Weapon.Name} · {loadout.WeaponProfile.Capabilities}", $"Str {stats.Strength} · Mag {stats.Magic} · Hit {stats.Hit} · Vit {stats.Vitality}"];
            foreach (BattleNativeLimitFamilyDefinition family in loadout.Families)
            {
                BattleWeaponCapabilities required = BattleLimitFamilyRules.RequiredWeapon(family.Family);
                string reason = loadout.WeaponProfile.Supports(required) ? "Native unlock/action rules apply" : "Unavailable: requires " + required;
                if (family.Family == BattleLimitFamily.AngelWing && (session.State.SavedBattleFlags is not { } flags || (flags.EffectiveKnownMask & 0x20) == 0 || (flags.Value & 0x20) == 0))
                {
                    reason = "Locked: Angel Wing is not unlocked";
                }
                summary.Add(family.Family + " · " + reason);
            }
            if (loadout.Families.Length == 0)
            {
                summary.Add("No Limit families assigned.");
            }
            return summary.ToArray();
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            return [exception.Message];
        }
    }
}

