using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly Dictionary<(int Recipient, int Costume, int Source), string?> battleLoadoutRigDiagnostics = [];
    private string battleLoadoutRigRevision = "";
    private string BattleLoadoutResourceRevision => resourceHost?.Mods.Catalog.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";

    private string LoadoutText(string key, string original)
    {
        return LocalizeUiText("debug.loadout." + key, original);
    }

    private string LoadoutFormat(string key, string original, params object?[] values)
    {
        return Memoria.FF8.Resources.Localization.SystemTextCatalog.Format(LoadoutText(key, original), original, values);
    }

    private void ConnectBattleLoadoutEditor()
    {
        gameMenus!.ConnectBattleLoadoutEditor(ReadBattleLoadoutEditor, draft =>
        {
            if (BattleLoadoutEditorBoundary() is { } unavailable)
            {
                throw new InvalidOperationException(unavailable);
            }
            session.ApplyBattleLoadoutDraft(draft, BattleLoadoutResourceRevision, (character, assignment) =>
            {
                NativeCharacterVitals vitals = session.State.CharacterVitals!;
                int nativeWeapon = assignment.WeaponId ?? (character < NativeCharacterVitals.NativeRecordCount ? vitals.Records[character].Weapon : vitals.DreamWeapons[character - NativeCharacterVitals.NativeRecordCount]);
                _ = PresentationBattleResources.ResolveBattleLoadout(session.State with { BattleLoadouts = draft.Overrides }, character, nativeWeapon);
            });
        }, BattleLoadoutEditorBoundary, DescribeBattleLoadout, () => !InFf7 && session.State.Scene != SceneKind.Title && session.State.CharacterVitals is not null, DescribeBattleLoadoutFamily);
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
            characters.Add(new(identity, name, LoadoutText("character-help", "Edit this character's equipment and ordered Limit families.")));
        }
        battleLoadoutRigDiagnostics.Clear();
        battleLoadoutRigRevision = BattleLoadoutResourceRevision;
        int[] sources = PresentationBattleResources.WeaponDefinitions.Select(weapon => weapon.CharacterId).Concat(Enum.GetValues<BattleLimitFamily>().Select(BattleLimitFamilyRules.SourceCharacter)).Distinct().ToArray();
        foreach (BattleLoadoutEditorCharacter character in characters)
        {
            int costume = BattleLoadoutCostume(character.CharacterId);
            foreach (int source in sources)
            {
                string? reason = null;
                try
                {
                    _ = BattleHumanoidRigProfiles.SemanticPairs(source, PresentationBattleResources.LoadPlayer(source), character.CharacterId, PresentationBattleResources.LoadPlayer(character.CharacterId, costume));
                }
                catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IOException or ArgumentException)
                {
                    reason = LoadoutFormat("rig-unavailable", "Rig mapping unavailable: {0}", exception.Message);
                }
                battleLoadoutRigDiagnostics[(character.CharacterId, costume, source)] = reason;
            }
        }
        BattleLoadoutDraft draft = session.CreateBattleLoadoutDraft(defaults, BattleLoadoutResourceRevision);
        BattleLoadoutEditorWeapon[] weapons = PresentationBattleResources.WeaponDefinitions.Select(weapon => new BattleLoadoutEditorWeapon(weapon.Id, liveMenuCatalog?.WeaponName(weapon.Id) ?? weapon.Name, LiveMenuData.CharacterName(session.State, checked((byte)weapon.CharacterId), events?.Scene.Names, liveMenuCatalog), LoadoutFormat("weapon-style-description", "Fighting style: {0}. Weapon requirements and recipient rig availability are checked separately.", BattleLoadoutLabels.Style(weapon.CharacterId, LocalizeUiText)), BattleLoadoutLabels.Style(weapon.CharacterId, LocalizeUiText))).ToArray();
        return new(draft, characters.ToArray(), weapons, (character, weapon) => BattleLoadoutRigReason(character, PresentationBattleResources.ReadWeaponProfile(weapon).ResourceOwner));
    }

    private string? BattleLoadoutRigReason(int character, int source)
    {
        if (battleLoadoutRigRevision != BattleLoadoutResourceRevision || !battleLoadoutRigDiagnostics.TryGetValue((character, BattleLoadoutCostume(character), source), out string? reason))
        {
            return LoadoutText("rig-stale", "Resource selection or costume changed. Reopen the editor to validate rig mapping.");
        }
        return reason;
    }

    private string[] DescribeBattleLoadout(BattleLoadoutDraft draft, int character)
    {
        try
        {
            NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new InvalidOperationException(LoadoutText("state-unavailable", "Current-game character state is unavailable."));
            int row = BattleLoadoutRow(character);
            if (row < 0)
            {
                return [LoadoutText("binding-stale", "Character binding changed. Reopen the draft.")];
            }
            BattleResolvedLoadout loadout = PresentationBattleResources.ResolveBattleLoadout(session.State with { BattleLoadouts = draft.Overrides }, character, vitals.GetBattleWeapon(row));
            BattlePlayerStats stats = PresentationBattleResources.ReadPlayerStats(vitals, row, loadout.Weapon.Id);
            string owner = LiveMenuData.CharacterName(session.State, checked((byte)loadout.WeaponProfile.ResourceOwner), events?.Scene.Names, liveMenuCatalog);
            List<string> summary = [(liveMenuCatalog?.WeaponName(loadout.Weapon.Id) ?? loadout.Weapon.Name) + " · " + BattleLoadoutLabels.Style(loadout.WeaponProfile.ResourceOwner, LocalizeUiText), LoadoutFormat("weapon-owner", "Original owner: {0}.", owner), LoadoutFormat("key-stats", "Str {0} · Mag {1} · Hit {2} · Vit {3}", stats.Strength, stats.Magic, stats.Hit, stats.Vitality)];
            if (BattleLoadoutRigReason(character, loadout.WeaponProfile.ResourceOwner) is { } rigReason)
            {
                summary.Add(rigReason);
            }
            else
            {
                summary.Add(LoadoutText("rig-valid", "Current costume semantic rig mapping is available."));
            }
            foreach (BattleNativeLimitFamilyDefinition family in loadout.Families)
            {
                string[] facts = DescribeBattleLoadoutFamily(draft, character, family.Family);
                summary.Add(BattleLoadoutLabels.Family(family.Family, LocalizeUiText) + " · " + string.Join(" ", facts.Skip(1).Take(2)));
            }
            if (loadout.Families.Length == 0)
            {
                summary.Add(LoadoutText("no-families", "No Limit families assigned; no family-owned support hooks."));
            }
            return summary.ToArray();
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            return [exception.Message];
        }
    }

    private string[] DescribeBattleLoadoutFamily(BattleLoadoutDraft draft, int character, BattleLimitFamily family)
    {
        List<string> facts = [draft.ResolveFamilies(character).Contains(family) ? LoadoutText("assigned", "Assigned to this character.") : LoadoutText("not-assigned", "Not assigned to this character.")];
        try
        {
            BattleWeaponProfile weapon = PresentationBattleResources.ReadWeaponProfile(draft.ResolveWeapon(character));
            BattleWeaponCapabilities required = BattleLimitFamilyRules.RequiredWeapon(family);
            facts.Add(weapon.Supports(required) ? LoadoutText("weapon-compatible", "Equipped weapon meets the family requirement.") : LoadoutFormat("weapon-incompatible", "Unavailable with this weapon; requires {0}.", BattleLoadoutLabels.Capabilities(required, LocalizeUiText)));
            if (BattleLoadoutRigReason(character, BattleLimitFamilyRules.SourceCharacter(family)) is { } rigReason)
            {
                facts.Add(rigReason);
            }
            if (family == BattleLimitFamily.Renzokuken)
            {
                facts.Add(LoadoutFormat("finishers", "Finishers allowed by this weapon: {0}.", System.Numerics.BitOperations.PopCount((uint)weapon.RenzokukenFinisherMask)));
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or ArgumentException)
        {
            facts.Add(LoadoutFormat("weapon-unavailable", "Equipped weapon is unavailable: {0}", exception.Message));
        }
        facts.AddRange(BattleLoadoutLabels.CanonicalFacts(session.State, family, LocalizeUiText));
        facts.Add(LoadoutText("crisis-caveat", "Crisis, status and action timing are checked in battle; this preview does not roll or unlock anything."));
        return facts.ToArray();
    }
}
