using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record GameplaySaveWeaponRecipes([property: JsonRequired] uint Discovered, [property: JsonRequired] uint ObtainedWeapons, [property: JsonRequired] uint MagazineRecipes);
public sealed record GameplaySavePocketStation([property: JsonRequired] ImmutableArray<byte> Values, [property: JsonRequired] ImmutableArray<byte>? ChocoboName);

public sealed record GameplaySaveCollections
{
    [JsonRequired]
    public required byte[]? Dresses { get; init; }
    [JsonRequired]
    public required NativeCardInventory? Cards { get; init; }
    [JsonRequired]
    public required ImmutableArray<byte>? ShopHistory { get; init; }
    [JsonRequired]
    public required GameplaySaveWeaponRecipes? Recipes { get; init; }
    [JsonRequired]
    public required byte? ShotAvailability { get; init; }
    [JsonRequired]
    public required GameplaySavePocketStation? PocketStation { get; init; }
    [JsonRequired]
    public required ChocoboRewardLedger ChocoboRewards { get; init; }
    [JsonRequired]
    public required ImmutableArray<uint>? KnownMagic { get; init; }
    [JsonRequired]
    public required ImmutableArray<uint>? ScannedModels { get; init; }
    [JsonRequired]
    public required ImmutableArray<int> RefinedCardIds { get; init; }

    internal static GameplaySaveCollections Capture(GameState state)
    {
        state.CharacterAppearance?.Validate();
        state.CardInventory?.Validate();
        state.ShopHistory?.Validate();
        state.WeaponRecipes?.Validate();
        state.ShotAvailability?.Validate();
        state.PocketStation?.Validate();
        state.KnownMagic?.Validate();
        state.ScannedModels?.Validate();
        state.ChocoboRewards.Validate();
        return new()
        {
            Dresses = state.CharacterAppearance?.Records.Select(row => row.Dress).ToArray(),
            Cards = state.CardInventory?.DeepCopy(),
            ShopHistory = state.ShopHistory?.Bytes,
            Recipes = state.WeaponRecipes is { } recipes ? new(recipes.Bits, recipes.ObtainedWeaponsBits, recipes.MagazineRecipeBits) : null,
            ShotAvailability = state.ShotAvailability?.Flags770,
            PocketStation = state.PocketStation is { } pocket ? new(pocket.Raw, pocket.ChocoboName) : null,
            ChocoboRewards = state.ChocoboRewards,
            KnownMagic = state.KnownMagic?.Words,
            ScannedModels = state.ScannedModels?.Words,
            RefinedCardIds = state.RefinedCardIds
        };
    }

    public void Validate()
    {
        if (Dresses is { Length: not 8 } || ShopHistory is { } shops && (shops.IsDefault || shops.Length != 400) || KnownMagic is { } magic && (magic.IsDefault || magic.Length != 2) || ScannedModels is { } scans && (scans.IsDefault || scans.Length != 5) || ChocoboRewards is null || RefinedCardIds.IsDefault || RefinedCardIds.Any(card => card is < 0 or >= NativeCardInventory.CardCount) || RefinedCardIds.Distinct().Count() != RefinedCardIds.Length)
        {
            throw new InvalidDataException("Gameplay collection components are malformed.");
        }
        Cards?.Validate();
        ChocoboRewards.Validate();
        if (Recipes is { } recipes && ((recipes.MagazineRecipes & ~recipes.Discovered) != 0 || (recipes.MagazineRecipes & 0xF0000000u) != 0 || (recipes.ObtainedWeapons & 0xF0000000u) != 0))
        {
            throw new InvalidDataException("Gameplay weapon recipe bits are malformed.");
        }
        if (PocketStation is { } pocket)
        {
            new NativePocketStationState(NativeInputConfiguration.OriginalInitHash, pocket.Values) { ChocoboName = pocket.ChocoboName }.Validate();
        }
    }
}
