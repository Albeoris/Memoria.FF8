using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>Original A8..AB input PCs resolved against the native Junction graph, independently of the managed menu geometry.</summary>
internal static class NativeTutorialJunctionPlan
{
    public static IReadOnlyDictionary<int, NativeTutorialMenuStep> Create(int id, NativeTutorialDefinition definition, NativeCharacterVitals party)
    {
        if (id != definition.Id || id is < 0 or > 3)
        {
            throw new ArgumentException("Junction tutorial plan requires its matching original definition.");
        }
        Dictionary<int, NativeTutorialMenuStep> steps = [];
        string character = $"character-{definition.Descriptor.CharacterId}";
        void Bind(int pc, int bit, string screen, string? entry = null, NativeTutorialPress press = NativeTutorialPress.Select, MenuAction? context = null, string? destination = null)
        {
            if ((uint)pc >= definition.Inputs.Length || definition.Inputs[pc] != (0x1000 | bit))
            {
                throw new InvalidDataException($"Original tutorial {id} input at WORD {pc} does not match its native Junction plan.");
            }
            steps.Add(pc, new NativeTutorialMenuStep(screen, entry, press, character, context, destination));
        }
        void Select(int pc, int bit, string screen, string entry) => Bind(pc, bit, screen, entry);
        void Confirm(int pc, string screen, string entry, string? destination = null, MenuAction? context = null) => Bind(pc, 6, screen, entry, NativeTutorialPress.Confirm, context, destination);
        void Cancel(int pc, string screen, string destination) => Bind(pc, 4, screen, press: NativeTutorialPress.Cancel, destination: destination);
        void Stock(int pc, int bit, int inventorySlot, string junctionSlot, NativeTutorialPress press = NativeTutorialPress.Select, string? destination = null)
        {
            NativeMagicStock stock = party.Records[definition.Descriptor.CharacterId].MagicInventory[inventorySlot];
            if (stock.SpellId == 0 || stock.Count == 0)
            {
                throw new InvalidDataException($"Original tutorial {id} has no stocked magic at native inventory slot {inventorySlot}.");
            }
            Bind(pc, bit, "junction-stock", $"magic-{definition.Descriptor.CharacterId}-{stock.SpellId}", press, new MenuAction("select-slot", junctionSlot), destination);
        }
        void Slot(int pc, int bit, string screen, string slot, bool confirm = false) => Bind(pc, bit, screen, slot, confirm ? NativeTutorialPress.Confirm : NativeTutorialPress.Select);
        if (id == 0)
        {
            Confirm(58, "junction", "junction-options");
            Confirm(85, "junction-options", "junction-gf");
            Bind(101, 7, "junction-gf", "gf-0", NativeTutorialPress.Inspect, new MenuAction("select-gf", "gf-0"), "gf-abilities");
            Cancel(112, "gf-abilities", "junction-gf");
            Select(122, 14, "junction-gf", "gf-1");
            Select(124, 14, "junction-gf", "gf-2");
            Select(126, 12, "junction-gf", "gf-1");
            Select(128, 12, "junction-gf", "gf-0");
            Confirm(136, "junction-gf", "gf-0");
            Cancel(169, "junction-gf", "junction-options");
            Cancel(172, "junction-options", "junction-command");
            Select(181, 14, "junction-command", "2");
            Select(183, 14, "junction-command", "3");
            Select(185, 14, "junction-ability", "0");
            Select(194, 12, "junction-command", "3");
            Select(196, 12, "junction-command", "2");
            Select(198, 12, "junction-command", "1");
            Select(239, 14, "junction-command", "2");
            Select(241, 14, "junction-command", "3");
            Select(243, 14, "junction-ability", "0");
            Select(252, 12, "junction-command", "3");
            Select(254, 12, "junction-command", "2");
            Select(256, 12, "junction-command", "1");
            Select(265, 14, "junction-command", "2");
            Select(267, 14, "junction-command", "3");
            Select(269, 14, "junction-ability", "0");
            Select(278, 12, "junction-command", "3");
            Select(280, 12, "junction-command", "2");
            Select(282, 12, "junction-command", "1");
            Confirm(303, "junction-command", "1");
            Select(311, 14, "junction-command-choice", "21");
            Select(313, 14, "junction-command-choice", "22");
            Confirm(321, "junction-command-choice", "22", "junction-command", new MenuAction("select-command-slot", "1"));
            Select(335, 14, "junction-command", "2");
            Confirm(337, "junction-command", "2");
            Select(339, 12, "junction-command-choice", "21");
            Confirm(341, "junction-command-choice", "21", "junction-command", new MenuAction("select-command-slot", "2"));
            Select(343, 14, "junction-command", "3");
            Confirm(345, "junction-command", "3");
            Select(347, 14, "junction-command-choice", "22");
            Select(349, 14, "junction-command-choice", "23");
            Confirm(351, "junction-command-choice", "23", "junction-command", new MenuAction("select-command-slot", "3"));
            Select(372, 14, "junction-ability", "0");
            Confirm(392, "junction-ability", "0");
            Confirm(400, "junction-ability-choice", "48", "junction-ability", new MenuAction("select-ability-slot", "0"));
            Cancel(414, "junction-ability", "junction");
            Cancel(424, "junction", "junction");
            Cancel(426, "junction", "junction");
        }
        else if (id == 1)
        {
            Confirm(38, "junction", "junction-options");
            Select(58, 13, "junction-options", "junction-magic");
            Confirm(60, "junction-options", "junction-magic");
            Slot(75, 14, "junction-magic", "Str");
            Slot(77, 14, "junction-magic", "Vit");
            Slot(79, 12, "junction-magic", "Str");
            Slot(87, 6, "junction-magic", "Str", true);
            Stock(101, 14, 1, "Str");
            Stock(103, 14, 2, "Str");
            Stock(105, 12, 1, "Str");
            Stock(107, 12, 0, "Str");
            Stock(115, 6, 0, "Str", NativeTutorialPress.Confirm, "junction-magic");
            Cancel(149, "junction-magic", "junction-options");
            Cancel(151, "junction-options", "junction");
            Select(153, 13, "junction", "junction-off");
            Select(155, 13, "junction", "junction-auto");
            Confirm(163, "junction", "junction-auto");
            Select(177, 13, "junction-auto", "magic");
            Select(185, 13, "junction-auto", "defense");
            Select(205, 15, "junction-auto", "magic");
            Select(207, 15, "junction-auto", "attack");
            Confirm(209, "junction-auto", "attack", "junction");
            Cancel(223, "junction", "junction");
            Cancel(225, "junction", "junction");
            Cancel(227, "junction", "junction");
        }
        else if (id == 2)
        {
            Confirm(31, "junction", "junction-options");
            Select(33, 13, "junction-options", "junction-magic");
            Confirm(35, "junction-options", "junction-magic");
            Slot(51, 15, "junction-element", "Elem-Atk");
            Slot(74, 14, "junction-element", "Elem-Def 1");
            Slot(76, 12, "junction-element", "Elem-Atk");
            Slot(78, 6, "junction-element", "Elem-Atk", true);
            Stock(87, 14, 1, "Elem-Atk");
            Stock(89, 14, 2, "Elem-Atk");
            Stock(91, 6, 2, "Elem-Atk", NativeTutorialPress.Confirm, "junction-element");
            Slot(100, 6, "junction-element", "Elem-Atk", true);
            Stock(102, 14, 3, "Elem-Atk");
            Cancel(111, "junction-stock", "junction-element");
            Slot(152, 14, "junction-element", "Elem-Def 1");
            Slot(154, 6, "junction-element", "Elem-Def 1", true);
            Stock(156, 12, 2, "Elem-Def 1");
            Stock(158, 12, 1, "Elem-Def 1");
            Stock(160, 12, 0, "Elem-Def 1");
            Stock(162, 6, 0, "Elem-Def 1", NativeTutorialPress.Confirm, "junction-element");
            Slot(182, 6, "junction-element", "Elem-Def 1", true);
            Stock(184, 14, 1, "Elem-Def 1");
            Stock(186, 6, 1, "Elem-Def 1", NativeTutorialPress.Confirm, "junction-element");
            Slot(212, 14, "junction-element", "Elem-Def 2");
            Slot(214, 6, "junction-element", "Elem-Def 2", true);
            Stock(216, 12, 0, "Elem-Def 2");
            Stock(218, 6, 0, "Elem-Def 2", NativeTutorialPress.Confirm, "junction-element");
            Cancel(243, "junction-element", "junction-options");
            Cancel(245, "junction-options", "junction");
            Cancel(247, "junction", "junction");
        }
        else
        {
            Confirm(24, "junction", "junction-options");
            Select(26, 13, "junction-options", "junction-magic");
            Confirm(28, "junction-options", "junction-magic");
            Slot(44, 15, "junction-element", "Elem-Atk");
            Slot(46, 15, "junction-status", "ST-Atk");
            Slot(69, 14, "junction-status", "ST-Def 1");
            Slot(71, 12, "junction-status", "ST-Atk");
            Slot(73, 6, "junction-status", "ST-Atk", true);
            Stock(82, 14, 1, "ST-Atk");
            Stock(84, 12, 0, "ST-Atk");
            Stock(86, 13, 4, "ST-Atk");
            Stock(88, 6, 4, "ST-Atk", NativeTutorialPress.Confirm, "junction-status");
            Slot(97, 6, "junction-status", "ST-Atk", true);
            Stock(99, 15, 0, "ST-Atk");
            Cancel(108, "junction-stock", "junction-status");
            Slot(157, 14, "junction-status", "ST-Def 1");
            Slot(159, 6, "junction-status", "ST-Def 1", true);
            Stock(161, 13, 4, "ST-Def 1");
            Stock(163, 14, 5, "ST-Def 1");
            Stock(165, 6, 5, "ST-Def 1", NativeTutorialPress.Confirm, "junction-status");
            Slot(197, 14, "junction-status", "ST-Def 2");
            Slot(199, 6, "junction-status", "ST-Def 2", true);
            Stock(201, 13, 9, "ST-Def 2");
            Stock(209, 6, 9, "ST-Def 2", NativeTutorialPress.Confirm, "junction-status");
            Cancel(223, "junction-status", "junction-options");
            Cancel(225, "junction-options", "junction");
            Cancel(227, "junction", "junction");
        }
        int[] originalInputs = definition.Inputs.TakeWhile(word => word >> 12 != 15).Select((word, pc) => (word, pc)).Where(pair => pair.word >> 12 == 1).Select(pair => pair.pc).ToArray();
        if (!originalInputs.Order().SequenceEqual(steps.Keys.Order()))
        {
            throw new InvalidDataException($"Original tutorial {id} has an unresolved native input WORD.");
        }
        return steps;
    }
}
