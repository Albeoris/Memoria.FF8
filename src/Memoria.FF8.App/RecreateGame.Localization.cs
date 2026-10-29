using System.Security.Cryptography;
using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private FieldAssets? localizedFieldOwner;
    private string? localizedFieldScriptHash;
    private string? visibleLocalizationFingerprint;
    private string? promptedLocalizationStack;

    private void ConnectLocalizationTools()
    {
        gameMenus!.ConnectLocalizationAuthoring(LocalizationProjectController.CreateServices(options.GameDirectory, Path.Combine(resourceHost!.Mods.PrimaryModsDirectory, "Memoria"), resourceHost.Mods.Refresh, () => (resourceHost.LocalizationPreview?.Layers ?? []).Select(layer => layer.Project.Manifest.TargetLanguage).ToArray(), () => resourceHost.CommittedLocalizations?.Layers ?? [], () => resourceHost.CommittedLocalizations?.RequestedOrder ?? []));
        gameMenus.ConnectLocalizationText(resourceHost.Resources.Localize);
        if (saveMenu is not null)
        {
            saveMenu.LocalizeText = resourceHost.Resources.Localize;
        }
        gameMenus.ConnectLocalizationStack(() =>
        {
            var preview = resourceHost.LocalizationPreview;
            return new LocalizationStackView((preview?.Layers ?? []).Select(layer => new LocalizationStackOption(layer.PackageId, layer.Name, layer.Project.Manifest.TargetLanguage, layer.Project.Manifest.BaseProjectId)).ToArray(), resourceHost.Mods.LocalizationOrder ?? preview?.RequestedOrder ?? [], preview?.RequiresSelection == true, resourceHost.Mods.LocalizationPrimaryTarget);
        }, (order, primaryTarget) =>
        {
            var result = resourceHost.Mods.SetLocalizationSelection(order, primaryTarget);
            if (!result.Success)
            {
                throw new IOException(result.Error);
            }
        });
    }

    private void RefreshLocalizationPresentation()
    {
        if (resourceHost?.LocalizationPreview is { RequiresSelection: true } preview)
        {
            string requested = string.Join(';', preview.Layers.Select(layer => layer.PackageId).Order(StringComparer.Ordinal));
            if (requested != promptedLocalizationStack)
            {
                promptedLocalizationStack = requested;
                gameMenus?.PromptLocalizationSelection();
            }
        }
        if (resourceHost is null || visibleLocalizationFingerprint == resourceHost.Resources.Localization.Fingerprint)
        {
            return;
        }
        visibleLocalizationFingerprint = resourceHost.Resources.Localization.Fingerprint;
        gameMenus?.InvalidateLiveData();
        nativePartyWindowUi = null;
    }

    private DialogueState LocalizedDialogue(DialogueState native)
    {
        if (resourceHost is null || native.IsNamePrompt)
        {
            return native;
        }
        if (native.MessageId < 0)
        {
            return TryGeneratedAuthoringDialogue(native, out GeneratedAuthoringDialogue? generated) ? AuthoringDialoguePreview(native, generated!.Project(native)) : native;
        }
        if (session.State.Scene == Runtime.SceneKind.WorldMap)
        {
            string key = LocalizationSourceExporter.IndexedKey(resourceHost.Resources.Source.Language, "messages_world", native.MessageId);
            string text = resourceHost.Resources.Localize(key, native.Text);
            return AuthoringDialoguePreview(native, text == native.Text ? native : native with { Text = text });
        }
        return AuthoringDialoguePreview(native, ProjectLocalizedDialogue(native, ResolveLocalizedField(native)));
    }

    internal static DialogueState ProjectLocalizedDialogue(DialogueState native, LocalizationFieldText? presentation)
    {
        return presentation is null ? native : native with
        {
            Text = presentation.Body,
            X = presentation.X ?? native.X,
            Y = presentation.Y ?? native.Y,
            FirstChoice = presentation.FirstChoice ?? native.FirstChoice,
            LastChoice = presentation.LastChoice ?? native.LastChoice,
            InitialChoice = presentation.InitialChoice ?? native.InitialChoice,
            CancelChoice = presentation.CancelChoice ?? native.CancelChoice
        };
    }

    private LocalizationFieldText? ResolveLocalizedField(DialogueState native)
    {
        // Generated/common-bank dialogue has no ordinal in the current field's message table.
        if (resourceHost is null || native.MessageId < 0 || world?.Field is not { } field || session.State.Scene != Runtime.SceneKind.Field || native.IsNamePrompt)
        {
            return null;
        }
        if (!ReferenceEquals(localizedFieldOwner, field))
        {
            localizedFieldOwner = field;
            localizedFieldScriptHash = Convert.ToHexString(SHA256.HashData(field.Jsm));
        }
        string sourceKey = FieldLocalizationKey.Create(resourceHost.Resources.Source.Language, field.Name, native.MessageId);
        return resourceHost.Resources.Localization.ResolveField(sourceKey, native.Operation, localizedFieldScriptHash!, native.FirstChoice, native.LastChoice, native.InitialChoice, native.CancelChoice);
    }

    private DialogueWindow? LocalizedWindow(DialogueState native, DialogueWindow? window)
    {
        LocalizationFieldText? presentation = ResolveLocalizedField(native);
        DialogueWindow? selected = presentation is null || window is null && presentation.Width is null
            ? window
            : new(native.Channel, presentation.X ?? window?.X ?? native.X, presentation.Y ?? window?.Y ?? native.Y, presentation.Width ?? window!.Width, presentation.Height ?? window!.Height);
        return authoringDraftPreview is not null && IsEditingAuthoringOccurrence(native) ? ProjectAuthoringWindow(native, selected, authoringDraftPreview) : selected;
    }

    internal static DialogueWindow? ProjectAuthoringWindow(DialogueState native, DialogueWindow? window, string target)
    {
        if (!FieldMessageContext.TryParseTag(0, target, out FieldMessageContext? context, out _) || context is null || context.Position is null && context.Size is null || window is null && context.Size is null)
        {
            return window;
        }
        return new(native.Channel, context.Position?.X ?? window?.X ?? native.X, context.Position?.Y ?? window?.Y ?? native.Y, context.Size?.Width ?? window!.Width, context.Size?.Height ?? window!.Height);
    }

    private int LocalizedChoiceLine(DialogueState native, int nativeLine)
    {
        DialogueState display = LocalizedDialogue(native);
        return native.FirstChoice is int original && display.FirstChoice is int translated ? nativeLine + translated - original : nativeLine;
    }
}
