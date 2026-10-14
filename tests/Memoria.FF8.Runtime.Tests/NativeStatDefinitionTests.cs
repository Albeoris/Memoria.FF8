using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeStatDefinitionTests
{
    private static NativeStatDefinitions Definitions()
    {
        NativeStatCurve character = new(100, 0, 50, 255, 100);
        NativeStatCurve guardianForce = new(100, 0, 20, 10, 100);
        NativeStatModifier[] passives = Enumerable.Range(0, 19).Select(_ => new NativeStatModifier(0, 9, 0)).ToArray();
        passives[0] = new NativeStatModifier(0, 0, 20);
        passives[1] = new NativeStatModifier(0, 0, 80);
        passives[2] = new NativeStatModifier(0, 0, 60);
        NativeStatModifier[] gfModifiers = Enumerable.Range(0, 9).Select(_ => new NativeStatModifier(0, 9, 0)).ToArray();
        gfModifiers[0] = new NativeStatModifier(0, 1, 20);
        gfModifiers[1] = new NativeStatModifier(0, 1, 80);
        return new NativeStatDefinitions(Enumerable.Repeat(character, 11).ToImmutableArray(), Enumerable.Range(0, 57).Select(id => id == 1 ? (byte)8 : (byte)0).ToImmutableArray(), passives.ToImmutableArray(), Enumerable.Repeat(guardianForce, 16).ToImmutableArray(), gfModifiers.ToImmutableArray());
    }

    private static NativeCharacterVital Character()
    {
        byte[] junctions = new byte[20];
        junctions[0] = 1;
        NativeMagicStock[] inventory = Enumerable.Range(0, 32).Select(_ => new NativeMagicStock(0, 0)).ToArray();
        inventory[0] = new NativeMagicStock(1, 25);
        inventory[1] = new NativeMagicStock(1, 100);
        return new NativeCharacterVital(8, new NativeHpWord(10, 20), 0, new NativeCharacterJunction(0, 0, 39u | 40u << 8, junctions), 6500, 10, 30, new byte[6], inventory, 1, 0);
    }

    [Fact]
    public void LevelUsesSignedExperienceAndNativeThresholdProgression()
    {
        NativeStatCurve curve = new(100, 0, 50, 255, 100);
        Assert.Equal((byte)1, NativeStatDefinitions.Level(0, curve));
        Assert.Equal((byte)1, NativeStatDefinitions.Level(999, curve));
        Assert.Equal((byte)2, NativeStatDefinitions.Level(1000, curve));
        Assert.Equal((byte)7, NativeStatDefinitions.Level(6500, curve));
        Assert.Equal((byte)100, NativeStatDefinitions.Level(int.MaxValue, curve));
        Assert.Equal((byte)1, NativeStatDefinitions.Level(0x80000000, curve));
        NativeStatCurve fractional = curve with { ExperienceFraction = 128 };
        Assert.Equal((byte)2, NativeStatDefinitions.Level(2001, fractional));
        Assert.Equal((byte)3, NativeStatDefinitions.Level(2002, fractional));
    }

    [Fact]
    public void CharacterMaximumUsesFirstSpellPairAndBytePassiveAccumulator()
    {
        NativeStatDefinitions definitions = Definitions();
        definitions.Validate();
        NativeCharacterVital character = Character();
        // 10 +100 +7*50 +25*8 -490/255 =659; passive100+20+80 =200.
        Assert.Equal((ushort)1318, definitions.CharacterMaximum(character));
        NativeCharacterVital overflowing = character with { Junction = character.Junction with { Abilities = 39u | 40u << 8 | 41u << 16 } };
        Assert.Equal((ushort)26, definitions.CharacterMaximum(overflowing));
        NativeCharacterVital missing = character with { MagicInventory = Enumerable.Range(0, 32).Select(_ => new NativeMagicStock(0, 0)).ToArray() };
        Assert.Equal((ushort)918, definitions.CharacterMaximum(missing));
        NativeCharacterVital capped = character with { HitPointBonus = ushort.MaxValue };
        Assert.Equal((ushort)9999, definitions.CharacterMaximum(capped));
        byte[] unsupported = character.Junction.MagicJunctions.ToArray();
        unsupported[0] = 255;
        Assert.Throws<NotSupportedException>(() => definitions.CharacterMaximum(character with { Junction = character.Junction with { MagicJunctions = unsupported } }));
    }

    [Fact]
    public void GuardianForceLearnedBitsUseOnlyTheNativeNineModifierRows()
    {
        NativeStatDefinitions definitions = Definitions();
        NativeGuardianForceVital row = new(6500, 1, 9999, 0, null);
        // 100 +7*20 +490/10 =289.
        Assert.Equal((ushort)289, definitions.GuardianForceMaximum(0, row));
        Assert.Equal((ushort)289, definitions.GuardianForceMaximum(0, row with { LearnedAbilities = 1UL << 18 | 1UL << 28 }));
        Assert.Equal((ushort)346, definitions.GuardianForceMaximum(0, row with { LearnedAbilities = 1UL << 19 }));
        Assert.Equal((ushort)578, definitions.GuardianForceMaximum(0, row with { LearnedAbilities = 1UL << 19 | 1UL << 20 }));
    }
}
