using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioDuelAdmissionTests
{
    [Theory]
    [InlineData(241, 0, true)]
    [InlineData(241, 5, true)]
    [InlineData(241, 6, false)]
    [InlineData(239, 0, false)]
    public void Only_original_basic_family_is_offered_to_the_ordinary_Duel_adapter(byte command, ushort action, bool supported)
    {
        BattleNativeAction native = new(command, action, 22, action)
        {
            CharacterId = 1,
            MagicId = 274,
            AttackType = 1,
            Power = 16,
            HitCount = 1,
            TargetFlags = 0x58,
            AttackFlags = 0x20,
            DamagePresentation = 4,
            Element = 0,
            ElementPercent = 0,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
        Assert.Equal(supported, ScenarioDiscovery.IsTypedLimitScenario(native));
    }
}
