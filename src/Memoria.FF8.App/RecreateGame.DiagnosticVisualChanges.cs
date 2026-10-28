using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private VisualFrameRate? diagnosticBattleImageObserver;

    private void ObserveDiagnosticBattleImage(Texture2D source, long frameId)
    {
        if (diagnosticTelemetry?.VisualChangesEnabled != true || diagnosticDriver is not { Finished: false })
        {
            DisposeDiagnosticBattleImage();
            return;
        }
        if (diagnosticBattleImageObserver is null)
        {
            diagnosticBattleImageObserver = new(GraphicsDevice);
            diagnosticBattleImageObserver.ComparisonResolved += ResolveDiagnosticBattleImage;
        }
        diagnosticBattleImageObserver.ObserveTexture(source, frameId);
    }

    private void PublishDiagnosticBattleImage(TimeSpan timestamp)
    {
        if (diagnosticBattleImageObserver is not { } observer)
        {
            return;
        }
        // EndDraw calls this only after the host successfully presents the observed image.
        if (observer.HasAwaitingPresentation)
        {
            observer.Presented(timestamp);
        }
        else
        {
            observer.ResolveCompletedPresentations();
        }
        if (diagnosticTelemetry?.VisualChangesEnabled != true || diagnosticDriver is not { Finished: false })
        {
            DisposeDiagnosticBattleImage();
        }
    }

    private void ResolveDiagnosticBattleImage(VisualFrameComparison result)
    {
        diagnosticTelemetry?.ResolveBattleImage(result.FrameOrdinal, result.Changed, result.Dropped);
    }

    private void DisposeDiagnosticBattleImage()
    {
        if (diagnosticBattleImageObserver is not { } observer)
        {
            return;
        }
        observer.ResolveCompletedPresentations();
        observer.Dispose();
        observer.ComparisonResolved -= ResolveDiagnosticBattleImage;
        diagnosticBattleImageObserver = null;
    }
}
