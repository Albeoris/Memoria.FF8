using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private sealed record WeaponExchangeOffer(BattleLoadoutEditorCharacter Character, BattleLoadoutEditorWeapon Weapon, BattleLimitFamily[] LostAccess);
    private sealed record WeaponExchange(int InstanceId, WeaponExchangeOffer Left, WeaponExchangeOffer Right);
    private WeaponExchange? battleLoadoutSwap;

    private void PreviewBattleLoadoutSwap(BattleLoadoutEditorContext editor, BattleWeaponInstance selected)
    {
        BattleLoadoutEditorWeapon Weapon(int weaponId) => editor.Weapons.First(weapon => weapon.WeaponId == weaponId);

        WeaponExchangeOffer Offer(int characterId, BattleLoadoutEditorWeapon outgoing, BattleLoadoutEditorWeapon incoming)
        {
            BattleLoadoutEditorCharacter actor = editor.Characters.First(character => character.CharacterId == characterId);
            BattleLoadoutEditorWeapon before = Weapon(editor.Draft.ResolveWeapon(characterId));
            BattleLimitFamily[] losses = editor.Draft.ResolveFamilies(characterId).Where(family => BattleLoadoutSupportsLimit(before, family) && !BattleLoadoutSupportsLimit(incoming, family)).ToArray();
            return new(actor, outgoing, losses);
        }

        BattleLoadoutEditorWeapon current = Weapon(editor.Draft.ResolveWeapon(battleLoadoutCharacter));
        BattleLoadoutEditorWeapon requested = Weapon(selected.WeaponId);
        battleLoadoutSwap = new(selected.Id, Offer(battleLoadoutCharacter, current, requested), Offer(selected.OwnerCharacterId, requested, current));
        battleLoadoutFocus = "no";
    }

    private MenuEntry[] BattleLoadoutSwapChoices()
    {
        return
        [
            new("no", J("no", "No"), "", Action: new("loadout-swap-no")),
            new("yes", J("yes", "Yes"), "", Action: new("loadout-swap-yes"))
        ];
    }

    private void DismissBattleLoadoutSwap()
    {
        battleLoadoutSwap = null;
        battleLoadoutFocus = BattleLoadoutWeaponListId;
    }

    private UiElement BuildBattleLoadoutSwap(WeaponExchange exchange)
    {
        float scale = TextScale;
        UiGrid body = JunctionRows([32 * scale, 0, 38 * scale], [1], 12 * scale);
        Place(body, JunctionLabel(LoadoutText("scene.swap-question", "Exchange weapons?"), 22), 0);

        UiGrid transfer = Grid([1], [1, 0.32f, 1], 12 * scale);
        float[] rows = [68 * scale, 42 * scale, 42 * scale, 28 * scale, 0];
        Place(transfer, BuildBattleLoadoutSwapCharacter(exchange.Left, rows, 1), 0);
        UiGrid arrows = JunctionRows(rows, [1], 4 * scale);
        Place(arrows, new JunctionTransferArrow(), 1);
        Place(arrows, new JunctionTransferArrow(pointsLeft: true), 2);
        Place(transfer, arrows, 0, 1);
        Place(transfer, BuildBattleLoadoutSwapCharacter(exchange.Right, rows, 2), 0, 2);
        Place(body, transfer, 1);

        UiGrid choices = Grid([1], [1, 1], 14 * scale);
        foreach (MenuEntry choice in BattleLoadoutSwapChoices())
        {
            UiButton button = BattleLoadoutButton(choice.Id, () => Execute(choice.Action!));
            button.DrawBackground = true;
            button.Text = choice.Label;
            button.FontSize = 16 * scale;
            Place(choices, button, 0, choice.Id == "yes" ? 0 : 1);
        }
        Place(body, choices, 2);
        UiPanel panel = new() { CaptionHeight = 0, Padding = 18 };
        panel.Children.Add(body);
        return new JunctionTransferOverlay(panel, scale) { Id = "loadout-swap-confirm" };
    }

    private UiGrid BuildBattleLoadoutSwapCharacter(WeaponExchangeOffer offer, float[] rows, int outgoingRow)
    {
        UiGrid card = JunctionRows(rows, [1], 4 * TextScale);
        card.Id = $"loadout-swap-character-{offer.Character.CharacterId}";
        UiGrid identity = JunctionRows([0], [1, 4], 12 * TextScale);
        Place(identity, BattleLoadoutPortrait(offer.Character.CharacterId), 0);
        Place(identity, JunctionLabel(offer.Character.Name, 20), 0, 1);
        Place(card, identity, 0);
        UiGrid weapon = JunctionRows([0], [1, 4], 12 * TextScale);
        Place(weapon, new UiImage { Source = new UiImageSource(UiImageKind.WeaponVariant, offer.Weapon.WeaponId) }, 0);
        Place(weapon, JunctionLabel(offer.Weapon.Name, 17), 0, 1);
        Place(card, weapon, outgoingRow);

        Place(card, JunctionLabel(LoadoutText("scene.swap-lost-limits", "Loses access to"), 15, true), 3);
        string losses = string.Join("\n", offer.LostAccess.Select(family => BattleLoadoutLabels.Family(family, localizeInterface)));
        UiLabel lost = Label(losses.Length == 0 ? J("none", "None") : losses, 17, UiTextFlow.Wrap);
        lost.Color = losses.Length == 0 ? null : AbilityActivationColor(false);
        Place(card, lost, 4);
        return card;
    }
}
