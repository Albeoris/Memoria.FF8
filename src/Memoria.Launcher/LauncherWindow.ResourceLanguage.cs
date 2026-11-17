using Memoria.FF8.Resources;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly Dictionary<string, Task<InstalledResourceLanguages>> installedResourceLanguages = new(LauncherCapabilities.PathComparer);
    private InstalledResourceLanguages selectedResourceLanguages = new(GameDataEdition.Classic, null, []);
    private string? resourceLanguageInstallationDirectory;
    private string? configuredResourceLanguage;
    private bool loadingResourceLanguages;
    private int pendingResourceLanguageDiscoveries;
    private bool ResourceLanguageDiscoveryPending => pendingResourceLanguageDiscoveries > 0;
    private string? EffectiveResourceLanguage => !ResourceLanguageDiscoveryPending && installations.SelectedItem is GameInstallation selected && LauncherCapabilities.PathComparer.Equals(resourceLanguageInstallationDirectory, selected.Directory) ? selectedResourceLanguages.Resolve((resourceLanguage.SelectedItem as ResourceLanguageChoice)?.Code) : null;
    private string? SelectedResourceLanguageOverride => selectedResourceLanguages.NormalizeOverride((resourceLanguage.SelectedItem as ResourceLanguageChoice)?.Code);
    private bool CanSelectResourceLanguage => selectedResourceLanguages.CanSelectLanguage;

    private async Task RefreshResourceLanguagesAsync()
    {
        GameInstallation? selected = installations.SelectedItem as GameInstallation;
        InstalledResourceLanguages available = new(GameDataEdition.Classic, null, []);
        if (selected is not null)
        {
            pendingResourceLanguageDiscoveries++;
            SetBusy(busy);
            try
            {
                if (!installedResourceLanguages.TryGetValue(selected.Directory, out Task<InstalledResourceLanguages>? discovery))
                {
                    discovery = Task.Run(() => GameInstallationResourceLanguages.Discover(selected.Directory, discoveryLifetime.Token), discoveryLifetime.Token);
                    installedResourceLanguages[selected.Directory] = discovery;
                }
                available = await discovery;
            }
            catch (OperationCanceledException) when (discoveryLifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                installedResourceLanguages.Remove(selected.Directory);
                available = new(GameInstallationSelection.ResolveEdition(selected.Directory), null, []);
                if (Equals(installations.SelectedItem, selected))
                {
                    status.Text = "Could not detect installed resource languages: " + exception.Message;
                }
            }
            finally
            {
                pendingResourceLanguageDiscoveries--;
                SetBusy(busy);
            }
        }
        if (!Equals(installations.SelectedItem, selected))
        {
            return;
        }
        loadingResourceLanguages = true;
        try
        {
            selectedResourceLanguages = available;
            resourceLanguageInstallationDirectory = selected?.Directory;
            string? preferred = available.NormalizeOverride(configuredResourceLanguage);
            List<ResourceLanguageChoice> choices = [];
            if (available.Edition == GameDataEdition.Remastered && available.DetectedLanguage is string detected)
            {
                choices.Add(new(null, "Auto (" + ResourceLanguageLabel(detected) + ")"));
                choices.AddRange(available.AvailableLanguages.Select(language => new ResourceLanguageChoice(language, ResourceLanguageLabel(language))));
            }
            resourceLanguage.ItemsSource = choices;
            resourceLanguage.SelectedItem = choices.FirstOrDefault(choice => choice.Code == preferred);
            resourceLanguage.IsEnabled = !busy && available.CanSelectLanguage;
            if (resourceLanguageCard is not null)
            {
                resourceLanguageCard.IsVisible = available.CanSelectLanguage;
            }
            configuredResourceLanguage = SelectedResourceLanguageOverride;
        }
        finally
        {
            loadingResourceLanguages = false;
            SetBusy(busy);
        }
    }

    private static string ResourceLanguageLabel(string language)
    {
        return language switch
        {
            "en" => "English",
            "fr" => "French",
            "de" => "German",
            "es" => "Spanish",
            "it" => "Italian",
            _ => language
        };
    }
}
