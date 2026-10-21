using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public enum MenuInput
{
    Up, Down, Left, Right, Confirm, Cancel, PageLeft, PageRight, Alternate, Tab, PageUp, PageDown
}

public sealed record MenuAction(string Kind, string SubjectId = "", string TargetId = "", string Value = "");

public sealed class MenuData
{
    public bool? CardCollectionComplete { get; set; }
    public bool SharedMagicStock { get; set; }
    public bool SmartAttack { get; set; }
    public bool GuardianFormsPreferenceLocked { get; set; }
    public List<MenuCharacter> Characters { get; } = [];
    public List<MenuGuardianForce> GuardianForces { get; } = [];
    public List<MenuItem> Items { get; } = [];
    public List<MenuMagic> Magic { get; } = [];
    public List<MenuCard> Cards { get; } = [];
    public List<MenuRecipe> Recipes { get; } = [];
    public List<MenuSaveSlot> SaveSlots { get; } = [];
    public List<MenuBattleTarget> BattleTargets { get; } = [];
    public MenuBattleRewards? BattleRewards { get; set; }
    public Dictionary<string, string> Settings { get; } = new(StringComparer.Ordinal);
    public HashSet<string> SupportedActions { get; } = new(StringComparer.Ordinal);
    public string Location { get; set; } = "Unavailable";
    public int? Currency { get; set; }
    public TimeSpan? PlayTime { get; set; }
    public int? SeeDRank { get; set; }
    public string SourceDescription { get; set; } = "No live menu provider";
    public string UnavailableReason { get; set; } = "";
    public bool SaveAvailable { get; set; }
}

