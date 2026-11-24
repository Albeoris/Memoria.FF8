using System.Globalization;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed record BattleLoadoutEditorCharacter(int CharacterId, string Name);
internal sealed record BattleLoadoutEditorWeapon(int WeaponId, string Name, int CharacterId, BattleWeaponCapabilities Capabilities, int Tier);
internal sealed record BattleLoadoutEditorContext(BattleLoadoutDraft Draft, BattleLoadoutEditorCharacter[] Characters, BattleLoadoutEditorWeapon[] Weapons);

public sealed partial class MenuController
{
    private const string BattleLoadoutScreen = "debug-loadouts";
    private const string BattleLoadoutWeaponListId = "loadout-weapons";

    private Func<BattleLoadoutEditorContext>? readBattleLoadoutEditor;
    private Action<BattleLoadoutDraft>? applyBattleLoadoutEditor;
    private Func<BattleLoadoutDraft, IReadOnlyList<int>, IProgress<(int Completed, int Total)>, CancellationToken, Task>? prepareBattleLoadoutBatch;
    private Func<string?>? battleLoadoutEditorBoundary;
    private Func<bool>? canOpenBattleLoadoutEditor;
    private BattleLoadoutEditorContext? battleLoadoutEditor;
    private int battleLoadoutCharacter;
    private int? battleLoadoutExpandedType;
    private string battleLoadoutWeaponFocus = "";
    private string battleLoadoutFocus = BattleLoadoutWeaponListId;
    private UiList? battleLoadoutWeaponList;
    private Task? battleLoadoutPreparation;
    private Action? battleLoadoutAfterApply;
    private BattleLoadoutProgress? battleLoadoutProgress;

    internal bool IsBattleLoadoutEditor => ScreenId == BattleLoadoutScreen;
    internal bool BattleLoadoutEditorAvailable => readBattleLoadoutEditor is not null && !IsFixtureContext && (canOpenBattleLoadoutEditor?.Invoke() ?? true);

    internal string BattleLoadoutEditorEntryHelp => BattleLoadoutEditorAvailable ? LoadoutText("scene.open", "Change equipment and Limit Breaks for the current game.") : battleLoadoutEditorBoundary?.Invoke() ?? LoadoutText("unavailable", "A current FFVIII game is required.");

    internal void ConnectBattleLoadoutEditor(Func<BattleLoadoutEditorContext> read, Action<BattleLoadoutDraft> apply, Func<string?> boundary, Func<bool>? available = null, Func<BattleLoadoutDraft, int, CancellationToken, Task<BattleLoadoutModelPreview>>? preview = null, Func<BattleLoadoutDraft, IReadOnlyList<int>, IProgress<(int Completed, int Total)>, CancellationToken, Task>? prepare = null)
    {
        readBattleLoadoutEditor = read;
        applyBattleLoadoutEditor = apply;
        battleLoadoutEditorBoundary = boundary;
        canOpenBattleLoadoutEditor = available;
        prepareBattleLoadoutPreview = preview;
        prepareBattleLoadoutBatch = prepare;
    }

    private string LoadoutText(string key, string original)
    {
        return SystemText("debug.loadout." + key, original);
    }

    private string LoadoutFormat(string key, string original, params object?[] values)
    {
        return FormatSystemText("debug.loadout." + key, original, values);
    }

    private void RegisterBattleLoadoutScreens()
    {
        Register(new()
        {
            Id = BattleLoadoutScreen,
            Title = "Weapons & Limit Breaks",
            Help = "",
            Entries = c => c.BattleLoadoutEntries()
        });
    }

    private BattleLoadoutEditorCharacter? BattleLoadoutAdjacentCharacter(int direction)
    {
        if (battleLoadoutEditor is not { Characters.Length: > 1 } editor)
        {
            return null;
        }
        int index = Array.FindIndex(editor.Characters, character => character.CharacterId == battleLoadoutCharacter);
        return editor.Characters[(index + editor.Characters.Length + direction) % editor.Characters.Length];
    }

