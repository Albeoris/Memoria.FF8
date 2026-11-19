using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NewGamePlusPreset
{
    Stored,
    Native,
    Empty,
    Custom
}

public sealed record NewGamePlusCharacter(byte CharacterId, int IndividualLevel, bool AvailableFromStart, bool NativeFallback = false);
public sealed record NewGamePlusMagicBank(byte CharacterId, ImmutableArray<NativeMagicStock> Stock);

/// <summary>Resolved initial recipe and continuing rules. Current progression remains in its existing gameplay owners.</summary>
public sealed record NewGamePlusDefinition
{
    public string SourceCompletionIdentity { get; init; } = "";
    public string SourceContentIdentity { get; init; } = "";
    public string SourceRevision { get; init; } = "";
    [JsonRequired]
    public GameplayPreferences Settings { get; init; } = new();
    [JsonRequired]
    public int ConversionPolicyVersion { get; init; } = 1;
    public ImmutableArray<string> RequiredCapabilities { get; init; } = [];
    [JsonRequired]
    public int SchemaVersion { get; init; } = 1;
    [JsonRequired]
    public string InitializationPolicy { get; init; } = "fresh-story-resolved-progression-v1";
    [JsonRequired]
    public int ExperienceMultiplierTenths { get; init; } = 10;
    [JsonRequired]
    public int ApMultiplierTenths { get; init; } = 10;
    [JsonRequired]
    public int GlobalLevel { get; init; }
    [JsonRequired]
    public bool AllowWorldMapPartyChanges { get; init; }
    [JsonRequired]
    public ImmutableArray<NewGamePlusCharacter> Characters { get; init; } = [];
    [JsonRequired]
    public NewGamePlusPreset CharacterPreset { get; init; }
    [JsonRequired]
    public NewGamePlusPreset GuardianForcePreset { get; init; }
    [JsonRequired]
    public NewGamePlusPreset MagicPreset { get; init; }
    [JsonRequired]
    public NewGamePlusPreset ItemPreset { get; init; }
    [JsonRequired]
    public NewGamePlusPreset CardPreset { get; init; }
    [JsonRequired]
    public ImmutableArray<NativeItemStock> InitialItems { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<int> InitialCardQuantities { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<NewGamePlusMagicBank> InitialMagic { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<NativeMagicStock> InitialMagicExtension { get; init; } = [];
    public ImmutableArray<MagicStockMove> InitialMagicConversionMoves { get; init; } = [];
    public ImmutableArray<MagicStockMove> InitialMagicOverflow { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<GameplaySaveGuardianForce> InitialGuardianForces { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> InitialGuardianForceTemplates { get; init; } = [];
    public ImmutableArray<string> GuardianForceExcludedDocuments { get; init; } = [];
    [JsonRequired]
    public bool StartingChoicesApplied { get; init; }
    [JsonRequired]
    public ImmutableArray<byte> InitializedCharacters { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<byte> InitializedGuardianForces { get; init; } = [];

    public bool RequiresExpandedRoster => Characters.Any(character => character.CharacterId >= 6 && character.AvailableFromStart);
    public int ResolvedLevel(byte identity) => GlobalLevel == 0 ? Characters.Single(character => character.CharacterId == identity).IndividualLevel : GlobalLevel;

    public void Validate()
    {
        if (SchemaVersion != 1 || InitializationPolicy != "fresh-story-resolved-progression-v1" || ConversionPolicyVersion != 1 || RequiredCapabilities.IsDefault || RequiredCapabilities.Any(capability => capability != "extended-roster-v1"))
        {
            throw new NotSupportedException("This New Game+ save requires an unsupported initialization policy. Preserve it and use a compatible Memoria version.");
        }
        if (Settings is null)
        {
            throw new InvalidDataException("New Game+ requires the selected gameplay settings.");
        }
        if (Settings is null || InitialMagicExtension.IsDefault || InitializedCharacters.IsDefault || InitializedGuardianForces.IsDefault || !Enum.IsDefined(CharacterPreset) || !Enum.IsDefined(GuardianForcePreset) || !Enum.IsDefined(MagicPreset) || !Enum.IsDefined(ItemPreset) || !Enum.IsDefined(CardPreset))
        {
            throw new InvalidDataException("New Game+ is missing required settings, choices or initialization receipts.");
        }
        Settings.Validate();
        if (ExperienceMultiplierTenths is < 0 or > 100 || ApMultiplierTenths is < 0 or > 100 || GlobalLevel is < 0 or > 100 || Characters.IsDefault || Characters.Length != 11 || Characters.Any(character => character is null || character.CharacterId > 10 || character.IndividualLevel is < 1 or > 100) || Characters.Select(character => character.CharacterId).Distinct().Count() != 11)
        {
            throw new InvalidDataException("New Game+ requires valid reward rates and eleven identity-based character choices.");
        }
        if (!Characters.Single(character => character.CharacterId == 0).AvailableFromStart)
        {
            throw new InvalidDataException("Squall must be available for the opening party.");
        }
        if (InitialItems.IsDefault || InitialItems.Length != NativeEconomy.ItemSlotCount || InitialItems.Any(stock => stock is null || stock.Count > 100) || InitialCardQuantities.IsDefault || InitialCardQuantities.Length != NativeCardInventory.CardCount || InitialCardQuantities.Where((quantity, id) => quantity < 0 || quantity > (id < NativeCardInventory.CommonCardCount ? 100 : 1)).Any())
        {
            throw new InvalidDataException("New Game+ starting collections contain invalid quantities.");
        }
        if (InitialGuardianForces.IsDefault || InitialGuardianForces.Length != 16 || InitialGuardianForceTemplates.IsDefault || InitialGuardianForceTemplates.Length != 16 || InitialGuardianForceTemplates.Any(row => row.IsDefault || row.Any(entry => entry is null) || row.Select(entry => entry.Ability).Distinct().Count() != row.Length) || InitialMagic.IsDefault || InitialMagic.Any(bank => bank is null || bank.Stock.IsDefault || bank.CharacterId > 10 || bank.Stock.Any(stock => stock is null || stock.SpellId > 56 || stock.Count > 100)) || InitialMagic.Select(bank => bank.CharacterId).Distinct().Count() != InitialMagic.Length)
        {
            throw new InvalidDataException("New Game+ starting magic or Guardian Force definitions are incomplete.");
        }
        if (GuardianForceExcludedDocuments.IsDefault || GuardianForceExcludedDocuments.Distinct(StringComparer.Ordinal).Count() != GuardianForceExcludedDocuments.Length)
        {
            throw new InvalidDataException("New Game+ embedded Guardian Force contributions require explicit distinct document exclusions.");
        }
        if (InitializedCharacters.IsDefault || InitializedGuardianForces.IsDefault || InitializedCharacters.Any(identity => identity > 10) || InitializedGuardianForces.Any(identity => identity > 15) || InitializedCharacters.Distinct().Count() != InitializedCharacters.Length || InitializedGuardianForces.Distinct().Count() != InitializedGuardianForces.Length)
        {
            throw new InvalidDataException("New Game+ initialization receipts contain invalid or duplicate identities.");
        }
        if (InitialMagic.Any(bank => bank is null || bank.CharacterId > 10 || bank.Stock.IsDefault || bank.Stock.Length != 32 || bank.Stock.Any(stock => stock is null || stock.SpellId > 56 || stock.Count > 100) || bank.Stock.Where(stock => stock.SpellId > 0 && stock.Count > 0).Select(stock => stock.SpellId).Distinct().Count() != bank.Stock.Count(stock => stock.SpellId > 0 && stock.Count > 0)) || InitialMagicExtension.IsDefault || InitialMagicExtension.Any(stock => stock is null || stock.SpellId is < 1 or > 56 || stock.Count is < 1 or > 100))
        {
            throw new InvalidDataException("New Game+ starting magic banks contain invalid stock.");
        }
        if (InitialMagicConversionMoves.IsDefault || InitialMagicOverflow.IsDefault || InitialMagicConversionMoves.Concat(InitialMagicOverflow).Any(move => move is null || move.RecordId is < 0 or > 10 || move.SpellId is < 1 or > 56 || move.Quantity is < 1 or > 100))
        {
            throw new InvalidDataException("New Game+ magic conversion receipts contain invalid stock movements.");
        }
        foreach (string identity in GuardianForceExcludedDocuments)
        {
            GameplaySaveGuardianForceIntent.ValidateIdentity(identity);
        }
        foreach (GameplaySaveGuardianForce gf in InitialGuardianForces)
        {
            if (gf is null)
            {
                throw new InvalidDataException("New Game+ is missing a Guardian Force starting definition.");
            }
            gf.Validate();
        }
    }
}
