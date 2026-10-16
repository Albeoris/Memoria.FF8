using System.Text;
using Albeoris.Games.FF8.KernelBin.Abstractions;
using Albeoris.Games.FF8.KernelBin.Internal;
using Albeoris.Games.FF8.KernelBin.Internal.Raw;

namespace Albeoris.Games.FF8.KernelBin;

/// <summary>
/// The full contents of a Final Fantasy VIII <c>kernel.bin</c> file, in memory. Use
/// <see cref="ReadFromArray"/> to parse an existing file content and
/// <see cref="WriteToArray"/> to save changes back to a file that
/// the game can read.
/// </summary>
public sealed class KernelData
{
    public List<BattleCommand> BattleCommands { get; private set; } = [];
    public List<MagicSpell> MagicSpells { get; private set; } = [];
    public List<Guardian> Guardians { get; private set; } = [];
    public List<EnemyAttack> EnemyAttacks { get; private set; } = [];
    public List<Weapon> Weapons { get; private set; } = [];
    public List<SquallLimit> SquallLimits { get; private set; } = [];
    public List<Character> Characters { get; private set; } = [];
    public List<BattleItem> BattleItems { get; private set; } = [];
    public List<FieldItem> FieldItems { get; private set; } = [];
    public List<IndependentGuardianAttack> IndependentGuardianAttacks { get; private set; } = [];
    public List<AdditionalCommand> AdditionalCommands { get; private set; } = [];
    public List<JunctionAbility> JunctionAbilities { get; private set; } = [];
    public List<CommandAbility> CommandAbilities { get; private set; } = [];
    public List<CharacterStatAbility> CharacterStatAbilities { get; private set; } = [];
    public List<CharacterAbility> CharacterAbilities { get; private set; } = [];
    public List<PartyAbility> PartyAbilities { get; private set; } = [];
    public List<GuardianAbility> GuardianAbilities { get; private set; } = [];
    public List<MenuAbility> MenuAbilities { get; private set; } = [];
    public List<NpcLimit> NpcLimits { get; private set; } = [];
    public List<BlueMagic> BlueMagics { get; private set; } = [];
    public List<QuistisLimit> QuistisLimits { get; private set; } = [];
    public List<IrvinLimit> IrvinLimits { get; private set; } = [];
    public List<ZellLimit> ZellLimits { get; private set; } = [];
    public List<ZellDuelMove> ZellDuelMoves { get; private set; } = [];
    public List<RinoaLimit> RinoaLimits { get; private set; } = [];
    public List<RinoaAngeloAttack> RinoaAngeloAttacks { get; private set; } = [];
    public List<Byte> SelphieSlotIds { get; private set; } = [];
    public List<SelphieSlotSet> SelphieSlotSets { get; private set; } = [];
    public List<DevourEffect> DevourEffects { get; private set; } = [];
    public TimerSettings TimerSettings { get; private set; } = new();

    /// <summary>Miscellaneous text entries not directly attached to any other section.</summary>
    public List<String?> MiscTexts { get; private set; } = [];

