namespace Memoria.FF8.Runtime;

public static class NativeBattleRestorationFade
{
    public static NativeBattleRestorationLeafStep Advance(NativeBattleRestorationFrame frame, uint address, Func<NativeBattleRestorationFrame, NativeBattleStageFadeWrite, NativeBattleRestorationFrame> execute)
    {
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(frame.Effect.Binding.Effect);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != profile.FadeInCallback && task.Callback != profile.FadeOutCallback)
        {
            throw new NotSupportedException("Native restoration fade requires its own original fade callback.");
        }
        NativeBattleStageFadeStep step = NativeBattleStageFadeTask.Advance(task, task.Callback == profile.FadeInCallback);
        task = step.Task;
        if (step.Write is { } write)
        {
            hierarchy = hierarchy.Write(address, task);
            frame = execute(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, write);
            hierarchy = new(frame.Effect.Pools);
            task = hierarchy.Read(address);
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, complete ? 2 : 0);
    }
}