    private void SetBattleLoadoutCharacter(int characterId)
    {
        battleLoadoutCharacter = characterId;
        battleLoadoutExpandedType = null;
        battleLoadoutWeaponFocus = "";
    }

    private BattleLoadoutEditorWeapon? BattleLoadoutEquippedWeapon()
    {
        if (battleLoadoutEditor is not { } editor)
        {
            return null;
        }
        int weaponId = editor.Draft.ResolveWeapon(battleLoadoutCharacter);
        return Array.Find(editor.Weapons, weapon => weapon.WeaponId == weaponId);
    }

    private static int BattleLoadoutWeaponType(BattleLoadoutEditorWeapon weapon)
    {
        return weapon.CharacterId == 6 ? 0 : weapon.CharacterId;
    }

    private static string BattleLoadoutTypeId(int type)
    {
        return "weapon-type-" + type.ToString(CultureInfo.InvariantCulture);
    }

    private readonly record struct BattleLoadoutWeaponRow(MenuEntry Entry, string Status, UiImageSource? Icon, bool Equipped = false, UiImageSource? OwnerPortrait = null);

    private BattleLoadoutWeaponRow[] BattleLoadoutWeaponRows()
    {
        if (battleLoadoutEditor is not { } editor)
        {
            return [];
        }
        BattleWeaponInventory? inventory = editor.Draft.WeaponInstances;
        int equipped = inventory?.Equipped.GetValueOrDefault(battleLoadoutCharacter) ?? 0;
        BattleLoadoutEditorWeapon? current = BattleLoadoutEquippedWeapon();
        Dictionary<int, string> owners = editor.Characters.ToDictionary(character => character.CharacterId, character => character.Name);
        List<BattleLoadoutWeaponRow> rows = [];
        foreach (var group in editor.Weapons.GroupBy(BattleLoadoutWeaponType).OrderBy(group => group.Key))
        {
            int type = group.Key;
            BattleLoadoutEditorWeapon starter = group.OrderBy(weapon => weapon.Tier).ThenBy(weapon => weapon.WeaponId).First();
            bool expanded = battleLoadoutExpandedType == type;
            string typeId = BattleLoadoutTypeId(type);
            bool equippedType = current is not null && BattleLoadoutWeaponType(current) == type;
            string typeLabel = (expanded ? "▾  " : "▸  ") + BattleLoadoutLabels.Style(type, localizeInterface);
            rows.Add(new(new(typeId, typeLabel, LoadoutText("scene.type-help", "Choose a weapon from this type."), Action: new("loadout-type", Value: type.ToString(CultureInfo.InvariantCulture))), equippedType ? LoadoutText("scene.equipped", "Equipped") : "", new UiImageSource(UiImageKind.Weapon, starter.CharacterId), Equipped: equippedType));
            if (!expanded)
            {
                continue;
            }
            Dictionary<int, BattleLoadoutEditorWeapon> definitions = group.ToDictionary(weapon => weapon.WeaponId);
            if (inventory is not null)
            {
                foreach (BattleWeaponInstance instance in inventory.Instances.Values.Where(instance => definitions.ContainsKey(instance.WeaponId) && owners.ContainsKey(instance.OwnerCharacterId)).OrderBy(instance => instance.Id == equipped ? 0 : instance.OwnerCharacterId == battleLoadoutCharacter ? 1 : 2).ThenBy(instance => instance.WeaponId))
                {
                    BattleLoadoutEditorWeapon weapon = definitions[instance.WeaponId];
                    bool currentInstance = instance.Id == equipped;
                    string id = instance.Id.ToString(CultureInfo.InvariantCulture);
                    string status;
                    string help;
                    UiImageSource? ownerPortrait = null;
                    if (currentInstance)
                    {
                        status = LoadoutText("scene.equipped", "Equipped");
                        help = LoadoutText("scene.equipped-help", "This weapon is equipped.");
                    }
                    else if (instance.OwnerCharacterId == battleLoadoutCharacter)
                    {
                        status = LoadoutText("scene.inventory", "Inventory");
                        help = LoadoutText("scene.inventory-help", "Equip this weapon from inventory.");
                    }
                    else
                    {
                        string owner = owners[instance.OwnerCharacterId];
                        help = LoadoutFormat("scene.swap-help", "Trade your equipped weapon with {0}.", owner);
                        if (inventory.Equipped.GetValueOrDefault(instance.OwnerCharacterId) == instance.Id)
                        {
                            status = "";
                            ownerPortrait = new UiImageSource(UiImageKind.CharacterPortrait, instance.OwnerCharacterId);
                        }
                        else
                        {
                            status = LoadoutFormat("scene.owner-inventory", "{0}’s inventory", owner);
                        }
                    }
                    rows.Add(new(new("weapon-instance-" + id, weapon.Name, help, Action: new("loadout-equip", Value: id)), status, new UiImageSource(UiImageKind.WeaponVariant, weapon.WeaponId), currentInstance, ownerPortrait));
                }
            }
            rows.Add(new(new("weapon-new-" + starter.WeaponId.ToString(CultureInfo.InvariantCulture), starter.Name, LoadoutText("scene.new-help", "Create and equip a new base-grade weapon."), Action: new("loadout-create", Value: starter.WeaponId.ToString(CultureInfo.InvariantCulture))), LoadoutText("scene.new", "Base"), new UiImageSource(UiImageKind.WeaponVariant, starter.WeaponId)));
        }
        return rows.ToArray();
    }

