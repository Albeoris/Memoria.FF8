using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

internal static class NativeTutorialOtherPlan
{
    public static IReadOnlyDictionary<int, NativeTutorialMenuStep> Create(int id, NativeTutorialDefinition definition, NativeCharacterVitals party)
    {
        Dictionary<int, NativeTutorialMenuStep> result = [];
        void Bind(int pc, ushort input, string screen, string? entry = null, NativeTutorialPress press = NativeTutorialPress.Select, string? destination = null, MenuAction? context = null)
        {
            if (definition.Inputs[pc] != input)
            {
                throw new InvalidDataException($"Original tutorial{id} input changed at{pc}.");
            }
            result.Add(pc, new NativeTutorialMenuStep(screen, entry, press, Context: context, Destination: destination));
        }
        switch (id)
        {
            case 4:
                Bind(73, 0x100D, "gf", "gf-1");
                Bind(75, 0x100D, "gf", "gf-2");
                Bind(77, 0x100F, "gf", "gf-1");
                Bind(79, 0x100F, "gf", "gf-0");
                Bind(88, 0x100D, "gf", "gf-1");
                Bind(90, 0x1006, "gf", "gf-1", NativeTutorialPress.Confirm);
                // The ability list is already focused; retain the original input without confirming a learning target.
                Bind(114, 0x1006, "gf-learn");
                //4ACB70 sorts native ability IDs.4D2A00 uses11 rows per column, copying and clamping the row on horizontal selection.
                (int Pc, ushort Word, int Ability)[] abilities = [(123, 0x100E, 3), (125, 0x100E, 5), (127, 0x100E, 10), (129, 0x100E, 20), (131, 0x100E, 21), (133, 0x100D, 98), (135, 0x100C, 91), (137, 0x100C, 98), (139, 0x100C, 91), (141, 0x100D, 91), (143, 0x100F, 2), (152, 0x100E, 3), (202, 0x100E, 5), (204, 0x100E, 10)];
                IReadOnlySet<int> available = party.GuardianForceAbilities(1).Select(ability => ability.Ability).ToHashSet();
                foreach ((int position, ushort word, int ability) in abilities)
                {
                    if (!available.Contains(ability))
                    {
                        throw new InvalidDataException("Original Shiva tutorial learning catalogue changed.");
                    }
                    Bind(position, word, "gf-learn", ability.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                Bind(213, 0x1006, "gf-learn", "10", NativeTutorialPress.Confirm);
                Bind(279, 0x1004, "gf-learn", press: NativeTutorialPress.Cancel, destination: "gf");
                Bind(290, 0x1004, "gf");
                Bind(292, 0x1004, "gf", press: NativeTutorialPress.Cancel, destination: "main");
                Bind(294, 0x1004, "main", press: NativeTutorialPress.Cancel, destination: "main");
                break;
            case 5:
                StatusPages([19, 28, 39], "status-renzokuken");
                Bind(75, 0x100F, "status-renzokuken", "Auto", NativeTutorialPress.Confirm);
                Bind(91, 0x100D, "status-renzokuken", "Auto", NativeTutorialPress.Confirm);
                Bind(107, 0x100E, "status-renzokuken", "Indicator");
                Bind(116, 0x100D, "status-renzokuken", "Indicator", NativeTutorialPress.Confirm);
                Bind(130, 0x100C, "status-renzokuken", "Auto");
                Bind(132, 0x100F, "status-renzokuken", "Auto", NativeTutorialPress.Confirm);
                CloseStatus([146, 148, 150], "status-renzokuken");
                break;
            case 6:
                StatusPages([10, 12, 14], "status-duel");
                Bind(32, 0x100F, "status-duel", "Duel-Auto", NativeTutorialPress.Confirm);
                CloseStatus([53, 55], "status-duel");
                break;
            case 7:
                StatusPages([17, 19, 21], "status-angelo");
                Bind(46, 0x100E, "status-angelo", "1");
                Bind(69, 0x1006, "status-angelo", "1", NativeTutorialPress.Confirm);
                Bind(85, 0x100C, "status-angelo", "0");
                Bind(87, 0x1006, "status-angelo", "0", NativeTutorialPress.Confirm);
                Bind(96, 0x1004, "status-angelo", press: NativeTutorialPress.Cancel, destination: "status-angelo-warning");
                Bind(105, 0x1006, "status-angelo-warning", "ok", NativeTutorialPress.Confirm);
                Bind(107, 0x100E, "status-angelo", "1");
                Bind(109, 0x1006, "status-angelo", "1", NativeTutorialPress.Confirm);
                CloseStatus([132, 134], "status-angelo");
                break;
            case 8:
                Bind(3, 0x1004, "switch-party", press: NativeTutorialPress.Cancel, destination: "switch");
                Bind(40, 0x1006, "switch", "members", NativeTutorialPress.Confirm);
                Bind(42, 0x100D, "switch-party", "character-3");
                Bind(44, 0x1006, "switch-party", "character-3", NativeTutorialPress.Confirm);
                Bind(53, 0x100F, "switch-party-target", "character-1");
                Bind(55, 0x100E, "switch-party-target", "character-0");
                Bind(57, 0x100E, "switch-party-target", "character-5");
                Bind(59, 0x100C, "switch-party-target", "character-0");
                Bind(61, 0x100C, "switch-party-target", "character-1");
                Bind(63, 0x1006, "switch-party-target", "character-1", NativeTutorialPress.Confirm);
                Bind(100, 0x1004, "switch-party", press: NativeTutorialPress.Cancel, destination: "switch");
                Bind(102, 0x100D, "switch", "junction");
                Bind(153, 0x1006, "switch", "junction", NativeTutorialPress.Confirm);
                Bind(155, 0x100D, "switch-junction", "character-1");
                Bind(157, 0x1006, "switch-junction", "character-1", NativeTutorialPress.Confirm);
                Bind(166, 0x100F, "switch-junction-target", "character-3");
                Bind(180, 0x1006, "switch-junction-target", "character-3", NativeTutorialPress.Confirm);
                Bind(182, 0x100C, "switch-junction-confirm", "yes");
                Bind(184, 0x1006, "switch-junction-confirm", "yes", NativeTutorialPress.Confirm);
                Bind(198, 0x1004, "switch-junction", press: NativeTutorialPress.Cancel, destination: "switch");
                Bind(200, 0x1004, "switch", press: NativeTutorialPress.Cancel, destination: "main");
                Bind(202, 0x1004, "main", press: NativeTutorialPress.Cancel, destination: "main");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(id));
        }
        return result;

        void StatusPages(int[] positions, string limitScreen)
        {
            Bind(positions[0], 0x1006, "status", "next", NativeTutorialPress.Confirm);
            Bind(positions[1], 0x1006, "status-elements", "next", NativeTutorialPress.Confirm);
            Bind(positions[2], 0x1006, "status-gf", "next", NativeTutorialPress.Confirm, destination: limitScreen);
        }
        void CloseStatus(int[] positions, string limitScreen)
        {
            Bind(positions[0], 0x1004, limitScreen, press: NativeTutorialPress.Cancel, destination: "status");
            Bind(positions[1], 0x1004, "status", press: NativeTutorialPress.Cancel, destination: "main");
            if (positions.Length == 3)
            {
                Bind(positions[2], 0x1004, "main", press: NativeTutorialPress.Cancel, destination: "main");
            }
        }
    }
}