public sealed class MenuCharacter
{
    public bool NativeFieldItemTargetAllowed { get; set; }
    public string AttackMode { get; set; } = "PhysicalOnly";
    public string Id { get; set; } = "";
    public int? NativeId { get; set; }
    public string Name { get; set; } = "";
    public bool InParty { get; set; }
    public int? Level { get; set; }
    public int? CurrentHp { get; set; }
    public string? HpDisplay { get; set; }
    public int? MaximumHp { get; set; }
    public int? Experience { get; set; }
    public int? NextLevelExperience { get; set; }
    public string Weapon { get; set; } = "Unavailable";
    public string LimitBreak { get; set; } = "Unavailable";
    public float? AtbProgress { get; set; }
    public List<string> Conditions { get; } = [];
    public List<string> Commands { get; } = [];
    public List<string> Abilities { get; } = [];
    public List<MenuAbilityChoice> AvailableCommands { get; } = [];
    public List<MenuAbilityChoice> AvailableAbilities { get; } = [];
    public int AbilitySlots { get; set; } = 2;
    public HashSet<string> AvailableJunctionSlots { get; } = new(StringComparer.Ordinal);
    public List<string> GuardianForceIds { get; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public Func<string, string, IReadOnlyDictionary<string, int>>? JunctionPreview { get; set; }
    public Dictionary<string, int> Stats { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Junctions { get; } = new(StringComparer.Ordinal);
    public List<MenuLimitMove> LimitMoves { get; } = [];
    public Dictionary<string, string> LimitSettings { get; } = new(StringComparer.Ordinal);
    public List<string> SlotPreview { get; } = [];
}

public sealed class MenuLimitMove
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool? Learned { get; set; }
    public int? LearningProgress { get; set; }
    public string InputSequence { get; set; } = "Unavailable";
    public int? Stock { get; set; }
    public string Description { get; set; } = "";
}

public sealed record MenuGuardianForceOwner(string Id, int? NativeId, string Name);

public sealed class MenuGuardianForce
{
    public string Id { get; set; } = "";
    public int? NativeId { get; set; }
    public string Name { get; set; } = "";
    public MenuGuardianForceOwner? Owner { get; set; }
    public int? Level { get; set; }
    public int? CurrentHp { get; set; }
    public int? MaximumHp { get; set; }
    public int? Experience { get; set; }
    public int? NextLevelExperience { get; set; }
    public int? AbilityPoints { get; set; }
    public string Learning { get; set; } = "Unavailable";
    public string Element { get; set; } = "Unavailable";
    public List<string> Abilities { get; } = [];
    public List<MenuAbilityChoice> LearningChoices { get; } = [];
}

public sealed record MenuAbilityChoice(string Id, string Name, bool Learned = false, int Progress = 0, int RequiredPoints = 0)
{
    public UiImageSource? Icon { get; init; }
    public string Description { get; init; } = "";
}

public sealed class MenuItem
{
    public Memoria.FF8.Runtime.NativeMagazinePage[] NativeMagazinePages { get; set; } = [];
    public Dictionary<byte, string> NativeMagazinePageUnavailableReasons { get; set; } = [];
    public bool NativeFieldUseSupported { get; set; }
    public bool NativeFieldBattleUse { get; set; }
    public bool NativeFieldNameConfirmation { get; set; }
    public int? NativeFieldNameSubject { get; set; }
    public bool NativeFieldTargetsGuardianForce { get; set; }
    public bool NativeFieldQuistisOnly { get; set; }
    public bool NativeFieldForgetsAbility { get; set; }
    public bool NativeFieldInventoryTarget { get; set; }
    public string Id { get; set; } = "";
    public UiImageSource? Icon { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public string Description { get; set; } = "";
    public int DemoHealing { get; set; }
    public int? Price { get; set; }
    public int? SalePrice { get; set; }
    public List<MenuMaterial> Materials { get; } = [];
}

public sealed record MenuMaterial(string Name, UiImageSource? Icon, int Owned, int Required);

public sealed class MenuMagic
{
    public string Id { get; set; } = "";
    public UiImageSource? Icon { get; set; }
    public string Name { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public int Quantity { get; set; }
    public string Description { get; set; } = "";
    public int DemoHealing { get; set; }
    public int? RemainingUses { get; set; }
    public int? Capacity { get; set; }
    public string Owner { get; set; } = "";
    public UiImageSource? OwnerIcon { get; set; }
    public string Availability { get; set; } = "";
    public string Caster { get; set; } = "";
    public bool JunctionAvailable { get; set; } = true;
    public string StockPresentation => Capacity is int capacity ? $"{RemainingUses}/{capacity}  {Quantity}" : Quantity.ToString();
    public string AccessPresentation => string.Join(" · ", new[] { Owner, Availability, Caster }.Where(value => value.Length > 0));
}

public sealed class MenuCard
{
    public string Id { get; set; } = "";
    public int? NativeId { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public int? Level { get; set; }
    public string Ranks { get; set; } = "Unavailable";
    public string Element { get; set; } = "Unavailable";
    public string Description { get; set; } = "";
    public string? RefinementItemId { get; set; }
    public int? RefinementQuantity { get; set; }
    public int? RefinementRequiredCards { get; set; }
    public string? RefinementItemName { get; set; }
}

public sealed class MenuBattleTarget
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int? CurrentHp { get; set; }
    public int? MaximumHp { get; set; }
    public List<MenuMagic> DrawSpells { get; } = [];
}

public sealed class MenuBattleRewards
{
    public Dictionary<string, int> CharacterExperience { get; } = new(StringComparer.Ordinal);
    public int? NextLevelExperience { get; set; }
    public int? AbilityPoints { get; set; }
    public int? Currency { get; set; }
    public List<MenuItem> Items { get; } = [];
}

public sealed record MenuRecipe(string Id, string Ability, string IngredientId, int IngredientQuantity, string ResultId, string ResultName, int ResultQuantity)
{
    public int? NativeAbility { get; init; }
    public int? NativeRecipeIndex { get; init; }
    public byte? NativeKind { get; init; }
    public string? IngredientName { get; init; }
    public IReadOnlyDictionary<int, int> OutputCapacity { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, int> MaximumBatches { get; init; } = new Dictionary<int, int>();
}

public sealed class MenuSaveSlot
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "Empty";
}

public enum MenuLayout
{
    Main, List, Character, Junction, GuardianForceGrid, CardGrid, Gallery, BattleHud
}

public sealed record MenuEntry(string Id, string Label, string Help, string? Destination = null, MenuAction? Action = null, bool Enabled = true);

public sealed class MenuScreenDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Help { get; init; }
    public MenuLayout Layout { get; init; } = MenuLayout.List;
    public int Columns { get; init; } = 1;
    public bool MainCommand { get; init; }
    public Func<MenuController, IReadOnlyList<MenuEntry>> Entries { get; init; } = _ => [];
    public Func<MenuController, IReadOnlyList<string>> Details { get; init; } = _ => [];
    public Action<MenuController, UiPanel>? BuildContent { get; init; }
}