    /// <summary>Reads and parses a kernel.bin file from a stream.</summary>
    public static KernelData ReadFromArray(Byte[] content, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(content);

        KernelSectionReader reader = new(content);

        BattleCommandRaw[] battleCommandsRaw = reader.ReadStructArray<BattleCommandRaw>();
        MagicRaw[] magicRaw = reader.ReadStructArray<MagicRaw>();
        GuardianRaw[] guardiansRaw = reader.ReadStructArray<GuardianRaw>();
        EnemyAttackRaw[] enemyAttacksRaw = reader.ReadStructArray<EnemyAttackRaw>();
        WeaponRaw[] weaponsRaw = reader.ReadStructArray<WeaponRaw>();
        SquallLimitRaw[] squallLimitsRaw = reader.ReadStructArray<SquallLimitRaw>();
        CharacterRaw[] charactersRaw = reader.ReadStructArray<CharacterRaw>();
        BattleItemRaw[] battleItemsRaw = reader.ReadStructArray<BattleItemRaw>();
        FieldItemRaw[] fieldItemsRaw = reader.ReadStructArray<FieldItemRaw>();
        IndependentGuardianAttackRaw[] independentGuardianAttacksRaw = reader.ReadStructArray<IndependentGuardianAttackRaw>();
        AdditionalCommandRaw[] additionalCommandsRaw = reader.ReadStructArray<AdditionalCommandRaw>();
        JunctionAbilityRaw[] junctionAbilitiesRaw = reader.ReadStructArray<JunctionAbilityRaw>();
        CommandAbilityRaw[] commandAbilitiesRaw = reader.ReadStructArray<CommandAbilityRaw>();
        CharacterStatAbilityRaw[] characterStatAbilitiesRaw = reader.ReadStructArray<CharacterStatAbilityRaw>();
        CharacterAbilityRaw[] characterAbilitiesRaw = reader.ReadStructArray<CharacterAbilityRaw>();
        PartyAbilityRaw[] partyAbilitiesRaw = reader.ReadStructArray<PartyAbilityRaw>();
        GuardianAbilityRaw[] guardianAbilitiesRaw = reader.ReadStructArray<GuardianAbilityRaw>();
        MenuAbilityRaw[] menuAbilitiesRaw = reader.ReadStructArray<MenuAbilityRaw>();
        NpcLimitRaw[] npcLimitsRaw = reader.ReadStructArray<NpcLimitRaw>();
        BlueMagicRaw[] blueMagicsRaw = reader.ReadStructArray<BlueMagicRaw>();
        QuistisLimitRaw[] quistisLimitsRaw = reader.ReadStructArray<QuistisLimitRaw>();
        IrvinLimitRaw[] irvinLimitsRaw = reader.ReadStructArray<IrvinLimitRaw>();
        ZellLimitRaw[] zellLimitsRaw = reader.ReadStructArray<ZellLimitRaw>();
        ZellDuelMoveRaw[] zellDuelMovesRaw = reader.ReadStructArray<ZellDuelMoveRaw>();
        RinoaLimitRaw[] rinoaLimitsRaw = reader.ReadStructArray<RinoaLimitRaw>();
        RinoaAngeloAttackRaw[] rinoaAngeloAttacksRaw = reader.ReadStructArray<RinoaAngeloAttackRaw>();
        Byte[] selphieSlotIdsRaw = reader.ReadStructArray<Byte>();
        SelphieSlotSetRaw[] selphieSlotSetsRaw = reader.ReadStructArray<SelphieSlotSetRaw>();
        DevourEffectRaw[] devourEffectsRaw = reader.ReadStructArray<DevourEffectRaw>();
        TimerSettingsRaw[] timerSettingsRaw = reader.ReadStructArray<TimerSettingsRaw>();
        UInt16[] miscPointers = reader.ReadStructArray<UInt16>();

        KernelTextBlobReader battleCommandsText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader magicText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader guardiansText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader enemyAttacksText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader weaponsText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader squallLimitsText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader charactersText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader battleItemsText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader fieldItemsText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader independentGuardianAttacksText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader junctionAbilitiesText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader commandAbilitiesText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader characterStatAbilitiesText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader characterAbilitiesText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader partyAbilitiesText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader guardianAbilitiesText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader menuAbilitiesText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader npcLimitsText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader blueMagicsText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader irvinLimitsText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader zellLimitsText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader rinoaLimitsText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader rinoaAngeloAttacksText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader devourEffectsText = reader.ReadTextBlob(encoding);
        KernelTextBlobReader miscText = reader.ReadTextBlob(encoding);

        KernelData data = new();
        data.BattleCommands = KernelSerializer.ReadBattleCommands(battleCommandsRaw, battleCommandsText);
        data.MagicSpells = KernelSerializer.ReadMagicSpells(magicRaw, magicText);
        data.Guardians = KernelSerializer.ReadGuardians(guardiansRaw, guardiansText);
        data.EnemyAttacks = KernelSerializer.ReadEnemyAttacks(enemyAttacksRaw, enemyAttacksText);
        data.Weapons = KernelSerializer.ReadWeapons(weaponsRaw, weaponsText);
        data.SquallLimits = KernelSerializer.ReadSquallLimits(squallLimitsRaw, squallLimitsText);
        data.Characters = KernelSerializer.ReadCharacters(charactersRaw, charactersText);
        data.BattleItems = KernelSerializer.ReadBattleItems(battleItemsRaw, battleItemsText);
        data.FieldItems = KernelSerializer.ReadFieldItems(fieldItemsRaw, fieldItemsText);
        data.IndependentGuardianAttacks = KernelSerializer.ReadIndependentGuardianAttacks(independentGuardianAttacksRaw, independentGuardianAttacksText);
        data.AdditionalCommands = KernelSerializer.ReadAdditionalCommands(additionalCommandsRaw);
        data.JunctionAbilities = KernelSerializer.ReadJunctionAbilities(junctionAbilitiesRaw, junctionAbilitiesText);
        data.CommandAbilities = KernelSerializer.ReadCommandAbilities(commandAbilitiesRaw, commandAbilitiesText);
        data.CharacterStatAbilities = KernelSerializer.ReadCharacterStatAbilities(characterStatAbilitiesRaw, characterStatAbilitiesText);
        data.CharacterAbilities = KernelSerializer.ReadCharacterAbilities(characterAbilitiesRaw, characterAbilitiesText);
        data.PartyAbilities = KernelSerializer.ReadPartyAbilities(partyAbilitiesRaw, partyAbilitiesText);
        data.GuardianAbilities = KernelSerializer.ReadGuardianAbilities(guardianAbilitiesRaw, guardianAbilitiesText);
        data.MenuAbilities = KernelSerializer.ReadMenuAbilities(menuAbilitiesRaw, menuAbilitiesText);
        data.NpcLimits = KernelSerializer.ReadNpcLimits(npcLimitsRaw, npcLimitsText);
        data.BlueMagics = KernelSerializer.ReadBlueMagics(blueMagicsRaw, blueMagicsText);
        data.QuistisLimits = KernelSerializer.ReadQuistisLimits(quistisLimitsRaw);
        data.IrvinLimits = KernelSerializer.ReadIrvinLimits(irvinLimitsRaw, irvinLimitsText);
        data.ZellLimits = KernelSerializer.ReadZellLimits(zellLimitsRaw, zellLimitsText);
        data.ZellDuelMoves = KernelSerializer.ReadZellDuelMoves(zellDuelMovesRaw);
        data.RinoaLimits = KernelSerializer.ReadRinoaLimits(rinoaLimitsRaw, rinoaLimitsText);
        data.RinoaAngeloAttacks = KernelSerializer.ReadRinoaAngeloAttacks(rinoaAngeloAttacksRaw, rinoaAngeloAttacksText);
        data.SelphieSlotIds = [.. selphieSlotIdsRaw];
        data.SelphieSlotSets = KernelSerializer.ReadSelphieSlotSets(selphieSlotSetsRaw);
        data.DevourEffects = KernelSerializer.ReadDevourEffects(devourEffectsRaw, devourEffectsText);
        data.TimerSettings = KernelSerializer.ReadTimerSettings(timerSettingsRaw);
        data.MiscTexts = KernelSerializer.ReadMiscTexts(miscPointers, miscText);
        return data;
    }

