using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public IReadOnlyList<Ff7BattleHitmarkState> Hitmarks => state.Hitmarks;

    private void ScheduleActorHitmark(int actor, ReadOnlySpan<byte> operands)
    {
        if (animations is null || operands[1] >= animations.Hitmarks.Count)
        {
            throw new NotSupportedException("The actor hitmark requires its original effect definition.");
        }
        Ff7BattleSecondaryState secondary = EnsureSecondaryEffects();
        state = state with { SecondaryEffects = Ff7BattleSecondaryEffects.ScheduleActorHitmark(secondary, actor, animations.Hitmarks[operands[1]], operands[0]) };
    }

    private void StartHitmark(int target, int effect)
    {
        if (animations is null || effect == -1)
        {
            return;
        }
        int index = effect & 0xFF;
        if ((uint)index >= animations.Hitmarks.Count)
        {
            throw new NotSupportedException($"FFVII impact effect {effect:X4} has no original hitmark definition.");
        }
        Ff7BattleSecondaryState secondary = EnsureSecondaryEffects();
        state = state with { SecondaryEffects = Ff7BattleSecondaryEffects.ScheduleHitmark(secondary, target, animations.Hitmarks[index], ImpactAnchor(target), CollisionRadius(target), (effect & 0x100) != 0) };
    }
}
