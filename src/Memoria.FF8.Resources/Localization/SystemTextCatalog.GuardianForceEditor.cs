namespace Memoria.FF8.Resources.Localization;

public static partial class SystemTextCatalog
{
    private static void AddGuardianForceEditorDefaults(Dictionary<string, string> defaults)
    {
        defaults.Add("guardian.editor.undo.default", "Undo");
        defaults.Add("guardian.editor.undo.swap", "Undo swap with {0}");
        defaults.Add("guardian.editor.undo.add", "Undo add");
        defaults.Add("guardian.editor.undo.replace", "Undo replace");
        defaults.Add("guardian.editor.undo.state", "Undo starting state");
        defaults.Add("guardian.editor.operation.EnsureAbility", "EnsureAbility");
        defaults.Add("guardian.editor.operation.ReplaceAbility", "ReplaceAbility");
        defaults.Add("guardian.editor.operation.SetInitialState", "SetInitialState");
        defaults.Add("guardian.editor.operation.OrderSubset", "OrderSubset");
        defaults.Add("guardian.editor.review.operation", "{0}: {1}");
        defaults.Add("guardian.editor.review.restore", "Restore inherited contribution");
        defaults.Add("guardian.editor.inherited.summary", "Restore inherited contribution for {0}. Current learned/AP progress is preserved.");
        defaults.Add("guardian.editor.inherited.absent", "absent");
        defaults.Add("guardian.editor.inherited.state", "position {0}, Start learned {1}, prerequisite {2}");
        defaults.Add("guardian.editor.common.none", "none");
        defaults.Add("guardian.editor.inherited.change", "{0}: {1} → {2}");
        defaults.Add("guardian.editor.inherited.nochange", "No inherited fields differ from the selected contribution.");
        defaults.Add("guardian.editor.boolean.true", "True");
        defaults.Add("guardian.editor.boolean.false", "False");
        defaults.Add("guardian.editor.preview.learning", "GF {0}: learning target {1} -> {2}.");
        defaults.Add("guardian.editor.preview.inactive", "GF {0}: learned ability {1} becomes inactive.");
        defaults.Add("guardian.editor.preview.first-learned", "GF {0}: ability {1} becomes learned for the first time.");
        defaults.Add("guardian.editor.preview.dormant-ap", "GF {0}: ability {1}, {2} AP retained dormant while absent from the new template.");
        defaults.Add("guardian.editor.preview.command", "Character {0}: command slot {1}, ability {2} -> {3}.");
        defaults.Add("guardian.editor.preview.passive", "Character {0}: passive slot {1}, ability {2} -> {3}.");
        defaults.Add("guardian.editor.preview.junction", "Character {0}: magic junction slot {1}, spell {2} -> {3}.");
        defaults.Add("guardian.editor.preview.hp", "Character {0}: HP {1}..{2} -> {3}..{4}.");
        defaults.Add("guardian.editor.preview.reset", "GF {0}, ability {1}: explicit reset Learned={2}, Forgotten={3}, AP={4} -> Learned={5}, Forgotten={6}, AP=0.");
        defaults.Add("guardian.editor.outcome.applied", "Applied authored contribution.");
        defaults.Add("guardian.editor.outcome.missing-producer", "The producing operation did not create this row.");
        defaults.Add("guardian.editor.outcome.existing-row", "The existing row retains its creator and starting state.");
        defaults.Add("guardian.editor.outcome.missing-ordering", "An ordering participant is absent; the entire permutation was skipped.");
        defaults.Add("guardian.editor.outcome.ordering", "Applied sequential ordering; earlier overlapping order may be overridden.");
        defaults.Add("guardian.editor.outcome.missing-ability", "The expected ability is absent; no other row was changed.");
        defaults.Add("guardian.editor.outcome.unsupported-source", "This source row has an unsupported preserved native condition and is read-only.");
        defaults.Add("guardian.editor.outcome.conflicting-start", "The inherited starting state differs from the authored guard.");
        defaults.Add("guardian.editor.outcome.replacement-exists", "The replacement already exists; neither row was removed.");
        defaults.Add("guardian.editor.outcome.condition-cycle", "Retargeted conditions would introduce a dependency cycle.");
        defaults.Add("guardian.editor.review.skipped", "Skipped: {0}");
        defaults.Add("guardian.editor.review.package", "{0}: {1}");
        defaults.Add("guardian.editor.review.imported-baseline", "Prior contributions are baked into the imported baseline. Disabling former packages cannot subtract those unknown edits.");
        defaults.Add("guardian.editor.review.remove-missing", "Remove retained missing GF contribution: {0}");
        defaults.Add("guardian.editor.review.reactivate", "Reactivate supported native documents over the observed opaque baseline after fresh reconciliation.");
        defaults.Add("guardian.editor.error.already-present", "This GF already has that ability.");
        defaults.Add("guardian.editor.error.replacement-present", "The replacement is already present. Neither entry was changed.");
        defaults.Add("guardian.editor.error.select-different", "Select two different entries.");
        defaults.Add("guardian.editor.error.swap-participants", "Both swap participants must exist.");
        defaults.Add("guardian.editor.boundary.battle", "GF changes must wait until the active or pending battle has ended.");
        defaults.Add("guardian.editor.boundary.transition", "GF abilities cannot change during a tutorial or scripted field/party transition.");
        defaults.Add("guardian.editor.boundary.idle", "GF abilities can change only while the field or normal Config menu is idle.");
    }
}
