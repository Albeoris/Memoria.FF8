using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private readonly AutoSaveScheduler autoSave = new();
    internal Action<AutoSaveKind, int, CombinedSessionSave>? WriteAutoSave { get; set; }

    private static AutoSaveLocation ReadAutoSaveLocation(Ff7Session current)
    {
        return current.World is { } world ? new(-1 - (int)world.Resources.World) : new(current.MapId, current.FieldGeneration);
    }

    private void UpdateAutoSave(TimeSpan elapsed, Ff7Session current)
    {
        if (WriteAutoSave is null)
        {
            return;
        }

        UiPreferences preferences = PresentationPreferences;
        autoSave.Update(preferences, ReadAutoSaveLocation(current), IsMenuPresentation ? TimeSpan.Zero : elapsed);
        if (autoSave.Pending is not null && CanSaveMemoria)
        {
            autoSave.TrySave(kind => WriteAutoSave(kind, preferences.AutoSaveCount, CapturePlayerSave(0)));
        }
    }
}