    private static string BattleLoadoutLimitId(BattleLimitFamily family)
    {
        return "limit-" + family;
    }

    private static bool BattleLoadoutSupportsLimit(BattleLoadoutEditorWeapon? weapon, BattleLimitFamily family)
    {
        BattleWeaponCapabilities required = BattleLimitFamilyRules.RequiredWeapon(family);
        BattleWeaponCapabilities equipped = weapon?.Capabilities ?? BattleWeaponCapabilities.None;
        return (equipped & required) == required;
    }

    private MenuEntry BattleLoadoutLimitEntry(BattleLimitFamily family)
    {
        BattleWeaponCapabilities requirement = BattleLimitFamilyRules.RequiredWeapon(family);
        string help = requirement == BattleWeaponCapabilities.None
            ? LoadoutText("scene.no-weapon-required", "Any equipped weapon supports this Limit Break.")
            : BattleLoadoutSupportsLimit(BattleLoadoutEquippedWeapon(), family)
                ? LoadoutText("scene.requirement-met", "Your equipped weapon supports this Limit Break.")
                : LoadoutFormat("scene.requires-weapon", "Requires {0}.", BattleLoadoutLabels.Capabilities(requirement, localizeInterface));
        return new(BattleLoadoutLimitId(family), BattleLoadoutLabels.Family(family, localizeInterface), help, Action: new("loadout-toggle", Value: family.ToString()));
    }

    private IReadOnlyList<MenuEntry> BattleLoadoutEntries()
    {
        if (battleLoadoutEditor is null || battleLoadoutPreparation is not null)
        {
            return [];
        }
        if (battleLoadoutSwap is not null)
        {
            return BattleLoadoutSwapChoices();
        }
        List<MenuEntry> entries = BattleLoadoutWeaponRows().Select(row => row.Entry).ToList();
        entries.AddRange(Enum.GetValues<BattleLimitFamily>().Select(BattleLoadoutLimitEntry));
        foreach (int direction in new[] { -1, 1 })
        {
            if (BattleLoadoutAdjacentCharacter(direction) is { } neighbor)
            {
                entries.Add(new(direction < 0 ? "previous-character" : "next-character", neighbor.Name, "", Action: new("loadout-cycle", Value: direction.ToString(CultureInfo.InvariantCulture))));
            }
        }
        return entries;
    }

