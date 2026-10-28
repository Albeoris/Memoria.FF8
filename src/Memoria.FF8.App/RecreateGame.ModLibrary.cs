using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private long modNotificationRevision = -1;
    private readonly Dictionary<Guid, string> modNotificationPhases = [];
    private string? pendingModNotification;

    private void UpdateModLibraryNotifications()
    {
        if (resourceHost?.ExistingModLibrary is not { } library)
        {
            return;
        }
        ModLibrarySnapshot snapshot = library.View;
        if (snapshot.Revision != modNotificationRevision)
        {
            bool first = modNotificationRevision < 0;
            modNotificationRevision = snapshot.Revision;
            foreach (ModLibraryJob job in snapshot.Jobs)
            {
                bool changed = !modNotificationPhases.TryGetValue(job.Id, out string? phase) || phase != job.Phase;
                modNotificationPhases[job.Id] = job.Phase;
                if (!first && changed && (job.CanInstall || job.CanRetry || job.Phase == "Restart required"))
                {
                    pendingModNotification = $"Mods · {job.Entry.Name}: {job.Phase}. Open Downloads for details.";
                }
            }
        }
        if (pendingModNotification is { } message && gameMenus?.IsOpen == true && !uiMovieSuspended && ActiveBattle is null)
        {
            ShowSaveNotice(message, 6);
            pendingModNotification = null;
        }
    }
}
