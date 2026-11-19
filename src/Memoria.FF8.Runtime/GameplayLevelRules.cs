namespace Memoria.FF8.Runtime;

public sealed record GameplayLevelRules
{
    public const int DefaultMaximumLevel = 100;
    public const int HighestLevel = 300;
    public const int HighestAdditionalPartyLevel = 200;

    public static GameplayLevelRules Default { get; } = new();
    public static GameplayLevelRules Hardcore { get; } = new() { MaximumEnemyLevel = 110, AdditionalPartyLevel = 10 };

    public int MaximumEnemyLevel { get; init; } = DefaultMaximumLevel;
    public int MaximumCharacterLevel { get; init; } = DefaultMaximumLevel;
    public int AdditionalPartyLevel { get; init; }

    public static GameplayLevelRules ForDifficulty(BattleDifficulty difficulty)
    {
        return difficulty == BattleDifficulty.Hardcore ? Hardcore : Default;
    }

    public void Validate()
    {
        if (MaximumEnemyLevel is < 1 or > HighestLevel || MaximumCharacterLevel is < 1 or > HighestLevel || AdditionalPartyLevel is < 0 or > HighestAdditionalPartyLevel)
        {
            throw new InvalidDataException("Gameplay level rules exceed the supported enemy, character or party level range.");
        }
    }
}
