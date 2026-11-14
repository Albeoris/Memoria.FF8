namespace Memoria.FF8.Resources.Localization;

public static partial class SystemTextCatalog
{
    public const string LevelBasedStatBonusesDescription = "While equipped, HP Bonus gives +30 × (level − 1), up to +2970 HP; Str/Vit/Mag/Spr Bonus give +(level − 1), up to +99 each. Removing the ability removes its benefit. Historical permanent Bonus growth is suppressed while this mod is on; item and Devour gains remain. No Speed or Luck Bonus abilities are added. Off by default; required in Hardcore.";

    /// <summary>Bundled translations remain overridable through the ordinary interface localization key.</summary>
    public static string LevelBasedStatBonusesDescriptionForLanguage(string? language)
    {
        string code = (language ?? "en").Split('-', '_')[0].ToLowerInvariant();
        return code switch
        {
            "ru" => "Пока способность экипирована, HP Bonus даёт +30 × (уровень − 1), максимум +2970 HP; Str/Vit/Mag/Spr Bonus дают +(уровень − 1), максимум +99 к каждой характеристике. Снятие способности убирает её прибавку. При включённом моде прежний постоянный рост от Bonus не учитывается; прибавки от предметов и Devour сохраняются. Способности Speed Bonus и Luck Bonus не добавляются. По умолчанию выключен; обязателен в Hardcore.",
            "ja" or "jp" => "装備中のみ、HP Bonus は +30 × (レベル − 1)、最大 +2970 HP。Str/Vit/Mag/Spr Bonus は各 +(レベル − 1)、最大 +99。外すと効果は失われます。この MOD が有効な間、過去の Bonus による永続成長は反映されません。アイテムと Devour の上昇分は維持されます。Speed Bonus や Luck Bonus は追加されません。初期設定は無効。Hardcore では必須です。",
            "de" => "Solange ausgerüstet, gewährt HP Bonus +30 × (Stufe − 1), höchstens +2970 HP; Str/Vit/Mag/Spr Bonus gewähren jeweils +(Stufe − 1), höchstens +99. Beim Ablegen entfällt der Vorteil. Frühere dauerhafte Bonus-Steigerungen werden bei aktiver Mod unterdrückt; Steigerungen durch Gegenstände und Devour bleiben erhalten. Keine Speed- oder Luck-Bonus-Fähigkeiten werden hinzugefügt. Standardmäßig aus; in Hardcore vorgeschrieben.",
            "fr" => "Tant que la capacité est équipée, HP Bonus accorde +30 × (niveau − 1), jusqu’à +2970 HP ; Str/Vit/Mag/Spr Bonus accordent chacun +(niveau − 1), jusqu’à +99. Retirer la capacité supprime son avantage. Les anciens gains permanents de Bonus sont ignorés lorsque le mod est actif ; les gains des objets et de Devour sont conservés. Aucune capacité Speed Bonus ou Luck Bonus n’est ajoutée. Désactivé par défaut ; obligatoire en Hardcore.",
            "it" => "Finché equipaggiata, HP Bonus conferisce +30 × (livello − 1), fino a +2970 HP; Str/Vit/Mag/Spr Bonus conferiscono ciascuna +(livello − 1), fino a +99. Rimuovendo l’abilità si perde il beneficio. Con il mod attivo, i precedenti aumenti permanenti di Bonus vengono ignorati; restano quelli di oggetti e Devour. Non vengono aggiunte abilità Speed Bonus o Luck Bonus. Disattivato per impostazione predefinita; obbligatorio in Hardcore.",
            "es" => "Mientras está equipada, HP Bonus otorga +30 × (nivel − 1), hasta +2970 HP; Str/Vit/Mag/Spr Bonus otorgan cada una +(nivel − 1), hasta +99. Al quitar la habilidad se pierde su beneficio. Con el mod activo se ignoran los aumentos permanentes anteriores de Bonus; se conservan los de objetos y Devour. No se añaden habilidades Speed Bonus ni Luck Bonus. Desactivado por defecto; obligatorio en Hardcore.",
            _ => LevelBasedStatBonusesDescription
        };
    }

    public static string? LevelBasedBonusAbilityDescription(int ability, string? language)
    {
        string? stat = ability switch
        {
            65 => "HP",
            66 => "Str",
            67 => "Vit",
            68 => "Mag",
            69 => "Spr",
            _ => null
        };
        if (stat is null)
        {
            return null;
        }
        string amount = ability == 65 ? "30 × " : "";
        int cap = ability == 65 ? 2970 : 99;
        string code = (language ?? "en").Split('-', '_')[0].ToLowerInvariant();
        return code switch
        {
            "ru" => $"Пока экипирована: +{amount}(уровень − 1) {stat}, максимум +{cap}. Снятие убирает прибавку. Накопленные бонусы повышения уровня не учитываются, пока мод активен.",
            "ja" or "jp" => $"装備中のみ {stat} +{amount}(レベル − 1)、最大 +{cap}。外すと効果は失われます。この MOD が有効な間、蓄積されたレベルアップボーナスは反映されません。",
            "de" => $"Solange ausgerüstet: +{amount}(Stufe − 1) {stat}, höchstens +{cap}. Beim Ablegen entfällt der Bonus. Angesammelte Stufenaufstiegsboni werden ignoriert, solange diese Mod aktiv ist.",
            "fr" => $"Tant que la capacité est équipée : +{amount}(niveau − 1) {stat}, jusqu’à +{cap}. La retirer supprime le bonus. Les bonus de niveau accumulés sont ignorés tant que ce mod est actif.",
            "it" => $"Finché equipaggiata: +{amount}(livello − 1) {stat}, fino a +{cap}. Rimuovendola si perde il bonus. I bonus accumulati salendo di livello vengono ignorati mentre il mod è attivo.",
            "es" => $"Mientras está equipada: +{amount}(nivel − 1) {stat}, hasta +{cap}. Al quitarla se pierde el bonus. Los bonus acumulados al subir de nivel se ignoran mientras este mod está activo.",
            _ => $"While equipped: +{amount}(level − 1) {stat}, up to +{cap}. Unequipping removes the bonus. Accumulated level-up bonuses are ignored while this mod is active."
        };
    }
}
