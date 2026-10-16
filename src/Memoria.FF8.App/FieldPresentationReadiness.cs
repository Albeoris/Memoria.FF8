using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum FieldPresentationRenderer
{
    None,
    Field,
    Movie
}

internal static class FieldPresentationReadiness
{
    public static FieldPresentationRenderer SelectRenderer(SceneKind scene, bool canPresentCurrentField, bool hasDecodedMovieFrame)
    {
        if (scene != SceneKind.WorldMap && !canPresentCurrentField)
        {
            return FieldPresentationRenderer.None;
        }
        // The scene owns renderer selection. Decoder startup never admits an ordinary field draw.
        return scene switch
        {
            SceneKind.Movie => hasDecodedMovieFrame ? FieldPresentationRenderer.Movie : FieldPresentationRenderer.None,
            SceneKind.Field or SceneKind.WorldMap => FieldPresentationRenderer.Field,
            _ => FieldPresentationRenderer.None
        };
    }

    public static bool HasPendingInitialization(NativeFieldInitializationProof? initialization)
    {
        // Native52BC00 completes52C650 before ordinary field presentation.
        return initialization?.Stage is NativeFieldInitializationStage.Allocated or NativeFieldInitializationStage.Initializing;
    }
}