    private bool ExecuteBattleLoadoutAction(MenuAction action)
    {
        if (!action.Kind.StartsWith("loadout-", StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            if (action.Kind == "loadout-open")
            {
                if (!BattleLoadoutEditorAvailable)
                {
                    Message = BattleLoadoutEditorEntryHelp;
                    return true;
                }
                if (battleLoadoutEditor is null)
                {
                    OpenBattleLoadoutEditor();
                    Push(BattleLoadoutScreen);
                }
                return true;
            }
            if (!IsBattleLoadoutEditor || battleLoadoutEditor is not { Draft: var draft } || battleLoadoutPreparation is not null)
            {
                return true;
            }
            Message = "";
            if (battleLoadoutSwap is { } swap)
            {
                if (action.Kind == "loadout-swap-yes")
                {
                    draft.EquipWeapon(battleLoadoutCharacter, swap.InstanceId);
                    DismissBattleLoadoutSwap();
                    CollapseBattleLoadoutWeapons();
                }
                else if (action.Kind == "loadout-swap-no")
                {
                    DismissBattleLoadoutSwap();
                }
                return true;
            }
            if (action.Kind == "loadout-toggle" && Enum.TryParse(action.Value, out BattleLimitFamily family) && Enum.IsDefined(family))
            {
                if (draft.ResolveFamilies(battleLoadoutCharacter).Contains(family))
                {
                    draft.RemoveLimit(battleLoadoutCharacter, family);
                }
                else
                {
                    draft.AddLimit(battleLoadoutCharacter, family);
                }
                return true;
            }
            if (!int.TryParse(action.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return true;
            }
            switch (action.Kind)
            {
                case "loadout-cycle" when value is -1 or 1 && BattleLoadoutAdjacentCharacter(value) is { } neighbor:
                    SetBattleLoadoutCharacter(neighbor.CharacterId);
                    break;
                case "loadout-type":
                    if (battleLoadoutExpandedType == value)
                    {
                        CollapseBattleLoadoutWeapons();
                    }
                    else
                    {
                        battleLoadoutExpandedType = value;
                        BattleLoadoutWeaponRow[] rows = BattleLoadoutWeaponRows();
                        int typeIndex = Array.FindIndex(rows, row => row.Entry.Id == BattleLoadoutTypeId(value));
                        battleLoadoutWeaponFocus = rows.ElementAtOrDefault(typeIndex + 1).Entry?.Id ?? BattleLoadoutTypeId(value);
                    }
                    break;
                case "loadout-equip":
                    if (draft.WeaponInstances?.Instances.GetValueOrDefault(value) is { } selected && selected.OwnerCharacterId != battleLoadoutCharacter)
                    {
                        PreviewBattleLoadoutSwap(battleLoadoutEditor, selected);
                        break;
                    }
                    draft.EquipWeapon(battleLoadoutCharacter, value);
                    CollapseBattleLoadoutWeapons();
                    break;
                case "loadout-create":
                    draft.CreateWeapon(battleLoadoutCharacter, value);
                    CollapseBattleLoadoutWeapons();
                    break;
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException or NotSupportedException or IOException)
        {
            Message = exception.Message;
        }
        return true;
    }

    private void OpenBattleLoadoutEditor()
    {
        BattleLoadoutEditorContext editor = readBattleLoadoutEditor!();
        BattleLoadoutEditorCharacter? initial = Array.Find(editor.Characters, character => character.CharacterId == Character?.NativeId) ?? editor.Characters.FirstOrDefault();
        if (initial is null)
        {
            throw new InvalidOperationException(LoadoutText("scene.empty-roster", "No characters are available."));
        }
        battleLoadoutEditor = editor;
        battleLoadoutFocus = BattleLoadoutWeaponListId;
        SetBattleLoadoutCharacter(initial.CharacterId);
    }

    private void CollapseBattleLoadoutWeapons()
    {
        if (battleLoadoutExpandedType is int type)
        {
            battleLoadoutWeaponFocus = BattleLoadoutTypeId(type);
            battleLoadoutExpandedType = null;
        }
    }

    private bool BackFromBattleLoadoutEditor()
    {
        if (!IsBattleLoadoutEditor || battleLoadoutEditor is null)
        {
            return false;
        }
        if (battleLoadoutSwap is not null)
        {
            DismissBattleLoadoutSwap();
            _view = null;
            SoundRequested?.Invoke(MenuSound.Cancel);
            return true;
        }
        if (battleLoadoutExpandedType.HasValue && battleLoadoutPreparation is null)
        {
            CollapseBattleLoadoutWeapons();
            Message = "";
            _view = null;
            SoundRequested?.Invoke(MenuSound.Cancel);
            return true;
        }
        return RequestBattleLoadoutExit(Back);
    }

    private bool RequestBattleLoadoutExit(Action next)
    {
        if (!IsBattleLoadoutEditor || battleLoadoutEditor is not { Draft: var draft })
        {
            return false;
        }
        if (battleLoadoutPreparation is not null)
        {
            return true;
        }
        if (battleLoadoutSwap is not null)
        {
            DismissBattleLoadoutSwap();
        }
        if (draft.ChangedCharacters.Count == 0)
        {
            ReleaseBattleLoadoutEditor();
            return false;
        }
        try
        {
            if (battleLoadoutEditorBoundary?.Invoke() is { } blocked)
            {
                throw new InvalidOperationException(blocked);
            }
            battleLoadoutAfterApply = next;
            IReadOnlyList<int> characters = draft.CharactersWithChangedBattleResources;
            if (characters.Count > 0 && prepareBattleLoadoutBatch is { } prepare)
            {
                battleLoadoutProgress = new(characters.Count);
                battleLoadoutPreparation = prepare(draft, characters, battleLoadoutProgress, CancellationToken.None);
            }
            else
            {
                battleLoadoutPreparation = Task.CompletedTask;
            }
            _view = null;
            RefreshBattleLoadoutPreparation();
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException or NotSupportedException or IOException)
        {
            FailBattleLoadoutPreparation(exception);
        }
        return true;
    }

    private void RefreshBattleLoadoutPreparation()
    {
        if (battleLoadoutPreparation is not { IsCompleted: true } operation || battleLoadoutEditor is not { Draft: var draft })
        {
            return;
        }
        try
        {
            operation.GetAwaiter().GetResult();
            if (draft.ChangedCharacters.Count > 0)
            {
                applyBattleLoadoutEditor!(draft);
            }
        }
        catch (Exception exception)
        {
            FailBattleLoadoutPreparation(exception);
            return;
        }
        Action? next = battleLoadoutAfterApply;
        ReleaseBattleLoadoutEditor();
        InvalidateLiveData();
        next?.Invoke();
    }

    private void FailBattleLoadoutPreparation(Exception exception)
    {
        battleLoadoutPreparation = null;
        battleLoadoutAfterApply = null;
        battleLoadoutProgress = null;
        _view = null;
        Message = exception.Message;
    }

    private void ReleaseBattleLoadoutEditor()
    {
        ReleaseBattleLoadoutPreview();
        battleLoadoutSwap = null;
        battleLoadoutEditor = null;
        battleLoadoutExpandedType = null;
        battleLoadoutWeaponList = null;
        battleLoadoutPreparation = null;
        battleLoadoutAfterApply = null;
        battleLoadoutProgress = null;
    }

    private sealed class BattleLoadoutProgress(int total) : IProgress<(int Completed, int Total)>
    {
        internal sealed record Count(int Completed, int Total);

        private Count current = new(0, total);

        internal Count Current => Volatile.Read(ref current);

        public void Report((int Completed, int Total) value)
        {
            Volatile.Write(ref current, new(value.Completed, value.Total));
        }
    }
}
