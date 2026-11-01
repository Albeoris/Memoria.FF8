using System.Globalization;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed record MenuJunctionChange(string CharacterId, string CharacterName, string Category, string Slot, string Before, string After);

/// <summary>A complete, immutable-source proposal. The action carries only a validated assignment transaction.</summary>
public sealed class MenuJunctionPlan
{
    public required string SourceFingerprint { get; init; }
    public required string AfterSourceFingerprint { get; init; }
    public required string Label { get; init; }
    public required MenuData Before { get; init; }
    public required MenuData After { get; init; }
    public required IReadOnlyList<MenuJunctionChange> Changes { get; init; }
    public required MenuAction Action { get; init; }
    public string? UnavailableReason { get; init; }
    public bool HasChanges => UnavailableReason is null && Changes.Count != 0;
    public bool RequiresReview { get; init; }
    public NativeJunctionEditOperation Operation { get; init; }
    public bool IsUndo { get; init; }
    internal NativeCharacterVitals Original { get; init; } = null!;
    internal NativeCharacterVitals Result { get; init; } = null!;
    internal string RulesFingerprint { get; init; } = "";
}

/// <summary>Plans menu edits against one captured source, including every affected saved record.</summary>
public sealed class MenuJunctionPlanner
{
    private static readonly string[] StatNames = ["HP", "Str", "Vit", "Mag", "Spr", "Spd", "Eva", "Hit", "Luck"];
    private readonly GameState state;
    private readonly NativeCharacterVitals source;
    private readonly IReadOnlyDictionary<int, string>? names;
    private readonly LiveMenuCatalog? catalog;
    private readonly int? placeId;
    private MenuData? before;
    private readonly string rulesFingerprint;
    private readonly string nativeFingerprint;
    private readonly Dictionary<int, BattleNativePlayerStatRules> sourceRules = [];
    private readonly Dictionary<int, IReadOnlyList<NativeMagicStock>> sourceInventories = [];
    private readonly Dictionary<MenuAction, MenuJunctionPlan> planCache = [];

    public string SourceFingerprint { get; }
    public MenuData Snapshot => before ??= View(source);
    public string? UnavailableReason => catalog is null ? "Original stat rules are unavailable; derived effects and stat optimization cannot be projected." : null;

    public MenuJunctionPlanner(GameState state, IReadOnlyDictionary<int, string>? names = null, LiveMenuCatalog? catalog = null, int? placeId = null)
    {
        this.state = state;
        source = (state.CharacterVitals ?? throw new ArgumentException("Junction planning requires saved characters.", nameof(state))).DeepCopy();
        this.names = names is null ? null : new Dictionary<int, string>(names);
        this.catalog = catalog;
        this.placeId = placeId;
        rulesFingerprint = (catalog?.StatRulesFingerprint ?? "Unavailable") + ":" + state.GameplayPreferences.SmartAttack;
        nativeFingerprint = NativeJunctionEdits.Fingerprint(source);
        SourceFingerprint = nativeFingerprint + ":" + rulesFingerprint;
    }

    public bool IsCurrent(MenuJunctionPlan plan) => plan.SourceFingerprint == SourceFingerprint;

    public bool IsMagicCandidate(string characterId, int spellId, string slot)
    {
        int index = Enumerable.Range(0, 19).Single(index => LiveMenuData.JunctionSlotName(index) == slot);
        if (spellId == 0 || index < 9)
        {
            return true;
        }
        if (catalog is null || spellId is < 1 or >= 57)
        {
            return false;
        }
        return NativeJunctionMagicEligibility.HasContribution(Rules(source, CharacterRow(characterId)).MagicRecords[spellId].AsSpan(), index);
    }

