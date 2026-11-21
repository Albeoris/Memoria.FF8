using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private void ScheduleActorUvAnimation(int actor, byte selector)
    {
        string model = ActorModel(actor);
        _ = animations!.ActorUvAnimation(model, selector);
        Ff7BattleActorModelState current = ActorModelState(actor);
        byte generation = unchecked((byte)(current.UvAnimationGeneration + 1));
        Ff7BattleSecondaryState secondary = Ff7BattleSecondaryEffects.ScheduleCallback(EnsureSecondaryEffects(), new Ff7BattleActorUvAnimationCallback(actor, model, selector, new(generation)));
        ReplaceActorModel(current with { UvAnimationGeneration = generation });
        state = state with { SecondaryEffects = secondary };
    }
}
