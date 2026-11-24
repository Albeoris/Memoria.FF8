using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private string BattleLoadoutResourceRevision => resourceHost?.Mods.Catalog.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";

    private string LoadoutText(string key, string original)
    {
        return LocalizeUiText("debug.loadout." + key, original);
    }

    private void ConnectBattleLoadoutEditor()
    {
        gameMenus!.ConnectBattleLoadoutEditor(ReadBattleLoadoutEditor, draft =>
        {
            if (BattleLoadoutEditorBoundary() is { } unavailable)
            {
                throw new InvalidOperationException(unavailable);
            }
            session.ApplyBattleLoadoutDraft(draft, BattleLoadoutResourceRevision);
        }, BattleLoadoutEditorBoundary, () => !InFf7 && session.State.Scene != SceneKind.Title && session.State.CharacterVitals is not null, PrepareBattleLoadoutPreviewAsync, PrepareBattleLoadoutBatchAsync);
    }

    private string? BattleLoadoutEditorBoundary()
    {
        if (BattleLoadoutLabels.CurrentWorldUnavailableReason(InFf7, LocalizeUiText) is { } dormant)
        {
            return dormant;
        }
        return ActiveBattle is not null ? LoadoutText("boundary-battle", "Loadouts cannot change during an active battle.") : session.BattleLoadoutMutationUnavailableReason;
    }

    private int BattleLoadoutRow(int character)
    {
        NativeCharacterVitals? vitals = session.State.CharacterVitals;
        return vitals is null ? -1 : Array.FindIndex(vitals.Records, 0, vitals.DreamEnabled ? NativeCharacterVitals.NativeRecordCount : vitals.Records.Length, record => record.CharacterId == character);
    }

    private int BattleLoadoutCostume(int character)
    {
        int row = BattleLoadoutRow(character);
        return row >= 0 && session.State.CharacterAppearance is { } appearance && row < appearance.Records.Length ? appearance.Records[row].Dress : 0;
    }

    private BattleLoadoutEditorContext ReadBattleLoadoutEditor()
    {
        if (BattleLoadoutLabels.CurrentWorldUnavailableReason(InFf7, LocalizeUiText) is { } dormant)
        {
            throw new InvalidOperationException(dormant);
        }
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new InvalidOperationException(LoadoutText("require-game", "Start or load a game to edit current-game assignments. At Title, use the Battle laboratory."));
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
            characters.Add(new(identity, name));
        }
        BattleAssets resources = world?.BattleResources ?? PresentationBattleResources;
        BattleLoadoutEditorWeapon[] weapons = resources.WeaponDefinitions.Select(weapon => new BattleLoadoutEditorWeapon(weapon.Id, liveMenuCatalog?.WeaponName(weapon.Id) ?? weapon.Name, weapon.CharacterId, resources.ReadWeaponProfile(weapon.Id).Capabilities, weapon.Tier)).ToArray();
        int[] initialWeapons = weapons.GroupBy(weapon => weapon.CharacterId).Select(group => group.OrderBy(weapon => weapon.Tier).ThenBy(weapon => weapon.WeaponId).First().WeaponId).ToArray();
        BattleLoadoutDraft draft = session.CreateBattleLoadoutDraft(defaults, BattleLoadoutResourceRevision, initialWeapons);
        return new(draft, characters.ToArray(), weapons);
    }
}
