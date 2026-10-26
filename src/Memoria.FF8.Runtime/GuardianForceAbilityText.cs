using System.Collections.Immutable;
using System.Globalization;

namespace Memoria.FF8.Runtime;

/// <summary>Presentation-only authoring prose with stable semantic identity and deferred arguments.</summary>
public sealed record GuardianForceAbilityText(string Id, string Template, ImmutableArray<object?> Arguments)
{
    public static IReadOnlyDictionary<string, string> Templates { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["guardian.editor.undo.default"] = "Undo",
        ["guardian.editor.undo.swap"] = "Undo swap with {0}",
        ["guardian.editor.undo.add"] = "Undo add",
        ["guardian.editor.undo.replace"] = "Undo replace",
        ["guardian.editor.undo.state"] = "Undo starting state",
        ["guardian.editor.operation.EnsureAbility"] = "EnsureAbility",
        ["guardian.editor.operation.ReplaceAbility"] = "ReplaceAbility",
        ["guardian.editor.operation.SetInitialState"] = "SetInitialState",
        ["guardian.editor.operation.OrderSubset"] = "OrderSubset",
        ["guardian.editor.review.operation"] = "{0}: {1}",
        ["guardian.editor.review.restore"] = "Restore inherited contribution",
        ["guardian.editor.inherited.summary"] = "Restore inherited contribution for {0}. Current learned/AP progress is preserved.",
        ["guardian.editor.inherited.absent"] = "absent",
        ["guardian.editor.inherited.state"] = "position {0}, Start learned {1}, prerequisite {2}",
        ["guardian.editor.common.none"] = "none",
        ["guardian.editor.inherited.change"] = "{0}: {1} → {2}",
        ["guardian.editor.inherited.nochange"] = "No inherited fields differ from the selected contribution.",
        ["guardian.editor.boolean.true"] = "True",
        ["guardian.editor.boolean.false"] = "False",
        ["guardian.editor.preview.learning"] = "GF {0}: learning target {1} -> {2}.",
        ["guardian.editor.preview.inactive"] = "GF {0}: learned ability {1} becomes inactive.",
        ["guardian.editor.preview.first-learned"] = "GF {0}: ability {1} becomes learned for the first time.",
        ["guardian.editor.preview.dormant-ap"] = "GF {0}: ability {1}, {2} AP retained dormant while absent from the new template.",
        ["guardian.editor.preview.command"] = "Character {0}: command slot {1}, ability {2} -> {3}.",
        ["guardian.editor.preview.passive"] = "Character {0}: passive slot {1}, ability {2} -> {3}.",
        ["guardian.editor.preview.junction"] = "Character {0}: magic junction slot {1}, spell {2} -> {3}.",
        ["guardian.editor.preview.hp"] = "Character {0}: HP {1}..{2} -> {3}..{4}.",
        ["guardian.editor.preview.reset"] = "GF {0}, ability {1}: explicit reset Learned={2}, Forgotten={3}, AP={4} -> Learned={5}, Forgotten={6}, AP=0.",
        ["guardian.editor.outcome.applied"] = "Applied authored contribution.",
        ["guardian.editor.outcome.missing-producer"] = "The producing operation did not create this row.",
        ["guardian.editor.outcome.existing-row"] = "The existing row retains its creator and starting state.",
        ["guardian.editor.outcome.missing-ordering"] = "An ordering participant is absent; the entire permutation was skipped.",
        ["guardian.editor.outcome.ordering"] = "Applied sequential ordering; earlier overlapping order may be overridden.",
        ["guardian.editor.outcome.missing-ability"] = "The expected ability is absent; no other row was changed.",
        ["guardian.editor.outcome.unsupported-source"] = "This source row has an unsupported preserved native condition and is read-only.",
        ["guardian.editor.outcome.conflicting-start"] = "The inherited starting state differs from the authored guard.",
        ["guardian.editor.outcome.replacement-exists"] = "The replacement already exists; neither row was removed.",
        ["guardian.editor.outcome.condition-cycle"] = "Retargeted conditions would introduce a dependency cycle.",
        ["guardian.editor.review.skipped"] = "Skipped: {0}",
        ["guardian.editor.review.package"] = "{0}: {1}",
        ["guardian.editor.review.imported-baseline"] = "Prior contributions are baked into the imported baseline. Disabling former packages cannot subtract those unknown edits.",
        ["guardian.editor.review.remove-missing"] = "Remove retained missing GF contribution: {0}",
        ["guardian.editor.review.reactivate"] = "Reactivate supported native documents over the observed opaque baseline after fresh reconciliation.",
        ["guardian.editor.error.already-present"] = "This GF already has that ability.",
        ["guardian.editor.error.replacement-present"] = "The replacement is already present. Neither entry was changed.",
        ["guardian.editor.error.select-different"] = "Select two different entries.",
        ["guardian.editor.error.swap-participants"] = "Both swap participants must exist.",
        ["guardian.editor.boundary.battle"] = "GF changes must wait until the active or pending battle has ended.",
        ["guardian.editor.boundary.transition"] = "GF abilities cannot change during a tutorial or scripted field/party transition.",
        ["guardian.editor.boundary.idle"] = "GF abilities can change only while the field or normal Config menu is idle.",
    }.ToImmutableDictionary(StringComparer.Ordinal);

    public static GuardianForceAbilityText Create(string id, params object?[] arguments) => new(id, Templates[id], arguments.ToImmutableArray());
    public static GuardianForceAbilityText Boolean(bool value) => Create(value ? "guardian.editor.boolean.true" : "guardian.editor.boolean.false");

    public string Format(Func<string, string, string>? localize = null)
    {
        object?[] arguments = Arguments.Select(argument => argument switch
        {
            GuardianForceAbilityText text => text.Format(localize),
            Func<string> value => value(),
            _ => argument
        }).ToArray();
        try
        {
            return string.Format(CultureInfo.CurrentCulture, localize?.Invoke(Id, Template) ?? Template, arguments);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.CurrentCulture, Template, arguments);
        }
    }
}

public sealed class GuardianForceAbilityTextException(GuardianForceAbilityText text) : InvalidOperationException(text.Format())
{
    public GuardianForceAbilityText Text { get; } = text;
}
