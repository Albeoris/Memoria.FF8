using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class FieldPresentationReadiness
{
    public static bool HasPendingInitialization(NativeFieldInitializationProof? initialization)
    {
        // Native52BC00 completes52C650 before ordinary field presentation.
        return initialization?.Stage is NativeFieldInitializationStage.Allocated or NativeFieldInitializationStage.Initializing;
    }
}
