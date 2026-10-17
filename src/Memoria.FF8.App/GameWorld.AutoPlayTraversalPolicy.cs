using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Traversal priorities and prerequisites for diagnostic coordinate exploration.</summary>
internal static class AutoPlayTraversalPolicy
{
    public static int PhaseOrder(string region, int eventOrder = 0)
    {
        return region switch
        {
            "bg" => 0,
            "bc" => 10,
            "bd" => 20,
            "world" => eventOrder,
            _ => 100
        };
    }

    public static bool CanEnterField(int sourceField, int destinationField, int story, NativeExamTimerState? timer)
    {
        if (sourceField == 136 && destinationField == 129 && story < 20)
        {
            return false;
        }
        if (sourceField == 129 && destinationField == 131 && story < 30)
        {
            return timer is { TimerMemoryActivated: true, Alternatives.Length: > 0 } && timer.Alternatives.All(alternative => alternative.Remaining > 0);
        }
        return true;
    }
}
