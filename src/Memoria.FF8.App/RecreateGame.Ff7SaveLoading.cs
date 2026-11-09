using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private string? Ff7OnlyLoadUnavailable()
    {
        if (InFf7)
        {
            return "Return to FFVIII before loading an FFVII-only save with the current FFVIII world.";
        }
        if (world is null || session.State.Scene == SceneKind.Title)
        {
            return "Start or load an FFVIII game and reach a save-enabled boundary to preserve a world to return to.";
        }
        return Ff8WorldSwitchUnavailable();
    }

    private void LoadFf7OnlyPlayerSave(CombinedSessionSave save)
    {
        string? reason = Ff7OnlyLoadUnavailable();
        var prepared = CrossoverSaveAdapter.PrepareFf7OnlyLoad(save, new(reason is null, reason), CaptureFf8Payload, PrepareFf7Scene);
        try
        {
            StopAutoPlayForPlayerSession();
            CommitPreparedFf7Scene(prepared.Save, prepared.Target);
        }
        catch
        {
            prepared.Target.Dispose();
            throw;
        }
    }
}
