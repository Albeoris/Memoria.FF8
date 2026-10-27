using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private bool ApplyMagicRulesMenuAction(MenuAction action)
    {
        if (action.Kind is "junction-magic-reassign" or "junction-magic-off-all")
        {
            NativeCharacterVitals source = session.State.CharacterVitals ?? throw new InvalidOperationException("A Junction preview requires an initialized party.");
            PlayerAction command = NativeJunctionMenuProjection.PlayerAction(action);
            NativeCharacterVitals candidate;
            if (action.Kind == "junction-magic-reassign")
            {
                candidate = source.SetJunctionMagic(command.Value, command.Quantity!.Value, int.Parse(command.Text!, System.Globalization.CultureInfo.InvariantCulture), true);
            }
            else
            {
                candidate = source.RemoveAllMagicJunctions();
            }
            List<MagicJunctionChange> removed = [];
            List<MagicHitPointChange> hp = [];
            for (int row = 0; row < 8; row++)
            {
                for (int slot = 0; slot < 19; slot++)
                {
                    int spell = source.Records[row].Junction.MagicJunctions[slot];
                    if (spell != 0 && candidate.Records[row].Junction.MagicJunctions[slot] != spell)
                    {
                        removed.Add(new(row, slot, spell));
                    }
                }
                int beforeHp = source.GetMagicMaximumHitPoints(row);
                int afterHp = candidate.GetMagicMaximumHitPoints(row);
                if (beforeHp != afterHp)
                {
                    hp.Add(new(row, beforeHp, afterHp));
                }
            }
            MagicModePreview review = new(candidate, session.State.GameplayPreferences, [], [], removed.ToArray(), hp.ToArray()) { Source = source.DeepCopy() };
            gameMenus!.ShowMagicRulesReview(DescribeMagicMigration(review), () =>
            {
                if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source), JsonSerializer.SerializeToElement(session.State.CharacterVitals)))
                {
                    throw new InvalidOperationException("The Junction preview is stale. Review the current party again.");
                }
                PlayerActionResult result = world!.Apply(command);
                if (!result.Accepted)
                {
                    throw new InvalidOperationException(result.Detail ?? "The current game state cannot apply this Junction change.");
                }
                fieldSeconds = 0;
            });
            return true;
        }
        if (action.Kind == "attack-mode")
        {
            int row = int.Parse(action.TargetId.AsSpan("character-".Length), System.Globalization.CultureInfo.InvariantCulture);
            EnsureMagicRuleBoundary();
            session.SetAttackMode(row, Enum.Parse<MagicAttackMode>(action.Value));
            return true;
        }
        if (action.Kind != "config" || action.SubjectId is not ("Shared magic stock" or "Infinite magic" or "Smart Attack"))
        {
            return false;
        }
        if (NewGameSettings.IsLocked(action.SubjectId, ReadCurrentUiPreferences().Difficulty))
        {
            throw new InvalidOperationException("Hardcore gameplay rules must remain enabled.");
        }
        bool enabled = action.Value switch { "On" => true, "Off" => false, _ => throw new ArgumentException("Magic rules require On or Off.") };
        GameplayPreferences current = session.State.Scene == SceneKind.Title ? newGameMagicPreferences : session.State.PendingGameplayPreferences ?? session.State.GameplayPreferences;
        GameplayPreferences next = action.SubjectId switch
        {
            "Shared magic stock" => current with { SharedMagicStock = enabled },
            "Infinite magic" => current with { InfiniteMagic = enabled },
            _ => current with { SmartAttack = enabled }
        };
        if (session.State.Scene == SceneKind.Title)
        {
            newGameMagicPreferences = next;
            return true;
        }
        if (session.HasActiveBattle)
        {
            session.SetGameplayPreferences(next);
            return true;
        }
        EnsureMagicRuleBoundary();
        MagicModePreview preview = session.PreviewGameplayPreferences(next);
        if (current.SharedMagicStock != next.SharedMagicStock)
        {
            gameMenus!.ShowMagicRulesReview(DescribeMagicMigration(preview), () => session.ApplyGameplayPreferences(preview));
        }
        else
        {
            session.ApplyGameplayPreferences(preview);
        }
        return true;
    }

    private bool ApplyHardcoreMenuAction(MenuAction action)
    {
        if (action.Kind != "config" || action.SubjectId != "Difficulty" || action.Value != "Hardcore" || session.State.Scene == SceneKind.Title)
        {
            return false;
        }
        if (session.State.GameplayPreferences.Difficulty == BattleDifficulty.Hardcore)
        {
            throw new InvalidOperationException("Hardcore is permanent.");
        }
        if (ActiveBattle is not null || session.HasActiveBattle || session.State.PendingBattleEncounter is not null)
        {
            throw new InvalidOperationException("Hardcore can be enabled only between battles.");
        }
        EnsureMagicRuleBoundary();
        GameplayPreferences preferences = ((session.State.PendingGameplayPreferences ?? session.State.GameplayPreferences) with { Difficulty = BattleDifficulty.Hardcore }).NormalizeForDifficulty();
        MagicModePreview preview = session.PreviewGameplayPreferences(preferences);
        if (preferences.SharedMagicStock != session.State.GameplayPreferences.SharedMagicStock)
        {
            gameMenus!.ShowMagicRulesReview(DescribeMagicMigration(preview), () => session.ApplyGameplayPreferences(preview));
        }
        else
        {
            session.ApplyGameplayPreferences(preview);
        }
        return true;
    }

    private void EnsureMagicRuleBoundary()
    {
        if (MagicRuleBoundaryUnavailableReason is { } reason)
        {
            throw new InvalidOperationException(reason);
        }
    }

    private string? MagicRuleBoundaryUnavailableReason
    {
        get
        {
            if (session.State.Scene == SceneKind.Title || session.HasActiveBattle)
            {
                return null;
            }
            if (session.MagicMutationUnavailableReason is { } reason)
            {
                return reason;
            }
            if (session.State.Scene == SceneKind.Field && events is not null && (events.Scene.NativeTutorial is not null || events.Scene.NativeMenuTutorial is not null || events.HasPendingBattle || events.PartyMenu is not null && events.PartyMenu is not { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active } || events.PartyMenu is null && (!events.IsUserControlEnabled || events.IsDialogueBlocking || !events.Scene.MenuEnabled || events.PendingTripleTriad is not null)))
            {
                return "Magic rules can change only while the field or normal Config menu is idle.";
            }
            return null;
        }
    }

    private void PresentPendingMagicModeReview()
    {
        if (session.State.PendingMagicModeReview && session.State.PendingGameplayPreferences is { } pending && !session.HasActiveBattle && session.State.PendingBattleEncounter is null && session.State.Scene is SceneKind.Field or SceneKind.WorldMap && gameMenus is not null && gameMenus.ScreenId != "mode-review")
        {
            if (MagicRuleBoundaryUnavailableReason is not null)
            {
                return;
            }
            MagicModePreview preview = session.PreviewGameplayPreferences(pending);
            if (!gameMenus.IsOpen)
            {
                gameMenus.Open("config-magic", false);
            }
            gameMenus.ShowMagicRulesReview(() => DescribeMagicMigration(preview), () => session.ApplyGameplayPreferences(preview), session.CancelPendingGameplayPreferences);
        }
    }

    private IReadOnlyList<string> DescribeMagicMigration(MagicModePreview preview)
    {
        List<string> lines = [];
        string Spell(int id) => liveMenuCatalog?.MagicName(id) ?? $"Magic {id}";
        foreach (MagicStockMove move in preview.Moves)
        {
            lines.Add(LocalizedMagicText("Moved", move.RecordId, 0, Spell(move.SpellId), move.Quantity));
        }
        foreach (MagicStockMove overflow in preview.Overflow)
        {
            lines.Add(LocalizedMagicText("Overflow", overflow.RecordId, Spell(overflow.SpellId), overflow.Quantity));
        }
        foreach (MagicJunctionChange removed in preview.RemovedJunctions)
        {
            lines.Add(LocalizedMagicText("Removed", removed.RecordId, LiveMenuData.JunctionSlotName(removed.Slot), Spell(removed.SpellId)));
        }
        foreach (MagicHitPointChange hp in preview.HitPointChanges)
        {
            lines.Add(LocalizedMagicText("HpChange", hp.RecordId, hp.PreviousMaximum, hp.Maximum));
        }
        return lines.Count == 0 ? [LocalizedMagicText("NoStockChanges")] : lines;
    }

    private string LocalizedMagicText(string id, params object?[] arguments)
    {
        string original = MagicUiText.Get(id);
        string template = resourceHost?.Resources.Localize(Memoria.FF8.Resources.Localization.SystemTextCatalog.Key("magic." + id), original) ?? original;
        return Memoria.FF8.Resources.Localization.SystemTextCatalog.Format(template, original, arguments);
    }
}
