using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record ModelBoundExternalBank(int CharacterId, ModelExternalBankDescriptor Descriptor, int SectionCount = 3, string OwnerRole = "body and independent linked weapon")
{
    public string? UnsupportedReason { get; init; }
}
public sealed record ModelExternalBankSource(ModelExternalBankDescriptor Descriptor, int SectionCount = 3, string OwnerRole = "actor body and independent linked weapon")
{
    public string? UnsupportedReason { get; init; }
}

/// <summary>Finite actor banks explicitly rebound by current native resource loaders.</summary>
public static class ModelExternalBankCatalog
{
    public static IReadOnlyList<ModelExternalBankSource> Sources { get; } = Array.AsReadOnly(ModelAssetDiscoveryBindings.SquallExternalBanks.Select(bank => new ModelExternalBankSource(bank)).Concat(new ModelExternalBankSource[]
    {
        Source("duel", 112, NativeBattleDuel112Resources.Address, 2),
        Source("duel", 212, 0x1181078, 2),
        Source("duel", 215, NativeBattleDuel215Resources.ActorDirectory, 2),
        Source("duel", 216, NativeBattleDuel216Resources.ActorDirectory, 2),
        Source("duel", 274, NativeBattleDuel274Resources.Address, 2),
        Source("shot", 188, 0xD1B4B8), Source("shot", 192, 0xD155E8), Source("shot", 193, 0xD12EC0), Source("shot", 194, 0xD0BD78),
        Source("shot", 195, 0xD07400), Source("shot", 196, 0xD05288), Source("shot", 197, 0xCFB348), Source("shot", 198, 0xCF3A78),
        Source("actor", 71, 0x1590024), Source("actor", 72, 0x158D9AC), Source("actor", 73, 0x1587F4C), Source("actor", 74, 0x15863B0),
        Source("actor", 184, 0xD328B4), Source("actor", 275, 0x10DA574), Source("actor", 297, 0x1056888), Source("actor", 299, 0x1047120),
        Source("actor", 300, 0x103DCB0), Source("actor", 305, 0xCEB5E8), Source("actor", 306, 0xCE454C), Source("actor", 307, 0xCDF86C),
        Source("actor", 317, 0xFFE6B0), Source("actor", 260, 0x111B3CC),
        Source("actor", 84, 0x1576020), Source("actor", 86, 0x156AA60), Source("actor", 87, 0x1560550), Source("actor", 88, 0x15560C0),
        Source("actor", 89, 0x1495530), new(new("actor-92", 92, 0x16C96E8), 3, "contact-selected actor body and linked weapon") { UnsupportedReason = "This loader binds the contact-selected target, whose logical model identity is unresolved without an explicit target binding; the action owner's rig is not the replacement target." },
        Source("actor", 200, 0xE2B518),
        ScriptSelected(228, 0x18821A0, 2), ScriptSelected(229, 0x188062C), ScriptSelected(230, 0x187EAB8),
        ScriptSelected(261, 0x187C20C), ScriptSelected(262, 0x187A2A4, 2), ScriptSelected(263, 0x1877DAC)
    }).ToArray());

    public static IReadOnlyList<ModelBoundExternalBank> ForCharacter(int character, IEnumerable<BattleNativeAction> actions)
    {
        if (character is < 0 or > 10)
        {
            return [];
        }
        HashSet<ushort> effects = actions.Where(action => action.CharacterId == character && (character < 6 || action.ActionId == NativeBattleLimitRules.GuestActionId(character))).Select(action => action.MagicId).ToHashSet();
        HashSet<string> squall = character == 0 ? ModelAssetDiscoveryBindings.SquallExternalBanks.Select(bank => bank.Bank).ToHashSet(StringComparer.Ordinal) : [];
        return Array.AsReadOnly(Sources.Where(source => squall.Contains(source.Descriptor.Bank) || effects.Contains(source.Descriptor.MagicId)).Select(source => new ModelBoundExternalBank(character, source.Descriptor, source.SectionCount, source.OwnerRole) { UnsupportedReason = source.UnsupportedReason }).ToArray());
    }

    private static ModelExternalBankSource Source(string family, ushort effect, uint address, int sections = 3) => new(new($"{family}-{effect}", effect, address), sections, sections == 2 ? "actor body with retained rig; no independent linked animation section" : "actor body and independent linked weapon");

    private static ModelExternalBankSource ScriptSelected(ushort effect, uint address, int sections = 3) => Source("actor", effect, address, sections) with { OwnerRole = "script-selected physical actor", UnsupportedReason = "The resource-binding opcode selects a physical actor through its program's actor table; the standalone source-to-model skeleton binding remains unresolved. Action ownership alone does not establish this binding." };
}
