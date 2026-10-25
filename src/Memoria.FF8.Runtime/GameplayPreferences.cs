using System.Reflection;

namespace Memoria.FF8.Runtime;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NewGameOptionAttribute(string label) : Attribute
{
    public string Label { get; } = label;
}

/// <summary>A save-owned optional rule shown during new-game setup.</summary>
public sealed class NewGameOption
{
    internal NewGameOption(PropertyInfo property)
    {
        Property = property;
        Label = property.GetCustomAttribute<NewGameOptionAttribute>()?.Label ?? property.Name;
    }

    private PropertyInfo Property { get; }
    public string Label { get; }
    public string PropertyName => Property.Name;
    public bool IsEnabled(GameplayPreferences preferences) => (bool)Property.GetValue(preferences)!;
    internal void Enable(GameplayPreferences preferences) => Property.SetValue(preferences, true);
}

/// <summary>Recreation gameplay choices owned by one standalone save, separate from native FF8 configuration.</summary>
public sealed record GameplayPreferences
{
    public BattleDifficulty Difficulty { get; init; } = BattleDifficulty.Normal;
    public BattleAtbMode AtbMode { get; init; } = BattleAtbMode.TurnBased;
    public BattleGunbladeMode GunbladeMode { get; init; } = BattleGunbladeMode.Automatic;
    public ChocoboWorldMode? ChocoboWorldMode { get; init; }

    [NewGameOption("Limit once per turn")]
    public bool LimitOncePerTurn { get; init; }
    [NewGameOption("Selphie card Slots")]
    public bool SelphieCardSlots { get; init; }
    [NewGameOption("Draw once per spell")]
    public bool DrawOncePerSpell { get; init; }
    [NewGameOption("Shared magic stock")]
    public bool SharedMagicStock { get; init; }
    [NewGameOption("Infinite magic")]
    public bool InfiniteMagic { get; init; }
    [NewGameOption("Smart Attack")]
    public bool SmartAttack { get; init; }
    [NewGameOption("GF Transformations")]
    public bool GuardianFormsEnabled { get; init; }
    [NewGameOption("Card Mod once")]
    public bool CardModOnce { get; init; }
    [NewGameOption("Weapon magazine required")]
    public bool WeaponMagazineRequired { get; init; }
    [NewGameOption("Weapon upgrade order")]
    public bool WeaponUpgradeOrder { get; init; }

    // Every boolean gameplay option participates, including options introduced later.
    public static IReadOnlyList<NewGameOption> NewGameOptions { get; } = Array.AsReadOnly(typeof(GameplayPreferences).GetProperties().Where(property => property.PropertyType == typeof(bool)).Select(property => new NewGameOption(property)).ToArray());

    public GameplayPreferences NormalizeForDifficulty()
    {
        if (Difficulty != BattleDifficulty.Hardcore)
        {
            return this;
        }
        GameplayPreferences normalized = this with { };
        foreach (NewGameOption option in NewGameOptions)
        {
            option.Enable(normalized);
        }
        return normalized;
    }

    public void Validate()
    {
        if (!Enum.IsDefined(Difficulty) || !Enum.IsDefined(AtbMode) || !Enum.IsDefined(GunbladeMode) || ChocoboWorldMode is { } mode && !Enum.IsDefined(mode))
        {
            throw new InvalidDataException("Invalid saved gameplay preferences.");
        }
        if (Difficulty == BattleDifficulty.Hardcore && NewGameOptions.Any(option => !option.IsEnabled(this)))
        {
            throw new InvalidDataException("Hardcore requires every optional gameplay rule to remain enabled.");
        }
    }
}