    public MenuJunctionPlan Plan(MenuAction action)
    {
        if (planCache.TryGetValue(action, out MenuJunctionPlan? cached))
        {
            return cached;
        }
        try
        {
            if (UnavailableReason is string unavailable)
            {
                return Cache(action, Unavailable(action.Kind, unavailable));
            }
            PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
            if (input.Kind is PlayerActionKind.SetJunctionMagic or PlayerActionKind.ReassignJunctionMagic && !IsMagicCandidate(action.TargetId, int.Parse(input.Text!, CultureInfo.InvariantCulture), action.Value))
            {
                return Cache(action, Unavailable(action.Kind, "This magic has no contribution for the selected junction slot."));
            }
            NativeCharacterVitals intended = NativeJunctionMenuInput.Apply(source.DeepCopy(), input);
            NativeJunctionEditOperation operation = action.Kind switch
            {
                "junction-off" when action.Value == "Magic" => NativeJunctionEditOperation.ClearCharacterMagic,
                "junction-off" when action.Value == "All" => NativeJunctionEditOperation.RemoveCharacterJunctions,
                "junction-magic-off-all" => NativeJunctionEditOperation.ClearPartyMagic,
                _ => NativeJunctionEditOperation.Assignments
            };
            return Cache(action, Complete(action.Kind, intended, action.TargetId, action.Kind is "junction-off" or "junction-magic-off-all", operation));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or InvalidDataException)
        {
            return Cache(action, Unavailable(action.Kind, exception.Message));
        }
    }

    private MenuJunctionPlan Cache(MenuAction action, MenuJunctionPlan plan)
    {
        if (planCache.Count >= 128)
        {
            planCache.Clear();
        }
        planCache.Add(action, plan);
        return plan;
    }

