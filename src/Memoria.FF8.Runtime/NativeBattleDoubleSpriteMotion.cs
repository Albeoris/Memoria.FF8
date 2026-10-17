namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoubleSpriteMotionStep(NativeBattleModulePoolTask Task, NativeCrtRandomState Random);

public static class NativeBattleDoubleSpriteMotion
{
    public static NativeBattleDoubleSpriteMotionStep Advance(NativeBattleModulePoolTask task, NativeCrtRandomState random, Func<uint, NativeBattleModulePoolTask>? readTask = null)
    {
        NativeBattleEnhancementSpriteMotionStep step;
        if (task.Callback == 0x8D58A0)
        {
            step = NativeBattleEnhancementSpriteMotion.AdvanceFan(task, random);
        }
        else if (task.Callback == 0x8D6200)
        {
            step = NativeBattleEnhancementSpriteMotion.AdvanceGround(task, random, readTask ?? (_ => throw new NotSupportedException("Native8D6420 requires the actual taskDWORD14 root link.")), false);
        }
        else
        {
            throw new NotSupportedException($"Native Double sprite motion callback{task.Callback:X} has no proved contract.");
        }
        return new(step.Task, step.Random);
    }
}
