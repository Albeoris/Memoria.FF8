using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Traversal priorities and prerequisites for diagnostic coordinate exploration.</summary>
internal static class AutoPlayTraversalPolicy
{
    public static int ProgressionEntryOrder(AutoPlayVisit visit, AutoPlayEvent item)
    {
        return (visit.Key, item.Id) switch
        {
            // NG-009: This authored entry follows the other reachable Garden frontiers.
            ("137:142", "137:line:15") => 1,
            // NG-010: Complete the ordinary clothing interaction before the night entrances.
            ("163:155", "163:line:17" or "163:line:18") => 1,
            ("241:155", "241:line:12") => -1,
            // NG-019: Quistis advances this phase once the ordinary approach is available.
            ("679:324", "679:talk:3") => -1,
            // NG-020: Prefer the station exits toward Caraway's guard during this phase.
            ("720:351", "720:gateway:0") => -1,
            ("752:351", "752:gateway:1") => -2,
            ("753:351", "753:gateway:1") => -3,
            ("758:350", "758:gateway:4" or "758:gateway:5" or "758:gateway:6" or "758:gateway:7" or "758:gateway:8" or "758:gateway:9") => -1,
            ("756:350" or "756:351", "756:gateway:4" or "756:gateway:5" or "756:gateway:6") => -4,
            ("760:350" or "760:351", "760:gateway:3" or "760:gateway:4" or "760:gateway:5") => -5,
            ("761:350" or "761:351", "761:talk:15") => -6,
            _ => 0
        };
    }

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