    public MenuJunctionPlan FillEmptyCommands(string characterId)
    {
        try
        {
            if (UnavailableReason is string unavailable)
            {
                return Unavailable("Fill empty commands", unavailable);
            }
            int row = CharacterRow(characterId);
            NativeCharacterVitals intended = source.DeepCopy();
            IReadOnlySet<int> learned = source.JunctionLearnedAbilities(row);
            HashSet<int> equipped = Enumerable.Range(0, 3).Select(slot => (int)(byte)(source.Records[row].Junction.Commands >> (8 * slot))).Where(id => id != 0).ToHashSet();
            int[] preferred = new byte[] { 2, 6, 4, 3 }.Select(command => source.MenuDefinitions.CommandAbilityCommands.IndexOf(command)).Where(index => index >= 0).Select(index => index + 20).ToArray();
            foreach (int slot in Enumerable.Range(0, 3))
            {
                if ((byte)(intended.Records[row].Junction.Commands >> (8 * slot)) != 0)
                {
                    continue;
                }
                int ability = preferred.FirstOrDefault(id => learned.Contains(id) && !equipped.Contains(id));
                if (ability == 0)
                {
                    break;
                }
                intended = intended.SetJunctionAbility(row, slot, ability, true);
                equipped.Add(ability);
            }
            return Complete("Fill empty commands", intended, characterId, true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or InvalidDataException)
        {
            return Unavailable("Fill empty commands", exception.Message);
        }
    }

    public MenuJunctionPlan Optimize(string characterId, string priority, IReadOnlySet<string> pins)
    {
        try
        {
            if (UnavailableReason is string unavailable)
            {
                return Unavailable("Optimize stats", unavailable);
            }
            int row = CharacterRow(characterId);
            int[] order = priority switch
            {
                "Attack" => [1, 5, 6, 7, 8, 0, 2, 3, 4],
                "Magic" => [3, 5, 0, 1, 2, 4, 6, 7, 8],
                "Defense" => [0, 2, 4, 6, 1, 3, 5, 7, 8],
                _ => throw new ArgumentException("Choose Attack, Magic or Defense priority.", nameof(priority))
            };
            bool Editable(int slot) => slot < 9 && source.JunctionMagicSlotAvailable(row, slot) && !pins.Contains(StatNames[slot]);
            byte[] original = source.Records[row].Junction.MagicJunctions;
            HashSet<int> reserved = Enumerable.Range(0, 20).Where(slot => !Editable(slot)).Select(slot => (int)original[slot]).Where(spell => spell != 0).ToHashSet();
            int[] candidates = source.GetMagicInventory(row).Where(stock => stock.SpellId != 0 && stock.Count > 0 && !reserved.Contains(stock.SpellId) && (!source.MagicSystem.SharedMagicStock || MagicJunctionPolicy.GetOwner(source, stock.SpellId) is not int owner || owner == row)).Select(stock => (int)stock.SpellId).Distinct().Order().ToArray();
            NativeCharacterVitals intended = source.DeepCopy();
            byte[] mapping = intended.Records[row].Junction.MagicJunctions;
            foreach (int slot in Enumerable.Range(0, 9).Where(Editable))
            {
                mapping[slot] = 0;
            }
            HashSet<int> assigned = [];
            foreach (int slot in order.Where(Editable))
            {
                int emptyValue = ProjectStats(intended, row)[StatNames[slot]];
                int bestSpell = 0;
                int bestValue = emptyValue;
                foreach (int spell in candidates.Where(spell => !assigned.Contains(spell)))
                {
                    mapping[slot] = checked((byte)spell);
                    int value = ProjectStats(intended, row)[StatNames[slot]];
                    if (value > bestValue || value == bestValue && value > emptyValue && spell == original[slot])
                    {
                        bestValue = value;
                        bestSpell = spell;
                    }
                }
                mapping[slot] = checked((byte)bestSpell);
                if (bestSpell != 0)
                {
                    assigned.Add(bestSpell);
                }
            }
            Dictionary<string, int> currentStats = ProjectStats(source, row);
            Dictionary<string, int> proposedStats = ProjectStats(intended, row);
            int comparison = 0;
            foreach (int slot in order)
            {
                comparison = proposedStats[StatNames[slot]].CompareTo(currentStats[StatNames[slot]]);
                if (comparison != 0)
                {
                    break;
                }
            }
            if (comparison <= 0)
            {
                return Complete("No stat changes suggested", source, characterId, true);
            }
            return Complete($"Optimize stats · {priority}", intended, characterId, true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or InvalidDataException)
        {
            return Unavailable("Optimize stats", exception.Message);
        }
    }

    public MenuJunctionPlan Reverse(MenuJunctionPlan applied)
    {
        if (NativeJunctionEdits.Fingerprint(applied.Result) != nativeFingerprint || applied.RulesFingerprint != rulesFingerprint)
        {
            return Unavailable("Undo", "Undo is unavailable because the character, inventory or rule state changed.");
        }
        NativeCharacterVitals intended = source.DeepCopy();
        for (int row = 0; row < intended.Records.Length; row++)
        {
            intended.Records[row] = intended.Records[row] with { Junction = applied.Original.Records[row].Junction with { MagicJunctions = applied.Original.Records[row].Junction.MagicJunctions.ToArray() } };
        }
        try
        {
            return Complete("Undo", intended, "", true, applied.Operation, isUndo: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or InvalidDataException)
        {
            return Unavailable("Undo", exception.Message);
        }
    }

    private BattleNativePlayerStatRules Rules(NativeCharacterVitals vitals, int row)
    {
        if (!sourceRules.TryGetValue(row, out BattleNativePlayerStatRules? original))
        {
            original = catalog!.ReadStatRules(source, row);
            sourceRules.Add(row, original);
        }
        NativeCharacterJunction junction = vitals.Records[row].Junction;
        if (junction.Abilities != source.Records[row].Junction.Abilities)
        {
            return catalog!.ReadStatRules(vitals, row);
        }
        return junction.MagicJunctions.AsSpan().SequenceEqual(source.Records[row].Junction.MagicJunctions) ? original : original with { MagicJunctions = junction.MagicJunctions.ToImmutableArray() };
    }

    private IReadOnlyList<NativeMagicStock> Inventory(int row)
    {
        int bank = source.MagicSystem.SharedMagicStock ? 0 : row;
        if (!sourceInventories.TryGetValue(bank, out IReadOnlyList<NativeMagicStock>? inventory))
        {
            inventory = source.GetMagicInventory(row);
            sourceInventories.Add(bank, inventory);
        }
        return inventory;
    }

    private Dictionary<string, int> ProjectStats(NativeCharacterVitals vitals, int row) => LiveMenuData.Statistics(Rules(vitals, row), vitals.Records[row], Inventory(row));

    private MenuData View(NativeCharacterVitals vitals, MenuData? baseline = null, IReadOnlySet<int>? changedRows = null) => LiveMenuData.BuildJunctionSnapshot(state with { CharacterVitals = vitals }, names, catalog, placeId, baseline, changedRows, catalog is null ? null : row => Rules(vitals, row));

    private MenuJunctionPlan Complete(string label, NativeCharacterVitals intended, string targetId, bool review, NativeJunctionEditOperation operation = NativeJunctionEditOperation.Assignments, bool isUndo = false)
    {
        intended = intended.DeepCopy();
        for (int row = 0; row < intended.Records.Length; row++)
        {
            if (operation == NativeJunctionEditOperation.Assignments)
            {
                intended.Records[row].Junction.MagicJunctions[19] = source.Records[row].Junction.MagicJunctions[19];
            }
            if (operation != NativeJunctionEditOperation.RemoveCharacterJunctions)
            {
                NativeCharacterJunction junction = intended.Records[row].Junction;
                intended.Records[row] = intended.Records[row] with { Junction = junction with { Commands = (junction.Commands & 0x00FFFFFF) | (source.Records[row].Junction.Commands & 0xFF000000) } };
            }
        }
        NativeJunctionEditPlan edit = NativeJunctionEdits.Create(source, intended, operation, isUndo);
        NativeCharacterVitals result = edit.Rows.Length == 0 ? source.DeepCopy() : NativeJunctionEdits.Apply(source, edit);
        MenuData current = Snapshot;
        MenuData after = edit.Rows.Length == 0 ? current : View(result, current, edit.Rows.Select(row => row.Record).ToHashSet());
        List<MenuJunctionChange> changes = Diff(current, after);
        for (int row = 0; row < source.Records.Length; row++)
        {
            MenuCharacter character = current.Characters.Single(character => character.Id == $"character-{row}");
            int previousCommand = (byte)(source.Records[row].Junction.Commands >> 24);
            int nextCommand = (byte)(result.Records[row].Junction.Commands >> 24);
            if (previousCommand != nextCommand)
            {
                string Command(int id) => id == 0 ? catalog?.SystemText("ui.junctiondashboard.empty", "Empty") ?? "Empty" : catalog?.AbilityName(id) ?? id.ToString(CultureInfo.InvariantCulture);
                changes.Add(new(character.Id, character.Name, "Command", catalog?.SystemText("ui.junctiondashboard.savedcommand", "Additional saved command binding") ?? "Additional saved command binding", Command(previousCommand), Command(nextCommand)));
            }
            int previous = source.Records[row].Junction.MagicJunctions[19];
            int next = result.Records[row].Junction.MagicJunctions[19];
            if (previous != next)
            {
                string Spell(int id) => id == 0 ? catalog?.SystemText("ui.junctiondashboard.none", "None") ?? "None" : catalog?.MagicName(id) ?? id.ToString(CultureInfo.InvariantCulture);
                changes.Add(new(character.Id, character.Name, "Magic", catalog?.SystemText("ui.junctiondashboard.savedbinding", "Additional saved magic binding") ?? "Additional saved magic binding", Spell(previous), Spell(next)));
            }
        }
        bool foreign = changes.Any(change => change.CharacterId != targetId);
        bool gfConsequences = changes.Any(change => change.Category == "GF") && changes.Any(change => change.Category != "GF");
        return new MenuJunctionPlan
        {
            SourceFingerprint = SourceFingerprint,
            AfterSourceFingerprint = NativeJunctionEdits.Fingerprint(result) + ":" + rulesFingerprint,
            Label = label,
            Before = current,
            After = after,
            Changes = changes,
            Action = new MenuAction("junction-edit", Value: JsonSerializer.Serialize(edit)),
            RequiresReview = review || foreign || gfConsequences,
            Operation = operation,
            IsUndo = isUndo,
            Original = source,
            Result = result,
            RulesFingerprint = rulesFingerprint
        };
    }

    private MenuJunctionPlan Unavailable(string label, string reason)
    {
        MenuData current = Snapshot;
        return new MenuJunctionPlan { SourceFingerprint = SourceFingerprint, AfterSourceFingerprint = SourceFingerprint, Label = label, Before = current, After = current, Changes = [], Action = new MenuAction("junction-edit"), UnavailableReason = reason, Original = source, Result = source, RulesFingerprint = rulesFingerprint };
    }

    private static int CharacterRow(string id)
    {
        if (!id.StartsWith("character-", StringComparison.Ordinal) || !int.TryParse(id.AsSpan(10), NumberStyles.None, CultureInfo.InvariantCulture, out int row) || (uint)row >= 8)
        {
            throw new ArgumentException("Select a stable saved character record.", nameof(id));
        }
        return row;
    }

    private List<MenuJunctionChange> Diff(MenuData before, MenuData after)
    {
        List<MenuJunctionChange> changes = [];
        Dictionary<string, MenuMagic> beforeMagic = before.Magic.ToDictionary(magic => magic.Id, StringComparer.Ordinal);
        Dictionary<string, MenuMagic> afterMagic = after.Magic.ToDictionary(magic => magic.Id, StringComparer.Ordinal);
        Dictionary<string, MenuCharacter> afterCharacters = after.Characters.ToDictionary(character => character.Id, StringComparer.Ordinal);
        string Text(string key, string fallback) => catalog?.SystemText(key, fallback) ?? fallback;
        string None() => Text("ui.junctiondashboard.none", "None");
        string Empty() => Text("ui.junctiondashboard.empty", "Empty");
        string Available() => Text("ui.junctiondashboard.available", "Available");
        string Locked() => Text("ui.junctiondashboard.locked", "Locked");
        string Unbound() => Text("magic.Unbound", "Unbound");
        string Owner(MenuData data, MenuMagic magic) => magic.OwnerCharacterId is string id ? (data.Characters.FirstOrDefault(character => character.Id == id)?.Name ?? id) + (magic.BoundSlot is string slot ? " · " + slot : "") : Unbound();
        string Access(MenuMagic value)
        {
            string access = string.Join(" · ", new[] { value.Caster, value.Availability }.Where(text => text.Length != 0));
            return access.Length != 0 ? access : Available();
        }
        foreach (MenuCharacter current in before.Characters)
        {
            MenuCharacter next = afterCharacters[current.Id];
            void Add(string category, string slot, string oldValue, string newValue)
            {
                if (oldValue != newValue)
                {
                    changes.Add(new(current.Id, current.Name, category, slot, oldValue, newValue));
                }
            }
            void AccessChanges()
            {
                foreach (MenuMagic magic in before.Magic.Where(magic => magic.CharacterId == current.Id))
                {
                    if (!afterMagic.TryGetValue(magic.Id, out MenuMagic? projected))
                    {
                        continue;
                    }
                    if (before.SharedMagicStock && magic.OwnerCharacterId != projected.OwnerCharacterId && (magic.OwnerCharacterId == current.Id || projected.OwnerCharacterId == current.Id))
                    {
                        Add("Ownership", magic.Name, Owner(before, magic), Owner(after, projected));
                    }
                    // A shared stock's preferred caster is repeated on every character's row; it does not change their access.
                    if (magic.Availability != projected.Availability)
                    {
                        Add("Access", magic.Name, Access(magic), Access(projected));
                    }
                }
            }
            if (ReferenceEquals(current, next))
            {
                AccessChanges();
                continue;
            }
            string Magic(Dictionary<string, MenuMagic> values, string id) => id == "0" ? None() : values.GetValueOrDefault(id)?.Name ?? id;
            string Ability(MenuCharacter character, string id, bool command) => id == "0" ? Empty() : (command ? character.AvailableCommands : character.AvailableAbilities).FirstOrDefault(choice => choice.Id == id)?.Name ?? id;
            foreach (string slot in current.JunctionIds.Keys)
            {
                string oldId = current.JunctionIds[slot];
                string newId = next.JunctionIds[slot];
                if (oldId != newId)
                {
                    changes.Add(new(current.Id, current.Name, "Magic", slot, Magic(beforeMagic, oldId), Magic(afterMagic, newId)));
                }
                bool available = current.AvailableJunctionSlots.Contains(slot);
                bool nextAvailable = next.AvailableJunctionSlots.Contains(slot);
                if (available != nextAvailable)
                {
                    Add("Capability", slot, available ? Available() : Locked(), nextAvailable ? Available() : Locked());
                }
            }
            for (int slot = 0; slot < 3; slot++)
            {
                string oldId = current.CommandIds[slot];
                string newId = next.CommandIds[slot];
                if (oldId != newId)
                {
                    changes.Add(new(current.Id, current.Name, "Command", $"Command {slot + 1}", Ability(current, oldId, true), Ability(next, newId, true)));
                }
            }
            for (int slot = 0; slot < 4; slot++)
            {
                string oldId = current.AbilityIds[slot];
                string newId = next.AbilityIds[slot];
                if (oldId != newId)
                {
                    changes.Add(new(current.Id, current.Name, "Passive", $"Passive {slot + 1}", Ability(current, oldId, false), Ability(next, newId, false)));
                }
            }
            Add("Capacity", "Passive capacity", current.AbilitySlots.ToString(CultureInfo.InvariantCulture), next.AbilitySlots.ToString(CultureInfo.InvariantCulture));
            foreach (string gf in current.GuardianForceIds.Union(next.GuardianForceIds).Order(StringComparer.Ordinal))
            {
                Add("GF", before.GuardianForces.FirstOrDefault(force => force.Id == gf)?.Name ?? gf, current.GuardianForceIds.Contains(gf) ? Text("ui.junctiondashboard.assigned", "Assigned") : Text("ui.junctiondashboard.unassigned", "Unassigned"), next.GuardianForceIds.Contains(gf) ? Text("ui.junctiondashboard.assigned", "Assigned") : Text("ui.junctiondashboard.unassigned", "Unassigned"));
            }
            foreach (string stat in current.Stats.Keys)
            {
                Add("Stat", stat, current.Stats[stat].ToString(CultureInfo.InvariantCulture), next.Stats[stat].ToString(CultureInfo.InvariantCulture));
            }
            Add("HP", "Current HP", current.HpDisplay ?? current.CurrentHp?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable", next.HpDisplay ?? next.CurrentHp?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
            if (current.JunctionEffects is { } effects && next.JunctionEffects is { } nextEffects)
            {
                void Effects(string category, IReadOnlyList<MenuJunctionPercentage> oldValues, IReadOnlyList<MenuJunctionPercentage> newValues)
                {
                    for (int index = 0; index < oldValues.Count; index++)
                    {
                        Add(category, oldValues[index].Name + " Attack", oldValues[index].Attack + "%", newValues[index].Attack + "%");
                        Add(category, oldValues[index].Name + " Defense", oldValues[index].Defense + "%", newValues[index].Defense + "%");
                    }
                }
                Effects("Element", effects.Elements, nextEffects.Elements);
                Effects("Status", effects.Statuses, nextEffects.Statuses);
            }
            AccessChanges();
        }
        return changes;
    }
}
