using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticWeaponPreparationTests
{
    [Fact]
    public void Omitted_or_null_weapon_retains_old_recipes_without_loading_weapon_definitions()
    {
        const string legacy = "{\"row\":0,\"character_id\":0,\"stocks\":[],\"guardian_forces\":[]}";
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        DiagnosticCharacterPreparation omitted = JsonSerializer.Deserialize<DiagnosticCharacterPreparation>(legacy, options)!;
        DiagnosticCharacterPreparation explicitNull = JsonSerializer.Deserialize<DiagnosticCharacterPreparation>(legacy[..^1] + ",\"weapon\":null}", options)!;
        Assert.Null(omitted.Weapon);
        Assert.Equal(omitted, explicitNull);
        NativeCharacterVitals original = Vitals();
        DiagnosticPreparationResult result = GameWorld.PrepareDiagnosticCharacters(original, new([], [omitted], false));
        AssertJsonEqual(original, result.Vitals);
        Assert.Empty(result.Changes);
    }

    [Theory]
    [InlineData(0, 11)]
    [InlineData(4, 15)]
    public void Owned_original_row_updates_only_canonical_saved_weapon_and_records_derived_resource_receipt(byte character, byte weapon)
    {
        NativeCharacterVitals original = Vitals(character);
        JsonElement before = JsonSerializer.SerializeToElement(original);
        NativeWeaponShopDefinitions definitions = Weapons(original.KernelSha256);
        DiagnosticPreparationResult result = GameWorld.PrepareDiagnosticCharacters(original, Recipe(character, weapon), weaponDefinitions: definitions);
        NativeCharacterVitals expected = original.DeepCopy();
        expected.Records[character] = expected.Records[character] with { Weapon = weapon };
        expected = expected.RebuildParty();
        AssertJsonEqual(expected, result.Vitals);
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(original)));
        Assert.Equal((byte)0, original.Records[character].Weapon);
        DiagnosticPreparationChange receipt = Assert.Single(result.Changes, change => change.Owner == "weapon-resource");
        Assert.Equal(character, receipt.Row);
        Assert.Equal(weapon, receipt.After.GetProperty("Weapon").GetByte());
        Assert.Equal(character, receipt.After.GetProperty("OriginalOwner").GetByte());
        Assert.Equal(4, receipt.After.GetProperty("KernelSection").GetInt32());
        Assert.Equal(weapon, receipt.After.GetProperty("KernelRow").GetByte());
        Assert.Equal(0xA5, receipt.After.GetProperty("KernelByte2").GetByte());
        Assert.Equal(definitions.KernelHash, receipt.After.GetProperty("KernelHash").GetString());
        Assert.Equal(definitions.RecipeHash, receipt.After.GetProperty("RecipeHash").GetString());
        Assert.Equal(definitions.ExecutableHash, receipt.After.GetProperty("ExecutableHash").GetString());
        Assert.Equal(12, receipt.After.GetProperty("KernelWords").GetArrayLength());
        Assert.Contains(result.Changes, change => change.Owner == "character" && change.Row == character);
        result.Vitals.Records[character] = result.Vitals.Records[character] with { Weapon = 1 };
        Assert.Equal(weapon, receipt.After.GetProperty("Weapon").GetByte());
    }

    [Fact]
    public void Same_weapon_keeps_saved_state_but_retains_explicit_resource_provenance()
    {
        NativeCharacterVitals original = Vitals();
        DiagnosticPreparationResult result = GameWorld.PrepareDiagnosticCharacters(original, Recipe(0, 0), weaponDefinitions: Weapons(original.KernelSha256));
        AssertJsonEqual(original, result.Vitals);
        Assert.Equal("weapon-resource", Assert.Single(result.Changes).Owner);
    }

    [Theory]
    [InlineData(33)]
    [InlineData(255)]
    public void Missing_original_kernel_index_is_rejected_without_mutating_source(byte weapon)
    {
        NativeCharacterVitals original = Vitals();
        JsonElement before = JsonSerializer.SerializeToElement(original);
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticCharacters(original, Recipe(0, weapon), weaponDefinitions: Weapons(original.KernelSha256)));
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(original)));
    }

    [Fact]
    public void Another_characters_original_weapon_is_rejected()
    {
        NativeCharacterVitals original = Vitals();
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticCharacters(original, Recipe(0, 15), weaponDefinitions: Weapons(original.KernelSha256)));
        Assert.Equal((byte)0, original.Records[0].Weapon);
    }

    [Fact]
    public void Weapon_requires_authenticated_matching_kernel_and_complete_original_row()
    {
        NativeCharacterVitals original = Vitals();
        NativeWeaponShopDefinitions definitions = Weapons(original.KernelSha256);
        DiagnosticBattlePreparation recipe = Recipe(0, 11);
        Assert.Throws<NotSupportedException>(() => GameWorld.PrepareDiagnosticCharacters(original, recipe));
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticCharacters(original, recipe, weaponDefinitions: definitions with { KernelHash = new string('F', 64) }));
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticCharacters(original, recipe, weaponDefinitions: definitions with { ExecutableHash = new string('0', 64) }));
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticCharacters(original, recipe, weaponDefinitions: definitions with { Recipes = definitions.Recipes.SetItem(11, definitions.Recipes[11] with { KernelWords = [] }) }));
        ImmutableArray<byte> foreignOwner = definitions.Recipes[11].KernelWords.SetItem(4, 4);
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticCharacters(original, recipe, weaponDefinitions: definitions with { Recipes = definitions.Recipes.SetItem(11, definitions.Recipes[11] with { KernelWords = foreignOwner }) }));
        Assert.Equal((byte)0, original.Records[0].Weapon);
    }

    [Fact]
    public void Actual_saved_identity_and_combat_party_are_validated_before_equipment_publication()
    {
        NativeCharacterVitals original = Vitals();
        NativeWeaponShopDefinitions definitions = Weapons(original.KernelSha256);
        Assert.Throws<InvalidOperationException>(() => GameWorld.PrepareDiagnosticCharacters(original, new([], [new(0, 4, [], []) { Weapon = 15 }], false), weaponDefinitions: definitions));
        Assert.Throws<InvalidOperationException>(() => GameWorld.PrepareDiagnosticCharacters(original, Recipe(4, 15), weaponDefinitions: definitions));
        Assert.Equal((byte)0, original.Records[0].Weapon);
    }

    [Fact]
    public void Active_dream_weapon_owner_cannot_be_replaced_by_unused_saved_character_byte()
    {
        NativeCharacterVitals original = Vitals();
        original.Records[0] = original.Records[0] with { CharacterId = 8 };
        original = original with { DreamEnabled = true };
        JsonElement before = JsonSerializer.SerializeToElement(original);
        Assert.Throws<NotSupportedException>(() => GameWorld.PrepareDiagnosticCharacters(original, new([], [new(0, 8, [], []) { Weapon = 19 }], false), weaponDefinitions: Weapons(original.KernelSha256)));
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(original)));
    }

    [Theory]
    [InlineData("\"weapon\":256")]
    [InlineData("\"weapon\":-1")]
    [InlineData("\"finisher_mask\":255")]
    public void Schema_accepts_only_the_optional_native_weapon_byte(string member)
    {
        string json = "{\"row\":0,\"character_id\":0,\"stocks\":[],\"guardian_forces\":[]," + member + "}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DiagnosticCharacterPreparation>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
    }

    private static DiagnosticBattlePreparation Recipe(byte character, byte weapon) => new([], [new(character, character, [], []) { Weapon = weapon }], false);
    private static void AssertJsonEqual(object expected, object actual) => Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(actual)));

    private static NativeWeaponShopDefinitions Weapons(string kernelHash)
    {
        ImmutableArray<NativeWeaponRecipe> rows = Enumerable.Range(0, 33).Select(index =>
        {
            byte[] kernel = new byte[12];
            kernel[2] = 0xA5;
            kernel[4] = (byte)(index % 11);
            return new NativeWeaponRecipe("Weapon", kernel[4], 0, Enumerable.Repeat(new NativeItemStock(0, 0), 4).ToImmutableArray(), kernel.ToImmutableArray(), new byte[12].ToImmutableArray());
        }).ToImmutableArray();
        return new(NativeFieldRuntimeProfile.FF8EnExecutableHash, kernelHash, new string('1', 64), new string('2', 64), rows, new byte[68].ToImmutableArray(), new byte[256].ToImmutableArray());
    }

    private static NativeCharacterVitals Vitals(byte character = 0)
    {
        NativeStatCurve curve = new(1, 0, 1, 10, 10);
        NativeStatModifier modifier = new(0, 0, 0);
        return new NativeCharacterVitals
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = new string('0', 64),
            KernelSha256 = new string('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new NativeHpWord(10, 10), 0, new NativeCharacterJunction((ushort)(1 << id), 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [character, 255, 255],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(0, 1, 10, new NativeGuardianForceLearnedBank(0x3FFFF, 0), 10, new byte[21].ToImmutableArray(), 0, 0)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), Enumerable.Repeat((byte)1, 57).ToImmutableArray(), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), new byte[116].ToImmutableArray(), new byte[19].ToImmutableArray(), new byte[5].ToImmutableArray()),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        }.RebuildParty();
    }
}
