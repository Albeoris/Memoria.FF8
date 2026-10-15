using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleExperienceTests
{
    [Fact]
    public void NativeLevelGainsApplyEquippedBonusesWithTheirCapsAndDoNotHealCurrentHp()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        NativeBattleExperienceDefinition definition = assets.ReadNativeBattleExperienceDefinition();
        NativeStatCurve curve = source.Definitions.Characters[0];
        NativeCharacterVital[] records = source.Records.ToArray();
        uint abilities = Ability(definition, 0x80) | (uint)Ability(definition, 0x100) << 8 | (uint)Ability(definition, 0x200) << 16 | (uint)Ability(definition, 0x400) << 24;
        uint before = Threshold(curve, 2) - 1;
        records[0] = records[0] with { Experience = before, HitPointBonus = 9990, StatBonuses = [254, 255, 12, 13, 14, 15], HitPoints = new(100, 100), Junction = records[0].Junction with { Abilities = abilities } };
        NativeGuardianForceVital[] gfs = source.GuardianForces.ToArray();
        NativeStatCurve gfCurve = source.Definitions.GuardianForces[0];
        gfs[0] = gfs[0] with { Experience = Threshold(gfCurve, 2) - 1, HitPoints = 100 };
        source = source with { Records = records, GuardianForces = gfs };
        string original = JsonSerializer.Serialize(source);
        NativeCharacterVitals result = source.AwardBattleExperience(new Dictionary<byte, ushort> { [0] = checked((ushort)(Threshold(curve, 4) - before)) }, new Dictionary<byte, ushort> { [0] = 1 }, definition);
        Assert.Equal((byte)4, NativeStatDefinitions.Level(result.Records[0].Experience, curve));
        Assert.Equal((ushort)9999, result.Records[0].HitPointBonus);
        Assert.Equal(new byte[] { 255, 255, 15, 13, 14, 15 }, result.Records[0].StatBonuses);
        Assert.Equal(new NativeHpWord(100, 100), result.Records[0].HitPoints);
        Assert.Equal((byte)2, NativeStatDefinitions.Level(result.GuardianForces[0].Experience, gfCurve));
        Assert.Equal((ushort)100, result.GuardianForces[0].HitPoints);
        Assert.Equal(result.Definitions.GuardianForceMaximum(0, result.GuardianForces[0]), result.GuardianForces[0].MaximumHitPoints);
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void DuplicateEquippedBonusFlagsDoNotMultiplyTheBonusAndSpiritUsesItsActualFourthStatByte()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals();
        NativeBattleExperienceDefinition definition = assets.ReadNativeBattleExperienceDefinition();
        NativeStatCurve curve = source.Definitions.Characters[0];
        byte spirit = Ability(definition, 0x800);
        NativeCharacterVital[] records = source.Records.ToArray();
        records[0] = records[0] with { Experience = Threshold(curve, 2) - 1, StatBonuses = [0, 0, 0, 9, 0, 0], Junction = records[0].Junction with { Abilities = spirit | (uint)spirit << 8 } };
        source = source with { Records = records };
        NativeCharacterVitals result = source.AwardBattleExperience(new Dictionary<byte, ushort> { [0] = 1 }, new Dictionary<byte, ushort>(), definition);
        Assert.Equal(new byte[] { 0, 0, 0, 10, 0, 0 }, result.Records[0].StatBonuses);
    }

    [Fact]
    public void NativeExperienceCapsUseThePhysicalCharacterRowAndActualGfCurveAndRejectForeignDefinitions()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        NativeBattleExperienceDefinition definition = assets.ReadNativeBattleExperienceDefinition();
        // Component-only distinct curves exercise native496E03's physical-row cap independently of the substituted identity.
        NativeStatDefinitions curves = source.Definitions with { Characters = source.Definitions.Characters.SetItem(0, source.Definitions.Characters[0] with { ExperienceStep = 50, ExperienceFraction = 0 }).SetItem(8, source.Definitions.Characters[8] with { ExperienceStep = 100, ExperienceFraction = 128 }) };
        NativeCharacterVital[] records = source.Records.ToArray();
        records[0] = records[0] with { CharacterId = 8, Experience = Threshold(curves.Characters[8], 100) - 1, Junction = records[0].Junction with { Abilities = 0 } };
        NativeGuardianForceVital[] gfs = source.GuardianForces.ToArray();
        gfs[0] = gfs[0] with { Experience = Threshold(curves.GuardianForces[0], 100) - 1 };
        source = source with { Records = records, GuardianForces = gfs, Definitions = curves };
        NativeCharacterVitals result = source.AwardBattleExperience(new Dictionary<byte, ushort> { [0] = ushort.MaxValue }, new Dictionary<byte, ushort> { [0] = ushort.MaxValue }, definition);
        Assert.Equal(Threshold(curves.Characters[0], 100), result.Records[0].Experience);
        Assert.Equal(Threshold(curves.GuardianForces[0], 100), result.GuardianForces[0].Experience);
        Assert.Throws<InvalidDataException>(() => source.AwardBattleExperience(new Dictionary<byte, ushort> { [1] = 1 }, new Dictionary<byte, ushort>(), definition));
        Assert.Throws<InvalidDataException>(() => source.AwardBattleExperience(new Dictionary<byte, ushort>(), new Dictionary<byte, ushort> { [1] = 1 }, definition));
        Assert.Throws<InvalidDataException>(() => source.AwardBattleExperience(new Dictionary<byte, ushort>(), new Dictionary<byte, ushort>(), definition with { KernelSha256 = new string('0', 64) }));
    }

    private static byte Ability(NativeBattleExperienceDefinition definition, uint flag)
    {
        int index = Enumerable.Range(0, 20).Single(index => (definition.CharacterAbilityFlags[index] & flag) != 0);
        return (byte)(58 + index);
    }

    private static uint Threshold(NativeStatCurve curve, int level)
    {
        return (uint)(10 * (level - 1) * curve.ExperienceStep + (level - 1) * (level - 1) * curve.ExperienceFraction / 256);
    }

    private static GameAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original kernel/init resources are required for native experience checks.");
        return new GameAssets(directory);
    }
}
