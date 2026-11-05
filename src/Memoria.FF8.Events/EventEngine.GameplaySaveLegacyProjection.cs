using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool extractingLegacyGameplay;

    /// <summary>Reads the saved execution domain for logical extraction, without installing current renderer caches.</summary>
    public void RestoreLegacyGameplayState(JsonElement snapshot, bool restoreSharedState = true)
    {
        extractingLegacyGameplay = true;
        try
        {
            RestoreCore(snapshot, restoreSharedState);
        }
        finally
        {
            extractingLegacyGameplay = false;
        }
    }

    /// <summary>Projects represented execution owners; current resources certify their reconstructed presentation on load.</summary>
    public GameplaySaveFieldContinuation CaptureLegacyGameplayContinuation()
    {
        extractingLegacyGameplay = true;
        try
        {
            return CaptureGameplaySaveFieldContinuation();
        }
        finally
        {
            extractingLegacyGameplay = false;
        }
    }
}
