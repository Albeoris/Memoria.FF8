using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Traversal priorities and prerequisites for diagnostic coordinate exploration.</summary>
internal static class AutoPlayTraversalPolicy
{
    internal const string MissileLauncherProgram = "89A2114D0D0CE9310CFE1ECE116B57C9DA2C8E608194D3D11CD73C179D5031AC";

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
            // NG-027: Carry the maintenance assignment between the two soldiers.
            ("781:458" or "781:460", "781:gateway:2") => -1,
            ("772:458" or "772:460", "772:talk:15") => -1,
            ("777:459", "777:talk:7") => -1,
            // NG-028: Complete the missile targeting sequence before the escape.
            ("781:461", "781:gateway:3") => -1,
            ("780:461", "780:gateway:0") => -2,
            ("779:461", "779:talk:16" or "779:line:18") => -3,
            ("779:462", "779:gateway:1" or "779:line:22") => -1,
            ("771:462", "771:line:13") => -2,
            ("771:463", "771:gateway:0") => -1,
            ("779:464" or "779:465", "779:gateway:0") => -1,
            ("780:464" or "780:465", "780:gateway:1") => -2,
            ("781:464" or "781:465", "781:gateway:1" or "781:line:15" or "781:line:16" or "781:line:20") => -3,
            ("782:464" or "782:465", "782:line:15" or "782:line:16") => -4,
            ("777:467", "777:line:11") => -1,
            ("777:468", "777:line:12") => -1,
            ("782:468", "782:line-action:14") => -2,
            ("781:468", "781:talk:10" or "781:line:17") => -1,
            ("769:474" or "769:475", "769:line-action:26") => -1,
            ("769:477", "769:gateway:0") => -1,
            ("770:477" or "770:478", "770:line-action:9") => -2,
            ("770:478", "770:gateway:1") => -1,
            ("778:478", "778:gateway:2") => -1,
            // NG-029: Continue inside Garden after the party returns from the missile base.
            ("159:490", "159:gateway:0") => -1,
            _ => 0
        };
    }

    public static int? ProgressionDialogueSelection(string key)
    {
        // NG-028: Follow the native blackout, maintenance and escape choices.
        return key switch
        {
            "771:462:7:44:264:0:2:3" => 2,
            "771:462:7:44:331:5:1:2" or "771:462:7:44:497:5:1:2" => 1,
            "779:463:8:85:1114:22:2:3" => 3,
            "779:463:8:85:1165:24:1:2" or "779:463:8:85:1195:27:1:3" => 1,
            "782:466:11:116:1042:2:1:3" or "782:466:11:116:1070:3:1:3" => 1,
            "770:478:3:19:120:6:3:6" => 4,
            _ => null
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
