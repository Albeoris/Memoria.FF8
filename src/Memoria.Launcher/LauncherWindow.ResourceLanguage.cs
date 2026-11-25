using Memoria.FF8.Platform;
using Memoria.FF8.Resources;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly Dictionary<string, Task<InstalledResourceLanguages>> installedResourceLanguages = new(LauncherCapabilities.PathComparer);
    private InstalledResourceLanguages selectedResourceLanguages = new(GameDataEdition.Classic, null, []);
    private string? resourceLanguageInstallationDirectory;
    private GameLanguageChoice? selectedLanguageChoice;
    private string? configuredResourceLanguage;
    private bool selectedInstallationPlayable;
    private int pendingResourceLanguageDiscoveries;
    private bool ResourceLanguageDiscoveryPending => pendingResourceLanguageDiscoveries > 0;
    private string? EffectiveResourceLanguage => !ResourceLanguageDiscoveryPending && installations.SelectedItem is GameInstallation selected && LauncherCapabilities.PathComparer.Equals(resourceLanguageInstallationDirectory, selected.Directory) ? configuredResourceLanguage : null;

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
        selectedResourceLanguages = available;
        resourceLanguageInstallationDirectory = selected?.Directory;
        selectedInstallationPlayable = selected is not null && GameInstallationSelection.ValidateForEngine(selected.Directory) is null && available.DetectedLanguage is not null;
        ReadSelectedLanguage();
        SetBusy(busy);
    }

    private void ReadSelectedLanguage()
    {
        LauncherSettings settings = LauncherSettings.Load(LauncherSettings.DefaultPath);
        selectedLanguageChoice = resourceLanguageInstallationDirectory is { } directory ? settings.LanguageChoiceFor(directory) : null;
        configuredResourceLanguage = resourceLanguageInstallationDirectory is { } path ? selectedResourceLanguages.Resolve(settings, path) : null;
    }
}
