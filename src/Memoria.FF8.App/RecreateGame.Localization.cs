using System.Security.Cryptography;
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
    }

    private DialogueState LocalizedDialogue(DialogueState native)
    {
        if (resourceHost is null || native.IsNamePrompt)
        {
            return native;
        }
        if (session.State.Scene == Runtime.SceneKind.WorldMap)
        {
            string key = LocalizationSourceExporter.IndexedKey(resourceHost.Resources.Source.Language, "messages_world", native.MessageId);
            string text = resourceHost.Resources.Localize(key, native.Text);
            return text == native.Text ? native : native with { Text = text };
        }
        LocalizationFieldText? presentation = ResolveLocalizedField(native);
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
        if (resourceHost is null || world?.Field is not { } field || session.State.Scene != Runtime.SceneKind.Field || native.IsNamePrompt)
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
        if (presentation is null || window is null && presentation.Width is null)
        {
            return window;
        }
        return new(native.Channel, presentation.X ?? window?.X ?? native.X, presentation.Y ?? window?.Y ?? native.Y, presentation.Width ?? window!.Width, presentation.Height ?? window!.Height);
    }

    private int LocalizedChoiceLine(DialogueState native, int nativeLine)
    {
        DialogueState display = LocalizedDialogue(native);
        return native.FirstChoice is int original && display.FirstChoice is int translated ? nativeLine + translated - original : nativeLine;
    }
}
