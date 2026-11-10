using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public IReadOnlyList<Ff7BattleHitmarkState> Hitmarks => state.Hitmarks;

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
        Ff7BattleHitmarkState hitmark = Ff7BattleHitmarkEffect.Start(target, animations.Hitmarks[index], (effect & 0x100) != 0);
        state = state with { Hitmarks = [.. state.Hitmarks, hitmark] };
    }

    private void AdvanceHitmarks()
    {
        state = state with { Hitmarks = state.Hitmarks.Where(hitmark => !hitmark.Completed).Select(hitmark => Ff7BattleHitmarkEffect.Advance(hitmark, ImpactAnchor, CollisionRadius)).ToArray() };
    }
}
