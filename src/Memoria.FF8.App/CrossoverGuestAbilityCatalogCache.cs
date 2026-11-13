using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Retains the native choices until their encounter-owned inputs are replaced.</summary>
internal sealed class CrossoverGuestAbilityCatalogCache(Func<NativeCharacterVitals, CrossoverFf8BattleKit, GameplaySaveLimitProgress?, byte, IReadOnlyList<CrossoverGuestAbility>> read)
{
    private NativeCharacterVitals? working;
    private CrossoverFf8BattleKit? kit;
    private GameplaySaveLimitProgress? limits;
    private byte crisis;
    private IReadOnlyList<CrossoverGuestAbility>? choices;

    public IReadOnlyList<CrossoverGuestAbility> Read(NativeCharacterVitals currentWorking, CrossoverFf8BattleKit currentKit, GameplaySaveLimitProgress? currentLimits, byte currentCrisis)
    {
        if (choices is not null && ReferenceEquals(working, currentWorking) && ReferenceEquals(kit, currentKit) && ReferenceEquals(limits, currentLimits) && crisis == currentCrisis)
        {
            return choices;
        }
        IReadOnlyList<CrossoverGuestAbility> currentChoices = read(currentWorking, currentKit, currentLimits, currentCrisis);
        working = currentWorking;
        kit = currentKit;
        limits = currentLimits;
        crisis = currentCrisis;
        choices = currentChoices;
        return currentChoices;
    }
}