    /// <summary>Writes this data to a stream as a kernel.bin file, recalculating all offsets.</summary>
    public Byte[] WriteToArray(Encoding encoding)
    {
        KernelTextBlobWriter battleCommandsText = new(encoding);
        KernelTextBlobWriter magicText = new(encoding);
        KernelTextBlobWriter guardiansText = new(encoding);
        KernelTextBlobWriter enemyAttacksText = new(encoding);
        KernelTextBlobWriter weaponsText = new(encoding);
        KernelTextBlobWriter squallLimitsText = new(encoding);
        KernelTextBlobWriter charactersText = new(encoding);
        KernelTextBlobWriter battleItemsText = new(encoding);
        KernelTextBlobWriter fieldItemsText = new(encoding);
        KernelTextBlobWriter independentGuardianAttacksText = new(encoding);
        KernelTextBlobWriter junctionAbilitiesText = new(encoding);
        KernelTextBlobWriter commandAbilitiesText = new(encoding);
        KernelTextBlobWriter characterStatAbilitiesText = new(encoding);
        KernelTextBlobWriter characterAbilitiesText = new(encoding);
        KernelTextBlobWriter partyAbilitiesText = new(encoding);
        KernelTextBlobWriter guardianAbilitiesText = new(encoding);
        KernelTextBlobWriter menuAbilitiesText = new(encoding);
        KernelTextBlobWriter npcLimitsText = new(encoding);
        KernelTextBlobWriter blueMagicsText = new(encoding);
        KernelTextBlobWriter irvinLimitsText = new(encoding);
        KernelTextBlobWriter zellLimitsText = new(encoding);
        KernelTextBlobWriter rinoaLimitsText = new(encoding);
        KernelTextBlobWriter rinoaAngeloAttacksText = new(encoding);
        KernelTextBlobWriter devourEffectsText = new(encoding);
        KernelTextBlobWriter miscText = new(encoding);

        BattleCommandRaw[] battleCommandsRaw = KernelSerializer.WriteBattleCommands(BattleCommands, battleCommandsText);
        MagicRaw[] magicRaw = KernelSerializer.WriteMagicSpells(MagicSpells, magicText);
        GuardianRaw[] guardiansRaw = KernelSerializer.WriteGuardians(Guardians, guardiansText);
        EnemyAttackRaw[] enemyAttacksRaw = KernelSerializer.WriteEnemyAttacks(EnemyAttacks, enemyAttacksText);
        WeaponRaw[] weaponsRaw = KernelSerializer.WriteWeapons(Weapons, weaponsText);
        SquallLimitRaw[] squallLimitsRaw = KernelSerializer.WriteSquallLimits(SquallLimits, squallLimitsText);
        CharacterRaw[] charactersRaw = KernelSerializer.WriteCharacters(Characters, charactersText);
        BattleItemRaw[] battleItemsRaw = KernelSerializer.WriteBattleItems(BattleItems, battleItemsText);
        FieldItemRaw[] fieldItemsRaw = KernelSerializer.WriteFieldItems(FieldItems, fieldItemsText);
        IndependentGuardianAttackRaw[] independentGuardianAttacksRaw = KernelSerializer.WriteIndependentGuardianAttacks(IndependentGuardianAttacks, independentGuardianAttacksText);
        AdditionalCommandRaw[] additionalCommandsRaw = KernelSerializer.WriteAdditionalCommands(AdditionalCommands);
        JunctionAbilityRaw[] junctionAbilitiesRaw = KernelSerializer.WriteJunctionAbilities(JunctionAbilities, junctionAbilitiesText);
        CommandAbilityRaw[] commandAbilitiesRaw = KernelSerializer.WriteCommandAbilities(CommandAbilities, commandAbilitiesText);
        CharacterStatAbilityRaw[] characterStatAbilitiesRaw = KernelSerializer.WriteCharacterStatAbilities(CharacterStatAbilities, characterStatAbilitiesText);
        CharacterAbilityRaw[] characterAbilitiesRaw = KernelSerializer.WriteCharacterAbilities(CharacterAbilities, characterAbilitiesText);
        PartyAbilityRaw[] partyAbilitiesRaw = KernelSerializer.WritePartyAbilities(PartyAbilities, partyAbilitiesText);
        GuardianAbilityRaw[] guardianAbilitiesRaw = KernelSerializer.WriteGuardianAbilities(GuardianAbilities, guardianAbilitiesText);
        MenuAbilityRaw[] menuAbilitiesRaw = KernelSerializer.WriteMenuAbilities(MenuAbilities, menuAbilitiesText);
        NpcLimitRaw[] npcLimitsRaw = KernelSerializer.WriteNpcLimits(NpcLimits, npcLimitsText);
        BlueMagicRaw[] blueMagicsRaw = KernelSerializer.WriteBlueMagics(BlueMagics, blueMagicsText);
        QuistisLimitRaw[] quistisLimitsRaw = KernelSerializer.WriteQuistisLimits(QuistisLimits);
        IrvinLimitRaw[] irvinLimitsRaw = KernelSerializer.WriteIrvinLimits(IrvinLimits, irvinLimitsText);
        ZellLimitRaw[] zellLimitsRaw = KernelSerializer.WriteZellLimits(ZellLimits, zellLimitsText);
        ZellDuelMoveRaw[] zellDuelMovesRaw = KernelSerializer.WriteZellDuelMoves(ZellDuelMoves);
        RinoaLimitRaw[] rinoaLimitsRaw = KernelSerializer.WriteRinoaLimits(RinoaLimits, rinoaLimitsText);
        RinoaAngeloAttackRaw[] rinoaAngeloAttacksRaw = KernelSerializer.WriteRinoaAngeloAttacks(RinoaAngeloAttacks, rinoaAngeloAttacksText);
        Byte[] selphieSlotIdsRaw = [.. SelphieSlotIds];
        SelphieSlotSetRaw[] selphieSlotSetsRaw = KernelSerializer.WriteSelphieSlotSets(SelphieSlotSets);
        DevourEffectRaw[] devourEffectsRaw = KernelSerializer.WriteDevourEffects(DevourEffects, devourEffectsText);
        TimerSettingsRaw[] timerSettingsRaw = KernelSerializer.WriteTimerSettings(TimerSettings);
        UInt16[] miscPointers = KernelSerializer.WriteMiscTexts(MiscTexts, miscText);

        KernelSectionWriter writer = new();
        writer.AddStructArray(battleCommandsRaw);
        writer.AddStructArray(magicRaw);
        writer.AddStructArray(guardiansRaw);
        writer.AddStructArray(enemyAttacksRaw);
        writer.AddStructArray(weaponsRaw);
        writer.AddStructArray(squallLimitsRaw);
        writer.AddStructArray(charactersRaw);
        writer.AddStructArray(battleItemsRaw);
        writer.AddStructArray(fieldItemsRaw);
        writer.AddStructArray(independentGuardianAttacksRaw);
        writer.AddStructArray(additionalCommandsRaw);
        writer.AddStructArray(junctionAbilitiesRaw);
        writer.AddStructArray(commandAbilitiesRaw);
        writer.AddStructArray(characterStatAbilitiesRaw);
        writer.AddStructArray(characterAbilitiesRaw);
        writer.AddStructArray(partyAbilitiesRaw);
        writer.AddStructArray(guardianAbilitiesRaw);
        writer.AddStructArray(menuAbilitiesRaw);
        writer.AddStructArray(npcLimitsRaw);
        writer.AddStructArray(blueMagicsRaw);
        writer.AddStructArray(quistisLimitsRaw);
        writer.AddStructArray(irvinLimitsRaw);
        writer.AddStructArray(zellLimitsRaw);
        writer.AddStructArray(zellDuelMovesRaw);
        writer.AddStructArray(rinoaLimitsRaw);
        writer.AddStructArray(rinoaAngeloAttacksRaw);
        writer.AddStructArray(selphieSlotIdsRaw);
        writer.AddStructArray(selphieSlotSetsRaw);
        writer.AddStructArray(devourEffectsRaw);
        writer.AddStructArray(timerSettingsRaw);
        writer.AddStructArray(miscPointers);

        writer.AddTextBlob(battleCommandsText);
        writer.AddTextBlob(magicText);
        writer.AddTextBlob(guardiansText);
        writer.AddTextBlob(enemyAttacksText);
        writer.AddTextBlob(weaponsText);
        writer.AddTextBlob(squallLimitsText);
        writer.AddTextBlob(charactersText);
        writer.AddTextBlob(battleItemsText);
        writer.AddTextBlob(fieldItemsText);
        writer.AddTextBlob(independentGuardianAttacksText);
        writer.AddTextBlob(junctionAbilitiesText);
        writer.AddTextBlob(commandAbilitiesText);
        writer.AddTextBlob(characterStatAbilitiesText);
        writer.AddTextBlob(characterAbilitiesText);
        writer.AddTextBlob(partyAbilitiesText);
        writer.AddTextBlob(guardianAbilitiesText);
        writer.AddTextBlob(menuAbilitiesText);
        writer.AddTextBlob(npcLimitsText);
        writer.AddTextBlob(blueMagicsText);
        writer.AddTextBlob(irvinLimitsText);
        writer.AddTextBlob(zellLimitsText);
        writer.AddTextBlob(rinoaLimitsText);
        writer.AddTextBlob(rinoaAngeloAttacksText);
        writer.AddTextBlob(devourEffectsText);
        writer.AddTextBlob(miscText);

        return writer.Build();
    }
}