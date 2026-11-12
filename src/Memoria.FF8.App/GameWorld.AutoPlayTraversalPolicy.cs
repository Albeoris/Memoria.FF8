using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Traversal priorities and prerequisites for diagnostic coordinate exploration.</summary>
internal static class AutoPlayTraversalPolicy
{
    internal const string MissileLauncherProgram = "89A2114D0D0CE9310CFE1ECE116B57C9DA2C8E608194D3D11CD73C179D5031AC";

    public static int ProgressionEntryOrder(AutoPlayVisit visit, AutoPlayEvent item, int galbadiaKeys = 0)
    {
        if (visit.Key.EndsWith(":894", StringComparison.Ordinal) && GalbadiaKeyRouteOrder(galbadiaKeys, item.Id) is int keyOrder)
        {
            return keyOrder;
        }
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
            // NG-030: Follow Xu from the Garden lobby through the second floor to Cid.
            ("159:510", "159:gateway:0") => -1,
            ("160:510", "160:gateway:0") => -2,
            ("161:510", "161:gateway:0" or "161:gateway:1" or "161:gateway:2") => -3,
            ("162:510", "162:gateway:3" or "162:gateway:4" or "162:gateway:5") => -4,
            ("163:510", "163:line:16") => -5,
            ("167:510", "167:gateway:5") => -6,
            ("172:510", "172:line:12") => -7,
            ("137:510", "137:line:15") => -8,
            ("142:510", "142:gateway:0") => -9,
            ("143:514", "143:line:13") => -1,
            ("142:514", "142:gateway:1") => -2,
            ("137:514", "137:line:14") => -3,
            ("249:518", "249:line:12") => -1,
            ("251:518", "251:talk:9") => -2,
            // NG-031: Open the MD level valve before taking the lower ladder.
            ("194:530", "194:gateway:0") => -1,
            ("195:530", "195:line-action:7") => -2,
            ("196:530", "196:gateway:0") => -3,
            ("197:530", "197:line-action:7" or "197:line-action:8") => -4,
            ("197:531", "197:gateway:0") => -1,
            ("196:531", "196:line-action:8") => -2,
            // NG-032: Operate the MD control before descending from the reunion room.
            ("204:539", "204:line-action:8") => -1,
            ("204:540", "204:line-action:7") => -1,
            ("205:540", "205:line-action:11") => -2,
            ("205:541", "205:line:9") => -1,
            ("205:542", "205:gateway:0") => -1,
            ("206:542", "206:line-action:7") => -2,
            // NG-033: Activate the MD console using the center switch, then the left switch.
            ("209:550", "209:line-action:6") => -1,
            ("209:554", "209:line-action:7") => -1,
            // NG-034: Inspect the exterior before taking the bridge lift.
            ("254:560", "254:line:9") => -1,
            ("249:560", "249:line:11") => -2,
            ("137:560", "137:line:15") => -3,
            ("139:560", "139:gateway:0") => -4,
            ("143:560", "143:line:15") => -5,
            // NG-035: Follow the faculty summons to the basement.
            ("163:575", "163:line:16") => -1,
            ("167:575", "167:gateway:5") => -2,
            ("172:575", "172:line:12") => -3,
            ("192:600", "192:line:13") => -1,
            // NG-036: Visit the infirmary after NORG, then return to the lobby for Xu.
            ("163:610", "163:line:16") => -1,
            ("167:610", "167:gateway:3") => -2,
            ("168:610", "168:line:24") => -3,
            ("179:610", "179:talk:10") => -4,
            ("179:612", "179:line:12") => -1,
            ("168:612", "168:line:22") => -2,
            ("167:612", "167:line:20" or "167:line:21") => -3,
            ("163:614", "163:line:16") => -1,
            ("167:614", "167:gateway:5") => -2,
            ("172:614", "172:line:12") => -3,
            ("137:614", "137:line:15") => -4,
            ("139:614" or "142:614", "139:gateway:0" or "142:gateway:0") => -5,
            ("143:614", "143:line:15") => -6,
            // NG-038: Follow Cid's request to find Ellone in the library.
            ("163:616", "163:line:16") => -1,
            ("167:616", "167:gateway:4") => -2,
            ("171:616", "171:line:20") => -3,
            ("145:616", "145:line:23" or "145:line:24") => -4,
            ("147:616", "147:line:17" or "147:line:18") => -4,
            // NG-039, NG-040: Use the second-floor deck to reach Fishermans Horizon.
            ("163:639" or "163:640", "163:line:16") => -1,
            ("171:639" or "171:640", "171:line:19") => -1,
            ("174:639" or "174:640", "174:gateway:0") => -1,
            ("168:639" or "168:640", "168:line:22") => -2,
            ("167:639" or "167:640", "167:gateway:5") => -3,
            ("172:639" or "172:640", "172:line:12") => -4,
            ("254:639" or "254:640", "254:line:9") => -3,
            ("249:639" or "249:640", "249:line:11") => -4,
            ("137:639" or "137:640", "137:line:15") => -5,
            ("139:639" or "142:639" or "139:640" or "142:640", "139:gateway:0" or "142:gateway:0") => -6,
            ("143:639" or "143:640", "143:line:15") => -7,
            // NG-040: Follow the dock guide's directions to the mayor.
            ("601:640", "601:gateway:0") => -8,
            ("630:640", "630:line:7") => -9,
            ("625:640", "625:line:16" or "625:talk:18") => -10,
            ("600:640", "600:gateway:0") => -11,
            ("610:640", "610:gateway:1") => -12,
            ("619:640", "619:gateway:0") => -13,
            ("632:640", "632:gateway:0") => -14,
            ("636:640", "636:gateway:0") => -15,
            ("637:640", "637:line:8") => -16,
            // NG-041: Rejoin Selphie and Irvine on the stage after returning to Garden.
            ("174:655", "174:gateway:0") => -1,
            ("168:655", "168:line:23") => -2,
            ("185:655", "185:line:11") => -3,
            ("184:655", "184:talk:4") => -4,
            // NG-042: The original field scripts enable the current performer's approval actor.
            ("633:692", "633:talk:3" or "633:talk:6" or "633:talk:8" or "633:talk:9") => -1,
            // NG-043: Return to the bridge for the departure briefing.
            ("245:743", "245:line:13") => -1,
            ("145:743", "145:line:21") => -1,
            ("147:743", "147:line:15") => -1,
            ("174:743", "174:gateway:0") => -2,
            ("171:743", "171:line:19") => -2,
            ("168:743", "168:line:22") => -3,
            ("163:743", "163:line:16") => -3,
            ("167:743", "167:gateway:5") => -4,
            ("172:743", "172:line:12") => -5,
            ("249:743", "249:line:12") => -6,
            ("254:743", "254:talk:6") => -7,
            ("257:743", "257:talk:8") => -8,
            // NG-045: Learn the guard's refusal, then approach the two native contact lines in order.
            ("107:750", "107:talk:10") => -4,
            ("107:750", "107:line:13") => -3,
            ("107:750", "107:line:12") => -2,
            ("107:750", "107:gateway:0" or "107:gateway:1") => -1,
            // NG-048: Follow the battle orders to the quad, then meet Zell and return by the native bridge event.
            ("173:852", "173:gateway:0" or "173:gateway:1" or "173:gateway:2") => -1,
            ("166:852", "166:gateway:3") => -2,
            ("169:852", "169:gateway:1") => -3,
            ("173:854", "173:gateway:0" or "173:gateway:1" or "173:gateway:2") => -1,
            ("166:854", "166:gateway:3") => -2,
            ("169:854", "169:gateway:1") => -3,
            ("185:854", "185:line:11") => -4,
            ("188:854", "188:push:1") => -5,
            ("188:856", "188:line:7") => -5,
            // NG-049: Follow the rescue order through the 2F elevator and classroom.
            ("164:874" or "164:876" or "164:878", "164:gateway:0" or "164:gateway:1") => -1,
            ("185:874" or "185:876" or "185:878", "185:line:10" or "185:gateway:1") => -2,
            ("169:874" or "169:876" or "169:878", "169:gateway:0") => -3,
            ("166:874" or "166:876" or "166:878", "166:gateway:4") => -4,
            ("173:874" or "173:876" or "173:878", "173:line:6") => -5,
            ("138:874" or "138:876" or "138:878", "138:gateway:0") => -6,
            ("140:874" or "140:876" or "140:878", "140:gateway:1" or "140:line:15") => -7,
            ("233:880", "233:talk:8") => -8,
            ("233:882", "233:gateway:0") => -8,
            _ => 0
        };
    }

    internal static int? GalbadiaKeyRouteOrder(int keys, string eventId)
    {
        // NG-051: Use the original approaches gated by the card keys already awarded by the field scripts.
        return (keys, eventId) switch
        {
            (0, "682:gateway:1" or "696:gateway:1" or "695:gateway:3" or "694:gateway:3" or "698:gateway:0" or "707:talk:6") => -1,
            (0x10, "707:line:7" or "698:gateway:2" or "694:gateway:0" or "695:gateway:0" or "695:gateway:1" or "695:gateway:2" or "696:gateway:3" or "682:gateway:0" or "699:line:6" or "699:gateway:3" or "683:gateway:0" or "683:gateway:1" or "683:gateway:2" or "684:gateway:4" or "700:gateway:2" or "703:talk:9") => -1,
            (0x30, "703:line:10" or "700:gateway:1" or "684:gateway:0" or "684:gateway:1" or "684:gateway:2" or "683:gateway:3" or "699:gateway:1" or "682:gateway:1" or "696:gateway:1" or "695:gateway:3" or "694:gateway:1" or "693:line:6" or "693:gateway:0" or "709:gateway:1" or "711:line:6" or "680:gateway:1" or "681:gateway:1" or "690:gateway:2" or "686:gateway:1" or "685:gateway:1" or "697:gateway:2" or "706:talk:6") => -1,
            (0x70, "706:line:7" or "697:gateway:0" or "685:gateway:0" or "686:gateway:0" or "690:gateway:1" or "691:gateway:0" or "676:line:6" or "714:line:9") => -1,
            _ => null
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
            // NG-036: Accept the doctor's offer to speak with Cid now.
            "179:610:10:66:1085:17:2:3" => 3,
            // NG-049: Go to the second floor to evacuate the classroom.
            "173:874:6:55:289:3:0:1" or "173:876:6:55:289:3:0:1" or "173:878:6:55:289:3:0:1" => 0,
            // NG-050: Open the emergency exit and use the original assisted retry if needed.
            "144:892:9:70:1307:9:1:3" => 3,
            "144:892:9:70:1357:11:0:3" or "95:892:9:37:379:8:0:2" => 1,
            "675:892:0:13:147:26:2:3" or "675:893:0:13:147:26:2:3" => 3,
            // NG-036: Take the elevator to 2F for the approaching ship.
            "172:614:12:98:1227:4:2:4" => 3,
            // NG-039, NG-040: Take the elevator to 2F for Fishermans Horizon.
            "172:639:12:98:1227:4:2:4" or "249:639:11:89:902:2:2:3" or "172:640:12:98:1227:4:2:4" or "249:640:11:89:902:2:2:3" => 3,
            // NG-040: Take the town elevator down when the operator offers.
            "625:640:18:207:2293:19:2:3" or "625:640:18:207:2227:19:2:3" => 2,
            // NG-042: Accept the current performance and continue the original rehearsal.
            "633:692:6:33:848:31:2:3" or "633:692:3:20:190:32:2:3" or "633:692:8:42:1641:33:2:3" or "633:692:9:46:2101:34:2:3" => 2,
            // NG-043: Select 3F and confirm readiness when Nida asks.
            "172:743:12:98:1227:4:2:4" => 4,
            "257:743:8:84:3013:60:2:3" => 2,
            _ => null
        };
    }

    internal static int? ConcertInstrumentSlot(string key)
    {
        // NG-042: The original scripts store visible instrument values at bytes346–349.
        return key switch
        {
            "635:692:5:42:398:27:1:8" or "633:692:6:33:917:27:1:8" => 0,
            "633:692:6:33:870:28:1:8" or "633:692:3:20:269:28:1:8" => 1,
            "633:692:3:20:212:29:1:8" or "633:692:8:42:1726:29:1:8" => 2,
            "633:692:8:42:1663:30:1:8" or "633:692:9:46:2151:30:1:8" => 3,
            _ => null
        };
    }

    internal static int ConcertInstrumentSelection(IReadOnlyCollection<int> accepted)
    {
        int[] preferred = [1, 5, 6, 8, 2, 3, 4, 7];
        return preferred.First(instrument => !accepted.Contains(instrument));
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
